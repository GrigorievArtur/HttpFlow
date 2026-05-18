using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Httpflow.Desktop.Models.Nodes;
using Httpflow.Desktop.Models.Projects;

namespace Httpflow.Desktop.Services.Projects;

public sealed class ProjectTestRunnerService
{
    private readonly ProjectSessionService _projectSessionService;
    private readonly HttpClient _httpClient = new();
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private int _completedNodes;
    private int _totalNodes;
    private int _hasError;

    public ProjectTestRunnerService(ProjectSessionService projectSessionService)
    {
        _projectSessionService = projectSessionService;
    }

    public event Action<ProjectRunProgress>? ProgressChanged;

    public event Action<ProjectRunNotification>? NotificationRaised;

    public async Task RunCurrentProjectAsync(CancellationToken cancellationToken = default)
    {
        var project = _projectSessionService.CurrentProject;
        if (project is null)
        {
            return;
        }

        var tests = project.Tests
            .OrderBy(test => Math.Max(1, test.Order))
            .ThenBy(test => test.Id)
            .ToList();

        _completedNodes = 0;
        _totalNodes = tests.Sum(test => test.Nodes.Count);
        _hasError = 0;
        PublishProgress(true, "Starting tests");

        foreach (var test in tests)
        {
            await UpdateTestStatusAsync(test.Id, "Waiting", cancellationToken);
        }

        foreach (var orderGroup in tests.GroupBy(test => Math.Max(1, test.Order)).OrderBy(group => group.Key))
        {
            PublishProgress(true, $"Running order {orderGroup.Key}");
            await Task.WhenAll(orderGroup.Select(test => RunTestAsync(test.Id, cancellationToken)));
        }

        var finishedWithError = Interlocked.CompareExchange(ref _hasError, 0, 0) == 1;
        PublishProgress(false, finishedWithError ? "Run finished with errors" : "Run finished");
        NotificationRaised?.Invoke(new ProjectRunNotification(
            finishedWithError ? "Run finished with errors" : "Run finished",
            $"{_completedNodes}/{_totalNodes} nodes ran.",
            finishedWithError));
    }

    private async Task RunTestAsync(int testId, CancellationToken cancellationToken)
    {
        await UpdateTestStatusAsync(testId, "Running", cancellationToken);

        var test = _projectSessionService.CurrentProject?.Tests.FirstOrDefault(item => item.Id == testId);
        if (test is null)
        {
            return;
        }

        var context = new TestRunContext();
        var passed = true;

        foreach (var node in test.Nodes.OrderBy(node => node.Y).ThenBy(GetNodeOrder))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = node.NodeType == NodeTypeNames.Expected
                ? await RunExpectedNodeAsync(test.Id, node, context, cancellationToken)
                : await RunRequestNodeAsync(test.Id, node, context, cancellationToken);

            passed &= result.Passed;
            MarkNodeCompleted(result.Passed, result.Message);
            if (!result.ShouldContinue)
            {
                MarkError($"Test \"{test.Name}\" stopped: {result.Message}");
                break;
            }
        }

