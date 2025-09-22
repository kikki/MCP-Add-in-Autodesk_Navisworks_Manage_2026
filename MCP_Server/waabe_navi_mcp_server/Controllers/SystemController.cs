// Controllers/SystemController.cs
using waabe_navi_mcp_server.Contracts;
using waabe_navi_mcp_server.Infrastructure;
using static waabe_navi_mcp_server.Infrastructure.ErrorHandlingMiddleware;

namespace waabe_navi_mcp_server.Controllers
{
    /// <summary>
    /// Controller for handling system-level RPC requests.
    /// - Provides health checks, server information, and capability listings.
    /// </summary>
    public sealed class SystemController
    {
        /// <summary>
        /// RPC method: "ping"
        /// - Simple health check method.
        /// - Always responds with "pong".
        /// - Used to test connectivity and server responsiveness.
        /// </summary>
        public object Ping(RpcRequest req)
            => RpcResponse<PongDto>.Success(new PongDto { message = "pong" });

        /// <summary>
        /// RPC method: "get_server_info"
        /// - Returns version and API identifier of the server.
        /// - Useful for diagnostics and compatibility checks.
        /// - Output: RpcResponse&lt;ServerInfoDto&gt;.
        /// Example: "Tell me the current server version."
        /// </summary>
        public object GetServerInfo(RpcRequest req)
            => RpcResponse<ServerInfoDto>.Success(
                new ServerInfoDto { version = Settings.ServerVersion, api = "waabe_navi_mcpserver" });

        /// <summary>
        /// RPC method: "get_capabilities"
        /// - Lists the capabilities supported by this server.
        /// - Returns a list of strings such as "model", "search", "selection", etc.
        /// - Used by clients to discover available features dynamically.
        /// </summary>
        public object GetCapabilities(RpcRequest req)
            => RpcResponse<System.Collections.Generic.List<string>>.Success(
                new System.Collections.Generic.List<string> {
                    "model","search","selection","visibility","export","system"
                });
    }
}
