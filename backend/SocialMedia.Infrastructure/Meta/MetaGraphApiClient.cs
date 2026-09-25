using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;

namespace SocialMedia.Infrastructure.Meta;

public class MetaGraphApiClient : IMetaGraphApiClient
{
    private static readonly int[] RetryDelaysMs = [60_000, 120_000, 240_000];
    private static readonly HashSet<string> RateLimitErrorCodes = new(StringComparer.Ordinal)
    {
        "4", "17", "32", "80004", "80005"
    };

    private readonly MetaGraphClient _graph;
    private readonly IMetaMarketingContextFactory _contextFactory;
    private readonly IMetaApiCallTracker _callTracker;
    private readonly ILogger<MetaGraphApiClient> _logger;

    public MetaGraphApiClient(
        MetaGraphClient graph,
        IMetaMarketingContextFactory contextFactory,
        IMetaApiCallTracker callTracker,
        ILogger<MetaGraphApiClient> logger)
    {
        _graph = graph;
        _contextFactory = contextFactory;
        _callTracker = callTracker;
        _logger = logger;
    }

    public Task<JsonDocument> GetAsync(
        Guid userId,
        string menuType,
        string path,
        CancellationToken cancellationToken,
        params (string Key, string Value)[] query)
        => ExecuteAsync(
            userId,
            menuType,
            "GET",
            path,
            cancellationToken,
            async (version, token) =>
                await _graph.GetDetailedAsync(version, path, token, cancellationToken, query));

    public Task<JsonDocument> PostFormAsync(
        Guid userId,
        string menuType,
        string path,
        IDictionary<string, string> formFields,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            userId,
            menuType,
            "POST",
            path,
            cancellationToken,
            async (version, token) =>
                await _graph.PostFormDetailedAsync(version, path, token, formFields, cancellationToken));

    public Task<JsonDocument> PostJsonAsync(
        Guid userId,
        string menuType,
        string path,
        object payload,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            userId,
            menuType,
            "POST",
            path,
            cancellationToken,
            async (version, token) =>
                await _graph.PostJsonDetailedAsync(version, path, token, payload, cancellationToken));

    public async Task DeleteAsync(
        Guid userId,
        string menuType,
        string path,
        CancellationToken cancellationToken)
    {
        var context = await _contextFactory.CreateAsync(userId, menuType, cancellationToken);
        var sw = Stopwatch.StartNew();
        var attempt = 0;

        while (true)
        {
            try
            {
                await _graph.DeleteAsync(context.GraphApiVersion, path, context.AccessToken, cancellationToken);
                RecordSuccess(userId, "DELETE", path, 204, sw.ElapsedMilliseconds, null);
                return;
            }
            catch (InvalidOperationException ex)
            {
                var mapped = MapException(ex, userId, "DELETE", path, sw.ElapsedMilliseconds);
                if (mapped is MetaGraphApiException rateLimit && ShouldRetry(rateLimit, attempt))
                {
                    await DelayForRetry(attempt, cancellationToken);
                    attempt += 1;
                    sw.Restart();
                    continue;
                }

                throw mapped;
            }
        }
    }

    private async Task<JsonDocument> ExecuteAsync(
        Guid userId,
        string menuType,
        string method,
        string path,
        CancellationToken cancellationToken,
        Func<string, string, Task<MetaGraphHttpResult>> action)
    {
        var context = await _contextFactory.CreateAsync(userId, menuType, cancellationToken);
        var attempt = 0;

        while (true)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var result = await action(context.GraphApiVersion, context.AccessToken);
                RecordSuccess(userId, method, path, result.StatusCode, sw.ElapsedMilliseconds, result.Usage);
                return result.Document;
            }
            catch (InvalidOperationException ex)
            {
                var mapped = MapException(ex, userId, method, path, sw.ElapsedMilliseconds);
                if (mapped is MetaGraphApiException rateLimit && ShouldRetry(rateLimit, attempt))
                {
                    await DelayForRetry(attempt, cancellationToken);
                    attempt += 1;
                    continue;
                }

                throw mapped;
            }
        }
    }

    private void RecordSuccess(
        Guid userId,
        string method,
        string path,
        int status,
        long durationMs,
        Application.DTOs.Meta.MetaUsageSnapshotDto? usage)
    {
        MetaGraphCallScope.Current?.RecordCall(usage);
        _callTracker.Record(userId, method, path, true, status, null, usage);
        LogSuccess(userId, method, path, status, durationMs, null);
    }

    private Exception MapException(InvalidOperationException ex, Guid userId, string method, string path, long durationMs)
    {
        var message = ex.Message;
        var statusCode = TryReadStatusCode(message);
        MetaGraphApiException mapped = statusCode.HasValue
            ? MetaGraphResponseHelper.ParseError(statusCode.Value, ExtractBody(message))
            : new MetaGraphApiException(message);

        _callTracker.Record(
            userId,
            method,
            path,
            false,
            mapped.HttpStatusCode,
            mapped.MetaErrorCode,
            MetaGraphCallScope.Current?.LatestUsage);

        LogFailure(userId, method, path, mapped.HttpStatusCode, durationMs, mapped.MetaErrorCode, mapped.MetaErrorMessage);
        return mapped;
    }

    private static bool ShouldRetry(MetaGraphApiException ex, int attempt) =>
        attempt < RetryDelaysMs.Length && IsRateLimitError(ex);

    private static bool IsRateLimitError(MetaGraphApiException ex)
    {
        if (string.IsNullOrWhiteSpace(ex.MetaErrorCode))
            return false;

        var code = ex.MetaErrorCode.Split(':')[0];
        return RateLimitErrorCodes.Contains(code);
    }

    private static async Task DelayForRetry(int attempt, CancellationToken cancellationToken)
    {
        var delay = RetryDelaysMs[attempt];
        await Task.Delay(delay, cancellationToken);
    }

    private static int? TryReadStatusCode(string message)
    {
        var start = message.IndexOf('(');
        var end = message.IndexOf(')', start + 1);
        if (start < 0 || end <= start)
            return null;

        return int.TryParse(message[(start + 1)..end], out var code) ? code : null;
    }

    private static string ExtractBody(string message)
    {
        var idx = message.IndexOf("): ", StringComparison.Ordinal);
        return idx >= 0 ? message[(idx + 3)..] : message;
    }

    private void LogSuccess(Guid userId, string method, string path, int status, long durationMs, string? objectId) =>
        _logger.LogInformation(
            "Meta Graph {Method} {Path} succeeded for user {UserId}. Status={Status}, ObjectId={ObjectId}, DurationMs={DurationMs}",
            method,
            path,
            userId,
            status,
            objectId,
            durationMs);

    private void LogFailure(
        Guid userId,
        string method,
        string path,
        int? status,
        long durationMs,
        string? metaErrorCode,
        string? metaErrorMessage) =>
        _logger.LogWarning(
            "Meta Graph {Method} {Path} failed for user {UserId}. Status={Status}, MetaErrorCode={MetaErrorCode}, MetaErrorMessage={MetaErrorMessage}, DurationMs={DurationMs}",
            method,
            path,
            userId,
            status,
            metaErrorCode,
            metaErrorMessage,
            durationMs);
}
