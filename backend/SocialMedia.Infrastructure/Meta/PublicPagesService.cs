using System.Text.Json;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.PublicPages;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Infrastructure.Meta;

/// <summary>
/// Public Pages lookup uses Graph <c>GET me/accounts</c> with the connected Facebook user token
/// for the current process module (Integrations, App Connections, Developer Apps).
/// </summary>
public class PublicPagesService : IPublicPagesService
{
    private const string AccountFields = "id,name,about,category,fan_count,followers_count,link";
    private const string DefaultGraphVersion = "v21.0";

    private readonly IProcessDataStoreFactory _processData;
    private readonly MetaGraphClient _graph;

    public PublicPagesService(IProcessDataStoreFactory processData, MetaGraphClient graph)
    {
        _processData = processData;
        _graph = graph;
    }

    public async Task<ApiResponse<IReadOnlyList<PublicPageDto>>> SearchAsync(
        Guid userId,
        string? menuType,
        string? query,
        CancellationToken cancellationToken = default)
    {
        var keyword = (query ?? string.Empty).Trim();
        if (keyword.Length == 0)
            return ApiResponse<IReadOnlyList<PublicPageDto>>.Fail("Enter a page name to search.");
        if (keyword.Length > 100)
            return ApiResponse<IReadOnlyList<PublicPageDto>>.Fail("Search keyword is too long.");

        var normalizedMenu = MenuTypes.Normalize(menuType);
        var store = _processData.ForMenu(normalizedMenu);
        var token = await ResolveFacebookUserTokenAsync(store, userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return ApiResponse<IReadOnlyList<PublicPageDto>>.Fail(
                "Connect Facebook in this module first. Public Pages uses GET /me/accounts with that user token.");
        }

        var result = await _graph.TryGetFacebookAsync(
            DefaultGraphVersion,
            "me/accounts",
            token,
            cancellationToken,
            ("fields", AccountFields),
            ("limit", "100"));

        if (result.Status is < 200 or >= 300)
            return ApiResponse<IReadOnlyList<PublicPageDto>>.Fail(ReadGraphError(result.Body));

        try
        {
            using var doc = JsonDocument.Parse(result.Body);
            var pages = new List<PublicPageDto>();
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    var mapped = MapPage(item);
                    if (mapped is not null && MatchesKeyword(mapped, keyword))
                        pages.Add(mapped);
                }
            }

            return ApiResponse<IReadOnlyList<PublicPageDto>>.Ok(
                pages,
                pages.Count == 0
                    ? "No connected Facebook Pages matched that name."
                    : "Page details loaded.");
        }
        catch (JsonException)
        {
            return ApiResponse<IReadOnlyList<PublicPageDto>>.Fail("Facebook Graph returned an unexpected me/accounts payload.");
        }
    }

    private static async Task<string?> ResolveFacebookUserTokenAsync(
        IProcessDataStore store,
        Guid userId,
        CancellationToken cancellationToken)
    {
        foreach (var code in new[] { "facebook", "instagram" })
        {
            var platform = await store.GetPlatformByCodeAsync(code, cancellationToken);
            if (platform is null)
                continue;

            var account = await store.GetSocialAccountByUserAndPlatformAsync(userId, platform.Id, cancellationToken);
            if (account is null || account.Status != SocialAccountStatus.Connected)
                continue;

            var loaded = await store.GetSocialAccountWithAuthAndProfilesAsync(account.Id, cancellationToken) ?? account;
            var auth = ProcessEntityNav.Auth(loaded)
                       ?? await store.GetSocialAuthByAccountIdAsync(loaded.Id, cancellationToken);
            if (auth is null)
                continue;

            var token = !string.IsNullOrWhiteSpace(auth.RefreshToken) ? auth.RefreshToken : auth.AccessToken;
            if (!string.IsNullOrWhiteSpace(token))
                return token;
        }

        return null;
    }

    private static PublicPageDto? MapPage(JsonElement item)
    {
        var name = ReadString(item, "name");
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var id = ReadString(item, "id") ?? string.Empty;
        return new PublicPageDto
        {
            Id = id,
            Name = name,
            Category = ReadString(item, "category"),
            About = ReadString(item, "about"),
            FanCount = ReadCount(item, "fan_count"),
            FollowersCount = ReadCount(item, "followers_count"),
            Link = ReadString(item, "link") ?? (string.IsNullOrWhiteSpace(id) ? null : $"https://www.facebook.com/{id}")
        };
    }

    private static bool MatchesKeyword(PublicPageDto page, string keyword)
    {
        var needle = Normalize(keyword);
        if (needle.Length == 0)
            return false;

        return ContainsNormalized(page.Name, needle)
               || ContainsNormalized(page.Category, needle)
               || ContainsNormalized(page.About, needle)
               || ContainsNormalized(page.Id, needle);
    }

    private static bool ContainsNormalized(string? value, string needle)
    {
        var haystack = Normalize(value);
        return haystack.Length > 0 && haystack.Contains(needle, StringComparison.Ordinal);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value.Trim().ToLowerInvariant().Select(ch =>
            char.IsLetterOrDigit(ch) ? ch : ' ').ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string ReadGraphError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var message)
                && !string.IsNullOrWhiteSpace(message.GetString()))
                return message.GetString()!;
        }
        catch (JsonException)
        {
        }

        return string.IsNullOrWhiteSpace(body)
            ? "Facebook me/accounts lookup failed."
            : body;
    }

    private static string? ReadString(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? value.ToString()
            : null;

    private static int? ReadCount(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
            return null;
        if (value.TryGetInt32(out var number))
            return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
            return number;
        return null;
    }
}
