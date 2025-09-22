// waabe_navi_mcpserver/Services/Implementations/SelectionService.cs
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using waabe_navi_mcp_server.Contracts;
using waabe_navi_mcp_server.Services.Backends;
using waabe_navi_shared;

namespace waabe_navi_mcp_server.Services.Implementations
{
    /// <summary>
    /// Provides services for managing the current selection in Navisworks.
    /// - Wraps selection operations (apply, clear, snapshot).
    /// - Delegates execution to the configured <see cref="IWaabeNavisworksBackend"/>.
    /// </summary> 
    public sealed class SelectionService : ISelectionService
    {
        private static IWaabeNavisworksBackend BE => BackendResolver.Instance;

        /// <summary>
        /// Applies a selection to the current document.
        /// - Items are identified by their <paramref name="canonical_id"/> list.
        /// - If <paramref name="keepExistingSelection"/> is true, the new selection
        ///   is added to the existing one; otherwise, the selection is replaced.
        /// - Returns all items that were successfully applied.
        /// </summary> 
        public async Task<List<SimpleItemRef>> ApplySelectionAsync(List<string> canonical_id, bool keepExistingSelection, CancellationToken ct)
        {
             
            LogHelper.LogInfo($"SelectionService.ApplySelectionAsync(count={canonical_id.Count}, keepExistSelection: {keepExistingSelection})");
            var affected = await BE.ApplySelectionAsync(canonical_id, keepExistingSelection, ct).ConfigureAwait(false);
            return affected;  
        }

        /// <summary>
        /// Clears the current selection in the document.
        /// - Cancels all previously applied selections.
        /// - Returns an <see cref="ApplyResultDto"/> with the number of cleared items
        ///   and a status message.
        /// </summary>
        public async Task<ApplyResultDto> ClearSelectionAsync(CancellationToken ct)
        {
            LogHelper.LogInfo("SelectionService.ClearSelectionAsync()");
            var affected = await BE.ClearSelectionAsync(ct).ConfigureAwait(false);
            return new ApplyResultDto { affected = affected, message = "Selection cleared." };
        }

        /// <summary>
        /// Retrieves a snapshot of the current selection.
        /// - Includes canonical IDs and display paths of all selected items.
        /// </summary> 
        public Task<SelectionSnapshotDto> GetSnapshotAsync(CancellationToken ct)
        {
            LogHelper.LogDebug("SelectionService.GetSnapshotAsync()");
            return BE.GetCurrentSelectionSnapshotAsync(ct);
        }
    }
}
