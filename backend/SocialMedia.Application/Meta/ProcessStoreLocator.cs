using SocialMedia.Application.Interfaces;

namespace SocialMedia.Application.Meta;

/// <summary>
/// Finds entities across all process stores when the owning menu is unknown.
/// </summary>
public static class ProcessStoreLocator
{
    public static async Task<(IProcessDataStore Store, T Entity)?> FindAsync<T>(
        IProcessDataStoreFactory factory,
        Func<IProcessDataStore, Task<T?>> finder,
        CancellationToken cancellationToken = default)
        where T : class
    {
        foreach (var store in factory.AllStores())
        {
            var entity = await finder(store);
            if (entity is not null)
                return (store, entity);
        }

        return null;
    }
}
