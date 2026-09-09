using SocialMedia.Application.DTOs.Common;

namespace SocialMedia.Application.Meta;

public static class MetaApiExecutor
{
    public static async Task<ApiResponse<T>> RunAsync<T>(Func<Task<T>> action, string successMessage = "Success")
    {
        try
        {
            var data = await action();
            return ApiResponse<T>.Ok(data, successMessage);
        }
        catch (MetaGraphApiException ex)
        {
            return ApiResponse<T>.FailMeta(ex.Message, ex.MetaErrorCode, ex.MetaErrorMessage);
        }
        catch (Exception ex)
        {
            return ApiResponse<T>.Fail(ex.Message);
        }
    }
}
