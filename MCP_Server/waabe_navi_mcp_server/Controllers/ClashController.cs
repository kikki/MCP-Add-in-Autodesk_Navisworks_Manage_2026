using System.Threading;
using System.Web.Script.Serialization;
using waabe_navi_mcp_server.Contracts;
using waabe_navi_mcp_server.Infrastructure;
using waabe_navi_mcp_server.Services.Backends;
using static waabe_navi_mcp_server.Infrastructure.ErrorHandlingMiddleware;

namespace waabe_navi_mcp_server.Controllers
{
    /// <summary>
    /// Controller handling RPC requests for clash detection.
    /// - Accepts RPC requests with ClashRunArgs parameters.
    /// - Invokes the backend service to run a clash test.
    /// - Wraps execution in error handling middleware and returns a typed RPC response.
    /// </summary>
    public sealed class ClashController
    {
        private static readonly JavaScriptSerializer _jss = new JavaScriptSerializer();

        /// <summary>
        /// Runs a simple clash detection based on the arguments provided in the RPC request.
        /// - Deserializes request parameters into a ClashRunArgs object.
        /// - Executes BackendResolver.RunClashAsync with a cancellation token (timeout controlled).
        /// - Returns a RpcResponse with ClashSummaryDto as the data payload.
        /// - If an error occurs, Wrap() ensures standardized error handling.
        /// </summary>
        /// <param name="req">The incoming RPC request containing ClashRunArgs in @params.</param>
        /// <returns>
        /// A standardized RPC response containing a ClashSummaryDto on success,
        /// or an error response if execution fails.
        /// </returns>
        public object RunSimpleClash(RpcRequest req)
        {
            return Wrap(() =>
            {
                // Convert RPC parameters into ClashRunArgs
                var args = _jss.ConvertToType<ClashRunArgs>(req.@params);

                // Apply cancellation with a configured timeout
                var ct = new CancellationTokenSource(Settings.DefaultTimeoutMs).Token;

                // Run the clash detection via backend
                var dto = BackendResolver.Instance.RunClashAsync(args, ct)
                           .GetAwaiter().GetResult();

                // Wrap successful result in an RPC response
                return RpcResponse<ClashSummaryDto>.Success(dto);
            });
        }
    }
}
