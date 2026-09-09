using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Meta;

namespace SocialMedia.Application.Interfaces;

public interface IMetaAdsService
{
    Task<ApiResponse<IReadOnlyList<MetaAdAccountDto>>> GetAdAccountsAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaPagedResultDto<MetaCampaignDto>>> GetCampaignsAsync(
        Guid userId,
        string menuType,
        MetaListQuery query,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaCampaignDto>> CreateCampaignAsync(
        Guid userId,
        string menuType,
        CreateMetaCampaignRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaCampaignDto>> UpdateCampaignAsync(
        Guid userId,
        string menuType,
        string campaignId,
        UpdateMetaCampaignRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaPagedResultDto<MetaAdSetDto>>> GetAdSetsAsync(
        Guid userId,
        string menuType,
        MetaListQuery query,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaAdSetDto>> CreateAdSetAsync(
        Guid userId,
        string menuType,
        CreateMetaAdSetRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaAdSetDto>> UpdateAdSetAsync(
        Guid userId,
        string menuType,
        string adSetId,
        UpdateMetaAdSetRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaPagedResultDto<MetaAdDto>>> GetAdsAsync(
        Guid userId,
        string menuType,
        MetaListQuery query,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaAdDto>> UpdateAdAsync(
        Guid userId,
        string menuType,
        string adId,
        UpdateMetaAdRequest request,
        CancellationToken cancellationToken = default);
}
