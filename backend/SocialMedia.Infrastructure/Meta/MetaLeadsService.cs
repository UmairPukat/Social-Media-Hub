using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;
using SocialMedia.Domain.Entities;
using SocialMedia.Infrastructure.Persistence;

namespace SocialMedia.Infrastructure.Meta;

public class MetaLeadsService : IMetaLeadsService
{
    private const string FormFields = "id,name,status,created_time";
    private const string LeadFields = "id,created_time,field_data,form_id,ad_id,ad_name,campaign_id,campaign_name";

    private readonly MetaGraphClient _graph;
    private readonly IMetaMarketingContextFactory _marketing;
    private readonly IProcessDataStoreFactory _processData;
    private readonly AppDbContext _db;

    public MetaLeadsService(
        MetaGraphClient graph,
        IMetaMarketingContextFactory marketing,
        IProcessDataStoreFactory processData,
        AppDbContext db)
    {
        _graph = graph;
        _marketing = marketing;
        _processData = processData;
        _db = db;
    }

    public Task<Application.DTOs.Common.ApiResponse<IReadOnlyList<MetaPageOptionDto>>> GetPagesAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var access = await LoadPagesAsync(userId, menuType, cancellationToken);
            return (IReadOnlyList<MetaPageOptionDto>)access.Pages
                .Select(page => new MetaPageOptionDto { Id = page.Id, Name = page.Name })
                .ToList();
        }, "Facebook pages loaded.");

    public Task<Application.DTOs.Common.ApiResponse<MetaPagedResultDto<MetaLeadFormDto>>> GetLeadFormsAsync(
        Guid userId,
        string menuType,
        string pageId,
        string? after,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var page = await RequirePageAsync(userId, menuType, pageId, cancellationToken);
            var query = new List<(string, string)>
            {
                ("fields", FormFields),
                ("limit", "25")
            };
            if (!string.IsNullOrWhiteSpace(after))
                query.Add(("after", after.Trim()));

            using var doc = await GetGraphAsync(page.Version, $"{page.Id}/leadgen_forms", page.Token, cancellationToken, query.ToArray());
            var items = ReadArray(doc.RootElement, MapForm);
            return new MetaPagedResultDto<MetaLeadFormDto>
            {
                Items = items,
                NextCursor = MetaGraphResponseHelper.ReadNextCursor(doc.RootElement)
            };
        }, "Lead forms loaded.");

    public Task<Application.DTOs.Common.ApiResponse<string>> SubscribeLeadgenAsync(
        Guid userId,
        string menuType,
        string pageId,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var page = await RequirePageAsync(userId, menuType, pageId, cancellationToken);
            try
            {
                await _graph.SubscribePageAsync(
                    page.Version,
                    page.Id,
                    page.Token,
                    MetaGraphClient.PageSubscribedFields,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not MetaGraphApiException)
            {
                throw new MetaGraphApiException(Redact(ex.Message));
            }
            return MetaGraphClient.PageSubscribedFields;
        }, "Page subscribed to leadgen.");

    public Task<Application.DTOs.Common.ApiResponse<MetaPagedResultDto<MetaLeadDto>>> GetLeadsAsync(
        Guid userId,
        string menuType,
        string formId,
        string pageId,
        string? formName,
        string? after,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(formId))
                throw new MetaGraphApiException("Lead form id is required.");

            var menu = MenuTypes.Normalize(menuType);
            var page = await RequirePageAsync(userId, menu, pageId, cancellationToken);
            var query = new List<(string, string)>
            {
                ("fields", LeadFields),
                ("limit", "25")
            };
            if (!string.IsNullOrWhiteSpace(after))
                query.Add(("after", after.Trim()));

            using var doc = await GetGraphAsync(page.Version, $"{formId.Trim()}/leads", page.Token, cancellationToken, query.ToArray());
            var items = new List<MetaLeadDto>();
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in data.EnumerateArray())
                {
                    var stored = await UpsertFromGraphAsync(
                        userId,
                        menu,
                        page,
                        row,
                        formName,
                        "graph",
                        cancellationToken);
                    items.Add(MapStored(stored, row));
                }
            }

            return new MetaPagedResultDto<MetaLeadDto>
            {
                Items = items,
                NextCursor = MetaGraphResponseHelper.ReadNextCursor(doc.RootElement)
            };
        }, "Leads loaded.");

    public Task<Application.DTOs.Common.ApiResponse<MetaLeadDto>> GetLeadAsync(
        Guid userId,
        string menuType,
        string leadId,
        string? pageId,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var menu = MenuTypes.Normalize(menuType);
            var stored = await FindStoredAsync(userId, menu, leadId, cancellationToken);
            var resolvedPageId = FirstNonEmpty(pageId, stored?.PageId);
            if (string.IsNullOrWhiteSpace(resolvedPageId))
                throw new MetaGraphApiException("Page id is required to load this lead.");

            try
            {
                var page = await RequirePageAsync(userId, menu, resolvedPageId, cancellationToken);
                using var doc = await GetGraphAsync(
                    page.Version,
                    leadId.Trim(),
                    page.Token,
                    cancellationToken,
                    ("fields", LeadFields));
                var saved = await UpsertFromGraphAsync(
                    userId,
                    menu,
                    page,
                    doc.RootElement,
                    stored?.FormName,
                    stored?.Source ?? "graph",
                    cancellationToken);
                return MapStored(saved, doc.RootElement);
            }
            catch (Exception) when (stored is not null)
            {
                return MapStored(stored, null);
            }
        }, "Lead loaded.");

    public Task<Application.DTOs.Common.ApiResponse<MetaLeadDto>> UpdateLeadAsync(
        Guid userId,
        string menuType,
        string leadId,
        UpdateMetaLeadRequest request,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var menu = MenuTypes.Normalize(menuType);
            var stored = await FindStoredAsync(userId, menu, leadId, cancellationToken)
                ?? throw new MetaGraphApiException("Lead is not stored for this module. Retrieve it from the form first.");

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                if (!FacebookLeadStatuses.IsAllowed(request.Status))
                    throw new MetaGraphApiException("Status must be New, Contacted, Qualified, Converted, or Lost.");
                stored.Status = FacebookLeadStatuses.Normalize(request.Status);
            }

            if (request.Notes is not null)
                stored.Notes = request.Notes.Trim();

            stored.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return MapStored(stored, null);
        }, "Lead updated.");

    public async Task<bool> IngestWebhookLeadAsync(
        Guid userId,
        string menuType,
        string pageId,
        string leadId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(leadId) || string.IsNullOrWhiteSpace(pageId))
            return false;

        PageAccess page;
        try
        {
            page = await RequirePageAsync(userId, menuType, pageId, cancellationToken);
        }
        catch (MetaGraphApiException)
        {
            return false;
        }

        using var doc = await GetGraphAsync(
            page.Version,
            leadId.Trim(),
            page.Token,
            cancellationToken,
            ("fields", LeadFields));

        await UpsertFromGraphAsync(
            userId,
            MenuTypes.Normalize(menuType),
            page,
            doc.RootElement,
            null,
            "webhook",
            cancellationToken);
        return true;
    }

    private async Task<FacebookLead> UpsertFromGraphAsync(
        Guid userId,
        string menuType,
        PageAccess page,
        JsonElement row,
        string? formName,
        string source,
        CancellationToken cancellationToken)
    {
        var externalId = MetaGraphResponseHelper.ReadString(row, "id")
            ?? throw new MetaGraphApiException("Meta lead response did not include an id.");

        var stored = await FindStoredAsync(userId, menuType, externalId, cancellationToken);
        var created = stored is null;
        stored ??= new FacebookLead
        {
            UserId = userId,
            MenuType = menuType,
            ExternalLeadId = externalId,
            Status = "New",
            Source = source
        };

        stored.PageId = page.Id;
        stored.PageName = page.Name;
        stored.FormId = MetaGraphResponseHelper.ReadString(row, "form_id") ?? stored.FormId;
        if (!string.IsNullOrWhiteSpace(formName))
            stored.FormName = formName.Trim();
        stored.CampaignId = MetaGraphResponseHelper.ReadString(row, "campaign_id");
        stored.CampaignName = MetaGraphResponseHelper.ReadString(row, "campaign_name");
        stored.AdId = MetaGraphResponseHelper.ReadString(row, "ad_id");
        stored.AdName = MetaGraphResponseHelper.ReadString(row, "ad_name");
        stored.SubmittedAt = ReadTime(row, "created_time") ?? stored.SubmittedAt;
        ApplyFieldData(stored, row);
        if (created)
            stored.Source = source;
        stored.UpdatedAt = DateTime.UtcNow;

        if (created)
            _db.FacebookLeads.Add(stored);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (created)
        {
            _db.Entry(stored).State = EntityState.Detached;
            var existing = await FindStoredAsync(userId, menuType, externalId, cancellationToken)
                ?? throw new MetaGraphApiException("Lead was stored by another request and could not be reloaded.");
            return existing;
        }

        return stored;
    }

    private Task<FacebookLead?> FindStoredAsync(
        Guid userId,
        string menuType,
        string leadId,
        CancellationToken cancellationToken)
        => _db.FacebookLeads.FirstOrDefaultAsync(
            lead => lead.UserId == userId
                    && lead.MenuType == menuType
                    && lead.ExternalLeadId == leadId.Trim(),
            cancellationToken);

    private async Task<PageAccess> RequirePageAsync(
        Guid userId,
        string menuType,
        string pageId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pageId))
            throw new MetaGraphApiException("Facebook page id is required.");

        var access = await LoadPagesAsync(userId, menuType, cancellationToken);
        var page = access.Pages.FirstOrDefault(item => string.Equals(item.Id, pageId.Trim(), StringComparison.Ordinal));
        if (page is null || string.IsNullOrWhiteSpace(page.Token))
            throw new MetaGraphApiException("That Facebook page is not authorized for this module. Choose a page from Connect.");

        return page with { Version = access.Version };
    }

    private async Task<(string Version, IReadOnlyList<PageAccess> Pages)> LoadPagesAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken)
    {
        if (!MenuTypes.IsKnown(menuType))
            throw new MetaGraphApiException("Unknown process menu.");

        var menu = MenuTypes.Normalize(menuType);
        var context = await _marketing.CreateAsync(userId, menu, cancellationToken);
        var store = _processData.ForMenu(menu);
        var platform = await store.GetPlatformByCodeAsync("facebook", cancellationToken)
            ?? throw new MetaGraphApiException("Facebook is not configured for this module.");
        var account = await store.GetSocialAccountByUserAndPlatformAsync(userId, platform.Id, cancellationToken)
            ?? throw new MetaGraphApiException("Connect Facebook in this module before opening Facebook Leads.");
        var auth = await store.GetSocialAuthByAccountIdAsync(account.Id, cancellationToken);
        var token = MetaMarketingContextFactory.ResolveMarketingAccessToken(auth);
        if (string.IsNullOrWhiteSpace(token))
            throw new MetaGraphApiException("No Meta access token is stored. Reconnect Facebook.");

        IReadOnlyList<MetaPageInfo> pages;
        try
        {
            pages = await _graph.ListPagesAsync(context.GraphApiVersion, token, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new MetaGraphApiException(Redact(ex.Message));
        }

        var mapped = pages
            .Where(page => !string.IsNullOrWhiteSpace(page.PageId))
            .Select(page => new PageAccess(
                page.PageId,
                string.IsNullOrWhiteSpace(page.PageName) ? page.PageId : page.PageName,
                page.PageAccessToken ?? string.Empty,
                context.GraphApiVersion))
            .ToList();

        return (context.GraphApiVersion, mapped);
    }

    private static MetaLeadFormDto MapForm(JsonElement row) => new()
    {
        Id = MetaGraphResponseHelper.ReadString(row, "id") ?? string.Empty,
        Name = MetaGraphResponseHelper.ReadString(row, "name") ?? string.Empty,
        Status = MetaGraphResponseHelper.ReadString(row, "status"),
        CreatedTime = MetaGraphResponseHelper.ReadString(row, "created_time")
    };

    private static MetaLeadDto MapStored(FacebookLead stored, JsonElement? row)
    {
        var fields = ReadFields(stored.FieldDataJson);
        return new MetaLeadDto
        {
            Id = stored.ExternalLeadId,
            CreatedTime = stored.SubmittedAt?.ToString("o")
                          ?? (row.HasValue ? MetaGraphResponseHelper.ReadString(row.Value, "created_time") : null),
            FormId = stored.FormId,
            FormName = stored.FormName,
            PageId = stored.PageId,
            PageName = stored.PageName,
            AdId = stored.AdId,
            AdName = stored.AdName,
            CampaignId = stored.CampaignId,
            CampaignName = stored.CampaignName,
            CustomerName = stored.CustomerName,
            Email = stored.Email,
            Phone = stored.Phone,
            Status = stored.Status,
            Notes = stored.Notes,
            Source = stored.Source,
            Fields = fields
        };
    }

    private static void ApplyFieldData(FacebookLead stored, JsonElement row)
    {
        if (!row.TryGetProperty("field_data", out var fieldData) || fieldData.ValueKind != JsonValueKind.Array)
        {
            stored.FieldDataJson = "[]";
            return;
        }

        stored.FieldDataJson = fieldData.GetRawText();
        foreach (var field in fieldData.EnumerateArray())
        {
            var name = MetaGraphResponseHelper.ReadString(field, "name") ?? string.Empty;
            var value = ReadFieldValue(field);
            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (name.Equals("full_name", StringComparison.OrdinalIgnoreCase)
                || name.Equals("name", StringComparison.OrdinalIgnoreCase))
                stored.CustomerName = value;
            else if (name.Equals("email", StringComparison.OrdinalIgnoreCase))
                stored.Email = value;
            else if (name.Equals("phone_number", StringComparison.OrdinalIgnoreCase)
                     || name.Equals("phone", StringComparison.OrdinalIgnoreCase))
                stored.Phone = value;
        }
    }

    private static IReadOnlyList<MetaLeadFieldDto> ReadFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<MetaLeadFieldDto>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return Array.Empty<MetaLeadFieldDto>();

            return doc.RootElement.EnumerateArray()
                .Select(field => new MetaLeadFieldDto
                {
                    Name = MetaGraphResponseHelper.ReadString(field, "name") ?? "question",
                    Value = ReadFieldValue(field)
                })
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<MetaLeadFieldDto>();
        }
    }

    private static string? ReadFieldValue(JsonElement field)
    {
        if (!field.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
            return null;

        var parts = values.EnumerateArray()
            .Select(value => value.ToString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        return parts.Length == 0 ? null : string.Join(", ", parts);
    }

    private static DateTime? ReadTime(JsonElement row, string name)
    {
        var raw = MetaGraphResponseHelper.ReadString(row, name);
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return DateTime.TryParse(raw, out var parsed) ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc) : null;
    }

    private static List<T> ReadArray<T>(JsonElement root, Func<JsonElement, T> map)
    {
        var items = new List<T>();
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return items;
        foreach (var row in data.EnumerateArray())
            items.Add(map(row));
        return items;
    }

    private async Task<JsonDocument> GetGraphAsync(
        string version,
        string path,
        string accessToken,
        CancellationToken cancellationToken,
        params (string Key, string Value)[] query)
    {
        try
        {
            return await _graph.GetAsync(version, path, accessToken, cancellationToken, query);
        }
        catch (Exception ex) when (ex is not MetaGraphApiException)
        {
            throw new MetaGraphApiException(Redact(ex.Message));
        }
    }

    private static string Redact(string message)
        => Regex.Replace(message ?? string.Empty, "access_token=[^&\\s\"]+", "access_token=redacted", RegexOptions.IgnoreCase);

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private sealed record PageAccess(string Id, string Name, string Token, string Version);
}
