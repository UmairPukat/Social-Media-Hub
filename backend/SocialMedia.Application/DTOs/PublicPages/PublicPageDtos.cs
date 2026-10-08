namespace SocialMedia.Application.DTOs.PublicPages;

/// <summary>
/// Public Facebook Page metadata from Graph <c>/pages/search</c>.
/// Not persisted — returned live for competitor research / App Review only.
/// </summary>
public class PublicPageDto
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? About { get; set; }
    public int? FanCount { get; set; }
    public int? FollowersCount { get; set; }
    public string? Link { get; set; }
}