        await UpdateTestStatusAsync(test.Id, passed ? "Passed" : "Failed", cancellationToken);
    }

    private async Task<NodeRunResult> RunRequestNodeAsync(
        int testId,
        CanvasNodeRecord node,
        TestRunContext context,
        CancellationToken cancellationToken)
    {
        await UpdateNodeValuesAsync(testId, node.Id, [new("Status", "Running"), new("Error", string.Empty)], cancellationToken);

        var method = GetValue(node, "Method", "GET");
        var url = GetValue(node, "Url", string.Empty);
        var body = GetValue(node, "Body", string.Empty);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            await UpdateNodeValuesAsync(
                testId,
                node.Id,
                [new("Status", "Failed"), new("Error", "Request URL must be absolute.")],
                cancellationToken);
            return new NodeRunResult(false, false, "Request URL must be absolute.");
        }

        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), uri);
            if (!string.IsNullOrWhiteSpace(body) &&
                !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusCode = (int)response.StatusCode;

            context.LastStatusCode = statusCode;
            context.LastResponse = responseBody;

            await UpdateNodeValuesAsync(
                testId,
                node.Id,
                [
                    new("StatusCode", statusCode.ToString()),
                    new("Response", responseBody),
                    new("Error", string.Empty),
                    new("Status", response.IsSuccessStatusCode ? "Passed" : "Failed")
                ],
                cancellationToken);

            return new NodeRunResult(true, true, $"HTTP {statusCode}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            context.LastStatusCode = null;
            context.LastResponse = string.Empty;

            await UpdateNodeValuesAsync(
                testId,
                node.Id,
                [new("StatusCode", string.Empty), new("Response", string.Empty), new("Error", ex.Message), new("Status", "Failed")],
                cancellationToken);

            return new NodeRunResult(false, false, ex.Message);
        }
    }

    private async Task<NodeRunResult> RunExpectedNodeAsync(
        int testId,
        CanvasNodeRecord node,
        TestRunContext context,
        CancellationToken cancellationToken)
    {
        var expectedCodeText = GetValue(node, "ExpectedCode", "200");
        var throwbackError = GetValue(node, "ThrowbackError", "Expected response code did not match.");
        var continueTest = bool.TryParse(GetValue(node, "ContinueTest", bool.TrueString), out var shouldContinue)
            ? shouldContinue
            : true;

        await UpdateNodeValuesAsync(
            testId,
            node.Id,
            [new("ActualCode", string.Empty), new("Error", string.Empty), new("Status", "Running")],
            cancellationToken);

        if (!int.TryParse(expectedCodeText, out var expectedCode))
        {
            await UpdateExpectedResultAsync(testId, node.Id, string.Empty, "Expected code must be a number.", cancellationToken);
            return new NodeRunResult(false, continueTest, "Expected code must be a number.");
        }

        if (context.LastStatusCode is not { } actualCode)
        {
            await UpdateExpectedResultAsync(testId, node.Id, string.Empty, "No request response exists before this Expected node.", cancellationToken);
            return new NodeRunResult(false, continueTest, "No request response exists before this Expected node.");
        }

        if (actualCode == expectedCode)
        {
            await UpdateNodeValuesAsync(
                testId,
                node.Id,
                [new("ActualCode", actualCode.ToString()), new("Error", string.Empty), new("Status", "Passed")],
                cancellationToken);
            return new NodeRunResult(true, true, $"Expected {expectedCode}, got {actualCode}.");
        }

        var error = string.IsNullOrWhiteSpace(throwbackError)
            ? $"Expected {expectedCode}, got {actualCode}."
            : throwbackError;

        await UpdateExpectedResultAsync(testId, node.Id, actualCode.ToString(), error, cancellationToken);
        return new NodeRunResult(false, continueTest, error);
    }

    private void MarkNodeCompleted(bool passed, string message)
    {
        Interlocked.Increment(ref _completedNodes);
        if (!passed)
        {
            MarkError(message);
        }

        PublishProgress(true, message);
    }

    private void MarkError(string message)
    {
        Interlocked.Exchange(ref _hasError, 1);
        NotificationRaised?.Invoke(new ProjectRunNotification("Run problem", message, true));
    }

    private void PublishProgress(bool isRunning, string message)
    {
        ProgressChanged?.Invoke(new ProjectRunProgress(
            _completedNodes,
            _totalNodes,
            Interlocked.CompareExchange(ref _hasError, 0, 0) == 1,
            isRunning,
            message));
    }

    private Task UpdateExpectedResultAsync(
        int testId,
        int nodeId,
        string actualCode,
        string error,
        CancellationToken cancellationToken)
    {
        return UpdateNodeValuesAsync(
            testId,
            nodeId,
            [new("ActualCode", actualCode), new("Error", error), new("Status", "Failed")],
            cancellationToken);
    }

    private async Task UpdateTestStatusAsync(int testId, string status, CancellationToken cancellationToken)
    {
        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            _projectSessionService.UpdateTestStatus(testId, status);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task UpdateNodeValuesAsync(
        int testId,
        int nodeId,
        IEnumerable<NodeValueRecord> values,
        CancellationToken cancellationToken)
    {
        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var value in values)
            {
                _projectSessionService.UpdateNodeValue(testId, nodeId, value.Label, value.Value);
            }
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private static string GetValue(CanvasNodeRecord node, string label, string fallback)
    {
        return node.Values.FirstOrDefault(value => string.Equals(value.Label, label, StringComparison.OrdinalIgnoreCase))?.Value
               ?? fallback;
    }

    private static int GetNodeOrder(CanvasNodeRecord node)
    {
        return int.TryParse(GetValue(node, "Order", "0"), out var order) ? order : 0;
    }

    private sealed class TestRunContext
    {
        public int? LastStatusCode { get; set; }

        public string LastResponse { get; set; } = string.Empty;
    }

    private sealed record NodeRunResult(bool Passed, bool ShouldContinue, string Message);
}
