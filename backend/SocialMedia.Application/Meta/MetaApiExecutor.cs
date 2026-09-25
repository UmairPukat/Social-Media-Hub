using SocialMedia.Application.DTOs.Common;

namespace SocialMedia.Application.Meta;

public static class MetaApiExecutor
{
    public static async Task<ApiResponse<T>> RunAsync<T>(Func<Task<T>> action, string successMessage = "Success")
    {
        try
        {
            var data = await action();
            var response = ApiResponse<T>.Ok(data, successMessage);
            if (MetaGraphCallScope.Current is { } scope)
            {
                response.MetaUsage = scope.LatestUsage;
                response.GraphApiCallCount = scope.CallCount;
            }

            return response;
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
