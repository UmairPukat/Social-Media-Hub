using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;

namespace SocialMedia.Infrastructure.Meta;

public class MetaGraphApiClient : IMetaGraphApiClient
{
    private readonly MetaGraphClient _graph;
    private readonly IMetaMarketingContextFactory _contextFactory;
    private readonly ILogger<MetaGraphApiClient> _logger;

    public MetaGraphApiClient(
        MetaGraphClient graph,
        IMetaMarketingContextFactory contextFactory,
        ILogger<MetaGraphApiClient> logger)
    {
        _graph = graph;
        _contextFactory = contextFactory;
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
            async (version, token) => await _graph.GetAsync(version, path, token, cancellationToken, query));

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
            async (version, token) => await _graph.PostAsync(version, path, token, formFields, cancellationToken));

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
            async (version, token) => await _graph.PostJsonAsync(version, path, token, payload, cancellationToken));

    public async Task DeleteAsync(
        Guid userId,
        string menuType,
        string path,
        CancellationToken cancellationToken)
    {
        var context = await _contextFactory.CreateAsync(userId, menuType, cancellationToken);
        var sw = Stopwatch.StartNew();
        try
        {
            await _graph.DeleteAsync(context.GraphApiVersion, path, context.AccessToken, cancellationToken);
            LogSuccess(userId, "DELETE", path, 204, sw.ElapsedMilliseconds, null);
        }
        catch (InvalidOperationException ex)
        {
            throw MapException(ex, userId, "DELETE", path, sw.ElapsedMilliseconds);
        }
    }

    private async Task<JsonDocument> ExecuteAsync(
        Guid userId,
        string menuType,
        string method,
        string path,
        CancellationToken cancellationToken,
        Func<string, string, Task<JsonDocument>> action)
    {
        var context = await _contextFactory.CreateAsync(userId, menuType, cancellationToken);
        var sw = Stopwatch.StartNew();
        try
        {
            var doc = await action(context.GraphApiVersion, context.AccessToken);
            LogSuccess(userId, method, path, 200, sw.ElapsedMilliseconds, null);
            return doc;
        }
        catch (InvalidOperationException ex)
        {
            throw MapException(ex, userId, method, path, sw.ElapsedMilliseconds);
        }
    }

    private Exception MapException(InvalidOperationException ex, Guid userId, string method, string path, long durationMs)
    {
        var message = ex.Message;
        var statusCode = TryReadStatusCode(message);
        MetaGraphApiException mapped = statusCode.HasValue
            ? MetaGraphResponseHelper.ParseError(statusCode.Value, ExtractBody(message))
            : new MetaGraphApiException(message);

        LogFailure(userId, method, path, mapped.HttpStatusCode, durationMs, mapped.MetaErrorCode, mapped.MetaErrorMessage);
        return mapped;
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
