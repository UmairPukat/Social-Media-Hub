using Microsoft.Extensions.Logging;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Inbox;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Application.Services;

public class InboxService : IInboxService
{
    private readonly IProcessDataStoreFactory _processData;
    private readonly IFacebookService _facebookService;
    private readonly IInstagramService _instagramService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IYouTubeService _youTubeService;
    private readonly IInboxRealtimeNotifier _inboxRealtime;
    private readonly IPublishMediaCacheService _mediaCache;
    private readonly IWebhookService _webhookService;
    private readonly ILogger<InboxService> _logger;

    public InboxService(
        IProcessDataStoreFactory processData,
        IFacebookService facebookService,
        IInstagramService instagramService,
        IWhatsAppService whatsAppService,
        IYouTubeService youTubeService,
        IInboxRealtimeNotifier inboxRealtime,
        IPublishMediaCacheService mediaCache,
        IWebhookService webhookService,
        ILogger<InboxService> logger)
    {
        _processData = processData;
        _facebookService = facebookService;
        _instagramService = instagramService;
        _whatsAppService = whatsAppService;
        _youTubeService = youTubeService;
        _inboxRealtime = inboxRealtime;
        _mediaCache = mediaCache;
        _webhookService = webhookService;
        _logger = logger;
    }

    public async Task<ApiResponse<IReadOnlyList<InboxItemDto>>> GetInboxAsync(
        Guid userId,
        InboxFilterRequest? filter = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Guid? platformId = null;
            IReadOnlyList<Guid>? platformIds = null;
            var processMenu = string.IsNullOrWhiteSpace(filter?.MenuType) ? null : MenuTypes.Normalize(filter!.MenuType);
            var stores = ResolveStores(processMenu);

            await ReplayPendingWebhooksAsync(processMenu, cancellationToken);

            if (!string.IsNullOrWhiteSpace(filter?.PlatformCode))
            {
                if (string.Equals(filter.PlatformCode, "instagram", StringComparison.OrdinalIgnoreCase))
                {
                    var menus = processMenu is null ? ProcessModules.AllMenuTypes : [processMenu];
                    platformIds = menus.SelectMany(m => new[]
                    {
                        PlatformCatalog.IdForMenu(PlatformCatalog.InstagramId, m),
                        PlatformCatalog.IdForMenu(PlatformCatalog.InstagramLoginId, m)
                    }).Distinct().ToArray();
                }
                else
                {
                    var matching = new List<Guid>();
                    foreach (var store in stores)
                    {
                        matching.AddRange((await store.GetActivePlatformsAsync(cancellationToken))
                            .Where(p => string.Equals(p.Code, filter.PlatformCode, StringComparison.OrdinalIgnoreCase))
                            .Select(p => p.Id));
                    }

                    platformIds = matching.Count > 0 ? matching.Distinct().ToArray() : null;
                    platformId = matching.Count == 1 ? matching[0] : null;
                }

                if (string.Equals(filter.PlatformCode, "whatsapp", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(filter.ItemKind, "comment", StringComparison.OrdinalIgnoreCase))
                {
                    return ApiResponse<IReadOnlyList<InboxItemDto>>.Ok(Array.Empty<InboxItemDto>());
                }
            }
            else if (processMenu is not null)
            {
                // Load every comment/message for this user in the module. Filtering by
                // catalog platform ids hid Instagram Login rows when Post.PlatformId
                // did not match an IntegrationPlatforms.IsActive id.
                platformIds = null;
            }

            var kind = filter?.ItemKind?.ToLowerInvariant();
            var items = new List<InboxItemDto>();
            var userAccounts = await LoadUserAccountsAsync(userId, processMenu, cancellationToken);

            if (kind is null or "comment")
            {
                var comments = await LoadCommentsAsync(userId, platformId, platformIds, stores, cancellationToken);
                items.AddRange(comments.Select(row =>
                {
                    var menuType = FindMenuTypeForAccount(userAccounts, row.Account) ?? processMenu ?? MenuTypes.Integration;
                    var routingAccount = PickRoutingAccount(row.Profile, row.Account, userAccounts.Select(a => a.Account).ToList(), processMenu) ?? row.Account;
                    var item = new InboxItemDto
                    {
                        Id = row.Comment.Id,
                        ItemKind = "comment",
                        PlatformCode = InstagramConnectionResolver.ToInboxPlatformCode(row.Platform.Code),
                        ExternalId = row.Comment.ExternalCommentId,
                        AuthorName = row.Comment.AuthorName,
                        AuthorId = row.Comment.AuthorId,
                        Content = row.Comment.Message,
                        IsHidden = row.Comment.IsHidden,
                        IsRead = true,
                        IsOutgoing = !string.IsNullOrWhiteSpace(row.Comment.AuthorId) &&
                                     row.Comment.AuthorId == row.Profile.ExternalProfileId,
                        ReceivedAt = row.Comment.PlatformCreatedAt ?? row.Comment.CreatedAt,
                        CommentLikes = row.Comment.LikeCount,
                        ReplyCount = row.ReplyCount,
                        ParentId = row.Comment.ParentCommentId,
                        Post = ToPostMeta(row.Post, row.Profile, row.PostImageUrl)
                    };
                    InboxRoutingHelper.Apply(item, row.Profile, routingAccount, menuType);
                    return item;
                }));
            }

            if (kind is null or "message")
            {
                var messages = await LoadMessagesAsync(userId, platformId, platformIds, stores, cancellationToken);
                var byId = messages.ToDictionary(m => m.Message.Id);
                items.AddRange(messages.Select(row =>
                {
                    var menuType = FindMenuTypeForAccount(userAccounts, row.Account) ?? processMenu ?? MenuTypes.Integration;
                    var routingAccount = PickRoutingAccount(row.Profile, row.Account, userAccounts.Select(a => a.Account).ToList(), processMenu) ?? row.Account;
                    var connectedProfiles = userAccounts
                        .SelectMany(a => ProcessEntityNav.Profiles(a.Account))
                        .GroupBy(p => p.Id)
                        .Select(g => g.First())
                        .ToList();
                    if (connectedProfiles.Count == 0)
                        connectedProfiles.Add(row.Profile);
                    var connectedAccounts = userAccounts.Select(a => a.Account).ToList();
                    var outgoing = MetaConversationSide.IsOutgoing(
                        connectedAccounts,
                        connectedProfiles,
                        row.Message.SenderId,
                        row.Message.ReceiverId,
                        row.Message.Direction == MessageDirection.Outbound);
                    var peerId = MetaConversationSide.ResolvePeerId(
                        routingAccount,
                        connectedProfiles,
                        row.Conversation.CustomerId,
                        row.Message.SenderId,
                        row.Message.ReceiverId);
                    var item = new InboxItemDto
                    {
                        Id = row.Message.Id,
                        ItemKind = "message",
                        PlatformCode = InstagramConnectionResolver.ToInboxPlatformCode(row.Platform.Code),
                        ExternalId = row.Message.ExternalMessageId,
                        AuthorName = outgoing
                            ? "You"
                            : FirstNonEmpty(row.Conversation.CustomerName, peerId, row.Message.SenderId, "Instagram user"),
                        AuthorId = peerId ?? row.Conversation.CustomerId ?? row.Message.SenderId,
                        Content = row.Message.Body ?? string.Empty,
                        IsHidden = false,
                        IsRead = outgoing || row.Conversation.UnreadCount == 0,
                        IsOutgoing = outgoing,
                        ConversationId = row.Message.ConversationId,
                        ReceivedAt = row.Message.PlatformCreatedAt ?? row.Message.CreatedAt,
                        ReplyToId = row.Message.ReplyToMessageId,
                        ReplyToAuthor = QuotedAuthor(row.Message, row.Conversation, byId),
                        ReplyToContent = row.Message.ReplyToMessageId.HasValue &&
                                         byId.TryGetValue(row.Message.ReplyToMessageId.Value, out var quoted)
                            ? quoted.Message.Body
                            : null
                    };
                    InboxRoutingHelper.Apply(item, row.Profile, routingAccount, menuType);
                    return item;
                }));
            }

            return ApiResponse<IReadOnlyList<InboxItemDto>>.Ok(items.OrderByDescending(i => i.ReceivedAt).ToList());
        }
        catch (Exception ex)
        {
            return ApiResponse<IReadOnlyList<InboxItemDto>>.Fail(ex.Message);
        }
    }

