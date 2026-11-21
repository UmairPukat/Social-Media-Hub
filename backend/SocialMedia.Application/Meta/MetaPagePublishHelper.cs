using System.Text.Json;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Application.Meta;

public static class MetaPagePublishHelper
{
    public static string? ResolveSelectedPageId(
        SocialAccountEntityBase account,
        SocialProfileEntityBase? profile = null)
        => FirstNonEmpty(
            ReadJsonString(account.MetadataJson, "selectedPageId"),
            ReadJsonString(profile?.MetadataJson, "pageId"),
            profile?.ExternalProfileId);

    public static async Task<(string PageId, string PageAccessToken)> ResolveFacebookPublishCredentialsAsync(
        IFacebookService facebookService,
        SocialAccountEntityBase account,
        SocialProfileEntityBase profile,
        SocialAuthEntityBase auth,
        CancellationToken cancellationToken)
    {
        var pageId = ResolveSelectedPageId(account, profile)
            ?? throw new InvalidOperationException("No Facebook page is selected. Reconnect Facebook and choose a page.");

        var cachedPageToken = ReadJsonString(profile.MetadataJson, "pageAccessToken");
        foreach (var userToken in DistinctTokens(auth.RefreshToken, auth.AccessToken))
        {
            try
            {
                var pages = await facebookService.ListPagesAsync(userToken, cancellationToken);
                var page = pages.FirstOrDefault(p => string.Equals(p.PageId, pageId, StringComparison.Ordinal));
                if (!string.IsNullOrWhiteSpace(page?.PageAccessToken))
                    return (pageId, page.PageAccessToken);
            }
            catch
            {
                // Try the next stored token, then the cached page token.
            }
        }

        var storedToken = FirstNonEmpty(cachedPageToken, auth.AccessToken);
        if (!string.IsNullOrWhiteSpace(storedToken))
            return (pageId, storedToken);

        throw new InvalidOperationException(
            "No Facebook page access token is available. Disconnect Facebook, reconnect, and select your page again.");
    }

    public static HashSet<string> ConnectedFacebookPageIds(SocialAccountEntityBase account)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        AddPageId(ids, ReadJsonString(account.MetadataJson, "selectedPageId"));

        foreach (var profile in ProcessEntityNav.Profiles(account))
        {
            if (profile.ProfileType == ProfileType.FacebookPage)
                AddPageId(ids, profile.ExternalProfileId);

            AddPageId(ids, ReadJsonString(profile.MetadataJson, "pageId"));
        }

        return ids;
    }

    public static void AlignFacebookProfile(SocialProfileEntityBase profile, string pageId, string? pageName)
    {
        if (!string.Equals(profile.ExternalProfileId, pageId, StringComparison.Ordinal))
            profile.ExternalProfileId = pageId;

        if (!string.IsNullOrWhiteSpace(pageName)
            && !string.Equals(profile.Name, pageName, StringComparison.Ordinal))
        {
            profile.Name = pageName;
        }
    }

    private static IEnumerable<string> DistinctTokens(params string?[] tokens)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            if (!string.IsNullOrWhiteSpace(token) && seen.Add(token))
                yield return token;
        }
    }

    private static void AddPageId(HashSet<string> ids, string? pageId)
    {
        if (!string.IsNullOrWhiteSpace(pageId))
            ids.Add(pageId.Trim());
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? ReadJsonString(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(propertyName, out var value) ? value.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
