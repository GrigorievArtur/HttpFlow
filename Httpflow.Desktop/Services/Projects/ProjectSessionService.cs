using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Httpflow.Desktop.Dtos.Projects;
using Httpflow.Desktop.Models.Api;
using Httpflow.Desktop.Models.Nodes;
using Httpflow.Desktop.Models.Projects;

namespace Httpflow.Desktop.Services.Projects;

public sealed class ProjectSessionService
{
    private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromMilliseconds(250);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly App _app;
    private readonly Timer _autoSaveTimer;
    private int _hasPendingChanges;
    private int _isSaving;

    public ProjectSessionService(App app)
    {
        _app = app;
        _autoSaveTimer = new Timer(_ => _ = AutoSaveTickAsync(), null, AutoSaveInterval, AutoSaveInterval);
    }

    public ProjectSessionState? CurrentProject { get; private set; }

    public async Task<ProjectSessionState?> LoadProjectById(int projectId)
    {
        var project = await GetProjectDtoAsync(projectId);
        if (project is null)
        {
            CurrentProject = null;
            return null;
        }

        CurrentProject = DeserializeProject(project);
        _app.CurrentProject = project;
        Interlocked.Exchange(ref _hasPendingChanges, 0);
        return CurrentProject;
    }

    public async Task<ApiResult<ProjectDto>> SaveCurrentProject()
    {
        if (CurrentProject is null)
        {
            return ApiResult<ProjectDto>.Failure("No project is currently loaded.", HttpStatusCode.BadRequest);
        }

        var token = await _app.JwtSessionService.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return ApiResult<ProjectDto>.Failure("Please log in again to save the project.", HttpStatusCode.Unauthorized);
        }

        var result = await _app.ProjectsApiClient.UpdateProjectAsync(
            token,
            CurrentProject.ProjectId,
            new UpdateProjectDto(CurrentProject.Name, SessionToJson(CurrentProject.ProjectId)));

        if (result.IsSuccess && result.Data is not null)
        {
            _app.CurrentProject = result.Data;
            CurrentProject = DeserializeProject(result.Data);
            Interlocked.Exchange(ref _hasPendingChanges, 0);
        }