    private async Task ReplayPendingWebhooksAsync(string? processMenu, CancellationToken cancellationToken)
    {
        var menus = string.IsNullOrWhiteSpace(processMenu)
            ? ProcessModules.AllMenuTypes
            : [processMenu];

        foreach (var menu in menus)
        {
            try
            {
                await _webhookService.ImportStoredMessagingAsync(menu, 50, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Inbox could not replay stored webhooks for {MenuType}.", menu);
            }
        }
    }

    public async Task<InboxMediaFile?> GetPostMediaAsync(
        Guid userId,
        string postExternalId,
        string menuType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(postExternalId))
            return null;

        var store = _processData.ForMenu(menuType);
        var post = await store.FindPostByExternalIdAsync(postExternalId, cancellationToken);
        if (post is null)
            return null;

        var profile = await store.GetProfileByIdAsync(post.SocialProfileId, cancellationToken);
        if (profile is null)
            return null;

        var account = await store.GetSocialAccountByIdAsync(profile.SocialAccountId, cancellationToken);
        if (account is null || account.UserId != userId)
            return null;

        var platform = await store.GetPlatformByIdAsync(account.PlatformId, cancellationToken);
        var imageUrl = ProcessEntityNav.FirstMediaUrl(post);
        if (InstagramConnectionResolver.IsInstagramPlatform(platform?.Code)
            && !ProcessEntityNav.IsBrowserDisplayableUrl(imageUrl))
        {
            var auth = await store.GetSocialAuthByAccountIdAsync(account.Id, cancellationToken);
            var tokens = auth is null ? [] : CandidateTokens(auth, platform?.Code);
            var connectionType = InstagramConnectionResolver.FromPlatformCode(platform?.Code);
            foreach (var token in tokens)
            {
                var snapshot = await _instagramService.GetOwnedMediaSnapshotAsync(
                    token,
                    postExternalId,
                    connectionType,
                    ownerExternalId: profile.ExternalProfileId,
                    ownerMetadataJson: profile.MetadataJson,
                    cancellationToken: cancellationToken);
                if (snapshot is null)
                    continue;

                await MetaPostMediaWriter.ApplySnapshotAsync(store, post, snapshot, cancellationToken);
                imageUrl = ProcessEntityNav.FirstMediaUrl(post) ?? snapshot.ThumbnailUrl ?? snapshot.MediaUrl;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        var cached = await _mediaCache.TryReadLocalAsync(imageUrl);
        if (cached is not null)
            return new InboxMediaFile(cached.Value.Bytes, cached.Value.ContentType);

        var stored = await _mediaCache.StoreFromRemoteAsync(imageUrl, cancellationToken);
        if (string.IsNullOrWhiteSpace(stored))
            return null;

        cached = await _mediaCache.TryReadLocalAsync(stored);
        return cached is null ? null : new InboxMediaFile(cached.Value.Bytes, cached.Value.ContentType);
    }

    private IReadOnlyList<IProcessDataStore> ResolveStores(string? processMenu)
        => string.IsNullOrWhiteSpace(processMenu)
            ? _processData.AllStores()
            : [_processData.ForMenu(processMenu)];

    private async Task<IReadOnlyList<(string MenuType, SocialAccountEntityBase Account)>> LoadUserAccountsAsync(
        Guid userId,
        string? processMenu,
        CancellationToken cancellationToken)
    {
        var result = new List<(string MenuType, SocialAccountEntityBase Account)>();
        foreach (var store in ResolveStores(processMenu))
        {
            var accounts = await store.GetSocialAccountsByUserAsync(userId, cancellationToken);
            result.AddRange(accounts.Select(a => (store.MenuType, a)));
        }

        return result;
    }

    private static string? FindMenuTypeForAccount(
        IReadOnlyList<(string MenuType, SocialAccountEntityBase Account)> userAccounts,
        SocialAccountEntityBase account)
        => userAccounts.FirstOrDefault(a => a.Account.Id == account.Id).MenuType;

    private async Task<IReadOnlyList<InboxCommentRow>> LoadCommentsAsync(
        Guid userId,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds,
        IReadOnlyList<IProcessDataStore> stores,
        CancellationToken cancellationToken)
    {
        if (platformIds is not null && platformIds.Count == 0)
            return Array.Empty<InboxCommentRow>();

        var merged = new List<InboxCommentRow>();
        foreach (var store in stores)
        {
            var batch = new List<InboxCommentRow>();
            if (platformIds is null || platformIds.Count == 0)
            {
                batch.AddRange(await store.GetCommentsForInboxAsync(userId, platformId, null, cancellationToken));
            }
            else
            {
                foreach (var id in platformIds)
                    batch.AddRange(await store.GetCommentsForInboxAsync(userId, id, null, cancellationToken));
            }

            await EnsureMissingCommentPostMediaAsync(store, batch, cancellationToken);
            merged.AddRange(batch);
        }

        return merged
            .GroupBy(c => c.Comment.Id)
            .Select(g => g.First())
            .OrderByDescending(c => c.Comment.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Comments saved before media was persisted still have caption-only posts.
    /// Fetch the image/video once and store it so Inbox can render the media.
    /// </summary>
    private async Task EnsureMissingCommentPostMediaAsync(
        IProcessDataStore store,
        IReadOnlyList<InboxCommentRow> rows,
        CancellationToken cancellationToken)
    {
        var missing = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Post.ExternalPostId)
                          && (MetaPostMediaWriter.NeedsGraphRefresh(row.Post)
                              || string.IsNullOrWhiteSpace(row.PostImageUrl)))
            .GroupBy(row => row.Post.Id)
            .Select(group => group.First())
            .ToList();

        foreach (var row in missing)
        {
            try
            {
                var auth = await store.GetSocialAuthByAccountIdAsync(row.Account.Id, cancellationToken);
                if (auth is null)
                    continue;

                var tokens = CandidateTokens(auth, row.Platform.Code);
                if (tokens.Count == 0)
                    continue;

                RemotePostSnapshot? snapshot = null;
                if (InstagramConnectionResolver.IsInstagramPlatform(row.Platform.Code))
                {
                    var connectionType = InstagramConnectionResolver.FromPlatformCode(row.Platform.Code);
                    foreach (var token in tokens)
                    {
                        snapshot = await _instagramService.GetOwnedMediaSnapshotAsync(
                            token,
                            row.Post.ExternalPostId!,
                            connectionType,
                            row.Comment.ExternalCommentId,
                            row.Profile.ExternalProfileId,
                            row.Profile.MetadataJson,
                            cancellationToken);
                        if (snapshot is not null
                            && (!string.IsNullOrWhiteSpace(snapshot.MediaUrl)
                                || !string.IsNullOrWhiteSpace(snapshot.ThumbnailUrl)))
                            break;
                    }
                }
                else if (string.Equals(row.Platform.Code, "facebook", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var token in tokens)
                    {
                        snapshot = await _facebookService.GetPostSnapshotAsync(
                            token, row.Post.ExternalPostId!, cancellationToken);
                        if (snapshot is not null
                            && (!string.IsNullOrWhiteSpace(snapshot.MediaUrl)
                                || !string.IsNullOrWhiteSpace(snapshot.ThumbnailUrl)
                                || !string.IsNullOrWhiteSpace(snapshot.Text)))
                            break;
                    }
                }

                await MetaPostMediaWriter.ApplySnapshotAsync(store, row.Post, snapshot, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not persist Instagram/Facebook post media for ExternalPostId {PostId}.",
                    row.Post.ExternalPostId);
            }
        }
    }

    private static InboxPostMetaDto ToPostMeta(
        PostEntityBase post,
        SocialProfileEntityBase profile,
        string? fallbackImageUrl = null)
        => new()
        {
            PostId = post.ExternalPostId ?? post.Id.ToString(),
            PageName = profile.Name ?? profile.Username ?? "Instagram",
            PostText = DisplayPostText(post),
            PostImageUrl = ProcessEntityNav.FirstMediaUrl(post) ?? fallbackImageUrl,
            PostVideoUrl = ProcessEntityNav.FirstVideoUrl(post),
            LikesCount = post.LikeCount,
            CommentsCount = post.CommentCount,
            SharesCount = post.ShareCount,
            ViewsCount = post.ViewCount,
            PostedAt = post.PublishedAt ?? post.CreatedAt
        };

    private static async Task<IReadOnlyList<InboxMessageRow>> LoadMessagesAsync(
        Guid userId,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds,
        IReadOnlyList<IProcessDataStore> stores,
        CancellationToken cancellationToken)
    {
        if (platformIds is not null && platformIds.Count == 0)
            return Array.Empty<InboxMessageRow>();

        var merged = new List<InboxMessageRow>();
        foreach (var store in stores)
        {
            if (platformIds is null || platformIds.Count == 0)
            {
                merged.AddRange(await store.GetMessagesForInboxAsync(userId, platformId, null, cancellationToken));
                continue;
            }

            foreach (var id in platformIds)
                merged.AddRange(await store.GetMessagesForInboxAsync(userId, id, null, cancellationToken));
        }

        return merged
            .GroupBy(m => m.Message.Id)
            .Select(g => g.First())
            .OrderByDescending(m => m.Message.CreatedAt)
            .ToList();
    }

    public async Task<ApiResponse<object>> ReplyToCommentAsync(
        Guid userId,
        Guid commentId,
        ReplyCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return ApiResponse<object>.Fail("Message is required.");

            var menuType = MenuTypes.Normalize(request.MenuType);
            var located = await ProcessStoreLocator.FindInMenuAsync(
                _processData,
                menuType,
                store => store.GetCommentByIdAsync(commentId, cancellationToken),
                cancellationToken)
                ?? await ProcessStoreLocator.FindAsync(
                    _processData,
                    store => store.GetCommentByIdAsync(commentId, cancellationToken),
                    cancellationToken);
            if (located is null)
                throw new InvalidOperationException("Comment not found.");

            var (store, comment) = located.Value;
            var post = await store.GetPostByIdAsync(comment.PostId, cancellationToken)
                ?? throw new InvalidOperationException("Post not found.");
            var profile = await store.GetProfileByIdAsync(post.SocialProfileId, cancellationToken)
                ?? throw new InvalidOperationException("Profile not found.");
            var resolvedAuth = await ResolveReplyAuthAsync(
                store.MenuType,
                profile,
                userId,
                ReplyAuthHints.FromRequest(request.MenuType ?? store.MenuType, request.PageId, request.AccountId),
                cancellationToken);
            if (resolvedAuth is null)
                return ApiResponse<object>.Fail("No access token is available. Reconnect the account.");

            var (account, auth, accountMenuType, cardCode, cardTokens, cardPageId) = resolvedAuth;
            var code = !string.IsNullOrWhiteSpace(cardCode)
                ? cardCode
                : ResolveSendPlatformCode(
                    profile,
                    await ResolvePlatformCodeForReplyAsync(store, account, profile, accountMenuType, cancellationToken));
            if (!SupportsCommentReplies(code))
                return ApiResponse<object>.Fail("Platform does not support comment replies.");

            var replyTargetExternalId = await ResolveReplyTargetExternalIdAsync(store, comment, code, cancellationToken);
            if (string.IsNullOrWhiteSpace(replyTargetExternalId) ||
                replyTargetExternalId.StartsWith("local_reply_", StringComparison.OrdinalIgnoreCase) ||
                replyTargetExternalId.StartsWith("local_", StringComparison.OrdinalIgnoreCase))
                return ApiResponse<object>.Fail("Cannot reply until the original comment is synced from the platform.");

            var connectionType = InstagramConnectionResolver.FromPlatformCode(code);
            var tokens = cardTokens.Count > 0
                ? cardTokens.ToList()
                : code == "youtube"
                    ? (auth is null ? [] : CandidateYouTubeTokens(auth))
                    : ProcessCardAuthResolver.CollectTokens(auth, account, code);
            if (tokens.Count == 0)
                return ApiResponse<object>.Fail("No access token is available. Reconnect the account.");

            string? remoteCommentId = null;
            Exception? lastError = null;
            foreach (var token in tokens)
            {
                try
                {
                    remoteCommentId = code switch
                    {
                        "facebook" => await ReplyViaFacebookAsync(
                            token, profile, account, connectionType, replyTargetExternalId, request.Message.Trim(), cancellationToken, cardPageId),
                        "youtube" => await _youTubeService.ReplyCommentAsync(
                            token, replyTargetExternalId, request.Message.Trim(), cancellationToken),
                        _ when InstagramConnectionResolver.IsInstagramPlatform(code) => await ReplyViaInstagramAsync(
                            token, profile, account, connectionType, replyTargetExternalId, request.Message.Trim(), cancellationToken, cardPageId),
                        _ => null
                    };
                    lastError = null;
                    break;
                }
                catch (Exception ex) when (IsOAuthTokenError(ex))
                {
                    lastError = ex;
                }
            }

            if (lastError is not null)
                return ApiResponse<object>.Fail(lastError.Message);

            var externalId = string.IsNullOrWhiteSpace(remoteCommentId)
                ? $"local_reply_{Guid.NewGuid():N}"
                : remoteCommentId!;
            var inboxPlatformCode = InstagramConnectionResolver.ToInboxPlatformCode(code);
            var existing = await store.GetCommentByExternalIdAsync(externalId, cancellationToken);
            if (existing is not null)
            {
                await _inboxRealtime.NotifyInboxItemAsync(
                    userId,
                    MapCommentInboxItem(existing, post, profile, account, store.MenuType, inboxPlatformCode, isOutgoing: true),
                    cancellationToken);
                return ApiResponse<object>.Ok(new { replyId = existing.Id }, "Comment reply sent.");
            }

            var reply = store.NewComment();
            reply.PostId = post.Id;
            reply.ParentCommentId = comment.ParentCommentId ?? comment.Id;
            reply.ExternalCommentId = externalId;
            reply.AuthorId = profile.ExternalProfileId;
            reply.AuthorName = profile.Name ?? profile.Username ?? "You";
            reply.Message = request.Message.Trim();
            reply.PlatformCreatedAt = DateTime.UtcNow;
            await store.AddCommentAsync(reply, cancellationToken);
            post.CommentCount += 1;
            post.UpdatedAt = DateTime.UtcNow;
            store.UpdatePost(post);
            await store.SaveChangesAsync(cancellationToken);

            await _inboxRealtime.NotifyInboxItemAsync(
                userId,
                MapCommentInboxItem(reply, post, profile, account, store.MenuType, inboxPlatformCode, isOutgoing: true),
                cancellationToken);

            return ApiResponse<object>.Ok(new { replyId = reply.Id }, "Comment reply sent.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    private async Task<ReplyAuthResolution?> ResolveReplyAuthAsync(
        string defaultMenuType,
        SocialProfileEntityBase profile,
        Guid userId,
        ReplyAuthHints hints,
        CancellationToken cancellationToken)
    {
        var moduleMenu = MenuTypes.Normalize(
            string.IsNullOrWhiteSpace(hints.MenuType) ? defaultMenuType : hints.MenuType);

        var store = _processData.ForMenu(moduleMenu);
        var linked = await store.GetSocialAccountWithAuthAndProfilesAsync(profile.SocialAccountId, cancellationToken);
        string? linkedCode = null;
        if (linked is not null)
        {
            linkedCode = ProcessEntityNav.PlatformCode(linked)
                         ?? (await store.GetPlatformByIdAsync(linked.PlatformId, cancellationToken))?.Code;
        }

        ProcessCardAuth? matched = null;
        ProcessCardAuth? first = null;
        var tried = new List<string>();
        foreach (var code in EnumerateReplyCardCodes(profile, linkedCode))
        {
            tried.Add(code);
            var card = await ProcessCardAuthResolver.ResolveAsync(store, userId, code, cancellationToken);
            if (card is null)
                continue;

            first ??= card;
            if (CardMatchesHints(card, profile, hints))
            {
                matched = card;
                break;
            }
        }

        var chosen = matched ?? first;
        if (chosen is not null)
            return ToReplyAuthResolution(chosen, moduleMenu);

        _logger.LogWarning(
            "Inbox reply found no per-card token. UserId={UserId} Menu={Menu} Tried={Tried} ProfileType={ProfileType} LinkedPlatform={LinkedPlatform}",
            userId,
            moduleMenu,
            string.Join(",", tried),
            profile.ProfileType,
            linkedCode ?? "(none)");
        return null;
    }

    private static IReadOnlyList<string> EnumerateReplyCardCodes(SocialProfileEntityBase profile, string? linkedCode)
    {
        var codes = new List<string>();
        void Add(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return;
            var normalized = code.Trim().ToLowerInvariant();
            if (!codes.Contains(normalized))
                codes.Add(normalized);
        }

        Add(linkedCode);
        Add(InferPlatformCode(profile));

        if (profile.ProfileType == ProfileType.InstagramLogin
            || string.Equals(linkedCode, InstagramConnectionResolver.InstagramLoginPlatformCode, StringComparison.OrdinalIgnoreCase))
        {
            Add(InstagramConnectionResolver.InstagramLoginPlatformCode);
            Add(InstagramConnectionResolver.FacebookLoginPlatformCode);
        }
        else if (profile.ProfileType == ProfileType.InstagramBusiness
                 || InstagramConnectionResolver.IsInstagramPlatform(linkedCode))
        {
            Add(InstagramConnectionResolver.FacebookLoginPlatformCode);
            Add(InstagramConnectionResolver.InstagramLoginPlatformCode);
        }
        else if (profile.ProfileType == ProfileType.FacebookPage)
        {
            Add("facebook");
        }

        return codes;
    }

    private static bool CardMatchesHints(ProcessCardAuth card, SocialProfileEntityBase profile, ReplyAuthHints hints)
    {
        if (ProcessEntityNav.Profiles(card.Account).Any(p => ProfilesShareIdentity(p, profile)))
            return true;

        if (string.IsNullOrWhiteSpace(hints.PageId) && string.IsNullOrWhiteSpace(hints.AccountId))
            return false;

        if (ProcessEntityNav.Profiles(card.Account).Any(p => InboxRoutingHelper.ProfileMatchesRouting(p, hints.PageId, hints.AccountId)))
            return true;

        return InboxRoutingHelper.ProfileMatchesRouting(profile, hints.PageId, hints.AccountId)
               && string.Equals(card.PlatformCode, InferPlatformCode(profile), StringComparison.OrdinalIgnoreCase);
    }

    private static ReplyAuthResolution ToReplyAuthResolution(ProcessCardAuth card, string menuType)
        => new(
            card.Account,
            card.Auth,
            menuType,
            card.PlatformCode,
            card.Tokens,
            card.PageId);

    private async Task<string> ResolvePlatformCodeForReplyAsync(
        IProcessDataStore messageStore,
        SocialAccountEntityBase account,
        SocialProfileEntityBase profile,
        string accountMenuType,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(messageStore.MenuType, accountMenuType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Reply account must belong to the same module as the inbox item.");

        var platform = await messageStore.GetPlatformByIdAsync(account.PlatformId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(platform?.Code))
            return platform.Code.ToLowerInvariant();

        var fromNav = ProcessEntityNav.PlatformCode(account);
        if (!string.IsNullOrWhiteSpace(fromNav))
            return fromNav.ToLowerInvariant();

        var inferred = InferPlatformCode(profile);
        return string.IsNullOrWhiteSpace(inferred) ? string.Empty : inferred.ToLowerInvariant();
    }

    /// <summary>
    /// Instagram DMs must stay on the Instagram Graph path even when the usable
    /// token lives on the Facebook Page card (Facebook Login).
    /// </summary>
    private static string ResolveSendPlatformCode(SocialProfileEntityBase profile, string accountCode)
    {
        var isInstagramProfile = profile.ProfileType is ProfileType.InstagramBusiness or ProfileType.InstagramLogin
            || InstagramConnectionResolver.IsInstagramPlatform(InferPlatformCode(profile));
        if (!isInstagramProfile)
            return accountCode;

        return InstagramConnectionResolver.IsInstagramPlatform(accountCode)
            ? accountCode
            : InstagramConnectionResolver.FacebookLoginPlatformCode;
    }

    private static bool SupportsMessaging(string code)
        => code is "facebook" or "whatsapp" || InstagramConnectionResolver.IsInstagramPlatform(code);

    private static bool SupportsCommentReplies(string code)
        => code is "facebook" or "youtube" || InstagramConnectionResolver.IsInstagramPlatform(code);

    private async Task<string?> ReplyViaFacebookAsync(
        string token,
        SocialProfileEntityBase profile,
        SocialAccountEntityBase account,
        InstagramConnectionType connectionType,
        string replyTargetExternalId,
        string message,
        CancellationToken cancellationToken,
        string? pageId = null)
    {
        var context = BuildMetaCallContext(token, profile, account, connectionType, pageId);
        return await _facebookService.ReplyCommentAsync(context, replyTargetExternalId, message, cancellationToken);
    }

    private async Task<string?> ReplyViaInstagramAsync(
        string token,
        SocialProfileEntityBase profile,
        SocialAccountEntityBase account,
        InstagramConnectionType connectionType,
        string replyTargetExternalId,
        string message,
        CancellationToken cancellationToken,
        string? pageId = null)
    {
        var context = BuildMetaCallContext(token, profile, account, connectionType, pageId);
        return await _instagramService.ReplyCommentAsync(context, replyTargetExternalId, message, cancellationToken);
    }

    private static MetaCallContext BuildMetaCallContext(
        string token,
        SocialProfileEntityBase profile,
        SocialAccountEntityBase account,
        InstagramConnectionType connectionType,
        string? pageId = null)
        => new()
        {
            AccessToken = token,
            ProfileExternalId = profile.ExternalProfileId,
            PageExternalId = connectionType == InstagramConnectionType.FacebookLogin
                ? (pageId ?? ResolveReplyPageId(profile, account))
                : null,
            InstagramConnectionType = connectionType
        };

    private static string? ResolveReplyPageId(SocialProfileEntityBase profile, SocialAccountEntityBase? account)
    {
        var fromProfile = ReadPageId(profile.MetadataJson);
        if (!string.IsNullOrWhiteSpace(fromProfile))
            return fromProfile;

        if (profile.ProfileType == ProfileType.FacebookPage && !string.IsNullOrWhiteSpace(profile.ExternalProfileId))
            return profile.ExternalProfileId;

        if (account is not null)
        {
            foreach (var candidate in ProcessEntityNav.Profiles(account))
            {
                if (candidate.ProfileType == ProfileType.FacebookPage
                    && !string.IsNullOrWhiteSpace(candidate.ExternalProfileId))
                    return candidate.ExternalProfileId;

                var pageId = ReadPageId(candidate.MetadataJson);
                if (!string.IsNullOrWhiteSpace(pageId))
                    return pageId;
            }

            var selected = ReadSelectedPageId(account.MetadataJson);
            if (!string.IsNullOrWhiteSpace(selected))
                return selected;
        }

        return null;
    }

    private async Task<(SocialAccountEntityBase Account, SocialAuthEntityBase Auth)?> FindAccountByRoutingHintsAsync(
        Guid userId,
        SocialProfileEntityBase profile,
        ReplyAuthHints hints,
        CancellationToken cancellationToken)
    {
        var menuType = MenuTypes.Normalize(hints.MenuType!);
        var store = _processData.ForMenu(menuType);
        var platformCodes = ResolvePlatformCodes(profile, linkedAccount: null);
        var accounts = await store.GetSocialAccountsByUserAsync(userId, cancellationToken);
        (SocialAccountEntityBase Account, SocialAuthEntityBase Auth)? fallback = null;

        foreach (var row in accounts
                     .Where(a => a.Status == SocialAccountStatus.Connected || HasStoredTokens(ProcessEntityNav.Auth(a)))
                     .OrderByDescending(a => HasStoredTokens(ProcessEntityNav.Auth(a)))
                     .ThenByDescending(a => InstagramConnectionResolver.IsInstagramPlatform(ProcessEntityNav.PlatformCode(a)))
                     .ThenByDescending(a => a.Status == SocialAccountStatus.Connected)
                     .ThenByDescending(a => a.ConnectedAt ?? a.UpdatedAt ?? a.CreatedAt))
        {
            if (platformCodes.Count > 0 && !PlatformCodesOverlap(ProcessEntityNav.PlatformCode(row), platformCodes))
                continue;

            var loaded = await store.GetSocialAccountWithAuthAndProfilesAsync(row.Id, cancellationToken);
            var auth = loaded is null
                ? null
                : ProcessEntityNav.Auth(loaded) ?? await store.GetSocialAuthByAccountIdAsync(loaded.Id, cancellationToken);
            if (loaded is null || auth is null || CandidateTokens(auth, ProcessEntityNav.PlatformCode(loaded)).Count == 0)
                continue;

            if (ProcessEntityNav.Profiles(loaded).Any(p => InboxRoutingHelper.ProfileMatchesRouting(p, hints.PageId, hints.AccountId)))
                return (loaded, auth);

            if (ProcessEntityNav.Profiles(loaded).Any(p => ProfilesShareIdentity(p, profile)))
                return (loaded, auth);

            if (!string.IsNullOrWhiteSpace(hints.PageId) || !string.IsNullOrWhiteSpace(hints.AccountId))
            {
                if (InboxRoutingHelper.ProfileMatchesRouting(profile, hints.PageId, hints.AccountId))
                    return (loaded, auth);
            }

            fallback ??= (loaded, auth);
        }

        return fallback;
    }

    private static SocialAccountEntityBase? PickRoutingAccount(
        SocialProfileEntityBase profile,
        SocialAccountEntityBase? linkedAccount,
        IReadOnlyList<SocialAccountEntityBase> userAccounts,
        string? restrictMenuType = null)
    {
        if (linkedAccount is not null
            && ProcessEntityNav.Auth(linkedAccount) is { } linkedAuth
            && HasStoredTokens(linkedAuth))
            return linkedAccount;

        var platformCodes = ResolvePlatformCodes(profile, linkedAccount);
        var scopedAccounts = restrictMenuType is null
            ? userAccounts
            : userAccounts.Where(a => ProcessEntityNav.PlatformCode(a) is not null).ToList();

        return scopedAccounts
            .Where(a => HasStoredTokens(ProcessEntityNav.Auth(a))
                        && PlatformCodesOverlap(ProcessEntityNav.PlatformCode(a), platformCodes)
                        && (a.Status == SocialAccountStatus.Connected || HasStoredTokens(ProcessEntityNav.Auth(a))))
            .OrderByDescending(a => ProcessEntityNav.Profiles(a).Any(p => ProfilesShareIdentity(p, profile)))
            .ThenByDescending(a => InstagramConnectionResolver.IsInstagramPlatform(ProcessEntityNav.PlatformCode(a)))
            .ThenByDescending(a => a.Status == SocialAccountStatus.Connected)
            .ThenByDescending(a => a.ConnectedAt ?? a.UpdatedAt ?? a.CreatedAt)
            .FirstOrDefault();
    }

    private static HashSet<string> ResolvePlatformCodes(SocialProfileEntityBase profile, SocialAccountEntityBase? linkedAccount)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var linkedCode = linkedAccount is null ? null : ProcessEntityNav.PlatformCode(linkedAccount);
        if (!string.IsNullOrWhiteSpace(linkedCode))
            codes.Add(linkedCode);

        var inferred = InferPlatformCode(profile);
        if (!string.IsNullOrWhiteSpace(inferred))
            codes.Add(inferred);

        switch (profile.ProfileType)
        {
            case ProfileType.InstagramLogin:
                codes.Add(InstagramConnectionResolver.InstagramLoginPlatformCode);
                break;
            case ProfileType.InstagramBusiness:
                codes.Add(InstagramConnectionResolver.FacebookLoginPlatformCode);
                break;
            case ProfileType.FacebookPage:
                codes.Add("facebook");
                break;
            case ProfileType.WhatsAppPhone:
                codes.Add("whatsapp");
                break;
            case ProfileType.YouTubeChannel:
                codes.Add("youtube");
                break;
        }

        return codes;
    }

    private static bool PlatformCodesOverlap(string? platformCode, IReadOnlyCollection<string> expectedCodes)
    {
        if (expectedCodes.Count == 0)
            return true;

        if (string.IsNullOrWhiteSpace(platformCode))
            return false;

        return expectedCodes.Contains(platformCode);
    }

    private static bool HasStoredTokens(SocialAuthEntityBase? auth)
        => auth is not null
           && (!string.IsNullOrWhiteSpace(auth.AccessToken) || !string.IsNullOrWhiteSpace(auth.RefreshToken));

    private static string? InferPlatformCode(SocialProfileEntityBase profile)
        => profile.ProfileType switch
        {
            ProfileType.InstagramLogin => InstagramConnectionResolver.InstagramLoginPlatformCode,
            ProfileType.YouTubeChannel => "youtube",
            ProfileType.InstagramBusiness => InstagramConnectionResolver.FacebookLoginPlatformCode,
            ProfileType.FacebookPage => "facebook",
            ProfileType.WhatsAppPhone => "whatsapp",
            _ => null
        };

    private sealed record ReplyAuthHints(string? MenuType, string? PageId, string? AccountId)
    {
        public static ReplyAuthHints FromRequest(string? menuType, string? pageId, string? accountId)
            => new(
                string.IsNullOrWhiteSpace(menuType) ? null : MenuTypes.Normalize(menuType),
                string.IsNullOrWhiteSpace(pageId) ? null : pageId.Trim(),
                string.IsNullOrWhiteSpace(accountId) ? null : accountId.Trim());
    }

    private sealed record ReplyAuthResolution(
        SocialAccountEntityBase Account,
        SocialAuthEntityBase? Auth,
        string AccountMenuType,
        string PlatformCode,
        IReadOnlyList<string> Tokens,
        string? PageId);

    private static bool ProfilesShareIdentity(SocialProfileEntityBase a, SocialProfileEntityBase b)
    {
        if (string.Equals(a.ExternalProfileId, b.ExternalProfileId, StringComparison.Ordinal))
            return true;

        if (ProfileOwnsExternalId(a, b.ExternalProfileId) || ProfileOwnsExternalId(b, a.ExternalProfileId))
            return true;

        return false;
    }

    private static bool ProfileOwnsExternalId(SocialProfileEntityBase profile, string? externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            return false;

        if (string.Equals(profile.ExternalProfileId, externalId, StringComparison.Ordinal))
            return true;

        var pageId = ReadPageId(profile.MetadataJson);
        if (!string.IsNullOrWhiteSpace(pageId) && string.Equals(pageId, externalId, StringComparison.Ordinal))
            return true;

        foreach (var alternateId in ReadAlternateIds(profile.MetadataJson))
        {
            if (string.Equals(alternateId, externalId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static IReadOnlyList<string> ReadAlternateIds(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return Array.Empty<string>();

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(metadataJson);
            if (!doc.RootElement.TryGetProperty("alternateIds", out var ids) ||
                ids.ValueKind != System.Text.Json.JsonValueKind.Array)
                return Array.Empty<string>();

            return ids.EnumerateArray()
                .Select(id => id.ToString())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList()!;
        }
        catch (System.Text.Json.JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static List<string> CandidateTokens(SocialAuthEntityBase auth, string? platformCode = null)
    {
        var tokens = new List<string>();
        void Add(string? token)
        {
            if (!string.IsNullOrWhiteSpace(token) && !tokens.Contains(token))
                tokens.Add(token!);
        }

        Add(auth.AccessToken);
        if (string.Equals(platformCode, InstagramConnectionResolver.InstagramLoginPlatformCode, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(auth.AccessToken)
            && !string.Equals(auth.RefreshToken, auth.AccessToken, StringComparison.Ordinal))
            return tokens;

        Add(auth.RefreshToken);
        return tokens;
    }

    private static List<string> CandidateYouTubeTokens(SocialAuthEntityBase auth)
    {
        var tokens = new List<string>();
        if (!string.IsNullOrWhiteSpace(auth.AccessToken))
            tokens.Add(auth.AccessToken);
        return tokens;
    }

    private static bool IsOAuthTokenError(Exception ex)
    {
        var text = ex.Message ?? string.Empty;
        return text.Contains("OAuthException", StringComparison.OrdinalIgnoreCase)
            || text.Contains("\"code\":190", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Error validating access token", StringComparison.OrdinalIgnoreCase)
            || text.Contains("session is invalid", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInstagramRecipientError(Exception ex)
    {
        var text = ex.Message ?? string.Empty;
        return text.Contains("2534014", StringComparison.OrdinalIgnoreCase)
            || text.Contains("requested user cannot be found", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveGenericRecipientId(
        MessageEntityBase message,
        ConversationEntityBase conversation)
        => (message.Direction == MessageDirection.Outbound
            ? message.ReceiverId ?? conversation.CustomerId
            : message.SenderId ?? conversation.CustomerId)?.Trim();

    private static async Task<string> ResolveReplyTargetExternalIdAsync(
        IProcessDataStore store,
        CommentEntityBase comment,
        string platformCode,
        CancellationToken cancellationToken)
    {
        var current = comment;
        if (InstagramConnectionResolver.IsInstagramPlatform(platformCode))
        {
            var guard = 0;
            while (current.ParentCommentId.HasValue && guard++ < 20)
            {
                var parent = await store.GetCommentByIdAsync(current.ParentCommentId.Value, cancellationToken);
                if (parent is null) break;
                current = parent;
            }
        }

        var walk = current;
        var hops = 0;
        while (hops++ < 20)
        {
            var id = walk.ExternalCommentId ?? string.Empty;
            if (!IsLocalExternalId(id))
                return id;

            if (!walk.ParentCommentId.HasValue)
                return string.Empty;

            var parent = await store.GetCommentByIdAsync(walk.ParentCommentId.Value, cancellationToken);
            if (parent is null)
                return string.Empty;
            walk = parent;
        }

        return string.Empty;
    }

    private static bool IsLocalExternalId(string id)
        => id.StartsWith("local_reply_", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("local_", StringComparison.OrdinalIgnoreCase);

    private static InboxItemDto MapCommentInboxItem(
        CommentEntityBase comment,
        PostEntityBase post,
        SocialProfileEntityBase profile,
        SocialAccountEntityBase account,
        string menuType,
        string platformCode,
        bool isOutgoing)
    {
        var item = new InboxItemDto
        {
            Id = comment.Id,
            ItemKind = "comment",
            PlatformCode = platformCode,
            ExternalId = comment.ExternalCommentId,
            AuthorName = comment.AuthorName,
            AuthorId = comment.AuthorId,
            Content = comment.Message,
            IsHidden = comment.IsHidden,
            IsRead = true,
            IsOutgoing = isOutgoing,
            ReceivedAt = comment.PlatformCreatedAt ?? comment.CreatedAt,
            CommentLikes = comment.LikeCount,
            ReplyCount = 0,
            ParentId = comment.ParentCommentId,
            Post = ToPostMeta(post, profile)
        };
        InboxRoutingHelper.Apply(item, profile, account, menuType);
        return item;
    }

    public async Task<ApiResponse<object>> HideCommentAsync(
        Guid userId,
        Guid commentId,
        HideCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (context, code, store, comment) = await ResolveCommentContextAsync(userId, commentId, request.MenuType, cancellationToken);

            if (code == "facebook")
                await _facebookService.HideCommentAsync(context, comment!.ExternalCommentId, request.Hide, cancellationToken);
            else if (InstagramConnectionResolver.IsInstagramPlatform(code))
                await _instagramService.HideCommentAsync(context, comment!.ExternalCommentId, request.Hide, cancellationToken);
            else
                return ApiResponse<object>.Fail("Platform does not support hiding comments.");

            comment!.IsHidden = request.Hide;
            store.UpdateComment(comment);
            await store.SaveChangesAsync(cancellationToken);
            return ApiResponse<object>.Ok(new { }, request.Hide ? "Comment hidden." : "Comment unhidden.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<object>> DeleteCommentAsync(
        Guid userId,
        Guid commentId,
        string? menuType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (context, code, store, comment) = await ResolveCommentContextAsync(userId, commentId, menuType, cancellationToken);

            if (code == "facebook")
                await _facebookService.DeleteCommentAsync(context, comment!.ExternalCommentId, cancellationToken);
            else if (InstagramConnectionResolver.IsInstagramPlatform(code))
                await _instagramService.DeleteCommentAsync(context, comment!.ExternalCommentId, cancellationToken);
            else
                return ApiResponse<object>.Fail("Platform does not support deleting comments.");

            comment!.IsDeleted = true;
            store.UpdateComment(comment);
            await store.SaveChangesAsync(cancellationToken);
            return ApiResponse<object>.Ok(new { }, "Comment deleted.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<object>> ReplyToMessageAsync(
        Guid userId,
        Guid messageId,
        ReplyMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return ApiResponse<object>.Fail("Message is required.");

            var menuType = MenuTypes.Normalize(request.MenuType);
            var located = await ProcessStoreLocator.FindInMenuAsync(
                _processData,
                menuType,
                store => store.GetMessageByIdAsync(messageId, cancellationToken),
                cancellationToken)
                ?? await ProcessStoreLocator.FindAsync(
                    _processData,
                    store => store.GetMessageByIdAsync(messageId, cancellationToken),
                    cancellationToken);
            if (located is null)
                throw new InvalidOperationException("Message not found.");

            var (store, message) = located.Value;
            var conversation = await store.GetConversationByIdAsync(message.ConversationId, cancellationToken)
                ?? throw new InvalidOperationException("Conversation not found.");
            var profile = await store.GetProfileByIdAsync(conversation.SocialProfileId, cancellationToken)
                ?? throw new InvalidOperationException("Profile not found.");
            var resolvedAuth = await ResolveReplyAuthAsync(
                store.MenuType,
                profile,
                userId,
                ReplyAuthHints.FromRequest(request.MenuType ?? store.MenuType, request.PageId, request.AccountId),
                cancellationToken);
            if (resolvedAuth is null)
                return ApiResponse<object>.Fail("No access token is available. Reconnect the account.");

            var (account, auth, accountMenuType, cardCode, cardTokens, cardPageId) = resolvedAuth;
            var code = !string.IsNullOrWhiteSpace(cardCode)
                ? cardCode
                : ResolveSendPlatformCode(
                    profile,
                    await ResolvePlatformCodeForReplyAsync(store, account, profile, accountMenuType, cancellationToken));
            if (!SupportsMessaging(code))
                return ApiResponse<object>.Fail("Platform does not support messaging.");

            var recipientId = InstagramConnectionResolver.IsInstagramPlatform(code)
                ? InstagramMessagingRecipient.Resolve(message, conversation, profile)
                : ResolveGenericRecipientId(message, conversation);
            if (string.IsNullOrWhiteSpace(recipientId))
            {
                return ApiResponse<object>.Fail(
                    InstagramConnectionResolver.IsInstagramPlatform(code)
                        ? InstagramMessagingRecipient.FormatRecipientNotFoundError(store.MenuType, conversation.CustomerId)
                        : "Recipient is unknown for this conversation.");
            }

            MessageEntityBase? quoted = null;
            if (request.ReplyToMessageId.HasValue)
            {
                quoted = await store.GetMessageByIdAsync(request.ReplyToMessageId.Value, cancellationToken);
                if (quoted is null || quoted.ConversationId != conversation.Id)
                    return ApiResponse<object>.Fail("The message being replied to is not part of this conversation.");
            }

            var replyToMid = quoted is not null && !IsLocalExternalId(quoted.ExternalMessageId)
                ? quoted.ExternalMessageId
                : null;

            var connectionType = InstagramConnectionResolver.FromPlatformCode(code);
            var tokens = cardTokens.Count > 0
                ? cardTokens.ToList()
                : ProcessCardAuthResolver.CollectTokens(auth, account, code);
            if (tokens.Count == 0)
                return ApiResponse<object>.Fail("No access token is available. Reconnect the account.");

            string? remoteMessageId = null;
            Exception? lastError = null;
            foreach (var token in tokens)
            {
                var context = new MetaCallContext
                {
                    AccessToken = token,
                    ProfileExternalId = profile.ExternalProfileId,
                    PageExternalId = connectionType == InstagramConnectionType.FacebookLogin
                        ? (cardPageId ?? ResolveReplyPageId(profile, account))
                        : null,
                    InstagramConnectionType = connectionType
                };

                try
                {
                    remoteMessageId = code switch
                    {
                        "facebook" => await _facebookService.SendMessageAsync(context, recipientId, request.Message.Trim(), replyToMid, cancellationToken),
                        "instagram" or "instagram_login" => await _instagramService.SendMessageAsync(context, recipientId, request.Message.Trim(), replyToMid, cancellationToken),
                        "whatsapp" => await _whatsAppService.SendMessageAsync(context, recipientId, request.Message.Trim(), replyToMid, cancellationToken),
                        _ => null
                    };
                    lastError = null;
                    break;
                }
                catch (Exception ex) when (IsOAuthTokenError(ex))
                {
                    lastError = ex;
                }
                catch (Exception ex) when (IsInstagramRecipientError(ex))
                {
                    return ApiResponse<object>.Fail(
                        InstagramMessagingRecipient.FormatInstagramRecipientApiError(store.MenuType));
                }
            }

            if (lastError is not null)
                return ApiResponse<object>.Fail(lastError.Message);

            var externalId = string.IsNullOrWhiteSpace(remoteMessageId)
                ? $"local_msg_{Guid.NewGuid():N}"
                : remoteMessageId!;
            var inboxPlatformCode = InstagramConnectionResolver.ToInboxPlatformCode(code);
            var existing = await store.GetMessageByExternalIdAsync(externalId, cancellationToken);
            if (existing is not null)
            {
                await _inboxRealtime.NotifyInboxItemAsync(
                    userId,
                    MapMessageInboxItem(existing, conversation, profile, account, store.MenuType, inboxPlatformCode, quoted),
                    cancellationToken);
                return ApiResponse<object>.Ok(new { messageId = existing.Id }, "Message sent.");
            }

            var sentAt = DateTime.UtcNow;
            var outbound = store.NewMessage();
            outbound.ConversationId = conversation.Id;
            outbound.ExternalMessageId = externalId;
            outbound.SenderId = profile.ExternalProfileId;
            outbound.ReceiverId = recipientId;
            outbound.Direction = MessageDirection.Outbound;
            outbound.MessageType = MessageContentType.Text;
            outbound.Body = request.Message.Trim();
            outbound.Status = MessageDeliveryStatus.Sent;
            outbound.PlatformCreatedAt = sentAt;
            outbound.ReplyToMessageId = quoted?.Id;
            outbound.ReplyToExternalId = replyToMid;
            await store.AddMessageAsync(outbound, cancellationToken);

            conversation.LastMessageAt = sentAt;
            conversation.UpdatedAt = sentAt;
            store.UpdateConversation(conversation);
            await store.SaveChangesAsync(cancellationToken);

            await _inboxRealtime.NotifyInboxItemAsync(
                userId,
                MapMessageInboxItem(outbound, conversation, profile, account, store.MenuType, inboxPlatformCode, quoted),
                cancellationToken);

            return ApiResponse<object>.Ok(new { messageId = outbound.Id }, "Message sent.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    private static InboxItemDto MapMessageInboxItem(
        MessageEntityBase message,
        ConversationEntityBase conversation,
        SocialProfileEntityBase profile,
        SocialAccountEntityBase account,
        string menuType,
        string platformCode,
        MessageEntityBase? quoted = null)
    {
        var connectedProfiles = ProcessEntityNav.Profiles(account);
        if (connectedProfiles.Count == 0)
            connectedProfiles = [profile];
        var outgoing = MetaConversationSide.IsOutgoing(
            account,
            connectedProfiles,
            message.SenderId,
            message.ReceiverId,
            message.Direction == MessageDirection.Outbound);
        var peerId = MetaConversationSide.ResolvePeerId(
            account,
            connectedProfiles,
            conversation.CustomerId,
            message.SenderId,
            message.ReceiverId);
        var item = new InboxItemDto
        {
            Id = message.Id,
            ItemKind = "message",
            PlatformCode = platformCode,
            ExternalId = message.ExternalMessageId,
            AuthorName = outgoing
                ? "You"
                : conversation.CustomerName ?? peerId ?? message.SenderId ?? "User",
            AuthorId = peerId ?? conversation.CustomerId ?? message.SenderId,
            Content = message.Body ?? string.Empty,
            IsHidden = false,
            IsRead = true,
            IsOutgoing = outgoing,
            ConversationId = conversation.Id,
            ReceivedAt = message.PlatformCreatedAt ?? message.CreatedAt,
            ReplyToId = quoted?.Id ?? message.ReplyToMessageId,
            ReplyToAuthor = quoted is null
                ? null
                : quoted.Direction == MessageDirection.Outbound ? "You" : conversation.CustomerName ?? quoted.SenderId,
            ReplyToContent = quoted?.Body
        };
        InboxRoutingHelper.Apply(item, profile, account, menuType);
        return item;
    }

    public async Task<ApiResponse<object>> DeleteMessageAsync(
        Guid userId,
        Guid messageId,
        string? menuType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMenu = MenuTypes.Normalize(menuType);
            var located = await ProcessStoreLocator.FindInMenuAsync(
                _processData,
                normalizedMenu,
                store => store.GetMessageByIdAsync(messageId, cancellationToken),
                cancellationToken);
            if (located is null)
                return ApiResponse<object>.Fail("Message not found.");

            var (store, message) = located.Value;
            var conversation = await store.GetConversationByIdAsync(message.ConversationId, cancellationToken);
            var profile = conversation is null
                ? null
                : await store.GetProfileByIdAsync(conversation.SocialProfileId, cancellationToken);
            var account = profile is null
                ? null
                : await store.GetSocialAccountWithAuthAndProfilesAsync(profile.SocialAccountId, cancellationToken);
            if (account is null || account.UserId != userId)
                return ApiResponse<object>.Fail("Message not found.");

            store.RemoveMessage(message);
            await store.SaveChangesAsync(cancellationToken);
            return ApiResponse<object>.Ok(new { }, "Message deleted.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<object>> MarkReadAsync(
        Guid userId,
        Guid conversationId,
        string? menuType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMenu = MenuTypes.Normalize(menuType);
            var located = await ProcessStoreLocator.FindInMenuAsync(
                _processData,
                normalizedMenu,
                store => store.GetConversationByIdAsync(conversationId, cancellationToken),
                cancellationToken);
            if (located is null)
                return ApiResponse<object>.Fail("Conversation not found.");

            var (store, conversation) = located.Value;
            var profile = await store.GetProfileByIdAsync(conversation.SocialProfileId, cancellationToken);
            var account = profile is null
                ? null
                : await store.GetSocialAccountByIdAsync(profile.SocialAccountId, cancellationToken);
            if (account is null || account.UserId != userId)
                return ApiResponse<object>.Fail("Conversation not found.");

            conversation.UnreadCount = 0;
            conversation.UpdatedAt = DateTime.UtcNow;
            store.UpdateConversation(conversation);
            await store.SaveChangesAsync(cancellationToken);
            return ApiResponse<object>.Ok(new { }, "Marked as read.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    private static string? QuotedAuthor(
        MessageEntityBase message,
        ConversationEntityBase conversation,
        IReadOnlyDictionary<Guid, InboxMessageRow> byId)
    {
        if (!message.ReplyToMessageId.HasValue || !byId.TryGetValue(message.ReplyToMessageId.Value, out var quoted))
            return null;

        return quoted.Message.Direction == MessageDirection.Outbound
            ? "You"
            : conversation.CustomerName ?? quoted.Message.SenderId;
    }

    private static string DisplayPostText(PostEntityBase post)
    {
        var text = FirstNonEmpty(post.Caption, post.Text);
        return MetaPostMediaWriter.IsPlaceholderText(text) ? string.Empty : text;
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private async Task<(MetaCallContext Context, string Code, IProcessDataStore Store, CommentEntityBase Comment)> ResolveCommentContextAsync(
        Guid userId,
        Guid commentId,
        string? menuType,
        CancellationToken cancellationToken)
    {
        var normalizedMenu = MenuTypes.Normalize(menuType);
        var located = await ProcessStoreLocator.FindInMenuAsync(
            _processData,
            normalizedMenu,
            store => store.GetCommentByIdAsync(commentId, cancellationToken),
            cancellationToken)
            ?? throw new InvalidOperationException("Comment not found.");

        var (store, comment) = located;
        var post = await store.GetPostByIdAsync(comment.PostId, cancellationToken)
            ?? throw new InvalidOperationException("Post not found.");
        var profile = await store.GetProfileByIdAsync(post.SocialProfileId, cancellationToken)
            ?? throw new InvalidOperationException("Profile not found.");
        var resolvedAuth = await ResolveReplyAuthAsync(
            store.MenuType,
            profile,
            userId,
            ReplyAuthHints.FromRequest(normalizedMenu, null, null),
            cancellationToken)
            ?? throw new InvalidOperationException("Comment not found.");

        var (account, auth, accountMenuType, cardCode, cardTokens, cardPageId) = resolvedAuth;
        var code = !string.IsNullOrWhiteSpace(cardCode)
            ? cardCode
            : ResolveSendPlatformCode(
                profile,
                await ResolvePlatformCodeForReplyAsync(store, account, profile, accountMenuType, cancellationToken));
        var connectionType = InstagramConnectionResolver.FromPlatformCode(code);
        var accessToken = cardTokens.FirstOrDefault()
                          ?? ProcessCardAuthResolver.CollectTokens(auth, account, code).FirstOrDefault()
                          ?? auth?.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("No access token is available. Reconnect the account.");

        return (new MetaCallContext
        {
            AccessToken = accessToken,
            ProfileExternalId = profile.ExternalProfileId,
            PageExternalId = connectionType == InstagramConnectionType.FacebookLogin
                ? (cardPageId ?? ResolveReplyPageId(profile, account))
                : null,
            InstagramConnectionType = connectionType
        }, code, store, comment);
    }

    private static string? ReadPageId(string? metadataJson)
        => ReadMetadataString(metadataJson, "pageId");

    private static string? ReadSelectedPageId(string? metadataJson)
        => ReadMetadataString(metadataJson, "selectedPageId");

    private static string? ReadMetadataString(string? metadataJson, string property)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(metadataJson);
            return doc.RootElement.TryGetProperty(property, out var value) ? value.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
