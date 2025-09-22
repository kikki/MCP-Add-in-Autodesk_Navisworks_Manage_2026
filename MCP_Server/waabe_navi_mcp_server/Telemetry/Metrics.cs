using waabe_navi_mcp_server.Contracts;
using waabe_navi_mcp_server.Infrastructure;

namespace waabe_navi_mcp_server.Telemetry
{
    /// <summary>
    /// File: Telemetry/Metrics.cs
    /// Provides helper methods for building telemetry metadata
    /// that can be attached to RPC responses.
    /// </summary>
    public static class Metrics
    {
        /// <summary>
        /// Creates an <see cref="RpcMeta"/> object containing basic telemetry data.
        /// Includes:
        ///   - <paramref name="requestId"/>: unique identifier of the RPC request (or "n/a").
        ///   - <paramref name="ms"/>: duration of the request in milliseconds.
        ///   - <c>server_version</c>: taken from <see cref="Settings.ServerVersion"/>.
        ///   - <c>model_revision</c>: currently fixed to "n/a".
        /// </summary>
        /// <param name="requestId">The RPC request identifier, may be null.</param>
        /// <param name="ms">Execution time of the request in milliseconds.</param>
        /// <returns>A populated <see cref="RpcMeta"/> telemetry object.</returns>
        public static RpcMeta WithMeta(string requestId, long ms)
            => new RpcMeta
            {
                model_revision = "n/a",
                query_ms = (int)ms,
                server_version = Settings.ServerVersion,
                request_id = requestId ?? "n/a"
            };
    }
}
