using System.Text.Json;
using SocialMedia.Application.Interfaces;
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
        var userToken = auth.RefreshToken;

        if (!string.IsNullOrWhiteSpace(userToken))
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
                // Fall back to stored tokens below.
            }
        }

        var storedToken = FirstNonEmpty(auth.AccessToken, cachedPageToken);
        if (!string.IsNullOrWhiteSpace(storedToken))
            return (pageId, storedToken);

        throw new InvalidOperationException(
            "No Facebook page access token is available. Disconnect Facebook, reconnect, and select your page again.");
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
