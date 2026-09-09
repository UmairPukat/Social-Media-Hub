using SocialMedia.Application.Meta;

namespace SocialMedia.Application.Interfaces;

public interface IMetaMarketingContextFactory
{
    Task<MetaMarketingContext> CreateAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken = default);
}
