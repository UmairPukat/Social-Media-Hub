namespace SocialMedia.Application.DTOs.TikTok;

public class TikTokSyncResultDto
{
    public string PlatformCode { get; set; } = "tiktok";
    public string MenuType { get; set; } = string.Empty;
    public int Fetched { get; set; }
    public int Stored { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public string? Message { get; set; }
}
