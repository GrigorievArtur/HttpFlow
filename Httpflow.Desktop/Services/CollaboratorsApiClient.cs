using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Httpflow.Desktop.Dtos.Collaborators;
using Httpflow.Desktop.Models;

namespace Httpflow.Desktop.Services;

public sealed class CollaboratorsApiClient(HttpClient httpClient) : GenericApiClient(httpClient)
{
    public Task<ApiResult<List<ProjectCollaboratorDto>>> GetCollaboratorsAsync(
        string accessToken,
        int projectId) =>
        GetAsync<List<ProjectCollaboratorDto>>($"api/v1/projects/{projectId}/collaborators", accessToken);

    public Task<ApiResult<ProjectCollaboratorDto>> AddCollaboratorAsync(
        string accessToken,
        int projectId,
        AddProjectCollaboratorDto collaborator) =>
        PostAsync<ProjectCollaboratorDto>($"api/v1/projects/{projectId}/collaborators", collaborator, accessToken);

    public Task<ApiResult<ProjectCollaboratorDto>> UpdateCollaboratorRoleAsync(
        string accessToken,
        int projectId,
        int userId,
        UpdateProjectCollaboratorRoleDto role) =>
        PutAsync<ProjectCollaboratorDto>($"api/v1/projects/{projectId}/collaborators/{userId}/role", role, accessToken);
}
