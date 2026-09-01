using Microsoft.EntityFrameworkCore;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Common;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.Common.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.Integrations.Entities;
using SocialMedia.Infrastructure.Persistence;

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
            _ => AsBase<IntegrationSocialProfile, SocialProfileEntityBase>(
                _context.IntegrationSocialProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken))
        };

    public Task<SocialProfileEntityBase?> GetProfileByExternalIdAsync(
        string externalProfileId,
        CancellationToken cancellationToken = default)
        => _menuType switch
        {
            MenuTypes.AppConnection => QueryProfileByExternalIdAsync(_context.AppConnectionSocialProfiles, externalProfileId, cancellationToken),
            MenuTypes.DeveloperApp => QueryProfileByExternalIdAsync(_context.DeveloperAppSocialProfiles, externalProfileId, cancellationToken),
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
            default:
                UpdateTracked(_context.IntegrationSocialProfiles, (IntegrationSocialProfile)profile);
                break;
        }
    }

    public void RemoveSocialProfile(SocialProfileEntityBase profile)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                RemoveTracked(_context.AppConnectionSocialProfiles, (AppConnectionSocialProfile)profile);
                break;
            case MenuTypes.DeveloperApp:
                RemoveTracked(_context.DeveloperAppSocialProfiles, (DeveloperAppSocialProfile)profile);
                break;
            default:
                RemoveTracked(_context.IntegrationSocialProfiles, (IntegrationSocialProfile)profile);
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
            _ => AsBase<IntegrationPost, PostEntityBase>(
                _context.IntegrationPosts.AsNoTracking()
                    .Include(p => p.MediaItems)
                    .FirstOrDefaultAsync(p => p.SocialProfileId == socialProfileId && p.ExternalPostId == externalPostId, cancellationToken))
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
            default:
                await _context.IntegrationPosts.AddAsync((IntegrationPost)post, cancellationToken);
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
            default:
                UpdateTracked(_context.IntegrationPosts, (IntegrationPost)post);
                break;
        }
    }

    public void RemovePost(PostEntityBase post)
    {
        switch (_menuType)
        {
            case MenuTypes.AppConnection:
                RemoveTracked(_context.AppConnectionPosts, (AppConnectionPost)post);
                break;
            case MenuTypes.DeveloperApp:
                RemoveTracked(_context.DeveloperAppPosts, (DeveloperAppPost)post);
                break;
            default:
                RemoveTracked(_context.IntegrationPosts, (IntegrationPost)post);
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
            default:
                UpdateTracked(_context.IntegrationWebhookEvents, (IntegrationWebhookEvent)webhookEvent);
                break;
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

    private static IReadOnlyList<InboxCommentRow> MapInboxCommentRows(IEnumerable<IntegrationComment> comments)
        => comments.Select(c => new InboxCommentRow(
            c,
            c.Post!,
            c.Post!.SocialProfile!,
            c.Post!.SocialProfile!.SocialAccount!,
            c.Post!.SocialProfile!.SocialAccount!.Platform!,
            c.Replies.Count,
            c.Post!.MediaItems.FirstOrDefault()?.Url)).ToList();

    private static IReadOnlyList<InboxCommentRow> MapInboxCommentRows(IEnumerable<AppConnectionComment> comments)
        => comments.Select(c => new InboxCommentRow(
            c,
            c.Post!,
            c.Post!.SocialProfile!,
            c.Post!.SocialProfile!.SocialAccount!,
            c.Post!.SocialProfile!.SocialAccount!.Platform!,
            c.Replies.Count,
            c.Post!.MediaItems.FirstOrDefault()?.Url)).ToList();

    private static IReadOnlyList<InboxCommentRow> MapInboxCommentRows(IEnumerable<DeveloperAppComment> comments)
        => comments.Select(c => new InboxCommentRow(
            c,
            c.Post!,
            c.Post!.SocialProfile!,
            c.Post!.SocialProfile!.SocialAccount!,
            c.Post!.SocialProfile!.SocialAccount!.Platform!,
            c.Replies.Count,
            c.Post!.MediaItems.FirstOrDefault()?.Url)).ToList();

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

    private static void RemoveTracked<TEntity>(DbSet<TEntity> set, TEntity entity)
        where TEntity : BaseEntity
    {
        var tracked = set.Local.FirstOrDefault(e => e.Id == entity.Id);
        set.Remove(tracked ?? entity);
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
            new ProcessDataStore(_context, MenuTypes.DeveloperApp)
        ];
}
