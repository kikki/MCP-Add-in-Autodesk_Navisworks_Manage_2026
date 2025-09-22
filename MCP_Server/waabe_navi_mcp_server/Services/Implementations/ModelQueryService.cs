// waabe_navi_mcpserver/Services/Implementations/ModelQueryService.cs
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
    /// Provides high-level queries for model information.
    /// - Acts as a thin wrapper around the selected Navisworks backend (<see cref="IWaabeNavisworksBackend"/>).
    /// - Handles logging and delegates all work to the backend.
    /// </summary>
    public sealed class ModelQueryService : IModelQueryService
    {



        
        private static IWaabeNavisworksBackend BE => BackendResolver.Instance;

        /// <summary>
        /// Retrieves a model overview from the backend.
        /// - Returns metadata about loaded models and their structure.
        /// - Cancellation is supported via <paramref name="ct"/>.
        /// </summary> 
        public async Task<ModelOverviewDto> GetOverviewAsync(CancellationToken ct)
        {
              LogHelper.LogDebug("ModelQueryService.GetOverviewAsync()");
              return await BE.GetModelOverviewAsync(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists all models currently loaded in the document.
        /// - Returns details such as canonical IDs, names, and hierarchy info.
        /// - Cancellation is supported via <paramref name="ct"/>.
        /// </summary> 
        public Task<DtoList<ModelDetailDto>> ListModelsAsync(CancellationToken ct)
        {
            LogHelper.LogDebug("ModelQueryService.ListModelsAsync()");
            return BE.ListModelsAsync(ct);
        }



        /// <summary>
        /// Retrieves the unit and tolerance settings from the backend.
        /// - Returns information about length, area, volume units and tolerances.
        /// - Cancellation is supported via <paramref name="ct"/>.
        /// </summary> 
        public Task<UnitInfoDto> GetUnitsAsync(CancellationToken ct)
        {
            LogHelper.LogDebug("ModelQueryService.GetUnitsAsync()");
            return BE.GetUnitsAndTolerancesAsync(ct);
        }

        /// <summary>
        /// Retrieves property distribution counts grouped by category.
        /// - Returns statistics about properties across models.
        /// - Cancellation is supported via <paramref name="ct"/>.
        /// </summary>
        public Task<ElementCountDto> GetPropertyDistributionByCategoryAsync(CancellationToken ct)
        {
            LogHelper.LogDebug($"SearchService.GetPropertyDistributionByCategoryAsync()");
            return BE.GetPropertyDistributionByCategoryAsync( ct);
        }


    }
}
