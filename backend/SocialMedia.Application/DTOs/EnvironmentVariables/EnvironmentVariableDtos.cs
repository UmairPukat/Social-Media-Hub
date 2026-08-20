namespace SocialMedia.Application.DTOs.EnvironmentVariables;

public class EnvironmentVariableDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string Scope { get; set; } = string.Empty;
    public bool IsSensitive { get; set; }
    public bool IsMasked { get; set; }
}

public class UpsertEnvironmentVariableRequest
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string Scope { get; set; } = string.Empty;
}
