namespace SocialMedia.Application.YouTube;

/// <summary>
/// YouTube returns 403 commentsDisabled when comment threads cannot be read for a video.
/// </summary>
public sealed class YouTubeCommentsDisabledException : Exception
{
    public YouTubeCommentsDisabledException(string videoId)
        : base($"Comments are disabled for YouTube video '{videoId}'.")
    {
        VideoId = videoId;
    }

    public string VideoId { get; }
}
