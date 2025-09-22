// Services/ISearchService.cs
using System.Threading;
using System.Threading.Tasks;
using waabe_navi_mcp_server.Contracts;
using System.Collections.Generic;

namespace waabe_navi_mcp_server.Services
{
    /// <summary>
    /// Defines search operations for elements and properties in Navisworks models.
    /// Provides functionality for counting, inspecting, and filtering model items.
    /// </summary>
    public interface ISearchService
    {
        /// <summary>
        /// Counts elements that match a given category and scope within the current model(s).
        /// </summary>
        /// <param name="q">Category query containing category and scope parameters.</param>
        /// <param name="ct">Cancellation token to cancel the operation.</param>
        /// <returns>Count information as <see cref="ElementCountDto"/>.</returns>
        Task<ElementCountDto> GetCountByCategoryAsync(CategoryQuery q, CancellationToken ct);

        /// <summary>
        /// Retrieves all properties of a specific item identified by its canonical ID.
        /// </summary>
        /// <param name="itemId">Canonical ID of the item.</param>
        /// <param name="ct">Cancellation token to cancel the operation.</param>
        /// <returns>Detailed property information as <see cref="ItemPropertiesDto"/>.</returns>
        Task<ItemPropertiesDto> GetItemPropertiesAsync(string itemId, CancellationToken ct);

        /// <summary>
        /// Lists items that expose a given property within the defined scope and filter criteria.
        /// </summary>
        /// <param name="args">Arguments including category, property name, filters, and scope.</param>
        /// <param name="ct">Cancellation token to cancel the operation.</param>
        /// <returns>A list of items and their property values as <see cref="PropertyItemListDto"/>.</returns>
        Task<PropertyItemListDto> ListItemsToPropertyAsync(ListItemsToPropertyArgs args, CancellationToken ct);

    }
}
