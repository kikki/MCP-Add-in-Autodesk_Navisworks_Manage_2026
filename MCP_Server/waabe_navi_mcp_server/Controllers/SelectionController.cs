// Controllers/SelectionController.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Web.Script.Serialization;
using waabe_navi_mcp_server.Contracts;
using waabe_navi_mcp_server.Infrastructure;
using waabe_navi_mcp_server.Services;
using waabe_navi_mcp_server.Services.Implementations;
using static waabe_navi_mcp_server.Infrastructure.ErrorHandlingMiddleware;

namespace waabe_navi_mcp_server.Controllers
{
    /// <summary>
    /// Controller for handling RPC requests related to model selection.
    /// - Provides methods to clear the selection, get a snapshot, or apply a new selection.
    /// - Wraps service calls with error handling for consistent RPC responses.
    /// </summary>
    public sealed class SelectionController
    {
        private readonly ISelectionService _svc = new SelectionService();
        private static readonly JavaScriptSerializer _jss = new JavaScriptSerializer();

        /// <summary>
        /// RPC method: "clear_selection"
        /// - Clears the current selection in the active model.
        /// - Input: none.
        /// - Output: RpcResponse&lt;ApplyResultDto&gt; (number of elements affected).
        /// Example: "Clear all currently selected items."
        /// </summary>
        public object ClearSelection(RpcRequest req)
        {
            return Wrap(() =>
            {
                var cts = new CancellationTokenSource(Settings.DefaultTimeoutMs);
                var dto = _svc.ClearSelectionAsync(cts.Token).GetAwaiter().GetResult();
                return RpcResponse<ApplyResultDto>.Success(dto);
            });
        }

        /// <summary>
        /// RPC method: "get_current_selection_snapshot"
        /// - Returns a snapshot of the current selection.
        /// - Includes canonical IDs, element names, and paths.
        /// - Input: none.
        /// - Output: RpcResponse&lt;SelectionSnapshotDto&gt;.
        /// Example: "Give me a list of all currently selected elements."
        /// </summary>
        public object GetCurrentSelectionSnapshot(RpcRequest req)
        {
            return Wrap(() =>
            {
                var cts = new CancellationTokenSource(Settings.DefaultTimeoutMs);
                var dto = _svc.GetSnapshotAsync(cts.Token).GetAwaiter().GetResult();
                return RpcResponse<SelectionSnapshotDto>.Success(dto);
            });
        }

        /// <summary>
        /// RPC method: "apply_selection"
        /// - Applies a new selection to the model.
        /// - Input: dictionary with keys:
        ///     canonical_id[] (array of strings, required),
        ///     keepExistingSelection (bool, optional, default = true).
        /// - Output: RpcResponse&lt;List&lt;SimpleItemRef&gt;&gt; with selected items.
        /// Example: "Select elements with canonical IDs [p:123, p:456], keep existing = false."
        /// </summary>
        public object ApplySelection(RpcRequest req)
        {
            return Wrap(() =>
            {
                // 1) Convert params to dictionary
                var dict = _jss.ConvertToType<Dictionary<string, object>>(req.@params ?? new object());

                // 2) Extract canonical_id[]
                var ids = new List<string>();
                if (dict != null && dict.TryGetValue("canonical_id", out var raw) && raw is object[] arr)
                {
                    foreach (var x in arr)
                    {
                        var s = x?.ToString();
                        if (!string.IsNullOrWhiteSpace(s)) ids.Add(s);
                    }
                }

                // 3) Read keepExistingSelection (default true)
                bool keep = true;
                if (dict != null && dict.TryGetValue("keepExistingSelection", out var k) && k is bool b) keep = b;

                if (ids.Count == 0)
                    throw new ArgumentException("canonical_id[] (string) required.");

                // 4) Call selection service
                var cts = new CancellationTokenSource(Settings.DefaultTimeoutMs);
                var dto = _svc.ApplySelectionAsync(ids, keep, cts.Token).GetAwaiter().GetResult();

                // 5) Return unchanged as List<SimpleItemRef>
                return RpcResponse<List<SimpleItemRef>>.Success(dto);
            });
        }
    }
}
