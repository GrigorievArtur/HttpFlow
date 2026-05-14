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

public class GenericApiClient
{
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected readonly HttpClient _httpClient;

    public GenericApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    protected Task<ApiResult<T>> GetAsync<T>(string uri, string? accessToken = null) =>
        SendAsync<T>(HttpMethod.Get, uri, body: null, accessToken);

    protected async Task<ApiResult<T>> PostAsync<T>(string uri, object body)
    {
        return await SendAsync<T>(HttpMethod.Post, uri, body);
    }

    protected async Task<ApiResult<T>> PostAsync<T>(string uri, object body, string accessToken)
    {
        return await SendAsync<T>(HttpMethod.Post, uri, body, accessToken);
    }

    protected async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method,
        string uri,
        object? body = null,
        string? accessToken = null)
    {
        using var request = new HttpRequestMessage(method, uri);

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        using var response = await _httpClient.SendAsync(request);
        return await ToApiResultAsync<T>(response);
    }

    protected async Task<ApiResult<T>> ToApiResultAsync<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
            if (data is not null)
            {
                return ApiResult<T>.Success(data, response.StatusCode);
            }

            return ApiResult<T>.Failure("The server returned an empty response.", response.StatusCode);
        }

        var errorMessage = await ReadErrorMessageAsync(response);
        return ApiResult<T>.Failure(errorMessage, response.StatusCode);
    }

    protected static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
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
