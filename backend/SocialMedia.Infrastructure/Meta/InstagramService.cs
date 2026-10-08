using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialMedia.Application.DTOs.Inbox;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;
using SocialMedia.Application.Settings;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Infrastructure.Meta;

/// <summary>
/// Instagram Graph API for both connection types:
/// Facebook Login (graph.facebook.com + Page token) and Instagram Login (graph.instagram.com + IG user token).
/// Inbox/webhook pipelines stay shared; only the Meta host and token kind differ.
/// </summary>
public class InstagramService : IInstagramService
{
    private readonly MetaGraphClient _graph;
    private readonly IFacebookService _facebookService;
    private readonly IPublishMediaCacheService _publishMediaCache;
    private readonly InstagramSettings _instagram;
    private readonly InstagramLoginSettings _instagramLogin;
    private readonly FacebookSettings _facebook;
    private readonly IProcessDataStoreFactory _processData;
    private IProcessDataStore? _store;
    private string _menuType = string.Empty;
    private readonly IInboxRealtimeNotifier _inboxRealtime;
    private readonly ILogger<InstagramService> _logger;

    public InstagramService(
        MetaGraphClient graph,
        IFacebookService facebookService,
        IPublishMediaCacheService publishMediaCache,
        IOptions<MetaSettings> options,
        IProcessDataStoreFactory processData,
        IInboxRealtimeNotifier inboxRealtime,
        ILogger<InstagramService> logger)
    {
        _graph = graph;
        _facebookService = facebookService;
        _publishMediaCache = publishMediaCache;
        _instagram = options.Value.Instagram;
        _instagramLogin = options.Value.InstagramLogin;
        _facebook = options.Value.Facebook;
        _processData = processData;
        _inboxRealtime = inboxRealtime;
        _logger = logger;
    }

    private string GraphVersion =>
        !string.IsNullOrWhiteSpace(_instagram.GraphApiVersion)
            ? _instagram.GraphApiVersion
            : !string.IsNullOrWhiteSpace(_facebook.GraphApiVersion)
                ? _facebook.GraphApiVersion
                : "v21.0";

    private string InstagramLoginGraphVersion =>
        FirstNonEmpty(_instagramLogin.GraphApiVersion, _instagram.GraphApiVersion, GraphVersion);

    private string AppId =>
        !string.IsNullOrWhiteSpace(_facebook.AppId) ? _facebook.AppId : _instagram.AppId;

    private string AppSecret =>
        !string.IsNullOrWhiteSpace(_facebook.AppSecret) ? _facebook.AppSecret : _instagram.AppSecret;

    private string InstagramLoginAppId =>
        FirstNonEmpty(_instagramLogin.AppId, _instagram.AppId);

    private string InstagramLoginAppSecret =>
        FirstNonEmpty(_instagramLogin.AppSecret, _instagram.AppSecret);

