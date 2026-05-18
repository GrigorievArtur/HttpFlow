using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
            Nodes = []
        };

        CurrentProject.Tests.Add(test);
        SyncLegacyNodes(CurrentProject);
        MarkDirty();
        return test;
    }

    public CanvasNodeRecord AddNode(int testId)
    {
        EnsureProjectLoaded();
        EnsureTestsInitialized(CurrentProject!);

        var test = CurrentProject!.Tests.FirstOrDefault(item => item.Id == testId);
        if (test is null)
        {
            throw new InvalidOperationException($"Test with id {testId} was not found.");
        }

        var nextNodeId = GetNextNodeId(CurrentProject);
        var node = new CanvasNodeRecord(
            nextNodeId,
            $"Node {test.Nodes.Count + 1}",
            "Request",
            0,
            test.Nodes.Count,
            CreateDefaultValues(test.Nodes.Count + 1));

        test.Nodes.Add(node);
        NormalizeNodeOrder(test);
        SyncLegacyNodes(CurrentProject);
        MarkDirty();
        return node;
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
        CurrentProject.Tests.Insert(targetIndex, test);

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
        test.Nodes.Insert(targetIndex, node);

        NormalizeNodeOrder(test);
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
                Nodes = session.Nodes
                    .OrderBy(node => node.Y)
                    .ThenBy(node => node.X)
                    .ToList()
            });
        }

        foreach (var test in session.Tests)
        {
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

    private static IReadOnlyList<NodeValueRecord> CreateDefaultValues(int order)
    {
        return
        [
            new NodeValueRecord("Status", "Draft"),
            new NodeValueRecord("Order", order.ToString())
        ];
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
            return CreateDefaultValues(order);
        }

        var updated = values
            .Where(value => !string.Equals(value.Label, "Order", StringComparison.OrdinalIgnoreCase))
            .ToList();
        updated.Add(new NodeValueRecord("Order", order.ToString()));
        return updated;
    }
}
