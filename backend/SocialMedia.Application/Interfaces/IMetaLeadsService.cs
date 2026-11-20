using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Meta;

namespace SocialMedia.Application.Interfaces;

public interface IMetaLeadsService
{
    Task<ApiResponse<IReadOnlyList<MetaPageOptionDto>>> GetPagesAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaPagedResultDto<MetaLeadFormDto>>> GetLeadFormsAsync(
        Guid userId,
        string menuType,
        string pageId,
        string? after,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaLeadgenSubscriptionDto>> GetLeadgenSubscriptionAsync(
        Guid userId,
        string menuType,
        string pageId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<string>> SubscribeLeadgenAsync(
        Guid userId,
        string menuType,
        string pageId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaLeadgenSubscriptionDto>> UnsubscribeLeadgenAsync(
        Guid userId,
        string menuType,
        string pageId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaPagedResultDto<MetaLeadDto>>> GetLeadsAsync(
        Guid userId,
        string menuType,
        string formId,
        string pageId,
        string? formName,
        string? after,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaLeadDto>> GetLeadAsync(
        Guid userId,
        string menuType,
        string leadId,
        string? pageId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaLeadDto>> UpdateLeadAsync(
        Guid userId,
        string menuType,
        string leadId,
        UpdateMetaLeadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a leadgen webhook. Returns false when the page is not authorized for this user and module.
    /// Throws when Graph or the database fails so the stored event can be retried.
    /// </summary>
    Task<bool> IngestWebhookLeadAsync(
        Guid userId,
        string menuType,
        string pageId,
        string leadId,
        CancellationToken cancellationToken = default);
}
