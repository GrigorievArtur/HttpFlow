using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Httpflow.Desktop.Models;

namespace Httpflow.Desktop.Services;

public sealed class AuthApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public AuthApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

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

    public async Task<ApiResult<UserProfile>> GetCurrentUserAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var user = await response.Content.ReadFromJsonAsync<UserProfile>(JsonOptions);
            if (user is not null)
            {
                return ApiResult<UserProfile>.Success(user, response.StatusCode);
            }
        }

        var errorMessage = await ReadErrorMessageAsync(response);
        return ApiResult<UserProfile>.Failure(errorMessage, response.StatusCode);
    }

    private async Task<ApiResult<T>> PostAsync<T>(string uri, object body)
    {
        using var response = await _httpClient.PostAsJsonAsync(uri, body);
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
            if (data is not null)
            {
                return ApiResult<T>.Success(data, response.StatusCode);
            }
        }

        var errorMessage = await ReadErrorMessageAsync(response);
        return ApiResult<T>.Failure(errorMessage, response.StatusCode);
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>(JsonOptions);
            if (problem is null)
            {
                return $"Request failed with status {(int)response.StatusCode}.";
            }

            var validationErrors = problem.Errors?
                .SelectMany(entry => entry.Value)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToArray();

            if (validationErrors is { Length: > 0 })
            {
                return string.Join(Environment.NewLine, validationErrors);
            }

            if (!string.IsNullOrWhiteSpace(problem.Detail))
            {
                return problem.Detail;
            }

            if (!string.IsNullOrWhiteSpace(problem.Title))
            {
                return problem.Title;
            }
        }
        catch (JsonException)
        {
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Invalid email or password.",
            HttpStatusCode.Conflict => "That account already exists.",
            _ => $"Request failed with status {(int)response.StatusCode}."
        };
    }
}
