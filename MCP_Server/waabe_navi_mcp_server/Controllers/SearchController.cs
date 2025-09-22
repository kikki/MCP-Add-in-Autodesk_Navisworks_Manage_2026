// Controllers/SearchController.cs
using System.Collections.Generic;
using System.Threading;
using System.Web.Script.Serialization;
using waabe_navi_mcp_server.Contracts;
using waabe_navi_mcp_server.Infrastructure;
using waabe_navi_mcp_server.Services;
using waabe_navi_mcp_server.Services.Implementations;
using static waabe_navi_mcp_server.Infrastructure.ErrorHandlingMiddleware;

namespace waabe_navi_mcp_server.Controllers
{
    /// <summary>
    /// Controller for handling RPC requests related to searching elements and properties.
    /// - Provides element counts by category.
    /// - Lists items that belong to a given property value.
    /// - Retrieves all properties for a specific item.
    /// - Wraps service calls with error handling and standardized RPC responses.
    /// </summary>
    public sealed class SearchController
    {
        private readonly ISearchService _svc = new SearchService();
        private static readonly JavaScriptSerializer _jss = new JavaScriptSerializer();

        /// <summary>
        /// RPC method: "get_count_by_category"
        /// - Returns the number of elements for a given category and scope.
        /// - Input: CategoryQuery (category + scope).
        /// - Output: RpcResponse&lt;ElementCountDto&gt;.
        /// Example: "How many walls exist in this model?"
        /// </summary>
        public object GetCountByCategory(RpcRequest req)
        {
            return Wrap(() =>
            {
                var q = _jss.ConvertToType<CategoryQuery>(req.@params);
                var cts = new CancellationTokenSource(Settings.DefaultTimeoutMs);
                var dto = _svc.GetCountByCategoryAsync(q, cts.Token).GetAwaiter().GetResult();
                return RpcResponse<ElementCountDto>.Success(dto);
            });
        }

        /// <summary>
        /// RPC method: "list_items_to_property"
        /// - Returns items that match a given property/value filter.
        /// - Input: ListItemsToPropertyArgs (category, property, filters).
        /// - Output: RpcResponse&lt;PropertyItemListDto&gt;.
        /// Example: "List all elements where property Material contains 'Steel'."
        /// </summary>
        public object ListItemsToProperty(RpcRequest req)
        {
            return Wrap(() =>
            {
                var args = _jss.ConvertToType<ListItemsToPropertyArgs>(req.@params);
                var cts = new CancellationTokenSource(Settings.DefaultTimeoutMs);
                var dto = _svc.ListItemsToPropertyAsync(args, cts.Token).GetAwaiter().GetResult();
                return RpcResponse<PropertyItemListDto>.Success(dto);
            });
        }

        /// <summary>
        /// RPC method: "list_properties_for_item"
        /// - Returns all properties for a given item.
        /// - Input: ItemRef (item_id).
        /// - Output: RpcResponse&lt;ItemPropertiesDto&gt;.
        /// Example: "Get all properties of element with ID = p:12345."
        /// </summary>
        public object ListPropertiesForItem(RpcRequest req)
        {
            return Wrap(() =>
            {
                var p = _jss.ConvertToType<ItemRef>(req.@params);
                var cts = new CancellationTokenSource(Settings.DefaultTimeoutMs);
                var dto = _svc.GetItemPropertiesAsync(p.item_id, cts.Token).GetAwaiter().GetResult();
                return RpcResponse<ItemPropertiesDto>.Success(dto);
            });
        }
    }
}
