using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Process;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Modules.AccountConnections.Interfaces;

namespace SocialMedia.Application.Modules.AccountConnections.Services;

public class AccountConnectionsAppConfigService : IAccountConnectionsAppConfigService
{
    private readonly IProcessAppConfigService _configService;

    public AccountConnectionsAppConfigService(IProcessAppConfigService configService)
    {
        _configService = configService;
    }

    public Task<ApiResponse<ProcessAppConfigDto>> GetConfigAsync(
        Guid userId,
        string platformCode,
        bool revealSecret = false,
        CancellationToken cancellationToken = default)
        => _configService.GetConfigAsync(userId, platformCode, MenuTypes.AccountConnection, revealSecret, cancellationToken);

    public Task<ApiResponse<ProcessAppConfigDto>> SaveConfigAsync(
        Guid userId,
        SaveProcessAppConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        request.MenuType = MenuTypes.AccountConnection;
        return _configService.SaveConfigAsync(userId, request, cancellationToken);
    }

    public Task<ApiResponse<object>> DeleteConfigAsync(
        Guid userId,
        string platformCode,
        CancellationToken cancellationToken = default)
        => _configService.DeleteConfigAsync(userId, platformCode, MenuTypes.AccountConnection, cancellationToken);
}
