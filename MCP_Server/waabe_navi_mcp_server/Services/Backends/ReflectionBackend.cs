// waabe_navi_mcpserver/Services/Backends/ReflectionBackend.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using waabe_navi_mcp_server.Contracts;
using waabe_navi_mcp_server.Services;
using waabe_navi_mcp_server.Services.Backends;
using waabe_navi_shared;

namespace waabe_navi_mcp_server.Services.Backends
{
    /// <summary>
    /// Provides a dynamic Navisworks backend that tries to call real implementations
    /// from <c>waabe_navi_mcp</c> via UI thread if available, otherwise falls back
    /// to <see cref="FallbackBackend"/>.
    /// 
    /// Behavior:
    /// - Wraps all <see cref="IWaabeNavisworksBackend"/> methods.
    /// - If <see cref="UiThread.IsInitialized"/> is true, execution is preferred on the UI thread.
    /// - On failure or when UI thread is not initialized, methods are delegated to the fallback backend.
    /// - Reflection helpers (<c>CallStaticAsync</c>, <c>CallStaticIntAsync</c>) exist to enable
    ///   optional direct binding to static methods in waabe_navi_mcp (not yet wired).
    /// 
    /// Usage:
    /// - Acts as a decorator around <see cref="FallbackBackend"/>.
    /// - Used by <see cref="BackendResolver"/> to prefer reflection if available.
    /// - Suitable for both UI-sensitive calls (e.g. selections, clashes) and
    ///   data queries (e.g. model overview, properties).
    /// 
    /// Notes:
    /// - <see cref="IsAvailable"/> returns true if the UI thread is initialized.
    /// - Logging is provided for both success and fallback execution paths.
    /// </summary>
    public sealed class ReflectionBackend : IWaabeNavisworksBackend
    {
           

         
        private static Task<T> OnUi<T>(Func<Task<T>> func)
            => UiThread.IsInitialized ? UiThread.InvokeAsync(func) : func();  

        private async Task<T> TryUiCall<T>(
         Func<Task<T>> fbCall,                    
         Func<Task<T>> fbUiPreferredCall,         
         string opName)
        {
            if (UiThread.IsInitialized)
            {
                try
                {
                    LogHelper.LogDebug($"[ReflectionBackend::{opName}] Trying UI-path...");
                    var result = await OnUi(fbUiPreferredCall).ConfigureAwait(false);
                    LogHelper.LogDebug($"[ReflectionBackend::{opName}] UI-path succeeded.");
                    return result;
                }
                catch (Exception ex)
                {
                    LogHelper.LogWarning($"[ReflectionBackend::{opName}] UI-path failed → fallback. {ex.Message}");
                }
            }
            else
            {
                LogHelper.LogDebug($"[ReflectionBackend::{opName}] UiThread not initialized → fallback.");
            }

             
            LogHelper.LogDebug($"[ReflectionBackend::{opName}] Executing plain fallback.");
            return await fbCall().ConfigureAwait(false);
        }

        private readonly IWaabeNavisworksBackend _fb = new FallbackBackend();

        
        public bool IsAvailable => UiThread.IsInitialized; 

        public ReflectionBackend()  { }

        private static async Task<T> CallStaticAsync<T>(Type type, string method, object[] args)
        {
            if (type == null) return default(T);
            var mi = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            if (mi == null) return default(T);

            var result = mi.Invoke(null, args);
            if (result is Task<T> tt) return await tt.ConfigureAwait(false);
            if (result is Task t) { await t.ConfigureAwait(false); return default(T); }
            return (T)result;
        }

        private static async Task<int> CallStaticIntAsync(Type type, string method, object[] args)
        {
            var obj = await CallStaticAsync<object>(type, method, args).ConfigureAwait(false);
            return obj is int i ? i : 0;
        }

        // ===== Model / Structure =====

        /// <summary>
        /// Retrieves an overview of all loaded models.
        /// - Preferred execution on UI thread if available.
        /// - Falls back to <see cref="FallbackBackend.GetModelOverviewAsync"/>.
        /// </summary>
        public Task<ModelOverviewDto> GetModelOverviewAsync(CancellationToken ct)
              => TryUiCall(
                  fbCall: () => _fb.GetModelOverviewAsync(ct),
                  fbUiPreferredCall: () => _fb.GetModelOverviewAsync(ct),
                  opName: nameof(GetModelOverviewAsync));

        /// <summary>
        /// Lists all sub-models (root-level entries) within the current document.
        /// - Uses fallback implementation if reflection/UI call is not possible.
        /// </summary>
        public Task<DtoList<ModelDetailDto>> ListModelsAsync(CancellationToken ct)
              => TryUiCall(
                  fbCall: () => _fb.ListModelsAsync(ct),
                  fbUiPreferredCall: () => _fb.ListModelsAsync(ct),
                  opName: nameof(ListModelsAsync));

