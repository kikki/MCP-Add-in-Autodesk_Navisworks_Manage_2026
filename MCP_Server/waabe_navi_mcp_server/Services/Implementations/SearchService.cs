// waabe_navi_mcpserver/Services/Implementations/SearchService.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using waabe_navi_mcp_server.Contracts;
using waabe_navi_mcp_server.Services;
using waabe_navi_mcp_server.Services.Backends;
using waabe_navi_shared;

namespace waabe_navi_mcp_server.Services.Implementations
{
    /// <summary>
    /// Provides search-related queries for Navisworks models.
    /// - Acts as a wrapper around <see cref="IWaabeNavisworksBackend"/>.
    /// - Handles logging and delegates execution to the backend.
    /// </summary>
    public sealed class SearchService : ISearchService
    {
        private static IWaabeNavisworksBackend BE => BackendResolver.Instance;

        /// <summary>
        /// Retrieves the count of elements matching a given category within a defined scope.
        /// - Uses <paramref name="q"/> to specify category and scope.
        /// - Cancellation is supported via <paramref name="ct"/>.
        /// </summary> 
        public Task<ElementCountDto> GetCountByCategoryAsync(CategoryQuery q, CancellationToken ct)
        {
            LogHelper.LogDebug($"SearchService.GetCountByCategoryAsync(category={q?.category}, scope={q?.scope})");
            return BE.GetElementCountByCategoryAsync(q.category, q.scope, ct);
        }

        /// <summary>
        /// Retrieves all properties for a given item.
        /// - Identified by its canonical <paramref name="itemId"/>.
        /// - Returns metadata, categories, geometry info, and path.
        /// - Cancellation is supported via <paramref name="ct"/>.
        /// </summary> 
        public Task<ItemPropertiesDto> GetItemPropertiesAsync(string itemId, CancellationToken ct)
        {
            LogHelper.LogDebug($"SearchService.GetItemPropertiesAsync(itemId={itemId})");
            return BE.Get_ListProperties_For_Item(itemId, ct);
        }

        /// <summary>
        /// Lists items that match a property/value condition.
        /// - Controlled via <paramref name="args"/> (category, property, filters).
        /// - Returns all matching items with canonical IDs and property values.
        /// - Cancellation is supported via <paramref name="ct"/>.
        /// </summary>
        public Task<PropertyItemListDto> ListItemsToPropertyAsync(ListItemsToPropertyArgs args, CancellationToken ct)
        {
            LogHelper.LogDebug($"SearchService.ListItemsToPropertyAsync(property={args?.property}, model={args?.ModelFilter}, valueFilter={args?.ValueFilter})");
            return BE.ListItemsToPropertyAsync(args, ct);
        }
        


    }
}
