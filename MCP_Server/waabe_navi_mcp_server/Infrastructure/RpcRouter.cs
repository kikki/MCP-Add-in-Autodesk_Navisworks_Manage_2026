// waabe_navi_mcp_server/Infrastructure/RpcRouter.cs
using System;
using System.Collections.Generic;
using waabe_navi_mcp_server.Contracts;
using waabe_navi_mcp_server.Mapping;
using waabe_navi_shared;

namespace waabe_navi_mcp_server.Infrastructure
{
    /// <summary>
    /// Router for mapping RPC method names to controller actions.
    /// - Maintains a dictionary of routes (method name → handler).
    /// - Dispatches incoming requests to the appropriate controller method.
    /// - Supports dynamic registration of custom routes.
    /// </summary>
    public sealed class RpcRouter
    {
        // Internal route registry
        private readonly Dictionary<string, Func<RpcRequest, object>> _routes =
            new Dictionary<string, Func<RpcRequest, object>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Exposes the registered route dictionary.
        /// - Used by RpcMap.Register to populate routes.
        /// - Key: RPC method name, Value: handler delegate.
        /// </summary>
        public Dictionary<string, Func<RpcRequest, object>> Routes => _routes;

        /// <summary>
        /// Constructor.
        /// - Initializes the router with default routes by calling RpcMap.Register.
        /// </summary>
        public RpcRouter()
        {
            RpcMap.Register(_routes);
        }

        /// <summary>
        /// Alternative registration method.
        /// - Calls RpcMap.Register directly on this router.
        /// - Useful when reloading or reinitializing routes dynamically.
        /// </summary>
        public void RegisterDefaults()
        {
            RpcMap.Register(this);
        }

        /// <summary>
        /// Adds or replaces a route.
        /// - Allows dynamic extension of RPC routes beyond the defaults.
        /// </summary>
        /// <param name="name">The RPC method name.</param>
        /// <param name="handler">The handler function to execute for this method.</param>
        public void AddRoute(string name, Func<RpcRequest, object> handler)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _routes[name] = handler;
        }

        /// <summary>
        /// Dispatches an incoming RPC request to the correct handler.
        /// - Validates that the method is provided and registered.
        /// - Returns a standardized RpcResponse on failure:
        ///   <list type="bullet">
        ///     <item><b>NVX_BAD_REQUEST</b> → No method provided.</item>
        ///     <item><b>NVX_NOT_FOUND</b> → Unknown method name.</item>
        ///   </list>
        /// - Logs the dispatch event for debugging.
        /// </summary>
        /// <param name="req">The incoming RPC request.</param>
        /// <returns>The result of the handler, or a failed RpcResponse if invalid.</returns>
        public object Dispatch(RpcRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.method))
                return RpcResponse<object>.Fail("NVX_BAD_REQUEST", "Missing method.");

            if (!_routes.TryGetValue(req.method, out var handler))
                return RpcResponse<object>.Fail("NVX_NOT_FOUND", $"Unknown method '{req.method}'");

            LogHelper.LogDebug($"Dispatch: {req.method}");
            return handler(req);
        }
    }
}
