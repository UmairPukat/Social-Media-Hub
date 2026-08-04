using SocialMedia.Domain.Common;

namespace SocialMedia.Domain.Entities;

/// <summary>
/// Lookup table for supported platforms (Facebook, Instagram, WhatsApp, …).
/// </summary>
public class Platform : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<SocialAccount> SocialAccounts { get; set; } = new List<SocialAccount>();
}
