using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Httpflow.Desktop.Dtos.Collaborators;
using Httpflow.Desktop.Models.Api;
using Httpflow.Desktop.Services.Api;

namespace Httpflow.Desktop.Services.Collaborators;

public sealed class CollaboratorsApiClient(HttpClient httpClient) : ApiClientBase(httpClient)
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

    public Task<ApiResult<List<ProjectInviteDto>>> GetMyInvitesAsync(string accessToken) =>
        GetAsync<List<ProjectInviteDto>>("api/v1/users/me/invites", accessToken);

    public Task<ApiResult<ProjectCollaboratorDto>> AcceptInviteAsync(string accessToken, int projectId) =>
        PutAsync<ProjectCollaboratorDto>($"api/v1/users/me/invites/{projectId}/accept", new { }, accessToken);

    public Task<ApiResult<ProjectInviteDto>> DeclineInviteAsync(string accessToken, int projectId) =>
        DeleteAsync<ProjectInviteDto>($"api/v1/users/me/invites/{projectId}", accessToken);
}
