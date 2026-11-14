using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Process;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Modules.AccountManagement.Interfaces;

namespace SocialMedia.Application.Modules.AccountManagement.Services;

public class AccountManagementAppConfigService : IAccountManagementAppConfigService
{
    private readonly IProcessAppConfigService _configService;

    public AccountManagementAppConfigService(IProcessAppConfigService configService)
    {
        _configService = configService;
    }

    public Task<ApiResponse<ProcessAppConfigDto>> GetConfigAsync(
        Guid userId,
        string platformCode,
        bool revealSecret = false,
        CancellationToken cancellationToken = default)
        => _configService.GetConfigAsync(userId, platformCode, MenuTypes.AccountManagement, revealSecret, cancellationToken);

    public Task<ApiResponse<ProcessAppConfigDto>> SaveConfigAsync(
        Guid userId,
        SaveProcessAppConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        request.MenuType = MenuTypes.AccountManagement;
        return _configService.SaveConfigAsync(userId, request, cancellationToken);
    }

    public Task<ApiResponse<object>> DeleteConfigAsync(
        Guid userId,
        string platformCode,
        CancellationToken cancellationToken = default)
        => _configService.DeleteConfigAsync(userId, platformCode, MenuTypes.AccountManagement, cancellationToken);
}
