namespace SocialMedia.Application.DTOs.PublicPages;

/// <summary>
/// Facebook Page metadata from Graph <c>me/accounts</c> for the connected user in this module.
/// </summary>
public class PublicPageDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? About { get; set; }
    public int? FanCount { get; set; }
    public int? FollowersCount { get; set; }
    public string? Link { get; set; }
}
