using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Httpflow.Desktop.Dtos.Projects;
using Httpflow.Desktop.Models;

namespace Httpflow.Desktop.Services;

public sealed class ProjectsApiClient(HttpClient httpClient) : GenericApiClient(httpClient)
{
    public Task<ApiResult<List<ProjectDto>>> GetProjectsAsync(string accessToken, int pageNumber = 1, int pageSize = 5) =>
        GetAsync<List<ProjectDto>>($"api/v1/projects?pageNumber={pageNumber}&pageSize={pageSize}", accessToken);

    public Task<ApiResult<ProjectDto>> CreateProjectAsync(string accessToken, CreateProjectDto project) =>
        PostAsync<ProjectDto>("api/v1/projects", project, accessToken);
}
