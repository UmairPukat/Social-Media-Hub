using Microsoft.EntityFrameworkCore;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;
using SocialMedia.Domain.Common;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.Common.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.Integrations.Entities;
using SocialMedia.Infrastructure.Persistence;
using SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;
using SocialMedia.Domain.Modules.AccountConnections.Entities;
using SocialMedia.Domain.Modules.AccountManagement.Entities;
using SocialMedia.Domain.Modules.AccountHub.Entities;

namespace SocialMedia.Infrastructure.Repositories;

public sealed class ProcessDataStore : IProcessDataStore
{
    private readonly AppDbContext _context;
    private readonly string _menuType;

    public ProcessDataStore(AppDbContext context, string menuType)
    {
        _context = context;
        _menuType = MenuTypes.Normalize(menuType);
    }

    public string MenuType => _menuType;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<PlatformEntityBase>> GetActivePlatformsAsync(CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => await _context.AppConnectionPlatforms.AsNoTracking()
                .Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken),
            MenuTypes.DeveloperApp => await _context.DeveloperAppPlatforms.AsNoTracking()
                .Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken),
            MenuTypes.AccountConnection => await _context.AccountConnectionPlatforms.AsNoTracking()
                .Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken),
            MenuTypes.AccountManagement => await _context.AccountManagementPlatforms.AsNoTracking()
                .Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken),
            MenuTypes.AccountHub => await _context.AccountHubPlatforms.AsNoTracking()
                .Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken),
            MenuTypes.SocialMediaAccount => await _context.SocialMediaAccountPlatforms.AsNoTracking()
                .Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken),
            _ => await _context.IntegrationPlatforms.AsNoTracking()
                .Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken)
        };

    public Task<PlatformEntityBase?> GetPlatformByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionPlatform, PlatformEntityBase>(
                _context.AppConnectionPlatforms.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppPlatform, PlatformEntityBase>(
                _context.DeveloperAppPlatforms.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionPlatform, PlatformEntityBase>(
                _context.AccountConnectionPlatforms.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementPlatform, PlatformEntityBase>(
                _context.AccountManagementPlatforms.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubPlatform, PlatformEntityBase>(
                _context.AccountHubPlatforms.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountPlatform, PlatformEntityBase>(
                _context.SocialMediaAccountPlatforms.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            _ => AsBase<IntegrationPlatform, PlatformEntityBase>(
                _context.IntegrationPlatforms.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken))
        };

    public Task<PlatformEntityBase?> GetPlatformByCodeAsync(string code, CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionPlatform, PlatformEntityBase>(
                _context.AppConnectionPlatforms.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code, cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppPlatform, PlatformEntityBase>(
                _context.DeveloperAppPlatforms.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code, cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionPlatform, PlatformEntityBase>(
                _context.AccountConnectionPlatforms.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code, cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementPlatform, PlatformEntityBase>(
                _context.AccountManagementPlatforms.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code, cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubPlatform, PlatformEntityBase>(
                _context.AccountHubPlatforms.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code, cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountPlatform, PlatformEntityBase>(
                _context.SocialMediaAccountPlatforms.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code, cancellationToken)),
            _ => AsBase<IntegrationPlatform, PlatformEntityBase>(
                _context.IntegrationPlatforms.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code, cancellationToken))
        };

    public async Task AddPlatformAsync(PlatformEntityBase platform, CancellationToken cancellationToken = default)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                await _context.AppConnectionPlatforms.AddAsync((AppConnectionPlatform)platform, cancellationToken);
                break;
            case MenuTypes.DeveloperApp:
                await _context.DeveloperAppPlatforms.AddAsync((DeveloperAppPlatform)platform, cancellationToken);
                break;
            case MenuTypes.AccountConnection:
                await _context.AccountConnectionPlatforms.AddAsync((AccountConnectionPlatform)platform, cancellationToken);
                break;
            case MenuTypes.AccountManagement:
                await _context.AccountManagementPlatforms.AddAsync((AccountManagementPlatform)platform, cancellationToken);
                break;
            case MenuTypes.AccountHub:
                await _context.AccountHubPlatforms.AddAsync((AccountHubPlatform)platform, cancellationToken);
                break;
            case MenuTypes.SocialMediaAccount:
                await _context.SocialMediaAccountPlatforms.AddAsync((SocialMediaAccountPlatform)platform, cancellationToken);
                break;
            default:
                await _context.IntegrationPlatforms.AddAsync((IntegrationPlatform)platform, cancellationToken);
                break;
        }
    }

    public async Task<IReadOnlyList<SocialAccountEntityBase>> GetSocialAccountsByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => await _context.AppConnectionSocialAccounts.AsNoTracking()
                .Include(a => a.Platform).Include(a => a.Auth).Include(a => a.Profiles)
                .Where(a => a.UserId == userId).ToListAsync(cancellationToken),
            MenuTypes.DeveloperApp => await _context.DeveloperAppSocialAccounts.AsNoTracking()
                .Include(a => a.Platform).Include(a => a.Auth).Include(a => a.Profiles)
                .Where(a => a.UserId == userId).ToListAsync(cancellationToken),
            MenuTypes.AccountConnection => await _context.AccountConnectionSocialAccounts.AsNoTracking()
                .Include(a => a.Platform).Include(a => a.Auth).Include(a => a.Profiles)
                .Where(a => a.UserId == userId).ToListAsync(cancellationToken),
            MenuTypes.AccountManagement => await _context.AccountManagementSocialAccounts.AsNoTracking()
                .Include(a => a.Platform).Include(a => a.Auth).Include(a => a.Profiles)
                .Where(a => a.UserId == userId).ToListAsync(cancellationToken),
            MenuTypes.AccountHub => await _context.AccountHubSocialAccounts.AsNoTracking()
                .Include(a => a.Platform).Include(a => a.Auth).Include(a => a.Profiles)
                .Where(a => a.UserId == userId).ToListAsync(cancellationToken),
            MenuTypes.SocialMediaAccount => await _context.SocialMediaAccountSocialAccounts.AsNoTracking()
                .Include(a => a.Platform).Include(a => a.Auth).Include(a => a.Profiles)
                .Where(a => a.UserId == userId).ToListAsync(cancellationToken),
            _ => await _context.IntegrationSocialAccounts.AsNoTracking()
                .Include(a => a.Platform).Include(a => a.Auth).Include(a => a.Profiles)
                .Where(a => a.UserId == userId).ToListAsync(cancellationToken)
        };

    public Task<SocialAccountEntityBase?> GetSocialAccountByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionSocialAccount, SocialAccountEntityBase>(
                _context.AppConnectionSocialAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppSocialAccount, SocialAccountEntityBase>(
                _context.DeveloperAppSocialAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionSocialAccount, SocialAccountEntityBase>(
                _context.AccountConnectionSocialAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementSocialAccount, SocialAccountEntityBase>(
                _context.AccountManagementSocialAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubSocialAccount, SocialAccountEntityBase>(
                _context.AccountHubSocialAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountSocialAccount, SocialAccountEntityBase>(
                _context.SocialMediaAccountSocialAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken)),
            _ => AsBase<IntegrationSocialAccount, SocialAccountEntityBase>(
                _context.IntegrationSocialAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken))
        };

    public Task<SocialAccountEntityBase?> GetSocialAccountByUserAndPlatformAsync(
        Guid userId,
        Guid platformId,
        CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionSocialAccount, SocialAccountEntityBase>(
                _context.AppConnectionSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .Where(a => a.UserId == userId && a.PlatformId == platformId)
                    .OrderByDescending(a => a.Status == SocialAccountStatus.Connected ? 1 : 0)
                    .ThenByDescending(a => a.Auth != null && a.Auth.AccessToken != "" ? 1 : 0)
                    .ThenByDescending(a => a.Auth != null && a.Auth.RefreshToken != null && a.Auth.RefreshToken != "" ? 1 : 0)
                    .ThenByDescending(a => a.ConnectedAt ?? a.UpdatedAt ?? a.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppSocialAccount, SocialAccountEntityBase>(
                _context.DeveloperAppSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .Where(a => a.UserId == userId && a.PlatformId == platformId)
                    .OrderByDescending(a => a.Status == SocialAccountStatus.Connected ? 1 : 0)
                    .ThenByDescending(a => a.Auth != null && a.Auth.AccessToken != "" ? 1 : 0)
                    .ThenByDescending(a => a.Auth != null && a.Auth.RefreshToken != null && a.Auth.RefreshToken != "" ? 1 : 0)
                    .ThenByDescending(a => a.ConnectedAt ?? a.UpdatedAt ?? a.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionSocialAccount, SocialAccountEntityBase>(
                _context.AccountConnectionSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .Where(a => a.UserId == userId && a.PlatformId == platformId)
                    .OrderByDescending(a => a.Status == SocialAccountStatus.Connected ? 1 : 0)
                    .ThenByDescending(a => a.Auth != null && a.Auth.AccessToken != "" ? 1 : 0)
                    .ThenByDescending(a => a.Auth != null && a.Auth.RefreshToken != null && a.Auth.RefreshToken != "" ? 1 : 0)
                    .ThenByDescending(a => a.ConnectedAt ?? a.UpdatedAt ?? a.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementSocialAccount, SocialAccountEntityBase>(
                _context.AccountManagementSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .Where(a => a.UserId == userId && a.PlatformId == platformId)
                    .OrderByDescending(a => a.Status == SocialAccountStatus.Connected ? 1 : 0)
                    .ThenByDescending(a => a.Auth != null && a.Auth.AccessToken != "" ? 1 : 0)
                    .ThenByDescending(a => a.Auth != null && a.Auth.RefreshToken != null && a.Auth.RefreshToken != "" ? 1 : 0)
                    .ThenByDescending(a => a.ConnectedAt ?? a.UpdatedAt ?? a.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubSocialAccount, SocialAccountEntityBase>(
                _context.AccountHubSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .Where(a => a.UserId == userId && a.PlatformId == platformId)
                    .OrderByDescending(a => a.Status == SocialAccountStatus.Connected ? 1 : 0)
                    .ThenByDescending(a => a.Auth != null && a.Auth.AccessToken != "" ? 1 : 0)
                    .ThenByDescending(a => a.Auth != null && a.Auth.RefreshToken != null && a.Auth.RefreshToken != "" ? 1 : 0)
                    .ThenByDescending(a => a.ConnectedAt ?? a.UpdatedAt ?? a.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountSocialAccount, SocialAccountEntityBase>(
                _context.SocialMediaAccountSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .Where(a => a.UserId == userId && a.PlatformId == platformId)
                    .OrderByDescending(a => a.Status == SocialAccountStatus.Connected ? 1 : 0)
                    .ThenByDescending(a => a.Auth != null && a.Auth.AccessToken != "" ? 1 : 0)
                    .ThenByDescending(a => a.Auth != null && a.Auth.RefreshToken != null && a.Auth.RefreshToken != "" ? 1 : 0)
                    .ThenByDescending(a => a.ConnectedAt ?? a.UpdatedAt ?? a.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken)),
            _ => AsBase<IntegrationSocialAccount, SocialAccountEntityBase>(
                _context.IntegrationSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .Where(a => a.UserId == userId && a.PlatformId == platformId)
                    .OrderByDescending(a => a.Status == SocialAccountStatus.Connected ? 1 : 0)
                    .ThenByDescending(a => a.Auth != null && a.Auth.AccessToken != "" ? 1 : 0)
                    .ThenByDescending(a => a.Auth != null && a.Auth.RefreshToken != null && a.Auth.RefreshToken != "" ? 1 : 0)
                    .ThenByDescending(a => a.ConnectedAt ?? a.UpdatedAt ?? a.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken))
        };

    public Task<SocialAccountEntityBase?> GetSocialAccountByUserPlatformAndExternalIdAsync(
        Guid userId,
        Guid platformId,
        string externalAccountId,
        CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionSocialAccount, SocialAccountEntityBase>(
                _context.AppConnectionSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(
                        a => a.UserId == userId
                             && a.PlatformId == platformId
                             && a.ExternalAccountId == externalAccountId,
                        cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppSocialAccount, SocialAccountEntityBase>(
                _context.DeveloperAppSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(
                        a => a.UserId == userId
                             && a.PlatformId == platformId
                             && a.ExternalAccountId == externalAccountId,
                        cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionSocialAccount, SocialAccountEntityBase>(
                _context.AccountConnectionSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(
                        a => a.UserId == userId
                             && a.PlatformId == platformId
                             && a.ExternalAccountId == externalAccountId,
                        cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementSocialAccount, SocialAccountEntityBase>(
                _context.AccountManagementSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(
                        a => a.UserId == userId
                             && a.PlatformId == platformId
                             && a.ExternalAccountId == externalAccountId,
                        cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubSocialAccount, SocialAccountEntityBase>(
                _context.AccountHubSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(
                        a => a.UserId == userId
                             && a.PlatformId == platformId
                             && a.ExternalAccountId == externalAccountId,
                        cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountSocialAccount, SocialAccountEntityBase>(
                _context.SocialMediaAccountSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(
                        a => a.UserId == userId
                             && a.PlatformId == platformId
                             && a.ExternalAccountId == externalAccountId,
                        cancellationToken)),
            _ => AsBase<IntegrationSocialAccount, SocialAccountEntityBase>(
                _context.IntegrationSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(
                        a => a.UserId == userId
                             && a.PlatformId == platformId
                             && a.ExternalAccountId == externalAccountId,
                        cancellationToken))
        };

    public Task<SocialAccountEntityBase?> GetSocialAccountByExternalIdAsync(
        string externalAccountId,
        CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionSocialAccount, SocialAccountEntityBase>(
                _context.AppConnectionSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(a => a.ExternalAccountId == externalAccountId, cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppSocialAccount, SocialAccountEntityBase>(
                _context.DeveloperAppSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(a => a.ExternalAccountId == externalAccountId, cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionSocialAccount, SocialAccountEntityBase>(
                _context.AccountConnectionSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(a => a.ExternalAccountId == externalAccountId, cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementSocialAccount, SocialAccountEntityBase>(
                _context.AccountManagementSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(a => a.ExternalAccountId == externalAccountId, cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubSocialAccount, SocialAccountEntityBase>(
                _context.AccountHubSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(a => a.ExternalAccountId == externalAccountId, cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountSocialAccount, SocialAccountEntityBase>(
                _context.SocialMediaAccountSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(a => a.ExternalAccountId == externalAccountId, cancellationToken)),
            _ => AsBase<IntegrationSocialAccount, SocialAccountEntityBase>(
                _context.IntegrationSocialAccounts
                    .Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(a => a.ExternalAccountId == externalAccountId, cancellationToken))
        };

    public Task<SocialAccountEntityBase?> GetSocialAccountWithAuthAndProfilesAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionSocialAccount, SocialAccountEntityBase>(
                _context.AppConnectionSocialAccounts
                    .Include(a => a.Platform).Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppSocialAccount, SocialAccountEntityBase>(
                _context.DeveloperAppSocialAccounts
                    .Include(a => a.Platform).Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionSocialAccount, SocialAccountEntityBase>(
                _context.AccountConnectionSocialAccounts
                    .Include(a => a.Platform).Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementSocialAccount, SocialAccountEntityBase>(
                _context.AccountManagementSocialAccounts
                    .Include(a => a.Platform).Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubSocialAccount, SocialAccountEntityBase>(
                _context.AccountHubSocialAccounts
                    .Include(a => a.Platform).Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountSocialAccount, SocialAccountEntityBase>(
                _context.SocialMediaAccountSocialAccounts
                    .Include(a => a.Platform).Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)),
            _ => AsBase<IntegrationSocialAccount, SocialAccountEntityBase>(
                _context.IntegrationSocialAccounts
                    .Include(a => a.Platform).Include(a => a.Auth).Include(a => a.Profiles)
                    .FirstOrDefaultAsync(a => a.Id == id, cancellationToken))
        };

    public async Task AddSocialAccountAsync(SocialAccountEntityBase account, CancellationToken cancellationToken = default)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                await _context.AppConnectionSocialAccounts.AddAsync((AppConnectionSocialAccount)account, cancellationToken);
                break;
            case MenuTypes.DeveloperApp:
                await _context.DeveloperAppSocialAccounts.AddAsync((DeveloperAppSocialAccount)account, cancellationToken);
                break;
            case MenuTypes.AccountConnection:
                await _context.AccountConnectionSocialAccounts.AddAsync((AccountConnectionSocialAccount)account, cancellationToken);
                break;
            case MenuTypes.AccountManagement:
                await _context.AccountManagementSocialAccounts.AddAsync((AccountManagementSocialAccount)account, cancellationToken);
                break;
            case MenuTypes.AccountHub:
                await _context.AccountHubSocialAccounts.AddAsync((AccountHubSocialAccount)account, cancellationToken);
                break;
            case MenuTypes.SocialMediaAccount:
                await _context.SocialMediaAccountSocialAccounts.AddAsync((SocialMediaAccountSocialAccount)account, cancellationToken);
                break;
            default:
                await _context.IntegrationSocialAccounts.AddAsync((IntegrationSocialAccount)account, cancellationToken);
                break;
        }
    }

    public void UpdateSocialAccount(SocialAccountEntityBase account)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                UpdateTracked(_context.AppConnectionSocialAccounts, (AppConnectionSocialAccount)account);
                break;
            case MenuTypes.DeveloperApp:
                UpdateTracked(_context.DeveloperAppSocialAccounts, (DeveloperAppSocialAccount)account);
                break;
            case MenuTypes.AccountConnection:
                UpdateTracked(_context.AccountConnectionSocialAccounts, (AccountConnectionSocialAccount)account);
                break;
            case MenuTypes.AccountManagement:
                UpdateTracked(_context.AccountManagementSocialAccounts, (AccountManagementSocialAccount)account);
                break;
            case MenuTypes.AccountHub:
                UpdateTracked(_context.AccountHubSocialAccounts, (AccountHubSocialAccount)account);
                break;
            case MenuTypes.SocialMediaAccount:
                UpdateTracked(_context.SocialMediaAccountSocialAccounts, (SocialMediaAccountSocialAccount)account);
                break;
            default:
                UpdateTracked(_context.IntegrationSocialAccounts, (IntegrationSocialAccount)account);
                break;
        }
    }

    public void RemoveSocialAccount(SocialAccountEntityBase account)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                RemoveTracked(_context.AppConnectionSocialAccounts, (AppConnectionSocialAccount)account);
                break;
            case MenuTypes.DeveloperApp:
                RemoveTracked(_context.DeveloperAppSocialAccounts, (DeveloperAppSocialAccount)account);
                break;
            case MenuTypes.AccountConnection:
                RemoveTracked(_context.AccountConnectionSocialAccounts, (AccountConnectionSocialAccount)account);
                break;
            case MenuTypes.AccountManagement:
                RemoveTracked(_context.AccountManagementSocialAccounts, (AccountManagementSocialAccount)account);
                break;
            case MenuTypes.AccountHub:
                RemoveTracked(_context.AccountHubSocialAccounts, (AccountHubSocialAccount)account);
                break;
            case MenuTypes.SocialMediaAccount:
                RemoveTracked(_context.SocialMediaAccountSocialAccounts, (SocialMediaAccountSocialAccount)account);
                break;
            default:
                RemoveTracked(_context.IntegrationSocialAccounts, (IntegrationSocialAccount)account);
                break;
        }
    }

    public Task<SocialAuthEntityBase?> GetSocialAuthByAccountIdAsync(
        Guid socialAccountId,
        CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionSocialAuth, SocialAuthEntityBase>(
                _context.AppConnectionSocialAuths.FirstOrDefaultAsync(a => a.SocialAccountId == socialAccountId, cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppSocialAuth, SocialAuthEntityBase>(
                _context.DeveloperAppSocialAuths.FirstOrDefaultAsync(a => a.SocialAccountId == socialAccountId, cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionSocialAuth, SocialAuthEntityBase>(
                _context.AccountConnectionSocialAuths.FirstOrDefaultAsync(a => a.SocialAccountId == socialAccountId, cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementSocialAuth, SocialAuthEntityBase>(
                _context.AccountManagementSocialAuths.FirstOrDefaultAsync(a => a.SocialAccountId == socialAccountId, cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubSocialAuth, SocialAuthEntityBase>(
                _context.AccountHubSocialAuths.FirstOrDefaultAsync(a => a.SocialAccountId == socialAccountId, cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountSocialAuth, SocialAuthEntityBase>(
                _context.SocialMediaAccountSocialAuths.FirstOrDefaultAsync(a => a.SocialAccountId == socialAccountId, cancellationToken)),
            _ => AsBase<IntegrationSocialAuth, SocialAuthEntityBase>(
                _context.IntegrationSocialAuths.FirstOrDefaultAsync(a => a.SocialAccountId == socialAccountId, cancellationToken))
        };

    public async Task AddSocialAuthAsync(SocialAuthEntityBase auth, CancellationToken cancellationToken = default)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                await _context.AppConnectionSocialAuths.AddAsync((AppConnectionSocialAuth)auth, cancellationToken);
                break;
            case MenuTypes.DeveloperApp:
                await _context.DeveloperAppSocialAuths.AddAsync((DeveloperAppSocialAuth)auth, cancellationToken);
                break;
            case MenuTypes.AccountConnection:
                await _context.AccountConnectionSocialAuths.AddAsync((AccountConnectionSocialAuth)auth, cancellationToken);
                break;
            case MenuTypes.AccountManagement:
                await _context.AccountManagementSocialAuths.AddAsync((AccountManagementSocialAuth)auth, cancellationToken);
                break;
            case MenuTypes.AccountHub:
                await _context.AccountHubSocialAuths.AddAsync((AccountHubSocialAuth)auth, cancellationToken);
                break;
            case MenuTypes.SocialMediaAccount:
                await _context.SocialMediaAccountSocialAuths.AddAsync((SocialMediaAccountSocialAuth)auth, cancellationToken);
                break;
            default:
                await _context.IntegrationSocialAuths.AddAsync((IntegrationSocialAuth)auth, cancellationToken);
                break;
        }
    }

    public void UpdateSocialAuth(SocialAuthEntityBase auth)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                UpdateTracked(_context.AppConnectionSocialAuths, (AppConnectionSocialAuth)auth);
                break;
            case MenuTypes.DeveloperApp:
                UpdateTracked(_context.DeveloperAppSocialAuths, (DeveloperAppSocialAuth)auth);
                break;
            case MenuTypes.AccountConnection:
                UpdateTracked(_context.AccountConnectionSocialAuths, (AccountConnectionSocialAuth)auth);
                break;
            case MenuTypes.AccountManagement:
                UpdateTracked(_context.AccountManagementSocialAuths, (AccountManagementSocialAuth)auth);
                break;
            case MenuTypes.AccountHub:
                UpdateTracked(_context.AccountHubSocialAuths, (AccountHubSocialAuth)auth);
                break;
            case MenuTypes.SocialMediaAccount:
                UpdateTracked(_context.SocialMediaAccountSocialAuths, (SocialMediaAccountSocialAuth)auth);
                break;
            default:
                UpdateTracked(_context.IntegrationSocialAuths, (IntegrationSocialAuth)auth);
                break;
        }
    }

    public async Task<IReadOnlyList<SocialProfileEntityBase>> GetProfilesByAccountAsync(
        Guid socialAccountId,
        CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => await _context.AppConnectionSocialProfiles.AsNoTracking()
                .Where(p => p.SocialAccountId == socialAccountId).ToListAsync(cancellationToken),
            MenuTypes.DeveloperApp => await _context.DeveloperAppSocialProfiles.AsNoTracking()
                .Where(p => p.SocialAccountId == socialAccountId).ToListAsync(cancellationToken),
            MenuTypes.AccountConnection => await _context.AccountConnectionSocialProfiles.AsNoTracking()
                .Where(p => p.SocialAccountId == socialAccountId).ToListAsync(cancellationToken),
            MenuTypes.AccountManagement => await _context.AccountManagementSocialProfiles.AsNoTracking()
                .Where(p => p.SocialAccountId == socialAccountId).ToListAsync(cancellationToken),
            MenuTypes.AccountHub => await _context.AccountHubSocialProfiles.AsNoTracking()
                .Where(p => p.SocialAccountId == socialAccountId).ToListAsync(cancellationToken),
            MenuTypes.SocialMediaAccount => await _context.SocialMediaAccountSocialProfiles.AsNoTracking()
                .Where(p => p.SocialAccountId == socialAccountId).ToListAsync(cancellationToken),
            _ => await _context.IntegrationSocialProfiles.AsNoTracking()
                .Where(p => p.SocialAccountId == socialAccountId).ToListAsync(cancellationToken)
        };

    public Task<SocialProfileEntityBase?> GetProfileByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionSocialProfile, SocialProfileEntityBase>(
                _context.AppConnectionSocialProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppSocialProfile, SocialProfileEntityBase>(
                _context.DeveloperAppSocialProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionSocialProfile, SocialProfileEntityBase>(
                _context.AccountConnectionSocialProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementSocialProfile, SocialProfileEntityBase>(
                _context.AccountManagementSocialProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubSocialProfile, SocialProfileEntityBase>(
                _context.AccountHubSocialProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountSocialProfile, SocialProfileEntityBase>(
                _context.SocialMediaAccountSocialProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            _ => AsBase<IntegrationSocialProfile, SocialProfileEntityBase>(
                _context.IntegrationSocialProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken))
        };

    public Task<SocialProfileEntityBase?> GetProfileByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        SocialProfileEntityBase? profile = _menuType switch
        {
            MenuTypes.AppConnection => _context.AppConnectionSocialProfiles.Local.FirstOrDefault(p => p.Id == id)
                ?? _context.AppConnectionSocialProfiles.Find(id),
            MenuTypes.DeveloperApp => _context.DeveloperAppSocialProfiles.Local.FirstOrDefault(p => p.Id == id)
                ?? _context.DeveloperAppSocialProfiles.Find(id),
            MenuTypes.AccountConnection => _context.AccountConnectionSocialProfiles.Local.FirstOrDefault(p => p.Id == id)
                ?? _context.AccountConnectionSocialProfiles.Find(id),
            MenuTypes.AccountManagement => _context.AccountManagementSocialProfiles.Local.FirstOrDefault(p => p.Id == id)
                ?? _context.AccountManagementSocialProfiles.Find(id),
            MenuTypes.AccountHub => _context.AccountHubSocialProfiles.Local.FirstOrDefault(p => p.Id == id)
                ?? _context.AccountHubSocialProfiles.Find(id),
            MenuTypes.SocialMediaAccount => _context.SocialMediaAccountSocialProfiles.Local.FirstOrDefault(p => p.Id == id)
                ?? _context.SocialMediaAccountSocialProfiles.Find(id),
            _ => _context.IntegrationSocialProfiles.Local.FirstOrDefault(p => p.Id == id)
                ?? _context.IntegrationSocialProfiles.Find(id)
        };

        return Task.FromResult(profile);
    }

    public Task<SocialProfileEntityBase?> GetProfileByExternalIdAsync(
        string externalProfileId,
        CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => QueryProfileByExternalIdAsync(_context.AppConnectionSocialProfiles, externalProfileId, cancellationToken),
            MenuTypes.DeveloperApp => QueryProfileByExternalIdAsync(_context.DeveloperAppSocialProfiles, externalProfileId, cancellationToken),
            MenuTypes.AccountConnection => QueryProfileByExternalIdAsync(_context.AccountConnectionSocialProfiles, externalProfileId, cancellationToken),
            MenuTypes.AccountManagement => QueryProfileByExternalIdAsync(_context.AccountManagementSocialProfiles, externalProfileId, cancellationToken),
            MenuTypes.AccountHub => QueryProfileByExternalIdAsync(_context.AccountHubSocialProfiles, externalProfileId, cancellationToken),
            MenuTypes.SocialMediaAccount => QueryProfileByExternalIdAsync(_context.SocialMediaAccountSocialProfiles, externalProfileId, cancellationToken),
            _ => QueryProfileByExternalIdAsync(_context.IntegrationSocialProfiles, externalProfileId, cancellationToken)
        };

    public async Task AddSocialProfileAsync(SocialProfileEntityBase profile, CancellationToken cancellationToken = default)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                await _context.AppConnectionSocialProfiles.AddAsync((AppConnectionSocialProfile)profile, cancellationToken);
                break;
            case MenuTypes.DeveloperApp:
                await _context.DeveloperAppSocialProfiles.AddAsync((DeveloperAppSocialProfile)profile, cancellationToken);
                break;
            case MenuTypes.AccountConnection:
                await _context.AccountConnectionSocialProfiles.AddAsync((AccountConnectionSocialProfile)profile, cancellationToken);
                break;
            case MenuTypes.AccountManagement:
                await _context.AccountManagementSocialProfiles.AddAsync((AccountManagementSocialProfile)profile, cancellationToken);
                break;
            case MenuTypes.AccountHub:
                await _context.AccountHubSocialProfiles.AddAsync((AccountHubSocialProfile)profile, cancellationToken);
                break;
            case MenuTypes.SocialMediaAccount:
                await _context.SocialMediaAccountSocialProfiles.AddAsync((SocialMediaAccountSocialProfile)profile, cancellationToken);
                break;
            default:
                await _context.IntegrationSocialProfiles.AddAsync((IntegrationSocialProfile)profile, cancellationToken);
                break;
        }
    }

    public void UpdateSocialProfile(SocialProfileEntityBase profile)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                UpdateTracked(_context.AppConnectionSocialProfiles, (AppConnectionSocialProfile)profile);
                break;
            case MenuTypes.DeveloperApp:
                UpdateTracked(_context.DeveloperAppSocialProfiles, (DeveloperAppSocialProfile)profile);
                break;
            case MenuTypes.AccountConnection:
                UpdateTracked(_context.AccountConnectionSocialProfiles, (AccountConnectionSocialProfile)profile);
                break;
            case MenuTypes.AccountManagement:
                UpdateTracked(_context.AccountManagementSocialProfiles, (AccountManagementSocialProfile)profile);
                break;
            case MenuTypes.AccountHub:
                UpdateTracked(_context.AccountHubSocialProfiles, (AccountHubSocialProfile)profile);
                break;
            case MenuTypes.SocialMediaAccount:
                UpdateTracked(_context.SocialMediaAccountSocialProfiles, (SocialMediaAccountSocialProfile)profile);
                break;
            default:
                UpdateTracked(_context.IntegrationSocialProfiles, (IntegrationSocialProfile)profile);
                break;
        }
    }

    public void RemoveSocialProfile(SocialProfileEntityBase profile)
        => RemoveSocialProfileById(profile.Id);

    public void RemoveSocialProfileById(Guid profileId)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                RemoveTrackedById(_context.AppConnectionSocialProfiles, profileId);
                break;
            case MenuTypes.DeveloperApp:
                RemoveTrackedById(_context.DeveloperAppSocialProfiles, profileId);
                break;
            case MenuTypes.AccountConnection:
                RemoveTrackedById(_context.AccountConnectionSocialProfiles, profileId);
                break;
            case MenuTypes.AccountManagement:
                RemoveTrackedById(_context.AccountManagementSocialProfiles, profileId);
                break;
            case MenuTypes.AccountHub:
                RemoveTrackedById(_context.AccountHubSocialProfiles, profileId);
                break;
            case MenuTypes.SocialMediaAccount:
                RemoveTrackedById(_context.SocialMediaAccountSocialProfiles, profileId);
                break;
            default:
                RemoveTrackedById(_context.IntegrationSocialProfiles, profileId);
                break;
        }
    }

    public async Task<IReadOnlyList<SocialProfileEntityBase>> FindProfilesByExternalIdAsync(
        string externalProfileId,
        CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => await _context.AppConnectionSocialProfiles.AsNoTracking()
                .Where(p => p.ExternalProfileId == externalProfileId).ToListAsync(cancellationToken),
            MenuTypes.DeveloperApp => await _context.DeveloperAppSocialProfiles.AsNoTracking()
                .Where(p => p.ExternalProfileId == externalProfileId).ToListAsync(cancellationToken),
            MenuTypes.AccountConnection => await _context.AccountConnectionSocialProfiles.AsNoTracking()
                .Where(p => p.ExternalProfileId == externalProfileId).ToListAsync(cancellationToken),
            MenuTypes.AccountManagement => await _context.AccountManagementSocialProfiles.AsNoTracking()
                .Where(p => p.ExternalProfileId == externalProfileId).ToListAsync(cancellationToken),
            MenuTypes.AccountHub => await _context.AccountHubSocialProfiles.AsNoTracking()
                .Where(p => p.ExternalProfileId == externalProfileId).ToListAsync(cancellationToken),
            MenuTypes.SocialMediaAccount => await _context.SocialMediaAccountSocialProfiles.AsNoTracking()
                .Where(p => p.ExternalProfileId == externalProfileId).ToListAsync(cancellationToken),
            _ => await _context.IntegrationSocialProfiles.AsNoTracking()
                .Where(p => p.ExternalProfileId == externalProfileId).ToListAsync(cancellationToken)
        };

    public async Task<IReadOnlyList<PostEntityBase>> GetPostsByUserProfilesAsync(
        Guid userId,
        Guid? platformId = null,
        CancellationToken cancellationToken = default)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
            {
                var query = _context.AppConnectionPosts.AsNoTracking()
                    .Include(p => p.SocialProfile!).ThenInclude(sp => sp.SocialAccount)
                    .Include(p => p.Platform)
                    .Include(p => p.MediaItems)
                    .Where(p => p.SocialProfile!.SocialAccount!.UserId == userId);
                if (platformId.HasValue)
                    query = query.Where(p => p.PlatformId == platformId.Value);
                return await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
            }
            case MenuTypes.DeveloperApp:
            {
                var query = _context.DeveloperAppPosts.AsNoTracking()
                    .Include(p => p.SocialProfile!).ThenInclude(sp => sp.SocialAccount)
                    .Include(p => p.Platform)
                    .Include(p => p.MediaItems)
                    .Where(p => p.SocialProfile!.SocialAccount!.UserId == userId);
                if (platformId.HasValue)
                    query = query.Where(p => p.PlatformId == platformId.Value);
                return await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
            }
            case MenuTypes.AccountConnection:
            {
                var query = _context.AccountConnectionPosts.AsNoTracking()
                    .Include(p => p.SocialProfile!).ThenInclude(sp => sp.SocialAccount)
                    .Include(p => p.Platform)
                    .Include(p => p.MediaItems)
                    .Where(p => p.SocialProfile!.SocialAccount!.UserId == userId);
                if (platformId.HasValue)
                    query = query.Where(p => p.PlatformId == platformId.Value);
                return await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
            }
            case MenuTypes.AccountManagement:
            {
                var query = _context.AccountManagementPosts.AsNoTracking()
                    .Include(p => p.SocialProfile!).ThenInclude(sp => sp.SocialAccount)
                    .Include(p => p.Platform)
                    .Include(p => p.MediaItems)
                    .Where(p => p.SocialProfile!.SocialAccount!.UserId == userId);
                if (platformId.HasValue)
                    query = query.Where(p => p.PlatformId == platformId.Value);
                return await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
            }
            case MenuTypes.AccountHub:
            {
                var query = _context.AccountHubPosts.AsNoTracking()
                    .Include(p => p.SocialProfile!).ThenInclude(sp => sp.SocialAccount)
                    .Include(p => p.Platform)
                    .Include(p => p.MediaItems)
                    .Where(p => p.SocialProfile!.SocialAccount!.UserId == userId);
                if (platformId.HasValue)
                    query = query.Where(p => p.PlatformId == platformId.Value);
                return await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
            }
            case MenuTypes.SocialMediaAccount:
            {
                var query = _context.SocialMediaAccountPosts.AsNoTracking()
                    .Include(p => p.SocialProfile!).ThenInclude(sp => sp.SocialAccount)
                    .Include(p => p.Platform)
                    .Include(p => p.MediaItems)
                    .Where(p => p.SocialProfile!.SocialAccount!.UserId == userId);
                if (platformId.HasValue)
                    query = query.Where(p => p.PlatformId == platformId.Value);
                return await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
            }
            default:
            {
                var query = _context.IntegrationPosts.AsNoTracking()
                    .Include(p => p.SocialProfile!).ThenInclude(sp => sp.SocialAccount)
                    .Include(p => p.Platform)
                    .Include(p => p.MediaItems)
                    .Where(p => p.SocialProfile!.SocialAccount!.UserId == userId);
                if (platformId.HasValue)
                    query = query.Where(p => p.PlatformId == platformId.Value);
                return await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
            }
        }
    }

    public Task<PostEntityBase?> GetPostByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionPost, PostEntityBase>(
                _context.AppConnectionPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppPost, PostEntityBase>(
                _context.DeveloperAppPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionPost, PostEntityBase>(
                _context.AccountConnectionPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementPost, PostEntityBase>(
                _context.AccountManagementPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubPost, PostEntityBase>(
                _context.AccountHubPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountPost, PostEntityBase>(
                _context.SocialMediaAccountPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)),
            _ => AsBase<IntegrationPost, PostEntityBase>(
                _context.IntegrationPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken))
        };

    public Task<PostEntityBase?> GetPostByExternalIdAsync(
        Guid socialProfileId,
        string externalPostId,
        CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionPost, PostEntityBase>(
                _context.AppConnectionPosts.AsNoTracking()
                    .Include(p => p.MediaItems)
                    .FirstOrDefaultAsync(p => p.SocialProfileId == socialProfileId && p.ExternalPostId == externalPostId, cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppPost, PostEntityBase>(
                _context.DeveloperAppPosts.AsNoTracking()
                    .Include(p => p.MediaItems)
                    .FirstOrDefaultAsync(p => p.SocialProfileId == socialProfileId && p.ExternalPostId == externalPostId, cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionPost, PostEntityBase>(
                _context.AccountConnectionPosts.AsNoTracking()
                    .Include(p => p.MediaItems)
                    .FirstOrDefaultAsync(p => p.SocialProfileId == socialProfileId && p.ExternalPostId == externalPostId, cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementPost, PostEntityBase>(
                _context.AccountManagementPosts.AsNoTracking()
                    .Include(p => p.MediaItems)
                    .FirstOrDefaultAsync(p => p.SocialProfileId == socialProfileId && p.ExternalPostId == externalPostId, cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubPost, PostEntityBase>(
                _context.AccountHubPosts.AsNoTracking()
                    .Include(p => p.MediaItems)
                    .FirstOrDefaultAsync(p => p.SocialProfileId == socialProfileId && p.ExternalPostId == externalPostId, cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountPost, PostEntityBase>(
                _context.SocialMediaAccountPosts.AsNoTracking()
                    .Include(p => p.MediaItems)
                    .FirstOrDefaultAsync(p => p.SocialProfileId == socialProfileId && p.ExternalPostId == externalPostId, cancellationToken)),
            _ => AsBase<IntegrationPost, PostEntityBase>(
                _context.IntegrationPosts.AsNoTracking()
                    .Include(p => p.MediaItems)
                    .FirstOrDefaultAsync(p => p.SocialProfileId == socialProfileId && p.ExternalPostId == externalPostId, cancellationToken))
        };

    public Task<PostEntityBase?> FindPostByExternalIdAsync(
        string externalPostId,
        CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionPost, PostEntityBase>(
                _context.AppConnectionPosts.AsNoTracking()
                    .Include(p => p.MediaItems)
                    .FirstOrDefaultAsync(p => p.ExternalPostId == externalPostId, cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppPost, PostEntityBase>(
                _context.DeveloperAppPosts.AsNoTracking()
                    .Include(p => p.MediaItems)
                    .FirstOrDefaultAsync(p => p.ExternalPostId == externalPostId, cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionPost, PostEntityBase>(
                _context.AccountConnectionPosts.AsNoTracking()
                    .Include(p => p.MediaItems)
                    .FirstOrDefaultAsync(p => p.ExternalPostId == externalPostId, cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementPost, PostEntityBase>(
                _context.AccountManagementPosts.AsNoTracking()
                    .Include(p => p.MediaItems)
                    .FirstOrDefaultAsync(p => p.ExternalPostId == externalPostId, cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubPost, PostEntityBase>(
                _context.AccountHubPosts.AsNoTracking()
                    .Include(p => p.MediaItems)
                    .FirstOrDefaultAsync(p => p.ExternalPostId == externalPostId, cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountPost, PostEntityBase>(
                _context.SocialMediaAccountPosts.AsNoTracking()
                    .Include(p => p.MediaItems)
                    .FirstOrDefaultAsync(p => p.ExternalPostId == externalPostId, cancellationToken)),
            _ => AsBase<IntegrationPost, PostEntityBase>(
                _context.IntegrationPosts.AsNoTracking()
                    .Include(p => p.MediaItems)
                    .FirstOrDefaultAsync(p => p.ExternalPostId == externalPostId, cancellationToken))
        };

    public async Task AddPostAsync(PostEntityBase post, CancellationToken cancellationToken = default)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                await _context.AppConnectionPosts.AddAsync((AppConnectionPost)post, cancellationToken);
                break;
            case MenuTypes.DeveloperApp:
                await _context.DeveloperAppPosts.AddAsync((DeveloperAppPost)post, cancellationToken);
                break;
            case MenuTypes.AccountConnection:
                await _context.AccountConnectionPosts.AddAsync((AccountConnectionPost)post, cancellationToken);
                break;
            case MenuTypes.AccountManagement:
                await _context.AccountManagementPosts.AddAsync((AccountManagementPost)post, cancellationToken);
                break;
            case MenuTypes.AccountHub:
                await _context.AccountHubPosts.AddAsync((AccountHubPost)post, cancellationToken);
                break;
            case MenuTypes.SocialMediaAccount:
                await _context.SocialMediaAccountPosts.AddAsync((SocialMediaAccountPost)post, cancellationToken);
                break;
            default:
                await _context.IntegrationPosts.AddAsync((IntegrationPost)post, cancellationToken);
                break;
        }
    }

    public async Task AddMediaAsync(MediaEntityBase media, CancellationToken cancellationToken = default)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                await _context.AppConnectionMedia.AddAsync((AppConnectionMedia)media, cancellationToken);
                break;
            case MenuTypes.DeveloperApp:
                await _context.DeveloperAppMedia.AddAsync((DeveloperAppMedia)media, cancellationToken);
                break;
            case MenuTypes.AccountConnection:
                await _context.AccountConnectionMedia.AddAsync((AccountConnectionMedia)media, cancellationToken);
                break;
            case MenuTypes.AccountManagement:
                await _context.AccountManagementMedia.AddAsync((AccountManagementMedia)media, cancellationToken);
                break;
            case MenuTypes.AccountHub:
                await _context.AccountHubMedia.AddAsync((AccountHubMedia)media, cancellationToken);
                break;
            case MenuTypes.SocialMediaAccount:
                await _context.SocialMediaAccountMedia.AddAsync((SocialMediaAccountMedia)media, cancellationToken);
                break;
            default:
                await _context.IntegrationMedia.AddAsync((IntegrationMedia)media, cancellationToken);
                break;
        }
    }

    public void UpdateMedia(MediaEntityBase media)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                UpdateTracked(_context.AppConnectionMedia, (AppConnectionMedia)media);
                break;
            case MenuTypes.DeveloperApp:
                UpdateTracked(_context.DeveloperAppMedia, (DeveloperAppMedia)media);
                break;
            case MenuTypes.AccountConnection:
                UpdateTracked(_context.AccountConnectionMedia, (AccountConnectionMedia)media);
                break;
            case MenuTypes.AccountManagement:
                UpdateTracked(_context.AccountManagementMedia, (AccountManagementMedia)media);
                break;
            case MenuTypes.AccountHub:
                UpdateTracked(_context.AccountHubMedia, (AccountHubMedia)media);
                break;
            case MenuTypes.SocialMediaAccount:
                UpdateTracked(_context.SocialMediaAccountMedia, (SocialMediaAccountMedia)media);
                break;
            default:
                UpdateTracked(_context.IntegrationMedia, (IntegrationMedia)media);
                break;
        }
    }

    public void UpdatePost(PostEntityBase post)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                UpdateTracked(_context.AppConnectionPosts, (AppConnectionPost)post);
                break;
            case MenuTypes.DeveloperApp:
                UpdateTracked(_context.DeveloperAppPosts, (DeveloperAppPost)post);
                break;
            case MenuTypes.AccountConnection:
                UpdateTracked(_context.AccountConnectionPosts, (AccountConnectionPost)post);
                break;
            case MenuTypes.AccountManagement:
                UpdateTracked(_context.AccountManagementPosts, (AccountManagementPost)post);
                break;
            case MenuTypes.AccountHub:
                UpdateTracked(_context.AccountHubPosts, (AccountHubPost)post);
                break;
            case MenuTypes.SocialMediaAccount:
                UpdateTracked(_context.SocialMediaAccountPosts, (SocialMediaAccountPost)post);
                break;
            default:
                UpdateTracked(_context.IntegrationPosts, (IntegrationPost)post);
                break;
        }
    }

    public void RemovePost(PostEntityBase post)
        => RemovePostById(post.Id);

    public void RemovePostById(Guid postId)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                RemoveTrackedById(_context.AppConnectionPosts, postId);
                break;
            case MenuTypes.DeveloperApp:
                RemoveTrackedById(_context.DeveloperAppPosts, postId);
                break;
            case MenuTypes.AccountConnection:
                RemoveTrackedById(_context.AccountConnectionPosts, postId);
                break;
            case MenuTypes.AccountManagement:
                RemoveTrackedById(_context.AccountManagementPosts, postId);
                break;
            case MenuTypes.AccountHub:
                RemoveTrackedById(_context.AccountHubPosts, postId);
                break;
            case MenuTypes.SocialMediaAccount:
                RemoveTrackedById(_context.SocialMediaAccountPosts, postId);
                break;
            default:
                RemoveTrackedById(_context.IntegrationPosts, postId);
                break;
        }
    }

    public async Task<IReadOnlyList<InboxCommentRow>> GetCommentsForInboxAsync(
        Guid userId,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds,
        CancellationToken cancellationToken = default)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
            {
                var query = _context.AppConnectionComments.AsNoTracking()
                    .Include(c => c.Post).ThenInclude(p => p!.SocialProfile).ThenInclude(sp => sp!.SocialAccount).ThenInclude(a => a!.Platform)
                    .Include(c => c.Post).ThenInclude(p => p!.MediaItems)
                    .Include(c => c.Replies)
                    .Where(c => c.Post!.SocialProfile!.SocialAccount!.UserId == userId && !c.IsDeleted);
                query = ApplyCommentPlatformFilter(query, platformId, platformIds);
                var comments = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
                return MapInboxCommentRows(comments);
            }
            case MenuTypes.DeveloperApp:
            {
                var query = _context.DeveloperAppComments.AsNoTracking()
                    .Include(c => c.Post).ThenInclude(p => p!.SocialProfile).ThenInclude(sp => sp!.SocialAccount).ThenInclude(a => a!.Platform)
                    .Include(c => c.Post).ThenInclude(p => p!.MediaItems)
                    .Include(c => c.Replies)
                    .Where(c => c.Post!.SocialProfile!.SocialAccount!.UserId == userId && !c.IsDeleted);
                query = ApplyCommentPlatformFilter(query, platformId, platformIds);
                var comments = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
                return MapInboxCommentRows(comments);
            }
            case MenuTypes.AccountConnection:
            {
                var query = _context.AccountConnectionComments.AsNoTracking()
                    .Include(c => c.Post).ThenInclude(p => p!.SocialProfile).ThenInclude(sp => sp!.SocialAccount).ThenInclude(a => a!.Platform)
                    .Include(c => c.Post).ThenInclude(p => p!.MediaItems)
                    .Include(c => c.Replies)
                    .Where(c => c.Post!.SocialProfile!.SocialAccount!.UserId == userId && !c.IsDeleted);
                query = ApplyCommentPlatformFilter(query, platformId, platformIds);
                var comments = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
                return MapInboxCommentRows(comments);
            }
            case MenuTypes.AccountManagement:
            {
                var query = _context.AccountManagementComments.AsNoTracking()
                    .Include(c => c.Post).ThenInclude(p => p!.SocialProfile).ThenInclude(sp => sp!.SocialAccount).ThenInclude(a => a!.Platform)
                    .Include(c => c.Post).ThenInclude(p => p!.MediaItems)
                    .Include(c => c.Replies)
                    .Where(c => c.Post!.SocialProfile!.SocialAccount!.UserId == userId && !c.IsDeleted);
                query = ApplyCommentPlatformFilter(query, platformId, platformIds);
                var comments = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
                return MapInboxCommentRows(comments);
            }
            case MenuTypes.AccountHub:
            {
                var query = _context.AccountHubComments.AsNoTracking()
                    .Include(c => c.Post).ThenInclude(p => p!.SocialProfile).ThenInclude(sp => sp!.SocialAccount).ThenInclude(a => a!.Platform)
                    .Include(c => c.Post).ThenInclude(p => p!.MediaItems)
                    .Include(c => c.Replies)
                    .Where(c => c.Post!.SocialProfile!.SocialAccount!.UserId == userId && !c.IsDeleted);
                query = ApplyCommentPlatformFilter(query, platformId, platformIds);
                var comments = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
                return MapInboxCommentRows(comments);
            }
            case MenuTypes.SocialMediaAccount:
            {
                var query = _context.SocialMediaAccountComments.AsNoTracking()
                    .Include(c => c.Post).ThenInclude(p => p!.SocialProfile).ThenInclude(sp => sp!.SocialAccount).ThenInclude(a => a!.Platform)
                    .Include(c => c.Post).ThenInclude(p => p!.MediaItems)
                    .Include(c => c.Replies)
                    .Where(c => c.Post!.SocialProfile!.SocialAccount!.UserId == userId && !c.IsDeleted);
                query = ApplyCommentPlatformFilter(query, platformId, platformIds);
                var comments = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
                return MapInboxCommentRows(comments);
            }
            default:
            {
                var query = _context.IntegrationComments.AsNoTracking()
                    .Include(c => c.Post).ThenInclude(p => p!.SocialProfile).ThenInclude(sp => sp!.SocialAccount).ThenInclude(a => a!.Platform)
                    .Include(c => c.Post).ThenInclude(p => p!.MediaItems)
                    .Include(c => c.Replies)
                    .Where(c => c.Post!.SocialProfile!.SocialAccount!.UserId == userId && !c.IsDeleted);
                query = ApplyCommentPlatformFilter(query, platformId, platformIds);
                var comments = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
                return MapInboxCommentRows(comments);
            }
        }
    }

    public Task<CommentEntityBase?> GetCommentByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionComment, CommentEntityBase>(
                _context.AppConnectionComments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppComment, CommentEntityBase>(
                _context.DeveloperAppComments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionComment, CommentEntityBase>(
                _context.AccountConnectionComments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementComment, CommentEntityBase>(
                _context.AccountManagementComments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubComment, CommentEntityBase>(
                _context.AccountHubComments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountComment, CommentEntityBase>(
                _context.SocialMediaAccountComments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)),
            _ => AsBase<IntegrationComment, CommentEntityBase>(
                _context.IntegrationComments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken))
        };

    public Task<CommentEntityBase?> GetCommentByExternalIdAsync(
        string externalCommentId,
        CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionComment, CommentEntityBase>(
                _context.AppConnectionComments.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ExternalCommentId == externalCommentId, cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppComment, CommentEntityBase>(
                _context.DeveloperAppComments.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ExternalCommentId == externalCommentId, cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionComment, CommentEntityBase>(
                _context.AccountConnectionComments.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ExternalCommentId == externalCommentId, cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementComment, CommentEntityBase>(
                _context.AccountManagementComments.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ExternalCommentId == externalCommentId, cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubComment, CommentEntityBase>(
                _context.AccountHubComments.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ExternalCommentId == externalCommentId, cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountComment, CommentEntityBase>(
                _context.SocialMediaAccountComments.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ExternalCommentId == externalCommentId, cancellationToken)),
            _ => AsBase<IntegrationComment, CommentEntityBase>(
                _context.IntegrationComments.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ExternalCommentId == externalCommentId, cancellationToken))
        };

    public async Task AddCommentAsync(CommentEntityBase comment, CancellationToken cancellationToken = default)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                await _context.AppConnectionComments.AddAsync((AppConnectionComment)comment, cancellationToken);
                break;
            case MenuTypes.DeveloperApp:
                await _context.DeveloperAppComments.AddAsync((DeveloperAppComment)comment, cancellationToken);
                break;
            case MenuTypes.AccountConnection:
                await _context.AccountConnectionComments.AddAsync((AccountConnectionComment)comment, cancellationToken);
                break;
            case MenuTypes.AccountManagement:
                await _context.AccountManagementComments.AddAsync((AccountManagementComment)comment, cancellationToken);
                break;
            case MenuTypes.AccountHub:
                await _context.AccountHubComments.AddAsync((AccountHubComment)comment, cancellationToken);
                break;
            case MenuTypes.SocialMediaAccount:
                await _context.SocialMediaAccountComments.AddAsync((SocialMediaAccountComment)comment, cancellationToken);
                break;
            default:
                await _context.IntegrationComments.AddAsync((IntegrationComment)comment, cancellationToken);
                break;
        }
    }

    public void UpdateComment(CommentEntityBase comment)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                UpdateTracked(_context.AppConnectionComments, (AppConnectionComment)comment);
                break;
            case MenuTypes.DeveloperApp:
                UpdateTracked(_context.DeveloperAppComments, (DeveloperAppComment)comment);
                break;
            case MenuTypes.AccountConnection:
                UpdateTracked(_context.AccountConnectionComments, (AccountConnectionComment)comment);
                break;
            case MenuTypes.AccountManagement:
                UpdateTracked(_context.AccountManagementComments, (AccountManagementComment)comment);
                break;
            case MenuTypes.AccountHub:
                UpdateTracked(_context.AccountHubComments, (AccountHubComment)comment);
                break;
            case MenuTypes.SocialMediaAccount:
                UpdateTracked(_context.SocialMediaAccountComments, (SocialMediaAccountComment)comment);
                break;
            default:
                UpdateTracked(_context.IntegrationComments, (IntegrationComment)comment);
                break;
        }
    }

    public async Task<IReadOnlyList<InboxMessageRow>> GetMessagesForInboxAsync(
        Guid userId,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds,
        CancellationToken cancellationToken = default)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
            {
                var query = _context.AppConnectionMessages.AsNoTracking()
                    .Include(m => m.Conversation).ThenInclude(c => c!.SocialProfile).ThenInclude(p => p!.SocialAccount).ThenInclude(a => a!.Platform)
                    .Where(m => m.Conversation!.SocialProfile!.SocialAccount!.UserId == userId);
                query = ApplyMessagePlatformFilter(query, platformId, platformIds);
                var messages = await query.OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
                return MapInboxMessageRows(messages);
            }
            case MenuTypes.DeveloperApp:
            {
                var query = _context.DeveloperAppMessages.AsNoTracking()
                    .Include(m => m.Conversation).ThenInclude(c => c!.SocialProfile).ThenInclude(p => p!.SocialAccount).ThenInclude(a => a!.Platform)
                    .Where(m => m.Conversation!.SocialProfile!.SocialAccount!.UserId == userId);
                query = ApplyMessagePlatformFilter(query, platformId, platformIds);
                var messages = await query.OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
                return MapInboxMessageRows(messages);
            }
            case MenuTypes.AccountConnection:
            {
                var query = _context.AccountConnectionMessages.AsNoTracking()
                    .Include(m => m.Conversation).ThenInclude(c => c!.SocialProfile).ThenInclude(p => p!.SocialAccount).ThenInclude(a => a!.Platform)
                    .Where(m => m.Conversation!.SocialProfile!.SocialAccount!.UserId == userId);
                query = ApplyMessagePlatformFilter(query, platformId, platformIds);
                var messages = await query.OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
                return MapInboxMessageRows(messages);
            }
            case MenuTypes.AccountManagement:
            {
                var query = _context.AccountManagementMessages.AsNoTracking()
                    .Include(m => m.Conversation).ThenInclude(c => c!.SocialProfile).ThenInclude(p => p!.SocialAccount).ThenInclude(a => a!.Platform)
                    .Where(m => m.Conversation!.SocialProfile!.SocialAccount!.UserId == userId);
                query = ApplyMessagePlatformFilter(query, platformId, platformIds);
                var messages = await query.OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
                return MapInboxMessageRows(messages);
            }
            case MenuTypes.AccountHub:
            {
                var query = _context.AccountHubMessages.AsNoTracking()
                    .Include(m => m.Conversation).ThenInclude(c => c!.SocialProfile).ThenInclude(p => p!.SocialAccount).ThenInclude(a => a!.Platform)
                    .Where(m => m.Conversation!.SocialProfile!.SocialAccount!.UserId == userId);
                query = ApplyMessagePlatformFilter(query, platformId, platformIds);
                var messages = await query.OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
                return MapInboxMessageRows(messages);
            }
            case MenuTypes.SocialMediaAccount:
            {
                var query = _context.SocialMediaAccountMessages.AsNoTracking()
                    .Include(m => m.Conversation).ThenInclude(c => c!.SocialProfile).ThenInclude(p => p!.SocialAccount).ThenInclude(a => a!.Platform)
                    .Where(m => m.Conversation!.SocialProfile!.SocialAccount!.UserId == userId);
                query = ApplyMessagePlatformFilter(query, platformId, platformIds);
                var messages = await query.OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
                return MapInboxMessageRows(messages);
            }
            default:
            {
                var query = _context.IntegrationMessages.AsNoTracking()
                    .Include(m => m.Conversation).ThenInclude(c => c!.SocialProfile).ThenInclude(p => p!.SocialAccount).ThenInclude(a => a!.Platform)
                    .Where(m => m.Conversation!.SocialProfile!.SocialAccount!.UserId == userId);
                query = ApplyMessagePlatformFilter(query, platformId, platformIds);
                var messages = await query.OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
                return MapInboxMessageRows(messages);
            }
        }
    }

    public Task<ConversationEntityBase?> GetConversationByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionConversation, ConversationEntityBase>(
                _context.AppConnectionConversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppConversation, ConversationEntityBase>(
                _context.DeveloperAppConversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionConversation, ConversationEntityBase>(
                _context.AccountConnectionConversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementConversation, ConversationEntityBase>(
                _context.AccountManagementConversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubConversation, ConversationEntityBase>(
                _context.AccountHubConversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountConversation, ConversationEntityBase>(
                _context.SocialMediaAccountConversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)),
            _ => AsBase<IntegrationConversation, ConversationEntityBase>(
                _context.IntegrationConversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken))
        };

    public Task<ConversationEntityBase?> GetConversationByExternalIdAsync(
        Guid socialProfileId,
        string externalConversationId,
        CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionConversation, ConversationEntityBase>(
                _context.AppConnectionConversations.FirstOrDefaultAsync(
                    c => c.SocialProfileId == socialProfileId && c.ExternalConversationId == externalConversationId, cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppConversation, ConversationEntityBase>(
                _context.DeveloperAppConversations.FirstOrDefaultAsync(
                    c => c.SocialProfileId == socialProfileId && c.ExternalConversationId == externalConversationId, cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionConversation, ConversationEntityBase>(
                _context.AccountConnectionConversations.FirstOrDefaultAsync(
                    c => c.SocialProfileId == socialProfileId && c.ExternalConversationId == externalConversationId, cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementConversation, ConversationEntityBase>(
                _context.AccountManagementConversations.FirstOrDefaultAsync(
                    c => c.SocialProfileId == socialProfileId && c.ExternalConversationId == externalConversationId, cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubConversation, ConversationEntityBase>(
                _context.AccountHubConversations.FirstOrDefaultAsync(
                    c => c.SocialProfileId == socialProfileId && c.ExternalConversationId == externalConversationId, cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountConversation, ConversationEntityBase>(
                _context.SocialMediaAccountConversations.FirstOrDefaultAsync(
                    c => c.SocialProfileId == socialProfileId && c.ExternalConversationId == externalConversationId, cancellationToken)),
            _ => AsBase<IntegrationConversation, ConversationEntityBase>(
                _context.IntegrationConversations.FirstOrDefaultAsync(
                    c => c.SocialProfileId == socialProfileId && c.ExternalConversationId == externalConversationId, cancellationToken))
        };

    public Task<ConversationEntityBase?> GetConversationByProfileAndCustomerAsync(
        Guid socialProfileId,
        string customerId,
        CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionConversation, ConversationEntityBase>(
                _context.AppConnectionConversations.FirstOrDefaultAsync(
                    c => c.SocialProfileId == socialProfileId && c.CustomerId == customerId, cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppConversation, ConversationEntityBase>(
                _context.DeveloperAppConversations.FirstOrDefaultAsync(
                    c => c.SocialProfileId == socialProfileId && c.CustomerId == customerId, cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionConversation, ConversationEntityBase>(
                _context.AccountConnectionConversations.FirstOrDefaultAsync(
                    c => c.SocialProfileId == socialProfileId && c.CustomerId == customerId, cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementConversation, ConversationEntityBase>(
                _context.AccountManagementConversations.FirstOrDefaultAsync(
                    c => c.SocialProfileId == socialProfileId && c.CustomerId == customerId, cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubConversation, ConversationEntityBase>(
                _context.AccountHubConversations.FirstOrDefaultAsync(
                    c => c.SocialProfileId == socialProfileId && c.CustomerId == customerId, cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountConversation, ConversationEntityBase>(
                _context.SocialMediaAccountConversations.FirstOrDefaultAsync(
                    c => c.SocialProfileId == socialProfileId && c.CustomerId == customerId, cancellationToken)),
            _ => AsBase<IntegrationConversation, ConversationEntityBase>(
                _context.IntegrationConversations.FirstOrDefaultAsync(
                    c => c.SocialProfileId == socialProfileId && c.CustomerId == customerId, cancellationToken))
        };

    public async Task<IReadOnlyList<ConversationEntityBase>> GetConversationsByProfileIdAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => await _context.AppConnectionConversations.AsNoTracking()
                .Where(c => c.SocialProfileId == profileId).ToListAsync(cancellationToken),
            MenuTypes.DeveloperApp => await _context.DeveloperAppConversations.AsNoTracking()
                .Where(c => c.SocialProfileId == profileId).ToListAsync(cancellationToken),
            MenuTypes.AccountConnection => await _context.AccountConnectionConversations.AsNoTracking()
                .Where(c => c.SocialProfileId == profileId).ToListAsync(cancellationToken),
            MenuTypes.AccountManagement => await _context.AccountManagementConversations.AsNoTracking()
                .Where(c => c.SocialProfileId == profileId).ToListAsync(cancellationToken),
            MenuTypes.AccountHub => await _context.AccountHubConversations.AsNoTracking()
                .Where(c => c.SocialProfileId == profileId).ToListAsync(cancellationToken),
            MenuTypes.SocialMediaAccount => await _context.SocialMediaAccountConversations.AsNoTracking()
                .Where(c => c.SocialProfileId == profileId).ToListAsync(cancellationToken),
            _ => await _context.IntegrationConversations.AsNoTracking()
                .Where(c => c.SocialProfileId == profileId).ToListAsync(cancellationToken)
        };

    public async Task AddConversationAsync(ConversationEntityBase conversation, CancellationToken cancellationToken = default)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                await _context.AppConnectionConversations.AddAsync((AppConnectionConversation)conversation, cancellationToken);
                break;
            case MenuTypes.DeveloperApp:
                await _context.DeveloperAppConversations.AddAsync((DeveloperAppConversation)conversation, cancellationToken);
                break;
            case MenuTypes.AccountConnection:
                await _context.AccountConnectionConversations.AddAsync((AccountConnectionConversation)conversation, cancellationToken);
                break;
            case MenuTypes.AccountManagement:
                await _context.AccountManagementConversations.AddAsync((AccountManagementConversation)conversation, cancellationToken);
                break;
            case MenuTypes.AccountHub:
                await _context.AccountHubConversations.AddAsync((AccountHubConversation)conversation, cancellationToken);
                break;
            case MenuTypes.SocialMediaAccount:
                await _context.SocialMediaAccountConversations.AddAsync((SocialMediaAccountConversation)conversation, cancellationToken);
                break;
            default:
                await _context.IntegrationConversations.AddAsync((IntegrationConversation)conversation, cancellationToken);
                break;
        }
    }

    public void UpdateConversation(ConversationEntityBase conversation)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                UpdateTracked(_context.AppConnectionConversations, (AppConnectionConversation)conversation);
                break;
            case MenuTypes.DeveloperApp:
                UpdateTracked(_context.DeveloperAppConversations, (DeveloperAppConversation)conversation);
                break;
            case MenuTypes.AccountConnection:
                UpdateTracked(_context.AccountConnectionConversations, (AccountConnectionConversation)conversation);
                break;
            case MenuTypes.AccountManagement:
                UpdateTracked(_context.AccountManagementConversations, (AccountManagementConversation)conversation);
                break;
            case MenuTypes.AccountHub:
                UpdateTracked(_context.AccountHubConversations, (AccountHubConversation)conversation);
                break;
            case MenuTypes.SocialMediaAccount:
                UpdateTracked(_context.SocialMediaAccountConversations, (SocialMediaAccountConversation)conversation);
                break;
            default:
                UpdateTracked(_context.IntegrationConversations, (IntegrationConversation)conversation);
                break;
        }
    }

    public Task<MessageEntityBase?> GetMessageByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionMessage, MessageEntityBase>(
                _context.AppConnectionMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppMessage, MessageEntityBase>(
                _context.DeveloperAppMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionMessage, MessageEntityBase>(
                _context.AccountConnectionMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementMessage, MessageEntityBase>(
                _context.AccountManagementMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubMessage, MessageEntityBase>(
                _context.AccountHubMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountMessage, MessageEntityBase>(
                _context.SocialMediaAccountMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken)),
            _ => AsBase<IntegrationMessage, MessageEntityBase>(
                _context.IntegrationMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken))
        };

    public Task<MessageEntityBase?> GetMessageByExternalIdAsync(
        string externalMessageId,
        CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => AsBase<AppConnectionMessage, MessageEntityBase>(
                _context.AppConnectionMessages.FirstOrDefaultAsync(m => m.ExternalMessageId == externalMessageId, cancellationToken)),
            MenuTypes.DeveloperApp => AsBase<DeveloperAppMessage, MessageEntityBase>(
                _context.DeveloperAppMessages.FirstOrDefaultAsync(m => m.ExternalMessageId == externalMessageId, cancellationToken)),
            MenuTypes.AccountConnection => AsBase<AccountConnectionMessage, MessageEntityBase>(
                _context.AccountConnectionMessages.FirstOrDefaultAsync(m => m.ExternalMessageId == externalMessageId, cancellationToken)),
            MenuTypes.AccountManagement => AsBase<AccountManagementMessage, MessageEntityBase>(
                _context.AccountManagementMessages.FirstOrDefaultAsync(m => m.ExternalMessageId == externalMessageId, cancellationToken)),
            MenuTypes.AccountHub => AsBase<AccountHubMessage, MessageEntityBase>(
                _context.AccountHubMessages.FirstOrDefaultAsync(m => m.ExternalMessageId == externalMessageId, cancellationToken)),
            MenuTypes.SocialMediaAccount => AsBase<SocialMediaAccountMessage, MessageEntityBase>(
                _context.SocialMediaAccountMessages.FirstOrDefaultAsync(m => m.ExternalMessageId == externalMessageId, cancellationToken)),
            _ => AsBase<IntegrationMessage, MessageEntityBase>(
                _context.IntegrationMessages.FirstOrDefaultAsync(m => m.ExternalMessageId == externalMessageId, cancellationToken))
        };

    public async Task AddMessageAsync(MessageEntityBase message, CancellationToken cancellationToken = default)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                await _context.AppConnectionMessages.AddAsync((AppConnectionMessage)message, cancellationToken);
                break;
            case MenuTypes.DeveloperApp:
                await _context.DeveloperAppMessages.AddAsync((DeveloperAppMessage)message, cancellationToken);
                break;
            case MenuTypes.AccountConnection:
                await _context.AccountConnectionMessages.AddAsync((AccountConnectionMessage)message, cancellationToken);
                break;
            case MenuTypes.AccountManagement:
                await _context.AccountManagementMessages.AddAsync((AccountManagementMessage)message, cancellationToken);
                break;
            case MenuTypes.AccountHub:
                await _context.AccountHubMessages.AddAsync((AccountHubMessage)message, cancellationToken);
                break;
            case MenuTypes.SocialMediaAccount:
                await _context.SocialMediaAccountMessages.AddAsync((SocialMediaAccountMessage)message, cancellationToken);
                break;
            default:
                await _context.IntegrationMessages.AddAsync((IntegrationMessage)message, cancellationToken);
                break;
        }
    }

    public void UpdateMessage(MessageEntityBase message)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                UpdateTracked(_context.AppConnectionMessages, (AppConnectionMessage)message);
                break;
            case MenuTypes.DeveloperApp:
                UpdateTracked(_context.DeveloperAppMessages, (DeveloperAppMessage)message);
                break;
            case MenuTypes.AccountConnection:
                UpdateTracked(_context.AccountConnectionMessages, (AccountConnectionMessage)message);
                break;
            case MenuTypes.AccountManagement:
                UpdateTracked(_context.AccountManagementMessages, (AccountManagementMessage)message);
                break;
            case MenuTypes.AccountHub:
                UpdateTracked(_context.AccountHubMessages, (AccountHubMessage)message);
                break;
            case MenuTypes.SocialMediaAccount:
                UpdateTracked(_context.SocialMediaAccountMessages, (SocialMediaAccountMessage)message);
                break;
            default:
                UpdateTracked(_context.IntegrationMessages, (IntegrationMessage)message);
                break;
        }
    }

    public void RemoveMessage(MessageEntityBase message)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                RemoveTracked(_context.AppConnectionMessages, (AppConnectionMessage)message);
                break;
            case MenuTypes.DeveloperApp:
                RemoveTracked(_context.DeveloperAppMessages, (DeveloperAppMessage)message);
                break;
            case MenuTypes.AccountConnection:
                RemoveTracked(_context.AccountConnectionMessages, (AccountConnectionMessage)message);
                break;
            case MenuTypes.AccountManagement:
                RemoveTracked(_context.AccountManagementMessages, (AccountManagementMessage)message);
                break;
            case MenuTypes.AccountHub:
                RemoveTracked(_context.AccountHubMessages, (AccountHubMessage)message);
                break;
            case MenuTypes.SocialMediaAccount:
                RemoveTracked(_context.SocialMediaAccountMessages, (SocialMediaAccountMessage)message);
                break;
            default:
                RemoveTracked(_context.IntegrationMessages, (IntegrationMessage)message);
                break;
        }
    }

    public async Task AddWebhookEventAsync(WebhookEventEntityBase webhookEvent, CancellationToken cancellationToken = default)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                await _context.AppConnectionWebhookEvents.AddAsync((AppConnectionWebhookEvent)webhookEvent, cancellationToken);
                break;
            case MenuTypes.DeveloperApp:
                await _context.DeveloperAppWebhookEvents.AddAsync((DeveloperAppWebhookEvent)webhookEvent, cancellationToken);
                break;
            case MenuTypes.AccountConnection:
                await _context.AccountConnectionWebhookEvents.AddAsync((AccountConnectionWebhookEvent)webhookEvent, cancellationToken);
                break;
            case MenuTypes.AccountManagement:
                await _context.AccountManagementWebhookEvents.AddAsync((AccountManagementWebhookEvent)webhookEvent, cancellationToken);
                break;
            case MenuTypes.AccountHub:
                await _context.AccountHubWebhookEvents.AddAsync((AccountHubWebhookEvent)webhookEvent, cancellationToken);
                break;
            case MenuTypes.SocialMediaAccount:
                await _context.SocialMediaAccountWebhookEvents.AddAsync((SocialMediaAccountWebhookEvent)webhookEvent, cancellationToken);
                break;
            default:
                await _context.IntegrationWebhookEvents.AddAsync((IntegrationWebhookEvent)webhookEvent, cancellationToken);
                break;
        }
    }

    public void UpdateWebhookEvent(WebhookEventEntityBase webhookEvent)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                UpdateTracked(_context.AppConnectionWebhookEvents, (AppConnectionWebhookEvent)webhookEvent);
                break;
            case MenuTypes.DeveloperApp:
                UpdateTracked(_context.DeveloperAppWebhookEvents, (DeveloperAppWebhookEvent)webhookEvent);
                break;
            case MenuTypes.AccountConnection:
                UpdateTracked(_context.AccountConnectionWebhookEvents, (AccountConnectionWebhookEvent)webhookEvent);
                break;
            case MenuTypes.AccountManagement:
                UpdateTracked(_context.AccountManagementWebhookEvents, (AccountManagementWebhookEvent)webhookEvent);
                break;
            case MenuTypes.AccountHub:
                UpdateTracked(_context.AccountHubWebhookEvents, (AccountHubWebhookEvent)webhookEvent);
                break;
            case MenuTypes.SocialMediaAccount:
                UpdateTracked(_context.SocialMediaAccountWebhookEvents, (SocialMediaAccountWebhookEvent)webhookEvent);
                break;
            default:
                UpdateTracked(_context.IntegrationWebhookEvents, (IntegrationWebhookEvent)webhookEvent);
                break;
        }
    }

    public async Task<WebhookEventEntityBase?> GetWebhookEventByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => await _context.AppConnectionWebhookEvents
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken),
            MenuTypes.DeveloperApp => await _context.DeveloperAppWebhookEvents
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken),
            MenuTypes.AccountConnection => await _context.AccountConnectionWebhookEvents
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken),
            MenuTypes.AccountManagement => await _context.AccountManagementWebhookEvents
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken),
            MenuTypes.AccountHub => await _context.AccountHubWebhookEvents
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken),
            MenuTypes.SocialMediaAccount => await _context.SocialMediaAccountWebhookEvents
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken),
            _ => await _context.IntegrationWebhookEvents
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
        };

    public async Task<IReadOnlyList<WebhookEventEntityBase>> GetRecentWebhookEventsAsync(
        int take,
        DateTime? sinceUtc = null,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
            {
                var query = _context.AppConnectionWebhookEvents.AsQueryable();
                if (sinceUtc.HasValue)
                    query = query.Where(e => e.ReceivedAt >= sinceUtc.Value);
                return await query.OrderByDescending(e => e.ReceivedAt).Take(take).ToListAsync(cancellationToken);
            }
            case MenuTypes.DeveloperApp:
            {
                var query = _context.DeveloperAppWebhookEvents.AsQueryable();
                if (sinceUtc.HasValue)
                    query = query.Where(e => e.ReceivedAt >= sinceUtc.Value);
                return await query.OrderByDescending(e => e.ReceivedAt).Take(take).ToListAsync(cancellationToken);
            }
            case MenuTypes.AccountConnection:
            {
                var query = _context.AccountConnectionWebhookEvents.AsQueryable();
                if (sinceUtc.HasValue)
                    query = query.Where(e => e.ReceivedAt >= sinceUtc.Value);
                return await query.OrderByDescending(e => e.ReceivedAt).Take(take).ToListAsync(cancellationToken);
            }
            case MenuTypes.AccountManagement:
            {
                var query = _context.AccountManagementWebhookEvents.AsQueryable();
                if (sinceUtc.HasValue)
                    query = query.Where(e => e.ReceivedAt >= sinceUtc.Value);
                return await query.OrderByDescending(e => e.ReceivedAt).Take(take).ToListAsync(cancellationToken);
            }
            case MenuTypes.AccountHub:
            {
                var query = _context.AccountHubWebhookEvents.AsQueryable();
                if (sinceUtc.HasValue)
                    query = query.Where(e => e.ReceivedAt >= sinceUtc.Value);
                return await query.OrderByDescending(e => e.ReceivedAt).Take(take).ToListAsync(cancellationToken);
            }
            case MenuTypes.SocialMediaAccount:
            {
                var query = _context.SocialMediaAccountWebhookEvents.AsQueryable();
                if (sinceUtc.HasValue)
                    query = query.Where(e => e.ReceivedAt >= sinceUtc.Value);
                return await query.OrderByDescending(e => e.ReceivedAt).Take(take).ToListAsync(cancellationToken);
            }
            default:
            {
                var query = _context.IntegrationWebhookEvents.AsQueryable();
                if (sinceUtc.HasValue)
                    query = query.Where(e => e.ReceivedAt >= sinceUtc.Value);
                return await query.OrderByDescending(e => e.ReceivedAt).Take(take).ToListAsync(cancellationToken);
            }
        }
    }

    public async Task AddWebhookLogAsync(WebhookLogEntityBase webhookLog, CancellationToken cancellationToken = default)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                await _context.AppConnectionWebhookLogs.AddAsync((AppConnectionWebhookLog)webhookLog, cancellationToken);
                break;
            case MenuTypes.DeveloperApp:
                await _context.DeveloperAppWebhookLogs.AddAsync((DeveloperAppWebhookLog)webhookLog, cancellationToken);
                break;
            case MenuTypes.AccountConnection:
                await _context.AccountConnectionWebhookLogs.AddAsync((AccountConnectionWebhookLog)webhookLog, cancellationToken);
                break;
            case MenuTypes.AccountManagement:
                await _context.AccountManagementWebhookLogs.AddAsync((AccountManagementWebhookLog)webhookLog, cancellationToken);
                break;
            case MenuTypes.AccountHub:
                await _context.AccountHubWebhookLogs.AddAsync((AccountHubWebhookLog)webhookLog, cancellationToken);
                break;
            case MenuTypes.SocialMediaAccount:
                await _context.SocialMediaAccountWebhookLogs.AddAsync((SocialMediaAccountWebhookLog)webhookLog, cancellationToken);
                break;
            default:
                await _context.IntegrationWebhookLogs.AddAsync((IntegrationWebhookLog)webhookLog, cancellationToken);
                break;
        }
    }

    public async Task AddSyncJobAsync(SyncJobEntityBase syncJob, CancellationToken cancellationToken = default)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                await _context.AppConnectionSyncJobs.AddAsync((AppConnectionSyncJob)syncJob, cancellationToken);
                break;
            case MenuTypes.DeveloperApp:
                await _context.DeveloperAppSyncJobs.AddAsync((DeveloperAppSyncJob)syncJob, cancellationToken);
                break;
            case MenuTypes.AccountConnection:
                await _context.AccountConnectionSyncJobs.AddAsync((AccountConnectionSyncJob)syncJob, cancellationToken);
                break;
            case MenuTypes.AccountManagement:
                await _context.AccountManagementSyncJobs.AddAsync((AccountManagementSyncJob)syncJob, cancellationToken);
                break;
            case MenuTypes.AccountHub:
                await _context.AccountHubSyncJobs.AddAsync((AccountHubSyncJob)syncJob, cancellationToken);
                break;
            case MenuTypes.SocialMediaAccount:
                await _context.SocialMediaAccountSyncJobs.AddAsync((SocialMediaAccountSyncJob)syncJob, cancellationToken);
                break;
            default:
                await _context.IntegrationSyncJobs.AddAsync((IntegrationSyncJob)syncJob, cancellationToken);
                break;
        }
    }

    public PlatformEntityBase NewPlatform() => ProcessEntityFactory.NewPlatform(_menuType);
    public SocialAccountEntityBase NewSocialAccount() => ProcessEntityFactory.NewSocialAccount(_menuType);
    public SocialAuthEntityBase NewSocialAuth() => ProcessEntityFactory.NewSocialAuth(_menuType);
    public SocialProfileEntityBase NewSocialProfile() => ProcessEntityFactory.NewSocialProfile(_menuType);
    public PostEntityBase NewPost() => ProcessEntityFactory.NewPost(_menuType);
    public CommentEntityBase NewComment() => ProcessEntityFactory.NewComment(_menuType);
    public ConversationEntityBase NewConversation() => ProcessEntityFactory.NewConversation(_menuType);
    public MessageEntityBase NewMessage() => ProcessEntityFactory.NewMessage(_menuType);
    public WebhookEventEntityBase NewWebhookEvent() => ProcessEntityFactory.NewWebhookEvent(_menuType);
    public WebhookLogEntityBase NewWebhookLog() => ProcessEntityFactory.NewWebhookLog(_menuType);
    public MediaEntityBase NewMedia() => ProcessEntityFactory.NewMedia(_menuType);
    public SyncJobEntityBase NewSyncJob() => ProcessEntityFactory.NewSyncJob(_menuType);

    private static async Task<TBase?> AsBase<TDerived, TBase>(Task<TDerived?> task)
        where TDerived : class, TBase
        => await task;

    private static async Task<SocialProfileEntityBase?> QueryProfileByExternalIdAsync(
        IQueryable<AppConnectionSocialProfile> query,
        string externalProfileId,
        CancellationToken cancellationToken)
    {
        var profiles = await query
            .Include(p => p.SocialAccount).ThenInclude(a => a!.Auth)
            .Where(p => p.ExternalProfileId == externalProfileId)
            .ToListAsync(cancellationToken);
        return PickBestProfile(profiles);
    }

    private static async Task<SocialProfileEntityBase?> QueryProfileByExternalIdAsync(
        IQueryable<DeveloperAppSocialProfile> query,
        string externalProfileId,
        CancellationToken cancellationToken)
    {
        var profiles = await query
            .Include(p => p.SocialAccount).ThenInclude(a => a!.Auth)
            .Where(p => p.ExternalProfileId == externalProfileId)
            .ToListAsync(cancellationToken);
        return PickBestProfile(profiles);
    }
    private static async Task<SocialProfileEntityBase?> QueryProfileByExternalIdAsync(
        IQueryable<AccountConnectionSocialProfile> query,
        string externalProfileId,
        CancellationToken cancellationToken)
    {
        var profiles = await query
            .Include(p => p.SocialAccount).ThenInclude(a => a!.Auth)
            .Where(p => p.ExternalProfileId == externalProfileId)
            .ToListAsync(cancellationToken);
        return PickBestProfile(profiles);
    }

    private static async Task<SocialProfileEntityBase?> QueryProfileByExternalIdAsync(
        IQueryable<AccountManagementSocialProfile> query,
        string externalProfileId,
        CancellationToken cancellationToken)
    {
        var profiles = await query
            .Include(p => p.SocialAccount).ThenInclude(a => a!.Auth)
            .Where(p => p.ExternalProfileId == externalProfileId)
            .ToListAsync(cancellationToken);
        return PickBestProfile(profiles);
    }

    private static async Task<SocialProfileEntityBase?> QueryProfileByExternalIdAsync(
        IQueryable<AccountHubSocialProfile> query,
        string externalProfileId,
        CancellationToken cancellationToken)
    {
        var profiles = await query
            .Include(p => p.SocialAccount).ThenInclude(a => a!.Auth)
            .Where(p => p.ExternalProfileId == externalProfileId)
            .ToListAsync(cancellationToken);
        return PickBestProfile(profiles);
    }
    private static async Task<SocialProfileEntityBase?> QueryProfileByExternalIdAsync(
        IQueryable<SocialMediaAccountSocialProfile> query,
        string externalProfileId,
        CancellationToken cancellationToken)
    {
        var profiles = await query
            .Include(p => p.SocialAccount).ThenInclude(a => a!.Auth)
            .Where(p => p.ExternalProfileId == externalProfileId)
            .ToListAsync(cancellationToken);
        return PickBestProfile(profiles);
    }

    private static async Task<SocialProfileEntityBase?> QueryProfileByExternalIdAsync(
        IQueryable<IntegrationSocialProfile> query,
        string externalProfileId,
        CancellationToken cancellationToken)
    {
        var profiles = await query
            .Include(p => p.SocialAccount).ThenInclude(a => a!.Auth)
            .Where(p => p.ExternalProfileId == externalProfileId)
            .ToListAsync(cancellationToken);
        return PickBestProfile(profiles);
    }

    private static SocialProfileEntityBase? PickBestProfile(IEnumerable<AppConnectionSocialProfile> profiles)
        => profiles
            .OrderByDescending(p => p.SocialAccount?.Status == SocialAccountStatus.Connected ? 1 : 0)
            .ThenByDescending(p => HasStoredOAuthTokens(p.SocialAccount?.Auth) ? 1 : 0)
            .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .FirstOrDefault();

    private static SocialProfileEntityBase? PickBestProfile(IEnumerable<DeveloperAppSocialProfile> profiles)
        => profiles
            .OrderByDescending(p => p.SocialAccount?.Status == SocialAccountStatus.Connected ? 1 : 0)
            .ThenByDescending(p => HasStoredOAuthTokens(p.SocialAccount?.Auth) ? 1 : 0)
            .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .FirstOrDefault();
    private static SocialProfileEntityBase? PickBestProfile(IEnumerable<AccountConnectionSocialProfile> profiles)
        => profiles
            .OrderByDescending(p => p.SocialAccount?.Status == SocialAccountStatus.Connected ? 1 : 0)
            .ThenByDescending(p => HasStoredOAuthTokens(p.SocialAccount?.Auth) ? 1 : 0)
            .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .FirstOrDefault();
    private static SocialProfileEntityBase? PickBestProfile(IEnumerable<AccountManagementSocialProfile> profiles)
        => profiles
            .OrderByDescending(p => p.SocialAccount?.Status == SocialAccountStatus.Connected ? 1 : 0)
            .ThenByDescending(p => HasStoredOAuthTokens(p.SocialAccount?.Auth) ? 1 : 0)
            .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .FirstOrDefault();
    private static SocialProfileEntityBase? PickBestProfile(IEnumerable<AccountHubSocialProfile> profiles)
        => profiles
            .OrderByDescending(p => p.SocialAccount?.Status == SocialAccountStatus.Connected ? 1 : 0)
            .ThenByDescending(p => HasStoredOAuthTokens(p.SocialAccount?.Auth) ? 1 : 0)
            .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .FirstOrDefault();
    private static SocialProfileEntityBase? PickBestProfile(IEnumerable<SocialMediaAccountSocialProfile> profiles)
        => profiles
            .OrderByDescending(p => p.SocialAccount?.Status == SocialAccountStatus.Connected ? 1 : 0)
            .ThenByDescending(p => HasStoredOAuthTokens(p.SocialAccount?.Auth) ? 1 : 0)
            .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .FirstOrDefault();

    private static SocialProfileEntityBase? PickBestProfile(IEnumerable<IntegrationSocialProfile> profiles)
        => profiles
            .OrderByDescending(p => p.SocialAccount?.Status == SocialAccountStatus.Connected ? 1 : 0)
            .ThenByDescending(p => HasStoredOAuthTokens(p.SocialAccount?.Auth) ? 1 : 0)
            .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .FirstOrDefault();

    private static bool HasStoredOAuthTokens(SocialAuthEntityBase? auth)
        => auth is not null
           && (!string.IsNullOrWhiteSpace(auth.AccessToken) || !string.IsNullOrWhiteSpace(auth.RefreshToken));

    private static IQueryable<IntegrationComment> ApplyCommentPlatformFilter(
        IQueryable<IntegrationComment> query,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds)
    {
        if (platformId.HasValue)
            return query.Where(c => c.Post!.PlatformId == platformId.Value);
        if (platformIds is { Count: > 0 })
            return query.Where(c => platformIds.Contains(c.Post!.PlatformId));
        return query;
    }

    private static IQueryable<AppConnectionComment> ApplyCommentPlatformFilter(
        IQueryable<AppConnectionComment> query,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds)
    {
        if (platformId.HasValue)
            return query.Where(c => c.Post!.PlatformId == platformId.Value);
        if (platformIds is { Count: > 0 })
            return query.Where(c => platformIds.Contains(c.Post!.PlatformId));
        return query;
    }

    private static IQueryable<DeveloperAppComment> ApplyCommentPlatformFilter(
        IQueryable<DeveloperAppComment> query,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds)
    {
        if (platformId.HasValue)
            return query.Where(c => c.Post!.PlatformId == platformId.Value);
        if (platformIds is { Count: > 0 })
            return query.Where(c => platformIds.Contains(c.Post!.PlatformId));
        return query;
    }
    private static IQueryable<AccountConnectionComment> ApplyCommentPlatformFilter(
        IQueryable<AccountConnectionComment> query,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds)
    {
        if (platformId.HasValue)
            return query.Where(c => c.Post!.PlatformId == platformId.Value);
        if (platformIds is { Count: > 0 })
            return query.Where(c => platformIds.Contains(c.Post!.PlatformId));
        return query;
    }
    private static IQueryable<AccountManagementComment> ApplyCommentPlatformFilter(
        IQueryable<AccountManagementComment> query,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds)
    {
        if (platformId.HasValue)
            return query.Where(c => c.Post!.PlatformId == platformId.Value);
        if (platformIds is { Count: > 0 })
            return query.Where(c => platformIds.Contains(c.Post!.PlatformId));
        return query;
    }
    private static IQueryable<AccountHubComment> ApplyCommentPlatformFilter(
        IQueryable<AccountHubComment> query,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds)
    {
        if (platformId.HasValue)
            return query.Where(c => c.Post!.PlatformId == platformId.Value);
        if (platformIds is { Count: > 0 })
            return query.Where(c => platformIds.Contains(c.Post!.PlatformId));
        return query;
    }
    private static IQueryable<SocialMediaAccountComment> ApplyCommentPlatformFilter(
        IQueryable<SocialMediaAccountComment> query,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds)
    {
        if (platformId.HasValue)
            return query.Where(c => c.Post!.PlatformId == platformId.Value);
        if (platformIds is { Count: > 0 })
            return query.Where(c => platformIds.Contains(c.Post!.PlatformId));
        return query;
    }

    private static IQueryable<IntegrationMessage> ApplyMessagePlatformFilter(
        IQueryable<IntegrationMessage> query,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds)
    {
        if (platformId.HasValue)
            return query.Where(m => m.Conversation!.SocialProfile!.SocialAccount!.PlatformId == platformId.Value);
        if (platformIds is { Count: > 0 })
            return query.Where(m => platformIds.Contains(m.Conversation!.SocialProfile!.SocialAccount!.PlatformId));
        return query;
    }

    private static IQueryable<AppConnectionMessage> ApplyMessagePlatformFilter(
        IQueryable<AppConnectionMessage> query,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds)
    {
        if (platformId.HasValue)
            return query.Where(m => m.Conversation!.SocialProfile!.SocialAccount!.PlatformId == platformId.Value);
        if (platformIds is { Count: > 0 })
            return query.Where(m => platformIds.Contains(m.Conversation!.SocialProfile!.SocialAccount!.PlatformId));
        return query;
    }

    private static IQueryable<DeveloperAppMessage> ApplyMessagePlatformFilter(
        IQueryable<DeveloperAppMessage> query,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds)
    {
        if (platformId.HasValue)
            return query.Where(m => m.Conversation!.SocialProfile!.SocialAccount!.PlatformId == platformId.Value);
        if (platformIds is { Count: > 0 })
            return query.Where(m => platformIds.Contains(m.Conversation!.SocialProfile!.SocialAccount!.PlatformId));
        return query;
    }
    private static IQueryable<AccountConnectionMessage> ApplyMessagePlatformFilter(
        IQueryable<AccountConnectionMessage> query,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds)
    {
        if (platformId.HasValue)
            return query.Where(m => m.Conversation!.SocialProfile!.SocialAccount!.PlatformId == platformId.Value);
        if (platformIds is { Count: > 0 })
            return query.Where(m => platformIds.Contains(m.Conversation!.SocialProfile!.SocialAccount!.PlatformId));
        return query;
    }
    private static IQueryable<AccountManagementMessage> ApplyMessagePlatformFilter(
        IQueryable<AccountManagementMessage> query,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds)
    {
        if (platformId.HasValue)
            return query.Where(m => m.Conversation!.SocialProfile!.SocialAccount!.PlatformId == platformId.Value);
        if (platformIds is { Count: > 0 })
            return query.Where(m => platformIds.Contains(m.Conversation!.SocialProfile!.SocialAccount!.PlatformId));
        return query;
    }
    private static IQueryable<AccountHubMessage> ApplyMessagePlatformFilter(
        IQueryable<AccountHubMessage> query,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds)
    {
        if (platformId.HasValue)
            return query.Where(m => m.Conversation!.SocialProfile!.SocialAccount!.PlatformId == platformId.Value);
        if (platformIds is { Count: > 0 })
            return query.Where(m => platformIds.Contains(m.Conversation!.SocialProfile!.SocialAccount!.PlatformId));
        return query;
    }
    private static IQueryable<SocialMediaAccountMessage> ApplyMessagePlatformFilter(
        IQueryable<SocialMediaAccountMessage> query,
        Guid? platformId,
        IReadOnlyList<Guid>? platformIds)
    {
        if (platformId.HasValue)
            return query.Where(m => m.Conversation!.SocialProfile!.SocialAccount!.PlatformId == platformId.Value);
        if (platformIds is { Count: > 0 })
            return query.Where(m => platformIds.Contains(m.Conversation!.SocialProfile!.SocialAccount!.PlatformId));
        return query;
    }

    private static IReadOnlyList<InboxCommentRow> MapInboxCommentRows(IEnumerable<IntegrationComment> comments)
        => comments.Select(c => new InboxCommentRow(
            c,
            c.Post!,
            c.Post!.SocialProfile!,
            c.Post!.SocialProfile!.SocialAccount!,
            c.Post!.SocialProfile!.SocialAccount!.Platform!,
            c.Replies.Count,
            ProcessEntityNav.FirstMediaUrl(c.Post!))).ToList();

    private static IReadOnlyList<InboxCommentRow> MapInboxCommentRows(IEnumerable<AppConnectionComment> comments)
        => comments.Select(c => new InboxCommentRow(
            c,
            c.Post!,
            c.Post!.SocialProfile!,
            c.Post!.SocialProfile!.SocialAccount!,
            c.Post!.SocialProfile!.SocialAccount!.Platform!,
            c.Replies.Count,
            ProcessEntityNav.FirstMediaUrl(c.Post!))).ToList();

    private static IReadOnlyList<InboxCommentRow> MapInboxCommentRows(IEnumerable<DeveloperAppComment> comments)
        => comments.Select(c => new InboxCommentRow(
            c,
            c.Post!,
            c.Post!.SocialProfile!,
            c.Post!.SocialProfile!.SocialAccount!,
            c.Post!.SocialProfile!.SocialAccount!.Platform!,
            c.Replies.Count,
            ProcessEntityNav.FirstMediaUrl(c.Post!))).ToList();
    private static IReadOnlyList<InboxCommentRow> MapInboxCommentRows(IEnumerable<AccountConnectionComment> comments)
        => comments.Select(c => new InboxCommentRow(
            c,
            c.Post!,
            c.Post!.SocialProfile!,
            c.Post!.SocialProfile!.SocialAccount!,
            c.Post!.SocialProfile!.SocialAccount!.Platform!,
            c.Replies.Count,
            ProcessEntityNav.FirstMediaUrl(c.Post!))).ToList();
    private static IReadOnlyList<InboxCommentRow> MapInboxCommentRows(IEnumerable<AccountManagementComment> comments)
        => comments.Select(c => new InboxCommentRow(
            c,
            c.Post!,
            c.Post!.SocialProfile!,
            c.Post!.SocialProfile!.SocialAccount!,
            c.Post!.SocialProfile!.SocialAccount!.Platform!,
            c.Replies.Count,
            ProcessEntityNav.FirstMediaUrl(c.Post!))).ToList();
    private static IReadOnlyList<InboxCommentRow> MapInboxCommentRows(IEnumerable<AccountHubComment> comments)
        => comments.Select(c => new InboxCommentRow(
            c,
            c.Post!,
            c.Post!.SocialProfile!,
            c.Post!.SocialProfile!.SocialAccount!,
            c.Post!.SocialProfile!.SocialAccount!.Platform!,
            c.Replies.Count,
            ProcessEntityNav.FirstMediaUrl(c.Post!))).ToList();
    private static IReadOnlyList<InboxCommentRow> MapInboxCommentRows(IEnumerable<SocialMediaAccountComment> comments)
        => comments.Select(c => new InboxCommentRow(
            c,
            c.Post!,
            c.Post!.SocialProfile!,
            c.Post!.SocialProfile!.SocialAccount!,
            c.Post!.SocialProfile!.SocialAccount!.Platform!,
            c.Replies.Count,
            ProcessEntityNav.FirstMediaUrl(c.Post!))).ToList();

    private static IReadOnlyList<InboxMessageRow> MapInboxMessageRows(IEnumerable<IntegrationMessage> messages)
        => messages.Select(m => new InboxMessageRow(
            m,
            m.Conversation!,
            m.Conversation!.SocialProfile!,
            m.Conversation!.SocialProfile!.SocialAccount!,
            m.Conversation!.SocialProfile!.SocialAccount!.Platform!)).ToList();

    private static IReadOnlyList<InboxMessageRow> MapInboxMessageRows(IEnumerable<AppConnectionMessage> messages)
        => messages.Select(m => new InboxMessageRow(
            m,
            m.Conversation!,
            m.Conversation!.SocialProfile!,
            m.Conversation!.SocialProfile!.SocialAccount!,
            m.Conversation!.SocialProfile!.SocialAccount!.Platform!)).ToList();

    private static IReadOnlyList<InboxMessageRow> MapInboxMessageRows(IEnumerable<DeveloperAppMessage> messages)
        => messages.Select(m => new InboxMessageRow(
            m,
            m.Conversation!,
            m.Conversation!.SocialProfile!,
            m.Conversation!.SocialProfile!.SocialAccount!,
            m.Conversation!.SocialProfile!.SocialAccount!.Platform!)).ToList();
    private static IReadOnlyList<InboxMessageRow> MapInboxMessageRows(IEnumerable<AccountConnectionMessage> messages)
        => messages.Select(m => new InboxMessageRow(
            m,
            m.Conversation!,
            m.Conversation!.SocialProfile!,
            m.Conversation!.SocialProfile!.SocialAccount!,
            m.Conversation!.SocialProfile!.SocialAccount!.Platform!)).ToList();
    private static IReadOnlyList<InboxMessageRow> MapInboxMessageRows(IEnumerable<AccountManagementMessage> messages)
        => messages.Select(m => new InboxMessageRow(
            m,
            m.Conversation!,
            m.Conversation!.SocialProfile!,
            m.Conversation!.SocialProfile!.SocialAccount!,
            m.Conversation!.SocialProfile!.SocialAccount!.Platform!)).ToList();
    private static IReadOnlyList<InboxMessageRow> MapInboxMessageRows(IEnumerable<AccountHubMessage> messages)
        => messages.Select(m => new InboxMessageRow(
            m,
            m.Conversation!,
            m.Conversation!.SocialProfile!,
            m.Conversation!.SocialProfile!.SocialAccount!,
            m.Conversation!.SocialProfile!.SocialAccount!.Platform!)).ToList();
    private static IReadOnlyList<InboxMessageRow> MapInboxMessageRows(IEnumerable<SocialMediaAccountMessage> messages)
        => messages.Select(m => new InboxMessageRow(
            m,
            m.Conversation!,
            m.Conversation!.SocialProfile!,
            m.Conversation!.SocialProfile!.SocialAccount!,
            m.Conversation!.SocialProfile!.SocialAccount!.Platform!)).ToList();

    public async Task<IReadOnlyList<SocialAccountEntityBase>> FindConnectedSocialAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        return _menuType switch
        {
            MenuTypes.AppConnection => (IReadOnlyList<SocialAccountEntityBase>)await _context.AppConnectionSocialAccounts.AsNoTracking()
                .Where(a => a.Status == SocialAccountStatus.Connected)
                .ToListAsync(cancellationToken),
            MenuTypes.DeveloperApp => (IReadOnlyList<SocialAccountEntityBase>)await _context.DeveloperAppSocialAccounts.AsNoTracking()
                .Where(a => a.Status == SocialAccountStatus.Connected)
                .ToListAsync(cancellationToken),
            MenuTypes.AccountConnection => (IReadOnlyList<SocialAccountEntityBase>)await _context.AccountConnectionSocialAccounts.AsNoTracking()
                .Where(a => a.Status == SocialAccountStatus.Connected)
                .ToListAsync(cancellationToken),
            MenuTypes.AccountManagement => (IReadOnlyList<SocialAccountEntityBase>)await _context.AccountManagementSocialAccounts.AsNoTracking()
                .Where(a => a.Status == SocialAccountStatus.Connected)
                .ToListAsync(cancellationToken),
            MenuTypes.AccountHub => (IReadOnlyList<SocialAccountEntityBase>)await _context.AccountHubSocialAccounts.AsNoTracking()
                .Where(a => a.Status == SocialAccountStatus.Connected)
                .ToListAsync(cancellationToken),
            MenuTypes.SocialMediaAccount => (IReadOnlyList<SocialAccountEntityBase>)await _context.SocialMediaAccountSocialAccounts.AsNoTracking()
                .Where(a => a.Status == SocialAccountStatus.Connected)
                .ToListAsync(cancellationToken),
            _ => (IReadOnlyList<SocialAccountEntityBase>)await _context.IntegrationSocialAccounts.AsNoTracking()
                .Where(a => a.Status == SocialAccountStatus.Connected)
                .ToListAsync(cancellationToken)
        };
    }

    public async Task<SocialProfileEntityBase?> PickBestProfileForAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var profiles = await GetProfilesByAccountAsync(accountId, cancellationToken);
        return profiles
            .OrderByDescending(p => p.ProfileType == ProfileType.InstagramBusiness ? 1 : 0)
            .ThenByDescending(p => p.ProfileType == ProfileType.InstagramLogin ? 1 : 0)
            .ThenByDescending(p => p.ProfileType == ProfileType.FacebookPage ? 1 : 0)
            .FirstOrDefault();
    }

    /// <summary>
    /// Applies updates to an entity already tracked in this context, avoiding duplicate-key tracking errors
    /// when callers load rows with AsNoTracking (often with Include navigations) and then call Update.
    /// </summary>
    private void UpdateTracked<TEntity>(DbSet<TEntity> set, TEntity entity)
        where TEntity : BaseEntity
    {
        var tracked = set.Local.FirstOrDefault(e => e.Id == entity.Id);
        if (tracked is not null)
        {
            _context.Entry(tracked).CurrentValues.SetValues(entity);
            return;
        }

        var entry = _context.Entry(entity);
        foreach (var navigation in entry.Navigations)
            navigation.CurrentValue = null;
        foreach (var collection in entry.Collections)
            collection.CurrentValue = null;

        tracked = set.Local.FirstOrDefault(e => e.Id == entity.Id);
        if (tracked is not null)
        {
            _context.Entry(tracked).CurrentValues.SetValues(entity);
            return;
        }

        set.Attach(entity);
        entry.State = EntityState.Modified;
    }

    private void RemoveTrackedById<TEntity>(DbSet<TEntity> set, Guid id)
        where TEntity : BaseEntity
    {
        var tracked = set.Local.FirstOrDefault(e => e.Id == id) ?? set.Find(id);
        if (tracked is not null)
            set.Remove(tracked);
    }

    private static void RemoveTracked<TEntity>(DbSet<TEntity> set, TEntity entity)
        where TEntity : BaseEntity
    {
        var tracked = set.Local.FirstOrDefault(e => e.Id == entity.Id) ?? set.Find(entity.Id);
        if (tracked is not null)
            set.Remove(tracked);
    }
}

public sealed class ProcessDataStoreFactory : IProcessDataStoreFactory
{
    private readonly AppDbContext _context;
    private IReadOnlyList<IProcessDataStore>? _allStores;

    public ProcessDataStoreFactory(AppDbContext context)
    {
        _context = context;
    }

    public IProcessDataStore ForMenu(string menuType) => new ProcessDataStore(_context, menuType);

    public IReadOnlyList<IProcessDataStore> AllStores()
        => _allStores ??=
        [
            new ProcessDataStore(_context, MenuTypes.Integration),
            new ProcessDataStore(_context, MenuTypes.AppConnection),
            new ProcessDataStore(_context, MenuTypes.DeveloperApp),
            new ProcessDataStore(_context, MenuTypes.SocialMediaAccount),
            new ProcessDataStore(_context, MenuTypes.AccountConnection),
            new ProcessDataStore(_context, MenuTypes.AccountManagement),
            new ProcessDataStore(_context, MenuTypes.AccountHub)
        ];
}
