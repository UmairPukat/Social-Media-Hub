using SocialMedia.Application.DTOs.Meta;

namespace SocialMedia.Application.Meta;

public sealed class MetaGraphCallScope : IDisposable
{
    private static readonly AsyncLocal<MetaGraphCallScope?> CurrentScope = new();

    public static MetaGraphCallScope? Current => CurrentScope.Value;

    public int CallCount { get; private set; }
    public MetaUsageSnapshotDto? LatestUsage { get; private set; }

    public static MetaGraphCallScope BeginRequest()
    {
        var scope = new MetaGraphCallScope();
        CurrentScope.Value = scope;
        return scope;
    }

    public void RecordCall(MetaUsageSnapshotDto? usage)
    {
        CallCount += 1;
        if (usage is not null)
            LatestUsage = usage;
    }

    public void Dispose()
    {
        if (ReferenceEquals(CurrentScope.Value, this))
            CurrentScope.Value = null;
    }
}