        /// <summary>
        /// Retrieves current unit settings and tolerances of the active document.
        /// - Returns units (length, area, volume) and tolerance information.
        /// </summary>
        public Task<UnitInfoDto> GetUnitsAndTolerancesAsync(CancellationToken ct)
              => TryUiCall(
                  fbCall: () => _fb.GetUnitsAndTolerancesAsync(ct),
                  fbUiPreferredCall: () => _fb.GetUnitsAndTolerancesAsync(ct),
                  opName: nameof(GetUnitsAndTolerancesAsync));

        /// <summary>
        /// Computes distribution of property categories across all models.
        /// - Aggregates categories, properties, and element counts.
        /// - Delegates to fallback if reflection/UI path is unavailable.
        /// </summary>
        public Task<ElementCountDto> GetPropertyDistributionByCategoryAsync(CancellationToken ct)
            => TryUiCall(
                fbCall: () => _fb.GetPropertyDistributionByCategoryAsync(ct),
                fbUiPreferredCall: () => _fb.GetPropertyDistributionByCategoryAsync(ct),
                opName: nameof(GetPropertyDistributionByCategoryAsync));


        // ===== Search / Properties =====

        /// <summary>
        /// Counts all elements matching the specified category within the given scope.
        /// - Scope can be "all" or restricted to specific model roots.
        /// </summary>
        public Task<ElementCountDto> GetElementCountByCategoryAsync(string category, string scope, CancellationToken ct)
            => TryUiCall(
                fbCall: () => _fb.GetElementCountByCategoryAsync(category, scope, ct),
                fbUiPreferredCall: () => _fb.GetElementCountByCategoryAsync(category, scope, ct),
                opName: nameof(GetElementCountByCategoryAsync));

        /// <summary>
        /// Retrieves all property categories and values for a specific item by canonical ID.
        /// - Returns <see cref="ItemPropertiesDto"/> with metadata and geometry info.
        /// </summary>
        public Task<ItemPropertiesDto> Get_ListProperties_For_Item(string itemId, CancellationToken ct)
              => TryUiCall(
                  fbCall: () => _fb.Get_ListProperties_For_Item(itemId, ct),
                  fbUiPreferredCall: () => _fb.Get_ListProperties_For_Item(itemId, ct),
                  opName: nameof(Get_ListProperties_For_Item));

        /// <summary>
        /// Lists items that match a given category/property/value combination.
        /// - Supports filtering by model and property values (with predicates).
        /// </summary>
        public Task<PropertyItemListDto> ListItemsToPropertyAsync(ListItemsToPropertyArgs args, CancellationToken ct)
              => TryUiCall(
                  fbCall: () => _fb.ListItemsToPropertyAsync(args, ct),
                  fbUiPreferredCall: () => _fb.ListItemsToPropertyAsync(args, ct),
                  opName: nameof(ListItemsToPropertyAsync));

        /// <summary>
        /// Applies a selection of items by their canonical IDs.
        /// - Optionally preserves existing selection or replaces it.
        /// - Returns list of successfully selected items.
        /// </summary>
        public Task<List<SimpleItemRef>> ApplySelectionAsync(List<string> itemIds, bool keep, CancellationToken ct)
            => TryUiCall(
                fbCall: () => _fb.ApplySelectionAsync(itemIds, keep, ct),
                fbUiPreferredCall: () => _fb.ApplySelectionAsync(itemIds, keep, ct),
                opName: nameof(ApplySelectionAsync));

        /// <summary>
        /// Clears the current selection in the active document.
        /// - Returns the number of items previously selected.
        /// </summary>
        public Task<int> ClearSelectionAsync(CancellationToken ct)
            => TryUiCall(
                fbCall: () => _fb.ClearSelectionAsync(ct),
                fbUiPreferredCall: () => _fb.ClearSelectionAsync(ct),
                opName: nameof(ClearSelectionAsync));


        /// <summary>
        /// Retrieves a snapshot of the current selection, including canonical IDs and path info.
        /// - Useful for external clients to mirror the current user selection.
        /// </summary>
        public Task<SelectionSnapshotDto> GetCurrentSelectionSnapshotAsync(CancellationToken ct)
            => TryUiCall(
                fbCall: () => _fb.GetCurrentSelectionSnapshotAsync(ct),
                fbUiPreferredCall: () => _fb.GetCurrentSelectionSnapshotAsync(ct),
                opName: nameof(GetCurrentSelectionSnapshotAsync));

        // ===== Clash =====

        /// <summary>
        /// Runs a simple clash test between two scopes with given tolerance.
        /// - Builds and executes a temporary clash test.
        /// - Returns number of detected clashes and applied scope details.
        /// </summary>
        public Task<ClashSummaryDto> RunClashAsync(ClashRunArgs req, CancellationToken ct)
             => TryUiCall(
                 fbCall: () => _fb.RunClashAsync(req, ct),
                 fbUiPreferredCall: () => _fb.RunClashAsync(req, ct),
                 opName: nameof(RunClashAsync));

    }
}
