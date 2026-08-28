using SocialMedia.Application.Interfaces;

namespace SocialMedia.Application.Meta;

/// <summary>
/// Finds entities in a single process store (module endpoints) or across all stores when menu is unknown.
/// </summary>
public static class ProcessStoreLocator
{
    public static async Task<(IProcessDataStore Store, T Entity)?> FindInMenuAsync<T>(
        IProcessDataStoreFactory factory,
        string menuType,
        Func<IProcessDataStore, Task<T?>> finder,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var store = factory.ForMenu(menuType);
        var entity = await finder(store);
        return entity is null ? null : (store, entity);
    }

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
