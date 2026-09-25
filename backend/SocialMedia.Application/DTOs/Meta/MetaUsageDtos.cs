namespace SocialMedia.Application.DTOs.Meta;

public class MetaUsageSnapshotDto
{
    public int BusinessUseCasePercent { get; set; }
    public int AdAccountPercent { get; set; }
    public int AppPercent { get; set; }
    public int OverallPercent { get; set; }
    public bool IsApproachingLimit { get; set; }
    public string? RawBusinessUseCaseUsage { get; set; }
    public string? RawAdAccountUsage { get; set; }
    public string? RawAppUsage { get; set; }
}

public class MetaApiCallLogEntryDto
{
    public DateTime TimestampUtc { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int? StatusCode { get; set; }
    public string? ErrorCode { get; set; }
}

public class MetaApiHealthDailyPointDto
{
    public string Date { get; set; } = string.Empty;
    public int Calls { get; set; }
    public int Errors { get; set; }
}

public class MetaApiHealthDto
{
    public int TotalCalls15Days { get; set; }
    public double ErrorRatePercent { get; set; }
    public string TierLabel { get; set; } = string.Empty;
    public string TierStatus { get; set; } = string.Empty;
    public MetaUsageSnapshotDto CurrentUsage { get; set; } = new();
    public IReadOnlyList<MetaApiHealthDailyPointDto> DailyCalls { get; set; } = Array.Empty<MetaApiHealthDailyPointDto>();
}
