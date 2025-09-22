// waabe_navi_mcp_server/Mapping/RpcMap.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using waabe_navi_mcp_server.Controllers;
using waabe_navi_mcp_server.Contracts;
using waabe_navi_mcp_server.Infrastructure; // RpcRouter
using waabe_navi_mcp_server.Telemetry;

namespace waabe_navi_mcp_server.Mapping
{
    /// <summary>
    /// Central mapping of RPC method names → controller handlers.
    /// - Defines which controller methods are exposed as RPC endpoints.
    /// - Provides helper methods to build and register route dictionaries.
    /// - Attaches runtime metrics (execution time, request ID) to responses.
    ///
    /// Public methods:
    /// - BuildRoutes() → returns all routes as dictionary (not registered).
    /// - Register(Dictionary) → registers routes into an existing dictionary.
    /// - Register(RpcRouter) → convenience overload for registering directly on a router.
    /// </summary>
    public static class RpcMap
    {
        /// <summary>
        /// Builds all RPC routes as a dictionary without registering them.
        /// - Instantiates controllers.
        /// - Wraps each controller method with runtime metrics (execution time, request ID).
        /// - Returns a dictionary mapping method names to handlers.
        /// </summary>
        /// <returns>
        /// Dictionary with keys = RPC method names (string),
        /// values = handler delegates (Func&lt;RpcRequest, object&gt;).
        /// </returns>
        public static Dictionary<string, Func<RpcRequest, object>> BuildRoutes()
        {
            var routes = new Dictionary<string, Func<RpcRequest, object>>(StringComparer.OrdinalIgnoreCase);

            var model = new ModelController();
            var search = new SearchController();
            var sel = new SelectionController();
            var vis = new VisibilityController();
            var exp = new ExportController();
            var sys = new SystemController();
            var clash = new ClashController();

            // Wrapper: measure runtime and attach meta info to RpcResponse
            Func<Func<RpcRequest, object>, Func<RpcRequest, object>> wrap = (handler) => (req) =>
            {
                var sw = Stopwatch.StartNew();
                var res = handler(req);
                sw.Stop();

                // Attach meta to generic RpcResponse
                if (res is RpcResponse<object> gen)
                {
                    gen.meta = Metrics.WithMeta(req?.id, sw.ElapsedMilliseconds);
                    return gen;
                }

                var resType = res?.GetType();
                if (resType != null && resType.IsGenericType && resType.GetGenericTypeDefinition() == typeof(RpcResponse<>))
                {
                    dynamic dyn = res;
                    dyn.meta = Metrics.WithMeta(req?.id, (int)sw.ElapsedMilliseconds);
                    return dyn;
                }

                return res;
            };

            // ---------- Model ----------
            routes["get_model_overview"] = wrap(model.GetModelOverview);
            routes["get_units_and_tolerances"] = wrap(model.GetUnits);
            routes["get_property_distribution_by_category"] = wrap(model.GetPropertyDistributionByCategory);

            // ---------- Search ----------
            routes["get_element_count_by_category"] = wrap(search.GetCountByCategory);
            routes["list_properties_for_item"] = wrap(search.ListPropertiesForItem);
            routes["list_items_to_property"] = wrap(search.ListItemsToProperty);

            // ---------- Selection ----------
            routes["clear_selection"] = wrap(sel.ClearSelection);
            routes["get_current_selection_snapshot"] = wrap(sel.GetCurrentSelectionSnapshot);
            routes["apply_selection"] = wrap(sel.ApplySelection);

            // ---------- Clash ----------
            routes["run_simple_clash"] = wrap(clash.RunSimpleClash);

            // ---------- System ----------
           // routes["ping"] = wrap(sys.Ping);
          //  routes["get_server_info"] = wrap(sys.GetServerInfo);
          //  routes["get_capabilities"] = wrap(sys.GetCapabilities);

            return routes;
        }

        /// <summary>
        /// Registers all routes into an existing dictionary (usually provided by RpcRouter).
        /// - Overwrites any existing handlers with the same key.
        /// - Calls BuildRoutes() internally.
        /// </summary>
        /// <param name="routes">Dictionary of routes (method → handler).</param>
        public static void Register(Dictionary<string, Func<RpcRequest, object>> routes)
        {
            var map = BuildRoutes();
            foreach (var kv in map)
                routes[kv.Key] = kv.Value;
        }

        /// <summary>
        /// Convenience overload: registers all routes directly into a given RpcRouter.
        /// - Throws ArgumentNullException if router is null.
        /// - Internally calls Register(Dictionary).
        /// </summary>
        /// <param name="router">The RpcRouter instance where routes will be registered.</param>
        public static void Register(RpcRouter router)
        {
            if (router == null) throw new ArgumentNullException(nameof(router));
            Register(router.Routes);
        }
    }
}