        return result;
    }

    public Task<ApiResult<ProjectDto>> SyncCurrentProject()
    {
        return SaveCurrentProject();
    }

    public async Task<ApiResult<ProjectDto>> DeleteProjectById(int projectId)
    {
        var token = await _app.JwtSessionService.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return ApiResult<ProjectDto>.Failure("Please log in again to delete the project.", HttpStatusCode.Unauthorized);
        }

        var result = await _app.ProjectsApiClient.DeleteProjectAsync(token, projectId);
        if (result.IsSuccess)
        {
            if (CurrentProject?.ProjectId == projectId)
            {
                CurrentProject = null;
            }

            if (_app.CurrentProject?.Id == projectId)
            {
                _app.CurrentProject = null;
            }

            Interlocked.Exchange(ref _hasPendingChanges, 0);
        }

        return result;
    }

    public ProjectTestState AddTest()
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        var nextTestId = CurrentProject!.Tests.Count == 0
            ? 1
            : CurrentProject.Tests.Max(test => test.Id) + 1;

        var test = new ProjectTestState
        {
            Id = nextTestId,
            Name = $"Test {nextTestId}",
            Order = CurrentProject.Tests.Count == 0 ? 1 : CurrentProject.Tests.Max(item => item.Order) + 1,
            Status = "Not started",
            Nodes = []
        };

        CurrentProject.Tests.Add(test);
        SyncLegacyNodes(CurrentProject);
        MarkDirty();
        return test;
    }

    public CanvasNodeRecord AddNode(string nodeType = NodeTypeNames.Request)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        var testId = CurrentProject!.Tests.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("No test is available.");

        return AddNode(testId, nodeType);
    }

    public CanvasNodeRecord AddNode(int testId, string nodeType = NodeTypeNames.Request, int? insertIndex = null)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        var test = CurrentProject!.Tests.FirstOrDefault(item => item.Id == testId);
        if (test is null)
        {
            throw new InvalidOperationException($"Test with id {testId} was not found.");
        }

        var nextNodeId = GetNextNodeId(CurrentProject);
        var normalizedInsertIndex = Math.Clamp(insertIndex ?? test.Nodes.Count, 0, test.Nodes.Count);
        var nodeOrder = normalizedInsertIndex + 1;
        var node = new CanvasNodeRecord(
            nextNodeId,
            $"{nodeType} {nodeOrder}",
            nodeType,
            0,
            normalizedInsertIndex,
            CreateDefaultValues(nodeType, nodeOrder));

        test.Nodes.Insert(normalizedInsertIndex, node);
        NormalizeNodeOrder(test);
        SyncLegacyNodes(CurrentProject);
        MarkDirty();
        return node;
    }

    public CanvasNodeRecord? InsertNodeAfter(int testId, int targetNodeId, string nodeType)
    {
        var index = GetNodeIndex(testId, targetNodeId);
        return index < 0 ? null : AddNode(testId, nodeType, index + 1);
    }

    public CanvasNodeRecord? InsertNodeBefore(int testId, int targetNodeId, string nodeType)
    {
        var index = GetNodeIndex(testId, targetNodeId);
        return index < 0 ? null : AddNode(testId, nodeType, index);
    }

    public CanvasNodeRecord? DuplicateNodeAfter(int testId, int sourceNodeId)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        var test = CurrentProject!.Tests.FirstOrDefault(item => item.Id == testId);
        if (test is null)
        {
            return null;
        }

        var sourceIndex = test.Nodes.FindIndex(node => node.Id == sourceNodeId);
        if (sourceIndex < 0)
        {
            return null;
        }

        return InsertNodeCopy(test, test.Nodes[sourceIndex], sourceIndex + 1);
    }

    public CanvasNodeRecord? PasteNodeAfter(int testId, int targetNodeId, CanvasNodeRecord clipboardNode)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        var test = CurrentProject!.Tests.FirstOrDefault(item => item.Id == testId);
        if (test is null)
        {
            return null;
        }

        var targetIndex = test.Nodes.FindIndex(node => node.Id == targetNodeId);
        if (targetIndex < 0)
        {
            return null;
        }

        return InsertNodeCopy(test, clipboardNode, targetIndex + 1);
    }

    public CanvasNodeRecord? PasteNodeAtEnd(int testId, CanvasNodeRecord clipboardNode)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        var test = CurrentProject!.Tests.FirstOrDefault(item => item.Id == testId);
        return test is null ? null : InsertNodeCopy(test, clipboardNode, test.Nodes.Count);
    }

    public bool DeleteTest(int testId)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        if (CurrentProject!.Tests.Count <= 1)
        {
            return false;
        }

        var removed = CurrentProject.Tests.RemoveAll(test => test.Id == testId) > 0;
        if (!removed)
        {
            return false;
        }

        SyncLegacyNodes(CurrentProject);
        MarkDirty();
        return true;
    }

    public string? ExportTestToJson(int testId)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        var test = CurrentProject!.Tests.FirstOrDefault(item => item.Id == testId);
        return test is null ? null : JsonSerializer.Serialize(test, ExportJsonOptions);
    }

    public ProjectTestState? ImportTestFromJson(string json)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        ProjectTestState? importedTest;
        try
        {
            importedTest = JsonSerializer.Deserialize<ProjectTestState>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (importedTest is null)
        {
            return null;
        }

        var nextTestId = CurrentProject!.Tests.Count == 0
            ? 1
            : CurrentProject.Tests.Max(test => test.Id) + 1;
        var nextNodeId = GetNextNodeId(CurrentProject);

        var test = new ProjectTestState
        {
            Id = nextTestId,
            Name = string.IsNullOrWhiteSpace(importedTest.Name) ? $"Imported Test {nextTestId}" : importedTest.Name,
            Order = CurrentProject.Tests.Count == 0 ? 1 : CurrentProject.Tests.Max(item => item.Order) + 1,
            Status = "Not started",
            Nodes = importedTest.Nodes
                .OrderBy(node => node.Y)
                .ThenBy(node => GetNodeOrder(node.Values))
                .Select((node, index) => node with
                {
                    Id = nextNodeId++,
                    Name = string.IsNullOrWhiteSpace(node.Name) ? $"Node {index + 1}" : node.Name,
                    NodeType = string.IsNullOrWhiteSpace(node.NodeType) ? NodeTypeNames.Request : node.NodeType,
                    X = 0,
                    Y = index,
                    Values = ResetRuntimeValues(node.Values, index + 1)
                })
                .ToList()
        };

        CurrentProject.Tests.Add(test);
        NormalizeNodeOrder(test);
        SyncLegacyNodes(CurrentProject);
        MarkDirty();
        return test;
    }

    public bool UpdateTestName(int testId, string name)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        var test = CurrentProject!.Tests.FirstOrDefault(item => item.Id == testId);
        if (test is null)
        {
            return false;
        }

        test.Name = string.IsNullOrWhiteSpace(name) ? $"Test {test.Id}" : name;
        SyncLegacyNodes(CurrentProject);
        MarkDirty();
        return true;
    }

    public bool UpdateTestStatus(int testId, string status)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        var test = CurrentProject!.Tests.FirstOrDefault(item => item.Id == testId);
        if (test is null)
        {
            return false;
        }

        test.Status = string.IsNullOrWhiteSpace(status) ? "Not started" : status;
        SyncLegacyNodes(CurrentProject);
        MarkDirty();
        return true;
    }

    public bool UpdateTestOrder(int testId, int order)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        var test = CurrentProject!.Tests.FirstOrDefault(item => item.Id == testId);
        if (test is null)
        {
            return false;
        }

        test.Order = Math.Max(1, order);
        SyncLegacyNodes(CurrentProject);
        MarkDirty();
        return true;
    }

    public bool MoveTest(int sourceTestId, int targetTestId)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        var sourceIndex = CurrentProject!.Tests.FindIndex(test => test.Id == sourceTestId);
        var targetIndex = CurrentProject.Tests.FindIndex(test => test.Id == targetTestId);

        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
        {
            return false;
        }

        var test = CurrentProject.Tests[sourceIndex];
        CurrentProject.Tests.RemoveAt(sourceIndex);
        var insertIndex = sourceIndex < targetIndex ? targetIndex : targetIndex + 1;
        CurrentProject.Tests.Insert(Math.Min(insertIndex, CurrentProject.Tests.Count), test);

        SyncLegacyNodes(CurrentProject);
        MarkDirty();
        return true;
    }

    public bool DeleteNode(int testId, int nodeId)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        var test = CurrentProject!.Tests.FirstOrDefault(item => item.Id == testId);
        if (test is null)
        {
            return false;
        }

        var removed = test.Nodes.RemoveAll(node => node.Id == nodeId) > 0;
        if (!removed)
        {
            return false;
        }

        NormalizeNodeOrder(test);
        SyncLegacyNodes(CurrentProject);
        MarkDirty();
        return true;
    }

    public bool MoveNode(int testId, int sourceNodeId, int targetNodeId)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        var test = CurrentProject!.Tests.FirstOrDefault(item => item.Id == testId);
        if (test is null)
        {
            return false;
        }

        var sourceIndex = test.Nodes.FindIndex(node => node.Id == sourceNodeId);
        var targetIndex = test.Nodes.FindIndex(node => node.Id == targetNodeId);

        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
        {
            return false;
        }

        var node = test.Nodes[sourceIndex];
        test.Nodes.RemoveAt(sourceIndex);
        var insertIndex = sourceIndex < targetIndex ? targetIndex : targetIndex;
        test.Nodes.Insert(Math.Min(insertIndex, test.Nodes.Count), node);

        NormalizeNodeOrder(test);
        SyncLegacyNodes(CurrentProject);
        MarkDirty();
        return true;
    }

    private int GetNodeIndex(int testId, int nodeId)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        var test = CurrentProject!.Tests.FirstOrDefault(item => item.Id == testId);
        return test?.Nodes.FindIndex(node => node.Id == nodeId) ?? -1;
    }

    private CanvasNodeRecord InsertNodeCopy(ProjectTestState test, CanvasNodeRecord sourceNode, int insertIndex)
    {
        var normalizedInsertIndex = Math.Clamp(insertIndex, 0, test.Nodes.Count);
        var nextNodeId = GetNextNodeId(CurrentProject!);
        var node = sourceNode with
        {
            Id = nextNodeId,
            Name = GetNextCopyName(test, sourceNode.Name),
            X = 0,
            Y = normalizedInsertIndex,
            Values = ResetRuntimeValues(sourceNode.Values, normalizedInsertIndex + 1)
        };

        test.Nodes.Insert(normalizedInsertIndex, node);
        NormalizeNodeOrder(test);
        SyncLegacyNodes(CurrentProject!);
        MarkDirty();
        return node;
    }

    private static string GetNextCopyName(ProjectTestState test, string sourceName)
    {
        var baseName = Regex.Replace(sourceName, @"\s+Copy(?:\s+\(\d+\))?$", string.Empty, RegexOptions.IgnoreCase).Trim();
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "Node";
        }

        var existingNames = test.Nodes
            .Select(node => node.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var firstCopyName = $"{baseName} Copy";
        if (!existingNames.Contains(firstCopyName))
        {
            return firstCopyName;
        }

        for (var index = 2; ; index++)
        {
            var candidate = $"{baseName} Copy ({index})";
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    public bool UpdateNodeName(int testId, int nodeId, string name)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        var test = CurrentProject!.Tests.FirstOrDefault(item => item.Id == testId);
        if (test is null)
        {
            return false;
        }

        var index = test.Nodes.FindIndex(node => node.Id == nodeId);
        if (index < 0)
        {
            return false;
        }

        test.Nodes[index] = test.Nodes[index] with
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Node {index + 1}" : name
        };

        SyncLegacyNodes(CurrentProject);
        MarkDirty();
        return true;
    }

    public bool UpdateNodeValue(int testId, int nodeId, string label, string value)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        var test = CurrentProject!.Tests.FirstOrDefault(item => item.Id == testId);
        if (test is null)
        {
            return false;
        }

        var index = test.Nodes.FindIndex(node => node.Id == nodeId);
        if (index < 0)
        {
            return false;
        }

        var node = test.Nodes[index];
        test.Nodes[index] = node with
        {
            Values = UpsertNodeValue(node.Values, label, value)
        };

        SyncLegacyNodes(CurrentProject);
        MarkDirty();
        return true;
    }

    private async Task AutoSaveTickAsync()
    {
        if (CurrentProject is null || Interlocked.CompareExchange(ref _hasPendingChanges, 0, 0) == 0)
        {
            return;
        }

        if (Interlocked.Exchange(ref _isSaving, 1) == 1)
        {
            return;
        }

        try
        {
            await SaveCurrentProject();
        }
        finally
        {
            Interlocked.Exchange(ref _isSaving, 0);
        }
    }

    private async Task<ProjectDto?> GetProjectDtoAsync(int projectId)
    {
        if (_app.CurrentProject is not null && _app.CurrentProject.Id == projectId)
        {
            return _app.CurrentProject;
        }

        var token = await _app.JwtSessionService.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var result = await _app.ProjectsApiClient.GetProjectByIdAsync(token, projectId);
        return result.IsSuccess ? result.Data : null;
    }

    private string SessionToJson(int projectId)
    {
        EnsureProjectLoaded();

        if (CurrentProject!.ProjectId != projectId)
        {
            throw new InvalidOperationException("The requested project is not the active session.");
        }

        return JsonSerializer.Serialize(CurrentProject, JsonOptions);
    }

    private static ProjectSessionState DeserializeProject(ProjectDto project)
    {
        if (string.IsNullOrWhiteSpace(project.Value) || project.Value.Trim() == "{}")
        {
            var emptySession = new ProjectSessionState
            {
                ProjectId = project.Id,
                Name = project.Name
            };

            EnsureTestsInitialized(emptySession);
            return emptySession;
        }

        try
        {
            var session = JsonSerializer.Deserialize<ProjectSessionState>(project.Value, JsonOptions);
            if (session is not null)
            {
                session.ProjectId = project.Id;
                session.Name = string.IsNullOrWhiteSpace(session.Name) ? project.Name : session.Name;
                session.Nodes ??= [];
                session.Tests ??= [];
                EnsureTestsInitialized(session);
                return session;
            }
        }
        catch (JsonException)
        {
        }

        var fallbackSession = new ProjectSessionState
        {
            ProjectId = project.Id,
            Name = project.Name
        };

        EnsureTestsInitialized(fallbackSession);
        return fallbackSession;
    }

    private void EnsureProjectLoaded()
    {
        if (CurrentProject is null)
        {
            throw new InvalidOperationException("No project is currently loaded.");
        }
    }

    private void MarkDirty()
    {
        Interlocked.Exchange(ref _hasPendingChanges, 1);
    }

    private static void EnsureTestsInitialized(ProjectSessionState session)
    {
        session.Nodes ??= [];
        session.Tests ??= [];

        if (session.Tests.Count == 0)
        {
            session.Tests.Add(new ProjectTestState
            {
                Id = 1,
                Name = "Test 1",
                Order = 1,
                Status = "Not started",
                Nodes = session.Nodes
                    .OrderBy(node => node.Y)
                    .ThenBy(node => node.X)
                    .ToList()
            });
        }

        for (var index = 0; index < session.Tests.Count; index++)
        {
            var test = session.Tests[index];
            test.Order = test.Order <= 0 ? index + 1 : test.Order;
            test.Status = string.IsNullOrWhiteSpace(test.Status) ? "Not started" : test.Status;
            NormalizeNodeOrder(test);
        }

        SyncLegacyNodes(session);
    }

    private static void SyncLegacyNodes(ProjectSessionState session)
    {
        session.Nodes = session.Tests
            .SelectMany(test => test.Nodes)
            .ToList();
    }

    private static int GetNextNodeId(ProjectSessionState session)
    {
        var allNodes = session.Tests.SelectMany(test => test.Nodes);
        return allNodes.Any() ? allNodes.Max(node => node.Id) + 1 : 1;
    }

    private static IReadOnlyList<NodeValueRecord> CreateDefaultValues(string nodeType, int order)
    {
        return nodeType == NodeTypeNames.Expected
            ? CreateExpectedDefaultValues(order)
            : CreateRequestDefaultValues(order);
    }

    private static IReadOnlyList<NodeValueRecord> CreateRequestDefaultValues(int order)
    {
        return
        [
            new NodeValueRecord("Method", "GET"),
            new NodeValueRecord("Url", "https://api.example.com"),
            new NodeValueRecord("Body", string.Empty),
            new NodeValueRecord("Response", string.Empty),
            new NodeValueRecord("StatusCode", string.Empty),
            new NodeValueRecord("Error", string.Empty),
            new NodeValueRecord("Status", "Draft"),
            new NodeValueRecord("Order", order.ToString())
        ];
    }

    private static IReadOnlyList<NodeValueRecord> CreateExpectedDefaultValues(int order)
    {
        return
        [
            new NodeValueRecord("ExpectedCode", "200"),
            new NodeValueRecord("ThrowbackError", "Expected response code did not match."),
            new NodeValueRecord("ContinueTest", bool.TrueString),
            new NodeValueRecord("ActualCode", string.Empty),
            new NodeValueRecord("Error", string.Empty),
            new NodeValueRecord("Status", "Draft"),
            new NodeValueRecord("Order", order.ToString())
        ];
    }

    private static IReadOnlyList<NodeValueRecord> UpsertNodeValue(
        IReadOnlyList<NodeValueRecord> values,
        string label,
        string value)
    {
        var updated = values.ToList();
        var index = updated.FindIndex(item => string.Equals(item.Label, label, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            updated[index] = new NodeValueRecord(updated[index].Label, value);
            return updated;
        }

        updated.Add(new NodeValueRecord(label, value));
        return updated;
    }

    private static void NormalizeNodeOrder(ProjectTestState test)
    {
        test.Nodes = test.Nodes
            .Select((node, index) => node with
            {
                X = 0,
                Y = index,
                Values = UpdateNodeOrderValue(node.Values, index + 1)
            })
            .ToList();
    }

    private static IReadOnlyList<NodeValueRecord> UpdateNodeOrderValue(IReadOnlyList<NodeValueRecord> values, int order)
    {
        if (values.Count == 0)
        {
            return CreateRequestDefaultValues(order);
        }

        var updated = values
            .Where(value => !string.Equals(value.Label, "Order", StringComparison.OrdinalIgnoreCase))
            .ToList();
        updated.Add(new NodeValueRecord("Order", order.ToString()));
        return updated;
    }

    private static int GetNodeOrder(IReadOnlyList<NodeValueRecord> values)
    {
        var orderValue = values.FirstOrDefault(value => string.Equals(value.Label, "Order", StringComparison.OrdinalIgnoreCase))?.Value;
        return int.TryParse(orderValue, out var order) ? order : 0;
    }

    private static IReadOnlyList<NodeValueRecord> ResetRuntimeValues(IReadOnlyList<NodeValueRecord> values, int order)
    {
        var runtimeLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ActualCode",
            "Error",
            "Response",
            "Status",
            "StatusCode"
        };

        var updated = values
            .Where(value => !runtimeLabels.Contains(value.Label) &&
                            !string.Equals(value.Label, "Order", StringComparison.OrdinalIgnoreCase))
            .ToList();

        updated.Add(new NodeValueRecord("Status", "Draft"));
        updated.Add(new NodeValueRecord("Order", order.ToString()));
        return updated;
    }
}