    /// <summary>Facebook Login: authorization code → short token → long-lived user token.</summary>
    public async Task<OAuthTokenResult> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(AppId) || string.IsNullOrWhiteSpace(AppSecret))
            throw new InvalidOperationException("Facebook AppId/AppSecret are required for Instagram Facebook Login.");

        using var shortLived = await _graph.GetAsync(
            GraphVersion, "oauth/access_token", string.Empty, cancellationToken,
            ("client_id", AppId),
            ("client_secret", AppSecret),
            ("redirect_uri", redirectUri),
            ("code", code));

        var shortToken = shortLived.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Meta did not return an access token.");

        try
        {
            using var longLived = await _graph.GetAsync(
                GraphVersion, "oauth/access_token", string.Empty, cancellationToken,
                ("grant_type", "fb_exchange_token"),
                ("client_id", AppId),
                ("client_secret", AppSecret),
                ("fb_exchange_token", shortToken));

            return ParseToken(longLived.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Long-lived token exchange failed; using short-lived token.");
            return ParseToken(shortLived.RootElement);
        }
    }

    public async Task<(string Id, string Name)> GetMeAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var doc = await _graph.GetAsync(GraphVersion, "me", accessToken, cancellationToken, ("fields", "id,name"));
        var id = doc.RootElement.GetProperty("id").GetString() ?? string.Empty;
        var name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "Instagram User" : "Instagram User";
        return (id, name);
    }

    /// <summary>Native Instagram Login: authorization code → short token → long-lived IG user token.</summary>
    public async Task<OAuthTokenResult> ExchangeInstagramLoginCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(InstagramLoginAppId) || string.IsNullOrWhiteSpace(InstagramLoginAppSecret))
            throw new InvalidOperationException("Instagram Login AppId/AppSecret are required.");

        using var shortLived = await _graph.PostInstagramOAuthAsync(new Dictionary<string, string>
        {
            ["client_id"] = InstagramLoginAppId,
            ["client_secret"] = InstagramLoginAppSecret,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri,
            ["code"] = code
        }, cancellationToken);

        var shortToken = shortLived.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Instagram did not return an access token.");

        try
        {
            using var longLived = await _graph.GetInstagramTokenAsync(
                "access_token",
                cancellationToken,
                ("grant_type", "ig_exchange_token"),
                ("client_secret", InstagramLoginAppSecret),
                ("access_token", shortToken));

            return ParseToken(longLived.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Instagram Login long-lived token exchange failed; using short-lived token.");
            return ParseToken(shortLived.RootElement);
        }
    }

    public async Task<(string Id, string Name)> GetInstagramLoginMeAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var doc = await _graph.GetInstagramAsync(
            InstagramLoginGraphVersion,
            "me",
            accessToken,
            cancellationToken,
            ("fields", "user_id,username,name,account_type,profile_picture_url"));

        var root = doc.RootElement;
        var id =
            (root.TryGetProperty("user_id", out var userId) ? userId.ToString() : null)
            ?? (root.TryGetProperty("id", out var idProp) ? idProp.ToString() : null)
            ?? string.Empty;
        var username = root.TryGetProperty("username", out var u) ? u.GetString() : null;
        var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
        return (id, name ?? username ?? "Instagram User");
    }

    public async Task<IReadOnlyList<SocialProfileDraft>> DiscoverInstagramLoginProfilesAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var doc = await _graph.GetInstagramAsync(
            InstagramLoginGraphVersion,
            "me",
            accessToken,
            cancellationToken,
            ("fields", "user_id,username,name,account_type,profile_picture_url"));

        var root = doc.RootElement;
        var professionalId = root.TryGetProperty("user_id", out var userId) ? userId.ToString() : null;
        var appScopedId = root.TryGetProperty("id", out var idProp) ? idProp.ToString() : null;
        var id = professionalId ?? appScopedId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            return Array.Empty<SocialProfileDraft>();

        var username = root.TryGetProperty("username", out var u) ? u.GetString() : null;
        var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;

        // Webhooks may key entry.id on either id, so both are stored for lookup.
        var alternates = new[] { professionalId, appScopedId }
            .Where(value => !string.IsNullOrWhiteSpace(value) && value != id)
            .Select(value => value!)
            .Distinct()
            .ToList();

        return
        [
            new SocialProfileDraft
            {
                ExternalProfileId = id,
                Name = name ?? username ?? "Instagram",
                Username = username,
                ProfileImage = root.TryGetProperty("profile_picture_url", out var pic) ? pic.GetString() : null,
                ProfileType = "InstagramLogin",
                AlternateExternalIds = alternates
            }
        ];
    }

    private static OAuthTokenResult ParseToken(JsonElement root)
    {
        var token = root.GetProperty("access_token").GetString() ?? string.Empty;
        DateTime? expires = null;
        if (root.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var seconds))
            expires = DateTime.UtcNow.AddSeconds(seconds);

        return new OAuthTokenResult
        {
            AccessToken = token,
            ExpiresAt = expires,
            TokenType = root.TryGetProperty("token_type", out var tt) ? tt.GetString() : null
        };
    }

    public async Task<IReadOnlyList<SocialProfileDraft>> DiscoverProfilesAsync(string userAccessToken, CancellationToken cancellationToken = default)
    {
        using var pagesDoc = await _graph.GetAsync(
            GraphVersion,
            "me/accounts",
            userAccessToken,
            cancellationToken,
            ("fields", "id,name,access_token,instagram_business_account{id,username,profile_picture_url,name}"));

        var list = new List<SocialProfileDraft>();
        if (!pagesDoc.RootElement.TryGetProperty("data", out var data))
            return list;

        foreach (var page in data.EnumerateArray())
        {
            if (!page.TryGetProperty("instagram_business_account", out var ig))
                continue;

            var username = ig.TryGetProperty("username", out var u) ? u.GetString() : null;
            var name = ig.TryGetProperty("name", out var n) ? n.GetString() : null;
            list.Add(new SocialProfileDraft
            {
                ExternalProfileId = ig.GetProperty("id").GetString() ?? string.Empty,
                Name = name ?? username ?? "Instagram",
                Username = username,
                ProfileImage = ig.TryGetProperty("profile_picture_url", out var pic) ? pic.GetString() : null,
                ProfileType = "InstagramBusiness",
                PageId = page.TryGetProperty("id", out var pageId) ? pageId.GetString() : null,
                PageAccessToken = page.TryGetProperty("access_token", out var t) ? t.GetString() : null
            });
        }

        return list;
    }

    public Task<IReadOnlyList<MetaPageInfo>> ListPagesAsync(string userAccessToken, CancellationToken cancellationToken = default)
        => _graph.ListPagesAsync(GraphVersion, userAccessToken, cancellationToken);

    public async Task<PostDto> CreatePostAsync(
        MetaCallContext context,
        string content,
        string? mediaUrl,
        Stream? mediaStream = null,
        string? mediaFileName = null,
        string? mediaContentType = null,
        CancellationToken cancellationToken = default)
    {
        var connectionType = context.InstagramConnectionType;
        var resolvedUrl = mediaUrl;

        if (mediaStream is not null)
        {
            mediaStream.Position = 0;
            if (connectionType == InstagramConnectionType.InstagramLogin)
            {
                resolvedUrl = await _publishMediaCache.StoreAsync(
                    mediaStream,
                    mediaFileName,
                    mediaContentType,
                    cancellationToken);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(context.PageExternalId))
                    throw new InvalidOperationException("Instagram via Facebook Login requires a linked Facebook Page for file uploads.");

                if (IsVideoMedia(mediaContentType, mediaFileName))
                    throw new InvalidOperationException("Instagram video uploads from files are not supported yet. Use a public video URL.");

                resolvedUrl = await _facebookService.UploadUnpublishedPhotoUrlAsync(
                    context.PageExternalId,
                    context.AccessToken,
                    mediaStream,
                    mediaFileName ?? "photo.jpg",
                    mediaContentType,
                    cancellationToken);
            }
        }

        if (string.IsNullOrWhiteSpace(resolvedUrl))
            throw new InvalidOperationException("Instagram posts require an image or video.");

        var isVideo = IsVideoMedia(mediaContentType, resolvedUrl);
        var mediaFields = new Dictionary<string, string> { ["caption"] = content };
        if (isVideo)
            mediaFields["video_url"] = resolvedUrl;
        else
            mediaFields["image_url"] = resolvedUrl;

        using var containerDoc = connectionType == InstagramConnectionType.InstagramLogin
            ? await _graph.PostInstagramAsync(InstagramLoginGraphVersion, $"{context.ProfileExternalId}/media", context.AccessToken, mediaFields, cancellationToken)
            : await _graph.PostAsync(GraphVersion, $"{context.ProfileExternalId}/media", context.AccessToken, mediaFields, cancellationToken);

        var creationId = containerDoc.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Instagram did not return a media container id.");

        await WaitForMediaContainerReadyAsync(
            creationId,
            context.AccessToken,
            connectionType,
            isVideo,
            cancellationToken);

        using var publishDoc = connectionType == InstagramConnectionType.InstagramLogin
            ? await _graph.PostInstagramAsync(InstagramLoginGraphVersion, $"{context.ProfileExternalId}/media_publish", context.AccessToken,
                new Dictionary<string, string> { ["creation_id"] = creationId }, cancellationToken)
            : await _graph.PostAsync(GraphVersion, $"{context.ProfileExternalId}/media_publish", context.AccessToken,
                new Dictionary<string, string> { ["creation_id"] = creationId }, cancellationToken);

        return new PostDto
        {
            Id = publishDoc.RootElement.GetProperty("id").GetString() ?? creationId,
            Message = content,
            CreatedTime = DateTime.UtcNow
        };
    }

    private static bool IsVideoMedia(string? contentType, string? nameOrUrl)
    {
        if (!string.IsNullOrWhiteSpace(contentType) && contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return true;

        var value = (nameOrUrl ?? string.Empty).ToLowerInvariant();
        return value.EndsWith(".mp4") || value.EndsWith(".mov") || value.EndsWith(".webm") || value.EndsWith(".mkv");
    }

    /// <summary>
    /// Instagram rejects media_publish until the container status_code is FINISHED (especially for video).
    /// </summary>
    private async Task WaitForMediaContainerReadyAsync(
        string creationId,
        string accessToken,
        InstagramConnectionType connectionType,
        bool isVideo,
        CancellationToken cancellationToken)
    {
        var maxAttempts = isVideo ? 40 : 20;
        var delayMs = isVideo ? 3000 : 2000;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            using var doc = connectionType == InstagramConnectionType.InstagramLogin
                ? await _graph.GetInstagramAsync(
                    InstagramLoginGraphVersion,
                    creationId,
                    accessToken,
                    cancellationToken,
                    ("fields", "status_code"))
                : await _graph.GetAsync(
                    GraphVersion,
                    creationId,
                    accessToken,
                    cancellationToken,
                    ("fields", "status_code"));

            var status = doc.RootElement.TryGetProperty("status_code", out var statusProp)
                ? statusProp.GetString()
                : null;

            if (string.Equals(status, "FINISHED", StringComparison.OrdinalIgnoreCase))
                return;

            if (string.Equals(status, "ERROR", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "EXPIRED", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Instagram could not process the media (status: {status ?? "unknown"}).");
            }

            if (attempt < maxAttempts - 1)
                await Task.Delay(delayMs, cancellationToken);
        }

        throw new InvalidOperationException(
            "Instagram is still processing the media. Wait a moment and try publishing again.");
    }

    /// <summary>Instagram <c>media_url</c> is the same field Facebook Inbox uses as <c>full_picture</c>.</summary>
    private const string InstagramMediaPictureFields =
        "id,caption,media_type,media_url,thumbnail_url,permalink,timestamp";
    private const string InstagramMediaPictureFieldsWithChildren =
        "id,caption,media_type,media_url,thumbnail_url,permalink,timestamp,children{id,media_type,media_url,thumbnail_url}";

    public async Task<RemotePostSnapshot?> GetMediaSnapshotAsync(
        string accessToken,
        string mediaId,
        InstagramConnectionType connectionType = InstagramConnectionType.FacebookLogin,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await FetchMediaSnapshotAsync(accessToken, mediaId, connectionType, cancellationToken);
        return await LocalizeSnapshotMediaAsync(snapshot, cancellationToken);
    }

    public async Task<RemotePostSnapshot?> GetOwnedMediaSnapshotAsync(
        string accessToken,
        string mediaId,
        InstagramConnectionType connectionType,
        string? commentId = null,
        string? ownerExternalId = null,
        string? ownerMetadataJson = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await FetchOwnedMediaSnapshotAsync(
            accessToken,
            mediaId,
            connectionType,
            commentId,
            ownerExternalId,
            ownerMetadataJson,
            cancellationToken);
        return await LocalizeSnapshotMediaAsync(snapshot, cancellationToken);
    }

    /// <summary>
    /// Same as Facebook <c>ResolvePostAsync</c>: Graph GET the post picture, save a URL Inbox can
    /// render, then map the comment. Instagram Login CDN URLs are copied to publish-cache first
    /// because the browser cannot display <c>media_url</c> the way it can Facebook <c>full_picture</c>.
    /// </summary>
    private async Task<PostEntityBase> ResolveInstagramPostAsync(
        SocialProfileEntityBase profile,
        SocialAccountEntityBase account,
        InstagramConnectionType connectionType,
        string mediaId,
        DateTime publishedAt,
        string? commentId,
        RemotePostSnapshot? knownPost,
        CancellationToken cancellationToken)
    {
        var tokens = await ResolveAccessTokensAsync(account, connectionType, cancellationToken);

        return await MetaPostStore.ResolveAsync(
            _store!,
            profile,
            account.PlatformId,
            mediaId,
            publishedAt,
            ct => LoadInstagramPictureAsync(
                tokens,
                mediaId,
                connectionType,
                commentId,
                profile.ExternalProfileId,
                profile.MetadataJson,
                knownPost,
                ct),
            "Instagram post",
            requireMedia: true,
            cancellationToken: cancellationToken);
    }

    private async Task<RemotePostSnapshot?> LoadInstagramPictureAsync(
        IReadOnlyList<string> tokens,
        string mediaId,
        InstagramConnectionType connectionType,
        string? commentId,
        string? ownerExternalId,
        string? ownerMetadataJson,
        RemotePostSnapshot? knownPost,
        CancellationToken cancellationToken)
    {
        foreach (var token in tokens)
        {
            var snapshot = await GetOwnedMediaSnapshotAsync(
                token,
                mediaId,
                connectionType,
                commentId,
                ownerExternalId,
                ownerMetadataJson,
                cancellationToken);
            if (HasPicture(snapshot))
                return snapshot;
        }

        return await LocalizeSnapshotMediaAsync(knownPost, cancellationToken);
    }

    private async Task<RemotePostSnapshot?> FetchOwnedMediaSnapshotAsync(
        string accessToken,
        string mediaId,
        InstagramConnectionType connectionType,
        string? commentId,
        string? ownerExternalId,
        string? ownerMetadataJson,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(mediaId))
            return null;

        var byId = await FetchMediaSnapshotAsync(accessToken, mediaId, connectionType, cancellationToken);
        if (HasPicture(byId))
            return byId;

        RemotePostSnapshot? best = byId;
        foreach (var host in HostOrder(connectionType))
        {
            if (!string.IsNullOrWhiteSpace(commentId))
            {
                var fromComment = await FetchMediaFromCommentAsync(
                    accessToken, commentId, mediaId, host, cancellationToken);
                if (HasPicture(fromComment))
                    return fromComment;
                best ??= fromComment;
            }

            var fromFeed = await FindMediaInOwnedFeedAsync(
                accessToken, mediaId, ownerExternalId, ownerMetadataJson, host, cancellationToken);
            if (HasPicture(fromFeed))
                return fromFeed;
            best ??= fromFeed;
        }

        if (!HasPicture(best) && !string.IsNullOrWhiteSpace(best?.Permalink))
        {
            var oembed = await FetchOEmbedThumbnailAsync(
                accessToken, best!.Permalink!, connectionType, cancellationToken);
            if (!string.IsNullOrWhiteSpace(oembed))
            {
                best.ThumbnailUrl = oembed;
                return best;
            }
        }

        return best;
    }

    private async Task<RemotePostSnapshot?> FetchMediaSnapshotAsync(
        string accessToken,
        string mediaId,
        InstagramConnectionType connectionType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(mediaId))
            return null;

        RemotePostSnapshot? best = null;
        foreach (var host in HostOrder(connectionType))
        {
            foreach (var fields in new[] { InstagramMediaPictureFields, InstagramMediaPictureFieldsWithChildren })
            {
                try
                {
                    using var doc = await GetGraphJsonAsync(
                        host, mediaId, accessToken, fields, cancellationToken);
                    var snapshot = ParseMediaSnapshot(doc.RootElement, mediaId);
                    if (snapshot is null)
                        continue;

                    LogApiDecision(null, host, "GetPost", success: true);
                    if (HasPicture(snapshot))
                        return snapshot;
                    best ??= snapshot;
                }
                catch (Exception ex)
                {
                    LogApiDecision(null, host, "GetPost", success: false, metaError: ex.Message);
                }
            }
        }

        return best;
    }

    private async Task<RemotePostSnapshot?> FetchMediaFromCommentAsync(
        string accessToken,
        string commentId,
        string mediaId,
        InstagramConnectionType connectionType,
        CancellationToken cancellationToken)
    {
        foreach (var fields in new[]
                 {
                     $"media{{{InstagramMediaPictureFieldsWithChildren}}}",
                     $"media{{{InstagramMediaPictureFields}}}"
                 })
        {
            try
            {
                using var doc = await GetGraphJsonAsync(
                    connectionType, commentId, accessToken, fields, cancellationToken);
                if (!doc.RootElement.TryGetProperty("media", out var media))
                    continue;

                var snapshot = ParseMediaSnapshot(media, mediaId);
                if (HasPicture(snapshot))
                    return snapshot;
                if (snapshot is not null)
                    return snapshot;
            }
            catch (Exception ex)
            {
                LogApiDecision(null, connectionType, "GetCommentMedia", success: false, metaError: ex.Message);
            }
        }

        return null;
    }

    private async Task<RemotePostSnapshot?> FindMediaInOwnedFeedAsync(
        string accessToken,
        string mediaId,
        string? ownerExternalId,
        string? ownerMetadataJson,
        InstagramConnectionType connectionType,
        CancellationToken cancellationToken)
    {
        var owners = new List<string> { "me" };
        if (!string.IsNullOrWhiteSpace(ownerExternalId))
            owners.Add(ownerExternalId);
        owners.AddRange(ReadAlternateIds(ownerMetadataJson));

        foreach (var ownerId in owners.Distinct(StringComparer.Ordinal))
        {
            try
            {
                using var doc = await GetGraphJsonAsync(
                    connectionType,
                    $"{ownerId}/media",
                    accessToken,
                    InstagramMediaPictureFieldsWithChildren,
                    cancellationToken,
                    ("limit", "50"));
                if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in data.EnumerateArray())
                {
                    var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    if (string.Equals(id, mediaId, StringComparison.Ordinal))
                    {
                        var snapshot = ParseMediaSnapshot(item, mediaId);
                        if (snapshot is not null)
                            return snapshot;
                    }

                    if (item.TryGetProperty("children", out var children) &&
                        children.TryGetProperty("data", out var childData) &&
                        childData.ValueKind == JsonValueKind.Array &&
                        childData.EnumerateArray().Any(child =>
                            child.TryGetProperty("id", out var childId) &&
                            string.Equals(childId.GetString(), mediaId, StringComparison.Ordinal)))
                        return ParseMediaSnapshot(item, mediaId);
                }
            }
            catch (Exception ex)
            {
                LogApiDecision(ownerId, connectionType, "ListMedia", success: false, metaError: ex.Message);
            }
        }

        return null;
    }

    private async Task<string?> FetchOEmbedThumbnailAsync(
        string accessToken,
        string permalink,
        InstagramConnectionType connectionType,
        CancellationToken cancellationToken)
    {
        foreach (var host in HostOrder(connectionType))
        {
            foreach (var path in new[] { "instagram_oembed", "oembed" })
            {
                try
                {
                    using var doc = await GetGraphJsonAsync(
                        host,
                        path,
                        accessToken,
                        "thumbnail_url",
                        cancellationToken,
                        ("url", permalink));
                    var thumbnail = ReadGraphUrl(doc.RootElement, "thumbnail_url")
                                    ?? ReadGraphUrl(doc.RootElement, "thumbnail_url_www");
                    if (!string.IsNullOrWhiteSpace(thumbnail))
                        return thumbnail;
                }
                catch (Exception ex)
                {
                    LogApiDecision(null, host, "OEmbed", success: false, metaError: ex.Message);
                }
            }
        }

        return null;
    }

    private static InstagramConnectionType[] HostOrder(InstagramConnectionType preferred)
        => preferred == InstagramConnectionType.InstagramLogin
            ? [InstagramConnectionType.InstagramLogin, InstagramConnectionType.FacebookLogin]
            : [InstagramConnectionType.FacebookLogin, InstagramConnectionType.InstagramLogin];

    private async Task<RemotePostSnapshot?> LocalizeSnapshotMediaAsync(
        RemotePostSnapshot? snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot is null)
            return null;

        var remotePicture = snapshot.IsVideo
            ? FirstNonEmpty(snapshot.ThumbnailUrl, snapshot.MediaUrl)
            : FirstNonEmpty(snapshot.MediaUrl, snapshot.ThumbnailUrl);
        if (string.IsNullOrWhiteSpace(remotePicture)
            || ProcessEntityNav.IsBrowserDisplayableUrl(remotePicture))
            return snapshot;

        var local = await _publishMediaCache.StoreFromRemoteAsync(remotePicture, cancellationToken);
        if (string.IsNullOrWhiteSpace(local))
            return snapshot;

        if (snapshot.IsVideo)
            snapshot.ThumbnailUrl = local;
        else
            snapshot.MediaUrl = local;
        return snapshot;
    }

    private static bool HasPicture(RemotePostSnapshot? snapshot)
        => snapshot is not null
           && (!string.IsNullOrWhiteSpace(snapshot.MediaUrl)
               || !string.IsNullOrWhiteSpace(snapshot.ThumbnailUrl));

    private async Task<JsonDocument> GetGraphJsonAsync(
        InstagramConnectionType connectionType,
        string path,
        string accessToken,
        string fields,
        CancellationToken cancellationToken,
        params (string Key, string Value)[] extra)
    {
        var query = extra.Length == 0
            ? new (string Key, string Value)[] { ("fields", fields) }
            : extra.Prepend(("fields", fields)).ToArray();

        return connectionType == InstagramConnectionType.InstagramLogin
            ? await _graph.GetInstagramAsync(InstagramLoginGraphVersion, path, accessToken, cancellationToken, query)
            : await _graph.GetAsync(GraphVersion, path, accessToken, cancellationToken, query);
    }

    private static RemotePostSnapshot? ParseMediaSnapshot(JsonElement root, string fallbackId)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        var mediaType = ReadGraphString(root, "media_type");
        var productType = ReadGraphString(root, "media_product_type");
        var mediaUrl = ReadGraphUrl(root, "media_url");
        var thumbnailUrl = ReadGraphUrl(root, "thumbnail_url");

        if (string.IsNullOrWhiteSpace(mediaUrl) &&
            string.IsNullOrWhiteSpace(thumbnailUrl) &&
            root.TryGetProperty("children", out var children) &&
            children.TryGetProperty("data", out var childData) &&
            childData.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in childData.EnumerateArray())
            {
                var childMedia = ReadGraphUrl(child, "media_url");
                var childThumb = ReadGraphUrl(child, "thumbnail_url");
                if (string.IsNullOrWhiteSpace(childMedia) && string.IsNullOrWhiteSpace(childThumb))
                    continue;

                mediaType = ReadGraphString(child, "media_type") ?? mediaType;
                mediaUrl = childMedia;
                thumbnailUrl = childThumb;
                break;
            }
        }

        var caption = ReadGraphString(root, "caption");
        var permalink = ReadGraphString(root, "permalink");
        var externalId = root.TryGetProperty("id", out var id) ? id.GetString() ?? fallbackId : fallbackId;
        if (string.IsNullOrWhiteSpace(caption) &&
            string.IsNullOrWhiteSpace(mediaUrl) &&
            string.IsNullOrWhiteSpace(thumbnailUrl) &&
            string.IsNullOrWhiteSpace(permalink) &&
            string.IsNullOrWhiteSpace(externalId))
            return null;

        var isVideo = string.Equals(mediaType, "VIDEO", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(mediaType, "REELS", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(productType, "REELS", StringComparison.OrdinalIgnoreCase);

        return new RemotePostSnapshot
        {
            ExternalId = externalId ?? fallbackId,
            Text = caption,
            Permalink = permalink,
            MediaUrl = mediaUrl,
            ThumbnailUrl = thumbnailUrl,
            IsVideo = isVideo,
            LikeCount = root.TryGetProperty("like_count", out var likes) && likes.TryGetInt32(out var likeCount) ? likeCount : 0,
            CommentCount = root.TryGetProperty("comments_count", out var comments) && comments.TryGetInt32(out var commentCount) ? commentCount : 0,
            CreatedTime = root.TryGetProperty("timestamp", out var timestamp) &&
                          DateTime.TryParse(timestamp.GetString(), out var createdAt)
                ? createdAt.ToUniversalTime()
                : null
        };
    }

    public async Task<IReadOnlyList<PostDto>> GetPostsAsync(MetaCallContext context, CancellationToken cancellationToken = default)
    {
        using var doc = context.InstagramConnectionType == InstagramConnectionType.InstagramLogin
            ? await _graph.GetInstagramAsync(
                InstagramLoginGraphVersion,
                $"{context.ProfileExternalId}/media",
                context.AccessToken,
                cancellationToken,
                ("fields", "id,caption,permalink,timestamp"),
                ("limit", "25"))
            : await _graph.GetAsync(
                GraphVersion,
                $"{context.ProfileExternalId}/media",
                context.AccessToken,
                cancellationToken,
                ("fields", "id,caption,permalink,timestamp"),
                ("limit", "25"));

        var results = new List<PostDto>();
        if (!doc.RootElement.TryGetProperty("data", out var data)) return results;
        foreach (var item in data.EnumerateArray())
        {
            results.Add(new PostDto
            {
                Id = item.GetProperty("id").GetString() ?? string.Empty,
                Message = item.TryGetProperty("caption", out var c) ? c.GetString() : null,
                Permalink = item.TryGetProperty("permalink", out var p) ? p.GetString() : null,
                CreatedTime = item.TryGetProperty("timestamp", out var t) && DateTime.TryParse(t.GetString(), out var dt) ? dt : null
            });
        }
        return results;
    }

    public async Task<RemoteCommentSnapshot?> GetCommentSnapshotAsync(
        string accessToken,
        string commentId,
        InstagramConnectionType connectionType = InstagramConnectionType.FacebookLogin,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(commentId))
            return null;

        const string fieldsWithPost =
            "id,text,timestamp,username,from,like_count,parent_id,media{id,caption,media_type,media_url,thumbnail_url,permalink,timestamp}";
        const string fieldsBasic = "id,text,timestamp,username,from,like_count,parent_id,media";

        JsonDocument? doc = null;
        try
        {
            doc = await GetGraphJsonAsync(connectionType, commentId, accessToken, fieldsWithPost, cancellationToken);
        }
        catch (Exception ex)
        {
            LogApiDecision(null, connectionType, "GetComment", success: false, metaError: ex.Message);
            doc = await GetGraphJsonAsync(connectionType, commentId, accessToken, fieldsBasic, cancellationToken);
        }

        using (doc)
        {
            LogApiDecision(null, connectionType, "GetComment", success: true);

            var root = doc.RootElement;
            string? authorId = null;
            string? authorName = root.TryGetProperty("username", out var username) ? username.GetString() : null;
            if (root.TryGetProperty("from", out var from))
            {
                authorId = from.TryGetProperty("id", out var fromId) ? fromId.ToString() : null;
                if (from.TryGetProperty("username", out var fromUser) && !string.IsNullOrWhiteSpace(fromUser.GetString()))
                    authorName = fromUser.GetString();
            }

            var post = root.TryGetProperty("media", out var media)
                ? ParseMediaSnapshot(media, MetaWebhookPayloadNormalizer.ReadMediaId(root) ?? string.Empty)
                : null;

            return new RemoteCommentSnapshot
            {
                ExternalId = root.TryGetProperty("id", out var id) ? id.GetString() ?? commentId : commentId,
                Message = root.TryGetProperty("text", out var text) ? text.GetString() : null,
                PostExternalId = FirstNonEmpty(post?.ExternalId, MetaWebhookPayloadNormalizer.ReadMediaId(root)),
                ParentExternalId = root.TryGetProperty("parent_id", out var parentId) ? parentId.ToString() : null,
                AuthorId = authorId,
                AuthorName = authorName,
                AuthorUsername = authorName,
                LikeCount = root.TryGetProperty("like_count", out var likes) && likes.TryGetInt32(out var likeCount) ? likeCount : 0,
                CreatedTime = root.TryGetProperty("timestamp", out var timestamp) &&
                              DateTime.TryParse(timestamp.GetString(), out var createdAt)
                    ? createdAt.ToUniversalTime()
                    : null,
                Post = post
            };
        }
    }

    public async Task<string?> ReplyCommentAsync(MetaCallContext context, string commentId, string message, CancellationToken cancellationToken = default)
    {
        var connectionType = context.InstagramConnectionType;
        try
        {
            using var doc = connectionType == InstagramConnectionType.InstagramLogin
                ? await _graph.PostInstagramAsync(
                    InstagramLoginGraphVersion,
                    $"{commentId}/replies",
                    context.AccessToken,
                    new Dictionary<string, string> { ["message"] = message },
                    cancellationToken)
                : await _graph.PostAsync(
                    GraphVersion,
                    $"{commentId}/replies",
                    context.AccessToken,
                    new Dictionary<string, string> { ["message"] = message },
                    cancellationToken);

            LogApiDecision(context.ProfileExternalId, connectionType, "ReplyToComment", success: true);
            return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
        }
        catch (Exception ex)
        {
            LogApiDecision(context.ProfileExternalId, connectionType, "ReplyToComment", success: false, metaError: ex.Message);
            throw;
        }
    }

    public async Task HideCommentAsync(MetaCallContext context, string commentId, bool hide, CancellationToken cancellationToken = default)
    {
        var connectionType = context.InstagramConnectionType;
        try
        {
            var fields = new Dictionary<string, string> { ["hide"] = hide ? "true" : "false" };
            if (connectionType == InstagramConnectionType.InstagramLogin)
            {
                using var _ = await _graph.PostInstagramAsync(InstagramLoginGraphVersion, commentId, context.AccessToken, fields, cancellationToken);
            }
            else
            {
                using var _ = await _graph.PostAsync(GraphVersion, commentId, context.AccessToken, fields, cancellationToken);
            }

            LogApiDecision(context.ProfileExternalId, connectionType, "HideComment", success: true);
        }
        catch (Exception ex)
        {
            LogApiDecision(context.ProfileExternalId, connectionType, "HideComment", success: false, metaError: ex.Message);
            throw;
        }
    }

    public async Task DeleteCommentAsync(MetaCallContext context, string commentId, CancellationToken cancellationToken = default)
    {
        var connectionType = context.InstagramConnectionType;
        try
        {
            if (connectionType == InstagramConnectionType.InstagramLogin)
                await _graph.DeleteInstagramAsync(InstagramLoginGraphVersion, commentId, context.AccessToken, cancellationToken);
            else
                await _graph.DeleteAsync(GraphVersion, commentId, context.AccessToken, cancellationToken);

            LogApiDecision(context.ProfileExternalId, connectionType, "DeleteComment", success: true);
        }
        catch (Exception ex)
        {
            LogApiDecision(context.ProfileExternalId, connectionType, "DeleteComment", success: false, metaError: ex.Message);
            throw;
        }
    }

    public async Task<string?> SendMessageAsync(MetaCallContext context, string recipientId, string message, string? replyToMid = null, CancellationToken cancellationToken = default)
    {
        var connectionType = context.InstagramConnectionType;
        try
        {
            // Instagram Login: POST graph.instagram.com/me/messages with the IG user bearer token.
            // Facebook Login: POST graph.facebook.com/{page-id}/messages with the page access token.
            var pathId = connectionType == InstagramConnectionType.InstagramLogin
                ? "me"
                : (!string.IsNullOrWhiteSpace(context.PageExternalId)
                    ? context.PageExternalId
                    : context.ProfileExternalId);

            object payload = connectionType == InstagramConnectionType.InstagramLogin
                ? (string.IsNullOrWhiteSpace(replyToMid)
                    ? new
                    {
                        recipient = new { id = recipientId },
                        message = new { text = message }
                    }
                    : new
                    {
                        recipient = new { id = recipientId },
                        message = new { text = message },
                        reply_to = new { mid = replyToMid }
                    })
                : (string.IsNullOrWhiteSpace(replyToMid)
                    ? new
                    {
                        recipient = new { id = recipientId },
                        messaging_type = "RESPONSE",
                        message = new { text = message }
                    }
                    : new
                    {
                        recipient = new { id = recipientId },
                        messaging_type = "RESPONSE",
                        message = new { text = message },
                        reply_to = new { mid = replyToMid }
                    });

            using var doc = connectionType == InstagramConnectionType.InstagramLogin
                ? await _graph.PostInstagramJsonAsync(InstagramLoginGraphVersion, $"{pathId}/messages", context.AccessToken, payload, cancellationToken)
                : await _graph.PostJsonAsync(GraphVersion, $"{pathId}/messages", context.AccessToken, payload, cancellationToken);

            LogApiDecision(context.ProfileExternalId, connectionType, "SendMessage", success: true);
            if (doc.RootElement.TryGetProperty("message_id", out var messageId))
                return messageId.GetString();
            return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
        }
        catch (Exception ex)
        {
            LogApiDecision(context.ProfileExternalId, connectionType, "SendMessage", success: false, metaError: ex.Message);
            throw;
        }
    }

    public async Task DeleteMessageAsync(MetaCallContext context, string messageId, CancellationToken cancellationToken = default)
    {
        var connectionType = context.InstagramConnectionType;
        try
        {
            if (connectionType == InstagramConnectionType.InstagramLogin)
                await _graph.DeleteInstagramAsync(InstagramLoginGraphVersion, messageId, context.AccessToken, cancellationToken);
            else
                await _graph.DeleteAsync(GraphVersion, messageId, context.AccessToken, cancellationToken);

            LogApiDecision(context.ProfileExternalId, connectionType, "DeleteMessage", success: true);
        }
        catch (Exception ex)
        {
            LogApiDecision(context.ProfileExternalId, connectionType, "DeleteMessage", success: false, metaError: ex.Message);
            throw;
        }
    }

    private void LogApiDecision(
        string? instagramAccountId,
        InstagramConnectionType connectionType,
        string operation,
        bool success,
        string? metaError = null)
    {
        var endpointType = InstagramConnectionResolver.ToLogLabel(connectionType);
        if (success)
        {
            _logger.LogInformation(
                "Instagram API request | InstagramAccountId={InstagramAccountId} | ConnectionType={ConnectionType} | Operation={Operation} | EndpointType={EndpointType} | Result=Success",
                instagramAccountId ?? "(unknown)",
                endpointType,
                operation,
                endpointType);
            return;
        }

        _logger.LogWarning(
            "Instagram API request | InstagramAccountId={InstagramAccountId} | ConnectionType={ConnectionType} | Operation={Operation} | EndpointType={EndpointType} | Result=Failed | MetaError={MetaError}",
            instagramAccountId ?? "(unknown)",
            endpointType,
            operation,
            endpointType,
            metaError);
    }

    /// <summary>Subscribe the linked Facebook Page to comment and message webhook fields.</summary>
    public Task SubscribePageWebhooksAsync(string pageId, string pageAccessToken, CancellationToken cancellationToken = default)
        => _graph.SubscribePageAsync(GraphVersion, pageId, pageAccessToken, MetaGraphClient.InstagramPageSubscribedFields, cancellationToken);

    public Task UnsubscribePageWebhooksAsync(string pageId, string pageAccessToken, CancellationToken cancellationToken = default)
        => _graph.UnsubscribePageAsync(GraphVersion, pageId, pageAccessToken, cancellationToken);

    public Task<IReadOnlyList<string>> GetSubscribedFieldsAsync(string pageId, string pageAccessToken, CancellationToken cancellationToken = default)
        => _graph.GetPageSubscribedFieldsAsync(GraphVersion, pageId, pageAccessToken, cancellationToken);

    public async Task<WebhookProcessResult> ProcessWebhookPayloadAsync(WebhookEventEntityBase webhookEvent, string menuType, CancellationToken cancellationToken = default)
    {
        _store = _processData.ForMenu(menuType);
        _menuType = menuType;
        var result = new WebhookProcessResult();
        try
        {
            using var doc = JsonDocument.Parse(webhookEvent.PayloadJson);
            if (!doc.RootElement.TryGetProperty("entry", out _))
            {
                result.Skip("Payload has no 'entry' array — not a Meta webhook delivery.");
                return result;
            }

            foreach (var entry in MetaWebhookPayloadNormalizer.EnumerateEntries(doc.RootElement))
            {
                var igUserId = entry.TryGetProperty("id", out var idEl) ? idEl.ToString() : null;

                var profile = await MetaWebhookEntryHelper.ResolveProfileForEntryAsync(
                    _store, entry, result, cancellationToken)
                    ?? await MetaWebhookProfileResolver.TryResolveSoleInstagramAsync(
                        _store, cancellationToken);
                if (profile is null)
                    continue;

                var account = await _store.GetSocialAccountByIdAsync(profile.SocialAccountId, cancellationToken);
                if (account is null)
                {
                    result.Skip($"Entry '{igUserId ?? profile.ExternalProfileId}' has no owning account.");
                    continue;
                }

                if (!WebhookProfileGuard.CanProcess(profile, account, _menuType, result))
                    continue;

                await ProcessChangesAsync(profile, entry, result, cancellationToken);

                foreach (var messaging in MetaWebhookEntryHelper.EnumerateMessageArrays(entry))
                    await ProcessMessagesAsync(
                        profile,
                        igUserId ?? profile.ExternalProfileId,
                        messaging,
                        result,
                        cancellationToken);
            }

            await _store.SaveChangesAsync(cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Instagram webhook processing failed for {Id}", webhookEvent.Id);
            throw;
        }
        finally
        {
            _store = null;
            _menuType = string.Empty;
        }
    }

    private static IReadOnlyList<string> ReadAlternateIds(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return Array.Empty<string>();

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            if (!doc.RootElement.TryGetProperty("alternateIds", out var ids) ||
                ids.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();

            return ids.EnumerateArray()
                .Select(id => id.ToString())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>True when the id belongs to this profile — its own id, a linked Page, or a known alternate.</summary>
    private static bool ProfileOwnsId(SocialProfileEntityBase profile, string? externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            return false;

        return externalId == profile.ExternalProfileId
            || externalId == TryReadPageId(profile.MetadataJson)
            || ReadAlternateIds(profile.MetadataJson).Contains(externalId);
    }

    private async Task<IReadOnlyList<string>> ResolveAccessTokensAsync(
        SocialAccountEntityBase account,
        InstagramConnectionType connectionType,
        CancellationToken cancellationToken)
    {
        var auth = ProcessEntityNav.Auth(account)
            ?? await _store!.GetSocialAuthByAccountIdAsync(account.Id, cancellationToken);
        var tokens = new List<string>();

        void Add(string? token)
        {
            if (!string.IsNullOrWhiteSpace(token) && !tokens.Contains(token))
                tokens.Add(token);
        }

        // Always prefer the primary stored token for this connection.
        Add(auth?.AccessToken);

        // Facebook Login: RefreshToken retains the long-lived user token and is a useful fallback.
        // Instagram Login: do not invent a Page token — only reuse RefreshToken if it is also an IG user token.
        if (connectionType == InstagramConnectionType.FacebookLogin ||
            connectionType == InstagramConnectionType.InstagramLogin)
            Add(auth?.RefreshToken);

        return tokens;
    }

    private async Task ProcessChangesAsync(
        SocialProfileEntityBase profile,
        JsonElement entry,
        WebhookProcessResult result,
        CancellationToken cancellationToken)
    {
        var account = await _store!.GetSocialAccountByIdAsync(profile.SocialAccountId, cancellationToken);
        if (account is null)
        {
            result.Skip($"Profile '{profile.Id}' has no owning account.");
            return;
        }

        var platform = await _store!.GetPlatformByIdAsync(account.PlatformId, cancellationToken);
        var connectionType = InstagramConnectionResolver.FromProfile(profile, platform?.Code);
        _logger.LogInformation(
            "Instagram webhook comment/message routing | InstagramAccountId={InstagramAccountId} | ConnectionType={ConnectionType}",
            profile.ExternalProfileId,
            InstagramConnectionResolver.ToLogLabel(connectionType));

        foreach (var change in MetaWebhookPayloadNormalizer.EnumerateChanges(entry))
        {
            var field = change.TryGetProperty("field", out var fieldElement) ? fieldElement.GetString() : null;
            if (!change.TryGetProperty("value", out var value))
            {
                result.Skip($"Change '{field}' has no value object.");
                continue;
            }

            // Instagram messaging can arrive as a change with field=messages rather than entry.messaging.
            if (field is "messages" or "messaging" or "messaging_postbacks" or "message_reactions")
            {
                var entryId = entry.TryGetProperty("id", out var idEl) ? idEl.ToString() : profile.ExternalProfileId;
                if (value.TryGetProperty("message", out _))
                    await ProcessMessageAsync(profile, account, entryId, value, result, cancellationToken);
                else if (value.TryGetProperty("messaging", out var nestedMessaging) &&
                         nestedMessaging.ValueKind == JsonValueKind.Array)
                    await ProcessMessagesAsync(profile, entryId, nestedMessaging, result, cancellationToken);
                else
                    result.Skip($"Change '{field}' value has no message envelope.");
                continue;
            }

            if (field is not ("comments" or "live_comments" or "mentions" or "comment"))
            {
                result.Skip($"Field '{field}' is not handled.");
                continue;
            }

            var commentId = FirstNonEmpty(
                value.TryGetProperty("id", out var commentIdElement) ? commentIdElement.ToString() : null,
                value.TryGetProperty("comment_id", out var legacyCommentIdElement) ? legacyCommentIdElement.ToString() : null);
            if (string.IsNullOrWhiteSpace(commentId))
            {
                result.Skip("Comment change is missing id.");
                continue;
            }

            // Facebook: webhook post_id → Graph GET post (full_picture) → save → map comment.
            // Instagram: webhook media.id → Instagram/Facebook Graph GET media (media_url) → save → map comment.
            var mediaId = MetaWebhookPayloadNormalizer.ReadMediaId(value);
            var accessTokens = await ResolveAccessTokensAsync(account, connectionType, cancellationToken);
            RemoteCommentSnapshot? enriched = null;
            foreach (var accessToken in accessTokens)
            {
                try
                {
                    enriched = await GetCommentSnapshotAsync(accessToken, commentId!, connectionType, cancellationToken);
                    if (enriched is not null)
                        break;
                }
                catch (Exception ex)
                {
                    LogApiDecision(profile.ExternalProfileId, connectionType, "GetComment", success: false, metaError: ex.Message);
                    result.Skip($"Graph comment enrich failed for '{commentId}': {ex.Message}");
                }
            }

            mediaId = FirstNonEmpty(mediaId, enriched?.Post?.ExternalId, enriched?.PostExternalId);
            if (string.IsNullOrWhiteSpace(mediaId))
            {
                result.Skip("Comment change is missing media.id.");
                continue;
            }

            var commentText = FirstNonEmpty(
                enriched?.Message,
                MetaWebhookPayloadNormalizer.ReadCommentText(value)) ?? string.Empty;

            var post = await ResolveInstagramPostAsync(
                profile,
                account,
                connectionType,
                mediaId!,
                enriched?.CreatedTime ?? UnixSeconds(entry, "time") ?? DateTime.UtcNow,
                commentId,
                enriched?.Post,
                cancellationToken);

            var existing = await _store!.GetCommentByExternalIdAsync(commentId, cancellationToken);
            if (existing is not null)
            {
                var changed = existing.Message != commentText || existing.PostId != post.Id;
                if (!changed)
                {
                    result.Skip($"Comment '{commentId}' already stored.");
                    continue;
                }

                existing.PostId = post.Id;
                existing.Message = commentText;
                if (enriched?.LikeCount > 0) existing.LikeCount = enriched.LikeCount;
                existing.UpdatedAt = DateTime.UtcNow;
                _store!.UpdateComment(existing);
                await _store!.SaveChangesAsync(cancellationToken);
                result.Handled++;
                continue;
            }

            var authorId = FirstNonEmpty(
                enriched?.AuthorId,
                MetaWebhookPayloadNormalizer.ReadActorId(value, "from")) ?? string.Empty;
            var authorName = FirstNonEmpty(
                enriched?.AuthorUsername,
                enriched?.AuthorName,
                value.TryGetProperty("from", out var fromUser) && fromUser.ValueKind == JsonValueKind.Object &&
                fromUser.TryGetProperty("username", out var username)
                    ? username.GetString()
                    : null) ?? "Instagram user";

            var fromConnectedAccount = MetaMessagingHelper.ProfileOwnsSenderId(profile, authorId);

            CommentEntityBase? parentComment = null;
            var parentExternalId = FirstNonEmpty(
                enriched?.ParentExternalId,
                value.TryGetProperty("parent_id", out var parentIdElement) ? parentIdElement.ToString() : null);
            if (!string.IsNullOrWhiteSpace(parentExternalId) && parentExternalId != mediaId)
                parentComment = await _store!.GetCommentByExternalIdAsync(parentExternalId!, cancellationToken);

            var receivedAt = enriched?.CreatedTime ?? UnixSeconds(entry, "time") ?? DateTime.UtcNow;
            var comment = _store!.NewComment();
            comment.PostId = post.Id;
            comment.ParentCommentId = parentComment?.Id;
            comment.ExternalCommentId = commentId;
            comment.AuthorId = authorId;
            comment.AuthorName = authorName;
            comment.Message = commentText;
            comment.LikeCount = enriched?.LikeCount ?? 0;
            comment.PlatformCreatedAt = receivedAt;
            await _store!.AddCommentAsync(comment, cancellationToken);
            post.CommentCount += 1;
            post.UpdatedAt = DateTime.UtcNow;
            _store!.UpdatePost(post);

            await _store!.SaveChangesAsync(cancellationToken);
            result.Handled++;

            var inboxItem = new InboxItemDto
            {
                Id = comment.Id,
                ItemKind = "comment",
                PlatformCode = "instagram",
                ExternalId = comment.ExternalCommentId,
                AuthorName = comment.AuthorName,
                AuthorId = comment.AuthorId,
                Content = comment.Message,
                IsHidden = false,
                IsRead = false,
                IsOutgoing = fromConnectedAccount,
                ReceivedAt = receivedAt,
                CommentLikes = comment.LikeCount,
                ReplyCount = 0,
                ParentId = comment.ParentCommentId,
                Post = new InboxPostMetaDto
                {
                    PostId = post.ExternalPostId ?? post.Id.ToString(),
                    PageName = profile.Name ?? profile.Username ?? "Instagram",
                    PostText = FirstNonEmpty(post.Caption, post.Text),
                    PostImageUrl = ProcessEntityNav.FirstMediaUrl(post),
                    PostVideoUrl = ProcessEntityNav.FirstVideoUrl(post),
                    LikesCount = post.LikeCount,
                    CommentsCount = post.CommentCount,
                    SharesCount = post.ShareCount,
                    ViewsCount = post.ViewCount,
                    PostedAt = post.PublishedAt ?? post.CreatedAt
                }
            };

            InboxRoutingHelper.Apply(inboxItem, profile, account, _menuType);
            await _inboxRealtime.NotifyInboxItemAsync(account.UserId, inboxItem, cancellationToken);
        }
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string? ReadGraphString(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadGraphUrl(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.String)
            return value.GetString();

        if (value.ValueKind == JsonValueKind.Object)
        {
            if (value.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
                return url.GetString();
            if (value.TryGetProperty("uri", out var uri) && uri.ValueKind == JsonValueKind.String)
                return uri.GetString();
        }

        return null;
    }

    private async Task ProcessMessagesAsync(
        SocialProfileEntityBase profile,
        string? entryBusinessId,
        JsonElement messaging,
        WebhookProcessResult result,
        CancellationToken cancellationToken)
    {
        var account = await _store!.GetSocialAccountByIdAsync(profile.SocialAccountId, cancellationToken);
        if (account is null)
        {
            result.Skip($"Profile '{profile.Id}' has no owning account.");
            return;
        }

        foreach (var item in messaging.EnumerateArray())
            await ProcessMessageAsync(profile, account, entryBusinessId, item, result, cancellationToken);
    }

    /// <summary>
    /// Handles one messaging item. Accepts both delivery shapes: an element of
    /// <c>entry.messaging[]</c> and the <c>changes[field=messages].value</c> object.
    /// </summary>
    private async Task ProcessMessageAsync(
        SocialProfileEntityBase profile,
        SocialAccountEntityBase account,
        string? entryBusinessId,
        JsonElement item,
        WebhookProcessResult result,
        CancellationToken cancellationToken)
    {
        if (!item.TryGetProperty("message", out var message))
        {
            result.Skip("Messaging item has no message object.");
            return;
        }

        var messageId = MetaMessagingHelper.ReadMessageId(message);
        if (string.IsNullOrWhiteSpace(messageId))
        {
            result.Skip("Message has no mid.");
            return;
        }
        if (await _store!.GetMessageByExternalIdAsync(messageId, cancellationToken) is not null)
        {
            result.Skip($"Message '{messageId}' already stored.");
            return;
        }

        var senderId = FirstNonEmpty(
            MetaWebhookPayloadNormalizer.ReadActorId(item, "sender"),
            MetaWebhookPayloadNormalizer.ReadActorId(item, "from"));
        var receiverId = FirstNonEmpty(
            MetaWebhookPayloadNormalizer.ReadActorId(item, "recipient"),
            MetaWebhookPayloadNormalizer.ReadActorId(item, "to"));

        var outbound = MetaWebhookEchoHelper.IsEcho(item, message)
            || MetaMessagingHelper.ProfileOwnsSenderId(profile, senderId)
            || IdsMatchEntryBusiness(senderId, entryBusinessId, profile);

        var customerId = outbound
            ? FirstNonEmpty(
                !MetaMessagingHelper.ProfileOwnsSenderId(profile, receiverId)
                && !IdsMatchEntryBusiness(receiverId, entryBusinessId, profile)
                    ? receiverId
                    : null,
                receiverId)
            : senderId;
        if (string.IsNullOrWhiteSpace(customerId) && !outbound)
        {
            if (!string.IsNullOrWhiteSpace(receiverId) &&
                !MetaMessagingHelper.ProfileOwnsSenderId(profile, receiverId) &&
                !IdsMatchEntryBusiness(receiverId, entryBusinessId, profile))
                customerId = receiverId;
            else if (!string.IsNullOrWhiteSpace(senderId) &&
                     !MetaMessagingHelper.ProfileOwnsSenderId(profile, senderId) &&
                     !IdsMatchEntryBusiness(senderId, entryBusinessId, profile))
                customerId = senderId;
        }

        if (string.IsNullOrWhiteSpace(customerId))
        {
            result.Skip($"Message '{messageId}' has no customer sender/recipient id.");
            return;
        }

        var conversationKey = $"{profile.ExternalProfileId}:{customerId}";
        var conversation = await _store!.GetConversationByExternalIdAsync(
            profile.Id, conversationKey, cancellationToken);
        var isNewConversation = conversation is null;
        if (conversation is null)
        {
            conversation = _store!.NewConversation();
            conversation.SocialProfileId = profile.Id;
            conversation.ExternalConversationId = conversationKey;
            conversation.CustomerId = customerId;
            conversation.CustomerName = customerId;
            conversation.Status = ConversationStatus.Open;
            await _store!.AddConversationAsync(conversation, cancellationToken);
        }

        var receivedAt = ReadTimestamp(item) ?? DateTime.UtcNow;
        var body = message.TryGetProperty("text", out var text)
            ? text.GetString()
            : message.TryGetProperty("attachments", out _) ? "[Instagram attachment]" : string.Empty;

        var replyToMid = message.TryGetProperty("reply_to", out var replyTo) &&
                         replyTo.ValueKind == JsonValueKind.Object &&
                         replyTo.TryGetProperty("mid", out var quotedMid)
            ? quotedMid.ToString()
            : null;
        var quoted = string.IsNullOrWhiteSpace(replyToMid)
            ? null
            : await _store!.GetMessageByExternalIdAsync(replyToMid!, cancellationToken);

        var msg = _store!.NewMessage();
        msg.ConversationId = conversation.Id;
        msg.ExternalMessageId = messageId;
        msg.SenderId = senderId;
        msg.ReceiverId = receiverId;
        msg.Direction = outbound ? MessageDirection.Outbound : MessageDirection.Inbound;
        msg.MessageType = MessageContentType.Text;
        msg.Body = body;
        msg.Status = outbound ? MessageDeliveryStatus.Sent : MessageDeliveryStatus.Delivered;
        msg.PlatformCreatedAt = receivedAt;
        msg.ReplyToMessageId = quoted?.Id;
        msg.ReplyToExternalId = replyToMid;
        await _store!.AddMessageAsync(msg, cancellationToken);

        conversation.LastMessageAt = receivedAt;
        conversation.UpdatedAt = DateTime.UtcNow;
        if (!outbound) conversation.UnreadCount += 1;
        if (!isNewConversation) _store!.UpdateConversation(conversation);

        await _store!.SaveChangesAsync(cancellationToken);
        result.Handled++;

        var inboxItem = new InboxItemDto
        {
            Id = msg.Id,
            ItemKind = "message",
            PlatformCode = "instagram",
            ExternalId = msg.ExternalMessageId,
            AuthorName = outbound ? "You" : conversation.CustomerName ?? senderId,
            AuthorId = senderId,
            Content = body ?? string.Empty,
            IsHidden = false,
            IsRead = outbound,
            IsOutgoing = outbound,
            ConversationId = conversation.Id,
            ReceivedAt = receivedAt,
            ReplyToId = quoted?.Id,
            ReplyToAuthor = quoted is null
                ? null
                : quoted.Direction == MessageDirection.Outbound ? "You" : conversation.CustomerName ?? quoted.SenderId,
            ReplyToContent = quoted?.Body
        };

        InboxRoutingHelper.Apply(inboxItem, profile, account, _menuType);
        await _inboxRealtime.NotifyInboxItemAsync(account.UserId, inboxItem, cancellationToken);
    }

    private static string? TryReadPageId(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;
        try
        {
            using var meta = JsonDocument.Parse(metadataJson);
            return meta.RootElement.TryGetProperty("pageId", out var pageId) ? pageId.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? UnixSeconds(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : null;

    /// <summary>
    /// Reads a messaging timestamp. Meta sends it as a number or a string, and in seconds or
    /// milliseconds depending on the product, so both are normalised here.
    /// </summary>
    private static DateTime? ReadTimestamp(JsonElement element)
    {
        if (!element.TryGetProperty("timestamp", out var value))
            return null;

        long raw;
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetInt64(out raw)) return null;
        }
        else if (value.ValueKind != JsonValueKind.String || !long.TryParse(value.GetString(), out raw))
        {
            return null;
        }

        if (raw <= 0) return null;

        return raw > 100_000_000_000L
            ? DateTimeOffset.FromUnixTimeMilliseconds(raw).UtcDateTime
            : DateTimeOffset.FromUnixTimeSeconds(raw).UtcDateTime;
    }

    private static bool IdsMatchEntryBusiness(string? actorId, string? entryBusinessId, SocialProfileEntityBase profile)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return false;

        if (!string.IsNullOrWhiteSpace(entryBusinessId) &&
            string.Equals(actorId, entryBusinessId, StringComparison.Ordinal))
            return true;

        return MetaMessagingHelper.ProfileOwnsSenderId(profile, actorId);
    }

}
