// waabe_navi_mcpserver/Services/Backends/IWaabeNavisworksBackend.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using waabe_navi_mcp_server.Contracts;
using waabe_navi_mcp_server.Services;

namespace waabe_navi_mcp_server.Services.Backends
{
    /// <summary>
    ///  Abstraction for Navisworks backends (reflection into waabe_navi_mcp or fallback).
    /// </summary>
    public interface IWaabeNavisworksBackend
    {
        /// <summary>
        /// Returns an overview of all currently loaded models, including categories and counts.
        /// </summary>
        Task<ModelOverviewDto> GetModelOverviewAsync(CancellationToken ct);

        /// <summary>
        /// Lists all models/submodels available in the current document.
        /// </summary>
        Task<DtoList<ModelDetailDto>> ListModelsAsync(CancellationToken ct);

        /// <summary>
        /// Gets the current selection snapshot (canonical IDs and paths).
        /// </summary>
        Task<SelectionSnapshotDto> GetCurrentSelectionSnapshotAsync(CancellationToken ct);

        /// <summary>
        /// Applies a selection of items by their canonical IDs.
        /// </summary>
        /// <param name="itemIds">Canonical IDs of items to select.</param>
        /// <param name="keepExistingSelection">True to add to the current selection, false to replace it.</param>
        Task<List<SimpleItemRef>> ApplySelectionAsync(List<string> itemIds, bool keepExistingSelection, CancellationToken ct);

        /// <summary>
        /// Clears the current selection.
        /// </summary>
        Task<int> ClearSelectionAsync(CancellationToken ct);

        /// <summary>
        /// Returns all properties of a single item given its canonical ID.
        /// </summary>
        Task<ItemPropertiesDto> Get_ListProperties_For_Item(string itemId, CancellationToken ct);

        /// <summary>
        /// Returns unit and tolerance information from the current document.
        /// </summary>
        Task<UnitInfoDto> GetUnitsAndTolerancesAsync(CancellationToken ct);

        /// <summary>
        /// Counts elements belonging to a given category within the specified scope.
        /// </summary>
        Task<ElementCountDto> GetElementCountByCategoryAsync(string category, string scope, CancellationToken ct);

        /// <summary>
        /// Returns a property distribution grouped by category.
        /// </summary>
        Task<ElementCountDto> GetPropertyDistributionByCategoryAsync( CancellationToken ct);

        /// <summary>
        /// Lists items by matching a property value within a category.
        /// </summary>
        Task<PropertyItemListDto> ListItemsToPropertyAsync(ListItemsToPropertyArgs args, CancellationToken ct);

        /// <summary>
        /// Runs a clash detection test between two scopes with the given tolerance.
        /// </summary>
        Task<ClashSummaryDto> RunClashAsync(ClashRunArgs args, CancellationToken ct);
        
    }
}
