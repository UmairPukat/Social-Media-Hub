namespace SocialMedia.Application.DTOs.Common;

/// <summary>
/// Standard envelope every API endpoint returns, so the frontend can handle
/// success/failure the same way regardless of which endpoint it called.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public string? MetaErrorCode { get; set; }
    public string? MetaErrorMessage { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Success") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message) =>
        new() { Success = false, Message = message };

    public static ApiResponse<T> FailMeta(string message, string? metaErrorCode, string? metaErrorMessage) =>
        new()
        {
            Success = false,
            Message = message,
            MetaErrorCode = metaErrorCode,
            MetaErrorMessage = metaErrorMessage
        };
}
