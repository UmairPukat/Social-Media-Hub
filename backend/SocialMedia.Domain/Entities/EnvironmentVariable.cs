using SocialMedia.Domain.Common;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Domain.Entities;

/// <summary>
/// Deployment environment variable definition for frontend or backend hosting.
/// </summary>
public class EnvironmentVariable : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public EnvironmentVariableScope Scope { get; set; }
    public bool IsSensitive { get; set; }
}
