using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Application.Meta;

/// <summary>
/// Loads the connected Facebook / Instagram / Instagram Login card the same way
/// the Integrations eye-icon popup does: user + platform code, then that card's
/// stored token and selected page only.
/// </summary>
public static class ProcessCardAuthResolver
{
    public static async Task<ProcessCardAuth?> ResolveAsync(
        IProcessDataStore store,
        Guid userId,
        string platformCode,
        CancellationToken cancellationToken)
    {
        var code = (platformCode ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var platform = await store.GetPlatformByCodeAsync(code, cancellationToken);
        if (platform is null)
            return null;

        var account = await store.GetSocialAccountByUserAndPlatformAsync(userId, platform.Id, cancellationToken);
        if (account is null || account.Status != SocialAccountStatus.Connected)
            return null;

        var auth = ProcessEntityNav.Auth(account)
                   ?? await store.GetSocialAuthByAccountIdAsync(account.Id, cancellationToken);
        var tokens = CollectTokens(auth, account, code);
        if (tokens.Count == 0)
            return null;

        return new ProcessCardAuth(account, auth, code, tokens, ResolvePageId(account, code));
    }

    public static List<string> CollectTokens(
        SocialAuthEntityBase? auth,
        SocialAccountEntityBase? account,
        string? platformCode)
    {
        var tokens = new List<string>();
        void Add(string? token)
        {
            if (!string.IsNullOrWhiteSpace(token) && !tokens.Contains(token))
                tokens.Add(token);
        }

        if (account is not null)
        {
            foreach (var profile in ProcessEntityNav.Profiles(account))
                Add(ReadJsonString(profile.MetadataJson, "pageAccessToken"));
        }

        if (auth is not null)
        {
            Add(auth.AccessToken);
            var instagramLogin = string.Equals(
                platformCode,
                InstagramConnectionResolver.InstagramLoginPlatformCode,
                StringComparison.OrdinalIgnoreCase);
            var facebookLoginInstagram = string.Equals(
                platformCode,
                InstagramConnectionResolver.FacebookLoginPlatformCode,
                StringComparison.OrdinalIgnoreCase);

            // Facebook Login Instagram DMs must use the Page token, not the Meta user token.
            if (facebookLoginInstagram)
                return tokens;

            if (!(instagramLogin
                  && !string.IsNullOrWhiteSpace(auth.AccessToken)
                  && !string.Equals(auth.RefreshToken, auth.AccessToken, StringComparison.Ordinal)))
            {
                Add(auth.RefreshToken);
            }
        }

        return tokens;
    }

    public static string? ResolvePageId(SocialAccountEntityBase account, string? platformCode)
    {
        var selected = ReadJsonString(account.MetadataJson, "selectedPageId");
        if (!string.IsNullOrWhiteSpace(selected))
            return selected;

        foreach (var profile in ProcessEntityNav.Profiles(account))
        {
            var pageId = ReadJsonString(profile.MetadataJson, "pageId");
            if (!string.IsNullOrWhiteSpace(pageId))
                return pageId;

            if ((string.Equals(platformCode, "facebook", StringComparison.OrdinalIgnoreCase)
                 || profile.ProfileType == ProfileType.FacebookPage)
                && !string.IsNullOrWhiteSpace(profile.ExternalProfileId))
                return profile.ExternalProfileId;
        }

        return null;
    }

    private static string? ReadJsonString(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(propertyName, out var value) ? value.GetString() : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}

public sealed record ProcessCardAuth(
    SocialAccountEntityBase Account,
    SocialAuthEntityBase? Auth,
    string PlatformCode,
    IReadOnlyList<string> Tokens,
    string? PageId);