// Services/IModelQueryService.cs
using System.Threading;
using System.Threading.Tasks;
using waabe_navi_mcp_server.Contracts;

namespace waabe_navi_mcp_server.Services
{
    /// <summary>
    /// Defines query operations for models in Navisworks.
    /// Provides high-level access to model overviews, units, and property distribution.
    /// </summary>
    public interface IModelQueryService
    {
        /// <summary>
        /// Retrieves an overview of the current model, including metadata,
        /// sub-models, categories, and total element counts.
        /// </summary>
        Task<ModelOverviewDto> GetOverviewAsync(CancellationToken ct);

        /// <summary>
        /// Retrieves the active document’s unit system and tolerance information.
        /// </summary>
        Task<UnitInfoDto> GetUnitsAsync(CancellationToken ct);

        /// <summary>
        /// Analyzes the distribution of properties across categories for the loaded model(s).
        /// Returns counts per model, category, and property.
        /// </summary>
        Task<ElementCountDto> GetPropertyDistributionByCategoryAsync(  CancellationToken ct);
    }
}
