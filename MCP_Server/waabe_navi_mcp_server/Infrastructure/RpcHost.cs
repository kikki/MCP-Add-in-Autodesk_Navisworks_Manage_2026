using System;
using System.Threading;
using waabe_navi_mcp_server.Contracts;
using waabe_navi_shared;

namespace waabe_navi_mcp_server.Infrastructure
{
    /// <summary>
    /// RpcHost encapsulates the transport layer for handling RPC requests.
    /// - Responsible for connecting a transport bridge (e.g., NamedPipe, WebSocket, IPC).
    /// - Provides a synchronous JSON dispatch API for simplicity.
    /// - Uses RpcRouter internally to route incoming requests to the correct controller.
    /// </summary>
    public sealed class RpcHost : IDisposable
    {
        private RpcRouter _router;

        /// <summary>
        /// Starts the RpcHost.
        /// - Initializes the RpcRouter instance.
        /// - Logs that the host is ready.
        /// - Intended as the entry point for attaching custom transports (e.g., WebSocket bridge).
        /// </summary>
        public void Start()
        {
            _router = new RpcRouter();
            LogHelper.LogInfo("RpcHost ready.", "RpcHost");
            // NOTE: Existing transport bridge (NamedPipe/WebSocket) can be attached here.
        }

        /// <summary>
        /// Processes an incoming RPC request (called by the transport bridge).
        /// - Parses the JSON string into an RpcRequest object.
        /// - Routes the request via RpcRouter.Dispatch().
        /// - Serializes the RpcResponse back to JSON.
        /// Example: 
        /// Input  → { "id":"1", "method":"ping", "params":{} }
        /// Output → { "ok":true, "data":{"message":"pong"} }
        /// </summary>
        /// <param name="json">Raw JSON string containing the RPC request.</param>
        /// <returns>Serialized JSON string containing the RPC response.</returns>
        public string ProcessJson(string json)
        {
            var req = RpcRequest.FromJson(json);
            var resObj = _router.Dispatch(req);
            return RpcResponse.ToJson(resObj);
        }

        /// <summary>
        /// Disposes of the RpcHost.
        /// - Currently does nothing, but can be extended for cleanup (e.g., closing transport connections).
        /// </summary>
        public void Dispose() { }
    }
}
