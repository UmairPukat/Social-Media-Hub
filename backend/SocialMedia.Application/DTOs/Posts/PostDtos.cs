using SocialMedia.Domain.Enums;

namespace SocialMedia.Application.DTOs.Posts;

public class CreatePostRequest
{
    public Guid SocialProfileId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string? Title { get; set; }
    /// <summary>YouTube privacy: public, unlisted, or private.</summary>
    public string? Visibility { get; set; }
    /// <summary>TikTok privacy: public, friends, followers, or only_you.</summary>
    public string? Privacy { get; set; }
    public bool? AllowComment { get; set; }
    public bool? AllowDuet { get; set; }
    public bool? AllowStitch { get; set; }
}

public sealed class PublishMediaInput
{
    public Stream Stream { get; set; } = Stream.Null;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
}

public class SocialPostDto
{
    public Guid Id { get; set; }
    public Guid SocialProfileId { get; set; }
    public Guid PlatformId { get; set; }
    public string? PlatformCode { get; set; }
    public string? ProfileName { get; set; }
    public string? ProfileUsername { get; set; }
    public string? ExternalPostId { get; set; }
    public string? Text { get; set; }
    public string? Caption { get; set; }
    public ContentPostStatus Status { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public int ShareCount { get; set; }
    public int ViewCount { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PublishPostResponse
{
    public bool Success { get; set; }
    public SocialPostDto Post { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
