// Services/ISelectionService.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using waabe_navi_mcp_server.Contracts;

namespace waabe_navi_mcp_server.Services
{
    /// <summary>
    /// Provides operations for applying, clearing, and inspecting selections in Navisworks models.
    /// </summary>
    public interface ISelectionService
    {
        /// <summary>
        /// Applies a selection of model items based on their canonical IDs.
        /// </summary>
        /// <param name="canonical_id">List of canonical IDs of items to be selected.</param>
        /// <param name="keepExistingSelection">
        /// If true, keeps the existing selection and adds the new items.  
        /// If false, clears the current selection before applying.
        /// </param>
        /// <param name="ct">Cancellation token to cancel the operation.</param>
        /// <returns>
        /// A list of <see cref="SimpleItemRef"/> representing the items that were successfully selected.
        /// </returns>
        Task<List<SimpleItemRef>> ApplySelectionAsync(List<string> canonical_id, bool keepExistingSelection, CancellationToken ct);
        
        /// <summary>
        /// Clears the current selection of model items.
        /// </summary>
        /// <param name="ct">Cancellation token to cancel the operation.</param>
        /// <returns>
        /// An <see cref="ApplyResultDto"/> containing the number of items cleared and a status message.
        /// </returns>
        Task<ApplyResultDto> ClearSelectionAsync(CancellationToken ct);

        /// <summary>
        /// Gets a snapshot of the current selection, including canonical IDs and path information.
        /// </summary>
        /// <param name="ct">Cancellation token to cancel the operation.</param>
        /// <returns>
        /// A <see cref="SelectionSnapshotDto"/> containing details of the current selection.
        /// </returns>
        Task<SelectionSnapshotDto> GetSnapshotAsync(CancellationToken ct);
    }
}
