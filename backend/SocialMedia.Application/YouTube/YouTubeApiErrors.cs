namespace SocialMedia.Application.YouTube;

public static class YouTubeApiErrors
{
    public static bool IsCommentsDisabled(int statusCode, string? body)
    {
        if (statusCode != 403 || string.IsNullOrWhiteSpace(body))
            return false;

        return body.Contains("commentsDisabled", StringComparison.OrdinalIgnoreCase)
               || body.Contains("disabled comments", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCommentsDisabledMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return message.Contains("commentsDisabled", StringComparison.OrdinalIgnoreCase)
               || message.Contains("disabled comments", StringComparison.OrdinalIgnoreCase);
    }
}
