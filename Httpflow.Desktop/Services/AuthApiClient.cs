using System.Net.Http;
using System.Threading.Tasks;
using Httpflow.Desktop.Models;

namespace Httpflow.Desktop.Services;

public sealed class AuthApiClient(HttpClient httpClient) : GenericApiClient(httpClient)
{
    public Task<ApiResult<AuthResponse>> LoginAsync(string email, string password) =>
        PostAsync<AuthResponse>("api/v1/auth/login", new
        {
            email,
            password
        });

    public Task<ApiResult<AuthResponse>> RegisterAsync(
        string firstName,
        string lastName,
        string email,
        string password) =>
        PostAsync<AuthResponse>("api/v1/auth/register", new
        {
            firstname = firstName,
            lastname = lastName,
            email,
            password
        });

    public Task<ApiResult<UserProfile>> GetCurrentUserAsync(string accessToken) =>
        GetAsync<UserProfile>("api/v1/users/me", accessToken);
}
