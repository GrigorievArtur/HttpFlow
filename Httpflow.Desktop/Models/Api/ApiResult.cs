using System.Net;

namespace Httpflow.Desktop.Models.Api;

public sealed class ApiResult<T>
{
    private ApiResult(bool isSuccess, T? data, string? errorMessage, HttpStatusCode statusCode)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorMessage = errorMessage;
        StatusCode = statusCode;
    }

    public bool IsSuccess { get; }

    public T? Data { get; }

    public string? ErrorMessage { get; }

    public HttpStatusCode StatusCode { get; }

    public static ApiResult<T> Success(T data, HttpStatusCode statusCode) =>
        new(true, data, null, statusCode);

    public static ApiResult<T> Failure(string errorMessage, HttpStatusCode statusCode) =>
        new(false, default, errorMessage, statusCode);
}
