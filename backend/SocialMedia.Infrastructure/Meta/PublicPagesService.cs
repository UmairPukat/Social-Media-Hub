using System.Text.Json;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.PublicPages;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Interfaces;

namespace SocialMedia.Infrastructure.Meta;

/// <summary>
/// Competitor research: Graph <c>/pages/search</c> with the Facebook app token stored
/// for this process module. Never uses connected Page tokens (<c>pages_show_list</c>).
/// </summary>
public class PublicPagesService : IPublicPagesService
{
    private const string SearchFields = "id,name,about,category,fan_count,followers_count,link";
    private const string DefaultGraphVersion = "v20.0";

    private readonly IUnitOfWork _unitOfWork;
    private readonly MetaGraphClient _graph;

    public PublicPagesService(IUnitOfWork unitOfWork, MetaGraphClient graph)
    {
        _unitOfWork = unitOfWork;
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
            return ApiResponse<IReadOnlyList<PublicPageDto>>.Fail("Enter a keyword to search public Pages.");
        if (keyword.Length > 100)
            return ApiResponse<IReadOnlyList<PublicPageDto>>.Fail("Search keyword is too long.");

        var normalizedMenu = MenuTypes.Normalize(menuType);
        var credentials = await LoadFacebookAppTokenAsync(userId, normalizedMenu, cancellationToken);
        if (credentials is null)
        {
            return ApiResponse<IReadOnlyList<PublicPageDto>>.Fail(
                "Save this module's Facebook app Client Id and Client Secret first. Public Page search uses that app token, not a connected Page token.");
        }

        var result = await _graph.TryGetFacebookAsync(
            credentials.Version,
            "pages/search",
            credentials.AppToken,
            cancellationToken,
            ("q", keyword),
            ("fields", SearchFields),
            ("limit", "25"));

        if (result.Status is < 200 or >= 300)
            return ApiResponse<IReadOnlyList<PublicPageDto>>.Fail(ReadGraphError(result.Body));

        try
        {
            using var doc = JsonDocument.Parse(result.Body);
            var pages = new List<PublicPageDto>();
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return ApiResponse<IReadOnlyList<PublicPageDto>>.Ok(pages, "No public Pages matched.");

            foreach (var item in data.EnumerateArray())
            {
                var name = ReadString(item, "name");
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                pages.Add(new PublicPageDto
                {
                    Name = name,
                    Category = ReadString(item, "category"),
                    About = ReadString(item, "about"),
                    FanCount = ReadCount(item, "fan_count"),
                    FollowersCount = ReadCount(item, "followers_count"),
                    Link = ReadString(item, "link")
                });
            }

            return ApiResponse<IReadOnlyList<PublicPageDto>>.Ok(
                pages,
                pages.Count == 0 ? "No public Pages matched." : "Public Page metadata loaded.");
        }
        catch (JsonException)
        {
            return ApiResponse<IReadOnlyList<PublicPageDto>>.Fail("Facebook Graph returned an unexpected Pages Search payload.");
        }
    }

    private async Task<FacebookAppToken?> LoadFacebookAppTokenAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken)
    {
        string? clientId;
        string? clientSecret;

        switch (menuType)
        {
            case MenuTypes.AppConnection:
            {
                var row = await _unitOfWork.AppConnectionConfigs.GetByUserAndPlatformCodeAsync(
                    userId, "facebook", menuType, cancellationToken);
                clientId = row?.ClientId;
                clientSecret = row?.ClientSecret;
                break;
            }
            case MenuTypes.DeveloperApp:
            {
                var row = await _unitOfWork.DeveloperAppConfigs.GetByUserAndPlatformCodeAsync(
                    userId, "facebook", menuType, cancellationToken);
                clientId = row?.ClientId;
                clientSecret = row?.ClientSecret;
                break;
            }
            default:
            {
                var row = await _unitOfWork.IntegrationAppConfigs.GetByUserAndPlatformCodeAsync(
                    userId, "facebook", menuType, cancellationToken);
                clientId = row?.ClientId;
                clientSecret = row?.ClientSecret;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return null;

        return new FacebookAppToken($"{clientId.Trim()}|{clientSecret.Trim()}", DefaultGraphVersion);
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
            // Fall through to the raw body.
        }

        return string.IsNullOrWhiteSpace(body)
            ? "Facebook Pages Search failed."
            : body;
    }

    private static string? ReadString(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
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

    private sealed record FacebookAppToken(string AppToken, string Version);
}
