using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Inbox;
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
    private readonly IInboxRealtimeNotifier _inboxRealtime;

    public InboxService(
        IProcessDataStoreFactory processData,
        IFacebookService facebookService,
        IInstagramService instagramService,
        IWhatsAppService whatsAppService,
        IInboxRealtimeNotifier inboxRealtime)
    {
        _processData = processData;
        _facebookService = facebookService;
        _instagramService = instagramService;
        _whatsAppService = whatsAppService;
        _inboxRealtime = inboxRealtime;
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
                platformIds = (await _processData.ForMenu(processMenu).GetActivePlatformsAsync(cancellationToken))
                    .Select(p => p.Id)
                    .ToList();
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
                        Post = new InboxPostMetaDto
                        {
                            PostId = row.Post.ExternalPostId ?? row.Post.Id.ToString(),
                            PageName = row.Profile.Name ?? row.Profile.Username ?? "Instagram",
                            PostText = DisplayPostText(row.Post),
                            PostImageUrl = row.PostImageUrl,
                            LikesCount = row.Post.LikeCount,
                            CommentsCount = row.Post.CommentCount,
                            SharesCount = row.Post.ShareCount,
                            PostedAt = row.Post.PublishedAt ?? row.Post.CreatedAt
                        }
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
                    var item = new InboxItemDto
                    {
                        Id = row.Message.Id,
                        ItemKind = "message",
                        PlatformCode = InstagramConnectionResolver.ToInboxPlatformCode(row.Platform.Code),
                        ExternalId = row.Message.ExternalMessageId,
                        AuthorName = row.Message.Direction == MessageDirection.Outbound
                            ? "You"
                            : row.Conversation.CustomerName ?? row.Message.SenderId ?? "Instagram user",
                        AuthorId = row.Message.SenderId,
                        Content = row.Message.Body ?? string.Empty,
                        IsHidden = false,
                        IsRead = row.Message.Direction == MessageDirection.Outbound || row.Conversation.UnreadCount == 0,
                        IsOutgoing = row.Message.Direction == MessageDirection.Outbound,
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

    private static async Task<IReadOnlyList<InboxCommentRow>> LoadCommentsAsync(
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
            if (platformIds is null || platformIds.Count == 0)
            {
                merged.AddRange(await store.GetCommentsForInboxAsync(userId, platformId, null, cancellationToken));
                continue;
            }

            foreach (var id in platformIds)
                merged.AddRange(await store.GetCommentsForInboxAsync(userId, id, null, cancellationToken));
        }

        return merged
            .GroupBy(c => c.Comment.Id)
            .Select(g => g.First())
            .OrderByDescending(c => c.Comment.CreatedAt)
            .ToList();
    }

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
                ReplyAuthHints.FromRequest(request.MenuType, request.PageId, request.AccountId),
                cancellationToken);
            if (resolvedAuth is null)
                return ApiResponse<object>.Fail("No access token is available. Reconnect the account.");

            var (account, auth, accountMenuType) = resolvedAuth;
            var code = await ResolvePlatformCodeForReplyAsync(store, account, profile, accountMenuType, cancellationToken);
            if (!SupportsCommentReplies(code))
                return ApiResponse<object>.Fail("Platform does not support comment replies.");

            var replyTargetExternalId = await ResolveReplyTargetExternalIdAsync(store, comment, code, cancellationToken);
            if (string.IsNullOrWhiteSpace(replyTargetExternalId) ||
                replyTargetExternalId.StartsWith("local_reply_", StringComparison.OrdinalIgnoreCase) ||
                replyTargetExternalId.StartsWith("local_", StringComparison.OrdinalIgnoreCase))
                return ApiResponse<object>.Fail("Cannot reply until the original comment is synced from Meta.");

            var connectionType = InstagramConnectionResolver.FromProfile(profile, code);
            var tokens = CandidateTokens(auth);
            if (tokens.Count == 0)
                return ApiResponse<object>.Fail("No access token is available. Reconnect the account.");

            string? remoteCommentId = null;
            Exception? lastError = null;
            foreach (var token in tokens)
            {
                var context = new MetaCallContext
                {
                    AccessToken = token,
                    ProfileExternalId = profile.ExternalProfileId,
                    PageExternalId = connectionType == InstagramConnectionType.FacebookLogin
                        ? ReadPageId(profile.MetadataJson)
                        : null,
                    InstagramConnectionType = connectionType
                };

                try
                {
                    remoteCommentId = code == "facebook"
                        ? await _facebookService.ReplyCommentAsync(context, replyTargetExternalId, request.Message.Trim(), cancellationToken)
                        : await _instagramService.ReplyCommentAsync(context, replyTargetExternalId, request.Message.Trim(), cancellationToken);
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
        if (linked is not null
            && linked.UserId == userId
            && ProcessEntityNav.Auth(linked) is { } linkedAuth
            && CandidateTokens(linkedAuth).Count > 0)
            return new ReplyAuthResolution(linked, linkedAuth, moduleMenu);

        // Instagram IGSIDs only work with the same Meta app that received the webhook.
        if (linked is not null
            && linked.UserId == userId
            && InstagramConnectionResolver.IsInstagramPlatform(
                InferPlatformCode(profile) ?? ProcessEntityNav.PlatformCode(linked) ?? string.Empty))
            return null;

        var hinted = await FindAccountByRoutingHintsAsync(
            userId,
            profile,
            ReplyAuthHints.FromRequest(moduleMenu, hints.PageId, hints.AccountId),
            cancellationToken);
        if (hinted is not null)
            return new ReplyAuthResolution(hinted.Value.Account, hinted.Value.Auth, moduleMenu);

        var userAccounts = await LoadUserAccountsAsync(userId, moduleMenu, cancellationToken);
        var routingAccount = PickRoutingAccount(
            profile,
            linked,
            userAccounts.Select(a => a.Account).ToList(),
            moduleMenu);
        if (routingAccount is not null
            && ProcessEntityNav.Auth(routingAccount) is { } routingAuth
            && CandidateTokens(routingAuth).Count > 0)
            return new ReplyAuthResolution(routingAccount, routingAuth, moduleMenu);

        var platformCodes = ResolvePlatformCodes(profile, linked);
        if (platformCodes.Count == 0)
            return null;

        foreach (var (menuType, row) in userAccounts
                     .OrderByDescending(a => HasStoredTokens(ProcessEntityNav.Auth(a.Account)))
                     .ThenByDescending(a => a.Account.Status == SocialAccountStatus.Connected)
                     .ThenByDescending(a => a.Account.ConnectedAt ?? a.Account.UpdatedAt ?? a.Account.CreatedAt))
        {
            if (row.UserId != userId
                || !PlatformCodesOverlap(ProcessEntityNav.PlatformCode(row), platformCodes)
                || (row.Status != SocialAccountStatus.Connected && !HasStoredTokens(ProcessEntityNav.Auth(row))))
                continue;

            var loaded = await store.GetSocialAccountWithAuthAndProfilesAsync(row.Id, cancellationToken);
            var auth = loaded is null ? null : ProcessEntityNav.Auth(loaded);
            if (auth is null || CandidateTokens(auth).Count == 0)
                continue;

            if (ProcessEntityNav.Profiles(loaded!).Any(p => ProfilesShareIdentity(p, profile)))
                return new ReplyAuthResolution(loaded!, auth, menuType);

            if (InboxRoutingHelper.ProfileMatchesRouting(profile, hints.PageId, hints.AccountId)
                && ProcessEntityNav.Profiles(loaded!).Any(p => InboxRoutingHelper.ProfileMatchesRouting(p, hints.PageId, hints.AccountId)))
                return new ReplyAuthResolution(loaded!, auth, menuType);
        }

        return null;
    }

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

    private static bool SupportsMessaging(string code)
        => code is "facebook" or "whatsapp" || InstagramConnectionResolver.IsInstagramPlatform(code);

    private static bool SupportsCommentReplies(string code)
        => code is "facebook" || InstagramConnectionResolver.IsInstagramPlatform(code);

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

        foreach (var row in accounts
                     .Where(a => a.Status == SocialAccountStatus.Connected || HasStoredTokens(ProcessEntityNav.Auth(a)))
                     .OrderByDescending(a => HasStoredTokens(ProcessEntityNav.Auth(a)))
                     .ThenByDescending(a => a.Status == SocialAccountStatus.Connected)
                     .ThenByDescending(a => a.ConnectedAt ?? a.UpdatedAt ?? a.CreatedAt))
        {
            if (platformCodes.Count > 0 && !PlatformCodesOverlap(ProcessEntityNav.PlatformCode(row), platformCodes))
                continue;

            var loaded = await store.GetSocialAccountWithAuthAndProfilesAsync(row.Id, cancellationToken);
            var auth = loaded is null ? null : ProcessEntityNav.Auth(loaded);
            if (auth is null || CandidateTokens(auth).Count == 0)
                continue;

            if (ProcessEntityNav.Profiles(loaded!).Any(p => InboxRoutingHelper.ProfileMatchesRouting(p, hints.PageId, hints.AccountId)))
                return (loaded!, auth);

            if (ProcessEntityNav.Profiles(loaded!).Any(p => ProfilesShareIdentity(p, profile)))
                return (loaded!, auth);

            if (!string.IsNullOrWhiteSpace(hints.PageId) || !string.IsNullOrWhiteSpace(hints.AccountId))
            {
                if (InboxRoutingHelper.ProfileMatchesRouting(profile, hints.PageId, hints.AccountId))
                    return (loaded!, auth);
            }
        }

        return null;
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
                codes.Add(InstagramConnectionResolver.InstagramLoginPlatformCode);
                break;
            case ProfileType.FacebookPage:
                codes.Add("facebook");
                break;
            case ProfileType.WhatsAppPhone:
                codes.Add("whatsapp");
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

        if (expectedCodes.Contains(platformCode))
            return true;

        return InstagramConnectionResolver.IsInstagramPlatform(platformCode)
               && expectedCodes.Any(InstagramConnectionResolver.IsInstagramPlatform);
    }

    private static bool HasStoredTokens(SocialAuthEntityBase? auth)
        => auth is not null
           && (!string.IsNullOrWhiteSpace(auth.AccessToken) || !string.IsNullOrWhiteSpace(auth.RefreshToken));

    private static string? InferPlatformCode(SocialProfileEntityBase profile)
        => profile.ProfileType switch
        {
            ProfileType.InstagramLogin => InstagramConnectionResolver.InstagramLoginPlatformCode,
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
        SocialAuthEntityBase Auth,
        string AccountMenuType);

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

    private static List<string> CandidateTokens(SocialAuthEntityBase auth)
    {
        var tokens = new List<string>();
        void Add(string? token)
        {
            if (!string.IsNullOrWhiteSpace(token) && !tokens.Contains(token))
                tokens.Add(token!);
        }

        Add(auth.AccessToken);
        Add(auth.RefreshToken);
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
            Post = new InboxPostMetaDto
            {
                PostId = post.ExternalPostId ?? post.Id.ToString(),
                PageName = profile.Name ?? profile.Username ?? platformCode,
                PostText = DisplayPostText(post),
                PostImageUrl = ProcessEntityNav.FirstMediaUrl(post),
                LikesCount = post.LikeCount,
                CommentsCount = post.CommentCount,
                SharesCount = post.ShareCount,
                PostedAt = post.PublishedAt ?? post.CreatedAt
            }
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
                ReplyAuthHints.FromRequest(request.MenuType, request.PageId, request.AccountId),
                cancellationToken);
            if (resolvedAuth is null)
                return ApiResponse<object>.Fail("No access token is available. Reconnect the account.");

            var (account, auth, accountMenuType) = resolvedAuth;
            var code = await ResolvePlatformCodeForReplyAsync(store, account, profile, accountMenuType, cancellationToken);
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

            var connectionType = InstagramConnectionResolver.FromProfile(profile, code);
            var tokens = CandidateTokens(auth);
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
                        ? ReadPageId(profile.MetadataJson)
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
        var item = new InboxItemDto
        {
            Id = message.Id,
            ItemKind = "message",
            PlatformCode = platformCode,
            ExternalId = message.ExternalMessageId,
            AuthorName = message.Direction == MessageDirection.Outbound
                ? "You"
                : conversation.CustomerName ?? message.SenderId ?? "User",
            AuthorId = message.SenderId,
            Content = message.Body ?? string.Empty,
            IsHidden = false,
            IsRead = true,
            IsOutgoing = message.Direction == MessageDirection.Outbound,
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
        return !string.IsNullOrWhiteSpace(post.ExternalPostId) &&
               string.Equals(text, $"Facebook post {post.ExternalPostId}", StringComparison.Ordinal)
            ? "Facebook post"
            : text;
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

        var (account, auth, accountMenuType) = resolvedAuth;
        var code = await ResolvePlatformCodeForReplyAsync(store, account, profile, accountMenuType, cancellationToken);
        var connectionType = InstagramConnectionResolver.FromProfile(profile, code);
        return (new MetaCallContext
        {
            AccessToken = auth.AccessToken,
            ProfileExternalId = profile.ExternalProfileId,
            PageExternalId = connectionType == InstagramConnectionType.FacebookLogin
                ? ReadPageId(profile.MetadataJson)
                : null,
            InstagramConnectionType = connectionType
        }, code, store, comment);
    }

    private static string? ReadPageId(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(metadataJson);
            return doc.RootElement.TryGetProperty("pageId", out var pageId) ? pageId.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
