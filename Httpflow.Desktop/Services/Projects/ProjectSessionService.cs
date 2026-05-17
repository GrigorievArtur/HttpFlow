using System;
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

    public void UpsertNode(CanvasNodeRecord nodeRecord)
    {
        EnsureProjectLoaded();

        var index = CurrentProject!.Nodes.FindIndex(node => node.Id == nodeRecord.Id);
        if (index >= 0)
        {
            CurrentProject.Nodes[index] = nodeRecord;
            MarkDirty();
            return;
        }

        CurrentProject.Nodes.Add(nodeRecord);
        MarkDirty();
    }

    public void MoveNode(int nodeId, int x, int y)
    {
        EnsureProjectLoaded();

        var index = CurrentProject!.Nodes.FindIndex(node => node.Id == nodeId);
        if (index < 0)
        {
            return;
        }

        CurrentProject.Nodes[index] = CurrentProject.Nodes[index] with
        {
            X = x,
            Y = y
        };

        MarkDirty();
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
            return new ProjectSessionState
            {
                ProjectId = project.Id,
                Name = project.Name
            };
        }

        try
        {
            var session = JsonSerializer.Deserialize<ProjectSessionState>(project.Value, JsonOptions);
            if (session is not null)
            {
                session.ProjectId = project.Id;
                session.Name = string.IsNullOrWhiteSpace(session.Name) ? project.Name : session.Name;
                session.Nodes ??= [];
                return session;
            }
        }
        catch (JsonException)
        {
        }

        return new ProjectSessionState
        {
            ProjectId = project.Id,
            Name = project.Name
        };
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
}
