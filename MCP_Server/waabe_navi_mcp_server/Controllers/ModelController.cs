// Controllers/ModelController.cs
using System.Threading;
using System.Web.Script.Serialization;
using waabe_navi_mcp_server.Contracts;
using waabe_navi_mcp_server.Infrastructure;
using waabe_navi_mcp_server.Services;
using waabe_navi_mcp_server.Services.Implementations;
using waabe_navi_shared;
using static waabe_navi_mcp_server.Infrastructure.ErrorHandlingMiddleware;

namespace waabe_navi_mcp_server.Controllers
{
    /// <summary>
    /// Controller for handling RPC requests related to model information and metadata.
    /// - Provides overview of loaded models.
    /// - Exposes unit information and property distribution queries.
    /// - Wraps service calls in error handling middleware and returns standardized RPC responses.
    /// </summary>
    public sealed class ModelController
    {
        private readonly IModelQueryService _svc = new ModelQueryService();
        private static readonly JavaScriptSerializer _jss = new JavaScriptSerializer();

        /// <summary>
        /// RPC method: "get_model_overview"
        /// - Returns an overview of all currently loaded models.
        /// - No input parameters are required.
        /// - Uses IModelQueryService to fetch details.
        /// - Returns RpcResponse&lt;ModelOverviewDto&gt;.
        /// Example: "Give me an overview of all loaded models."
        /// </summary>
        public object GetModelOverview(RpcRequest req)
        {
            using (LoggingExtensions.Scope("get_model_overview"))
            {
                return Wrap(() =>
                {
                    var cts = new CancellationTokenSource(Settings.DefaultTimeoutMs);
                    var dto = _svc.GetOverviewAsync(cts.Token).GetAwaiter().GetResult();
                    return RpcResponse<ModelOverviewDto>.Success(dto);
                });
            }
        }

        /// <summary>
        /// RPC method: "get_units"
        /// - Returns the measurement units and tolerances of the current document.
        /// - Calls IModelQueryService.GetUnitsAsync.
        /// - Returns RpcResponse&lt;UnitInfoDto&gt;.
        /// Example: "What units are used in this model?"
        /// </summary>
        public object GetUnits(RpcRequest req)
        {
            return Wrap(() =>
            {
                var cts = new CancellationTokenSource(Settings.DefaultTimeoutMs);
                var dto = _svc.GetUnitsAsync(cts.Token).GetAwaiter().GetResult();
                return RpcResponse<UnitInfoDto>.Success(dto);
            });
        }

        /// <summary>
        /// RPC method: "get_property_distribution_by_category"
        /// - Returns a count of elements grouped by property category.
        /// - Useful for statistics and histograms.
        /// - Calls IModelQueryService.GetPropertyDistributionByCategoryAsync.
        /// - Returns RpcResponse&lt;ElementCountDto&gt;.
        /// Example: "How many elements exist per category in this model?"
        /// </summary>
        public object GetPropertyDistributionByCategory(RpcRequest req)
        {
            using (LoggingExtensions.Scope("get_property_distribution_by_category"))
            {
                return Wrap(() =>
                {
                    var cts = new CancellationTokenSource(Settings.DefaultTimeoutMs);
                    var dto = _svc.GetPropertyDistributionByCategoryAsync(cts.Token).GetAwaiter().GetResult();
                    return RpcResponse<ElementCountDto>.Success(dto);
                });
            }
        }
    }
}
