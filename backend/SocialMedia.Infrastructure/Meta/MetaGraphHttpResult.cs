using System.Text.Json;
using SocialMedia.Application.DTOs.Meta;

namespace SocialMedia.Infrastructure.Meta;

public sealed class MetaGraphHttpResult : IDisposable
{
    public MetaGraphHttpResult(JsonDocument document, MetaUsageSnapshotDto usage, int statusCode)
    {
        Document = document;
        Usage = usage;
        StatusCode = statusCode;
    }

    public JsonDocument Document { get; }
    public MetaUsageSnapshotDto Usage { get; }
    public int StatusCode { get; }

    public void Dispose() => Document.Dispose();
}
