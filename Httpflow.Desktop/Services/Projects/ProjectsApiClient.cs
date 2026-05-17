using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Httpflow.Desktop.Dtos.Projects;
using Httpflow.Desktop.Models.Api;
using Httpflow.Desktop.Services.Api;

namespace Httpflow.Desktop.Services.Projects;

public sealed class ProjectsApiClient(HttpClient httpClient) : ApiClientBase(httpClient)
{
    public Task<ApiResult<List<ProjectDto>>> GetProjectsAsync(string accessToken, int pageNumber = 1, int pageSize = 5) =>
        GetAsync<List<ProjectDto>>($"api/v1/projects?pageNumber={pageNumber}&pageSize={pageSize}", accessToken);

    public Task<ApiResult<ProjectDto>> GetProjectByIdAsync(string accessToken, int projectId) =>
        GetAsync<ProjectDto>($"api/v1/projects/{projectId}", accessToken);

    public Task<ApiResult<ProjectDto>> CreateProjectAsync(string accessToken, CreateProjectDto project) =>
        PostAsync<ProjectDto>("api/v1/projects", project, accessToken);

    public Task<ApiResult<ProjectDto>> UpdateProjectAsync(string accessToken, int projectId, UpdateProjectDto project) =>
        PutAsync<ProjectDto>($"api/v1/projects/{projectId}", project, accessToken);

    public Task<ApiResult<ProjectDto>> DeleteProjectAsync(string accessToken, int projectId) =>
        DeleteAsync<ProjectDto>($"api/v1/projects/{projectId}", accessToken);
}
