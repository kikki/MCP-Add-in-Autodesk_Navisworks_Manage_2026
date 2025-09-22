
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Controls;
using Autodesk.Navisworks.Api.DocumentParts;
using Autodesk.Navisworks.Api.Interop;
using Autodesk.Navisworks.Api.Interop.ComApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using waabe_navi_mcp_server.Contracts;
using waabe_navi_shared;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
//using ClashD = Autodesk.Navisworks.Clash;



namespace waabe_navi_mcp_server.Services.Backends
{

    

    /// <summary>
    /// Fallback implementation of IWaabeNavisworksBackend.
    /// - Provides model queries, selection handling, property retrieval and clash detection.
    /// - Used when ReflectionBackend is not available.
    /// - Contains unified helpers for:
    ///   * SubModel scanning and canonical ID resolution
    ///   * Property/geometry extraction
    ///   * Scope-based filtering (all, selection, submodel)
    ///   * Consistent logging (ID13453)
    /// </summary>
    public sealed class FallbackBackend : IWaabeNavisworksBackend
    {

        private const string DEFAULT_UNNAMED = "(unbenannt)";

        private const string DEFAULT_CATEGORY = "(Kategorie)";

        private const string DEFAULT_PROPERTY = "(Property)";



        /// <summary>
        /// Builds a Navisworks <see cref="Selection"/> from a list of canonical IDs.
        /// - Resolves items by their canonical IDs.
        /// - For each resolved item, tries to find a geometric ancestor/descendant (via <see cref="ResolveGeometricTargets"/>).
        /// - Collects applied items into a <see cref="ModelItemCollection"/>.
        /// - Records detailed mapping info (<see cref="ScopeAppliedInfo"/>) about the resolution process.
        /// Usage:
        ///   - Typically called internally by clash detection or selection logic when scopes are expressed as canonical IDs.
        /// </summary>
        /// <param name="doc">
        ///   The active Navisworks <see cref="Document"/> containing models and items.
        /// </param>
        /// <param name="tokens">
        ///   List of canonical ID strings to resolve into <see cref="ModelItem"/>s.
        /// </param>
        /// <param name="itemCount">
        ///   [out] Number of items that were added to the selection.
        /// </param>
        /// <param name="infos">
        ///   [out] Detailed information per input ID about how it was resolved and promoted/demoted.
        /// </param>
        /// <returns>
        ///   A <see cref="Selection"/> object containing the resolved and promoted geometric items.
        ///   Returns an empty selection if nothing could be resolved.
        /// </returns>
        private Selection BuildSelectionFromCanonicalTokens(
            Document doc,
            List<string> tokens,
            out int itemCount,
            out List<ScopeAppliedInfo> infos)
        {
            var col = new ModelItemCollection();
            infos = new List<ScopeAppliedInfo>();
            itemCount = 0;

            if (doc == null || tokens == null || tokens.Count == 0)
                return new Selection(col);

            var items = ResolveItemsByCanonicalIds(doc, tokens);
            if (items == null || items.Count == 0)
                return new Selection(col);

            foreach (var it in items)
            {
                if (it == null) continue;

                var inputId = GetCanonicalId(it);
                var name = SafeNameOrDefault(it.DisplayName, it.ClassDisplayName ?? it.ClassName, DEFAULT_UNNAMED);

                var (targets, reason, steps) = ResolveGeometricTargets(it);

                var appliedIds = new List<string>();
                foreach (var t in targets)
                {
                    col.Add(t);
                    appliedIds.Add(GetCanonicalId(t));
                }

                infos.Add(new ScopeAppliedInfo
                {
                    input_id = inputId,
                    resolved_id = inputId,  
                    applied_id = string.Join(";", appliedIds),  
                    reason = (steps > 0 && reason == "ok") ? "promoted:no-geometry" : reason,
                    element_name = name
                });
            }

            itemCount = col.Count;
            return new Selection(col);
        }


        /// <summary>
        /// Safely counts the number of clash results in a <see cref="ClashTest"/>.
        /// - Iterates over direct children and nested <see cref="ClashResultGroup"/> objects.
        /// - Returns the total number of <see cref="ClashResult"/> instances found.
        /// - Logs diagnostic messages if the test or children are null, disposed, or cause exceptions.
        /// Usage:
        ///   - Called after running a clash test to determine the number of detected collisions.
        /// </summary>
        /// <param name="test">
        ///   The <see cref="Autodesk.Navisworks.Api.Clash.ClashTest"/> whose results should be counted.
        /// </param>
        /// <returns>
        ///   The number of <see cref="ClashResult"/> objects in the given test.
        ///   Returns 0 if the test is null, disposed, or an error occurs.
        /// </returns>
        private int CountClashResultsSafe(Autodesk.Navisworks.Api.Clash.ClashTest test)
         {
             if (test == null)
             {
                 LogHelper.LogWarning("[CLASH] CountClashResultsSafe: test is null");
                 return 0;
             }
            try
            {
                int count = 0;
                var children = test.Children;
                if (children == null)
                {
                    LogHelper.LogEvent("[CLASH] CountClashResultsSafe: children=null");
                    return 0;
                }

                var top = children.ToList();
                foreach (Autodesk.Navisworks.Api.SavedItem child in top)
                {
                    if (child is Autodesk.Navisworks.Api.Clash.ClashResult)
                    {
                        count++;
                        continue;
                    }
                    if (child is Autodesk.Navisworks.Api.Clash.ClashResultGroup g)
                    {
                        var sub = g.Children;
                        if (sub == null) continue;
                        foreach (Autodesk.Navisworks.Api.SavedItem c in sub.ToList())
                        {
                            if (c is Autodesk.Navisworks.Api.Clash.ClashResult) count++;
                        }
                    }
                }

                LogHelper.LogEvent($"[CLASH] CountClashResultsSafe => {count}");
                return count;
            }
            catch (ObjectDisposedException ode)
            {
                LogHelper.LogWarning($"[CLASH] CountClashResultsSafe: ObjectDisposed → 0 ({ode.Message})");
                return 0;
            }
            catch (Exception ex)
            {
                LogHelper.LogWarning($"[CLASH] CountClashResultsSafe: exception → 0 ({ex.Message})");
                return 0;
            }
        }




        /// <summary>
        /// RPC: run_simple_clash
        /// Purpose:
        ///   - Executes a simple clash test in Navisworks between two given scopes (scopeA & scopeB).
        ///   - Builds selections from canonical IDs or scope strings (using promotion if necessary).
        ///   - Creates a temporary <see cref="ClashTest"/>, runs it, and counts the clash results.
        ///   - Produces detailed diagnostic info for each scope (mapping input IDs to applied items).
        /// Parameters:
        ///   - <see cref="ClashRunArgs"/> args:
        ///       * scopeA: "all", model_id, or canonical IDs (defines first selection).
        ///       * scopeB: "all", model_id, or canonical IDs (defines second selection).
        ///       * tolerance_m: clash tolerance in meters.
        ///       * test_name: name of the temporary test.
        ///   - <see cref="CancellationToken"/> ct: cancels the operation if triggered.
        /// Return:
        ///   - <see cref="ClashSummaryDto"/> containing:
        ///       * success flag
        ///       * test name
        ///       * result count
        ///       * details (JSON with scope resolution info)
        /// Behavior:
        ///   - Uses <see cref="BuildSelectionWithPromotionIfCanonical"/> to resolve scopes.
        ///   - Runs the clash test via Navisworks API and counts results using <see cref="CountClashResultsSafe"/>.
        ///   - Logs detailed progress and errors for diagnostics.
        /// Usage:
        ///   - Invoked via RPC mapping in <c>RpcMap</c> with method name "run_simple_clash".
        /// </summary>
        public async Task<ClashSummaryDto> RunClashAsync(ClashRunArgs args, CancellationToken ct)
        {
            var dto = new ClashSummaryDto { success = true, test_name = args?.test_name ?? "MCP API Test", results = 0 };
            var ev = new List<string>();

            LogHelper.LogEvent("[CLASH][NET] RunClashAsync ENTER");

             
            if (args == null)
                return new ClashSummaryDto { success = false, test_name = "MCP API Test", results = 0, message = "INPUT_VALIDATE;Fail;reason=args null" };

            LogHelper.LogEvent($"[CLASH][NET] ARGS scopeA='{args.scopeA}', scopeB='{args.scopeB}', tol={args.tolerance_m.ToString(CultureInfo.InvariantCulture)}");

            try
            {
                dto = await UiThread.InvokeAsync(() =>
                {
                    var doc = Application.MainDocument;
                    if (doc == null)
                        throw new InvalidOperationException("INPUT_VALIDATE;Fail;reason=no active document");

                     
                    int cntA, cntB; bool expA, expB;
                    List<ScopeAppliedInfo> infosA, infosB;

                    var selA = BuildSelectionWithPromotionIfCanonical(doc, args.scopeA ?? "all", out cntA, out expA, out infosA);
                    var selB = BuildSelectionWithPromotionIfCanonical(doc, args.scopeB ?? "all", out cntB, out expB, out infosB);

                    if (cntA <= 0) throw new InvalidOperationException("RESOLVE_A;Fail;reason=no items");
                    if (cntB <= 0) throw new InvalidOperationException("RESOLVE_B;Fail;reason=no items");
                    
                     
                    var clashDoc = Application.MainDocument.GetClash();
                    var td = clashDoc.TestsData;

                     
                    var test = new Autodesk.Navisworks.Api.Clash.ClashTest
                    {
                        DisplayName = dto.test_name + " " + DateTime.UtcNow.ToString("HHmmssfff"),
                        TestType = Autodesk.Navisworks.Api.Clash.ClashTestType.Hard,
                        Tolerance = args.tolerance_m
                    };
                    test.SelectionA.Selection.CopyFrom(selA);
                    test.SelectionB.Selection.CopyFrom(selB);

                     
                    td.TestsAddCopy(test);

                    
                    var added = td.Tests.OfType<ClashTest>()
                        .FirstOrDefault(t => t.DisplayName == test.DisplayName);


                    if (added == null) throw new InvalidOperationException("test not found after AddCopy");

                    td.TestsRunTest(added);

                    ClashTest fresh = null;
                    for (int i = 0; i < 10; i++)  
                    {
                        fresh = td.Tests.OfType<ClashTest>()
                            .FirstOrDefault(t => t.DisplayName == test.DisplayName);
                        if (fresh != null && fresh.Children != null) break;
                        System.Threading.Thread.Sleep(20);
                    }
                    if (fresh == null) throw new InvalidOperationException("test not found after Run");
                    
                     
                    dto.results = CountClashResultsSafe(added);
                    dto.message = $"ok; results={dto.results}";

                     
                    try
                    {
                        string MakeJson(List<ScopeAppliedInfo> lst)
                        {
                            if (lst == null) return "[]";
                            var sb = new StringBuilder();
                            sb.Append('[');
                            for (int i = 0; i < lst.Count; i++)
                            {
                                var x = lst[i];
                                sb.Append("{")
                                  .AppendFormat("\"input_id\":\"{0}\",", EscapeJson(x.input_id ?? ""))
                                  .AppendFormat("\"resolved_id\":\"{0}\",", EscapeJson(x.resolved_id ?? ""))
                                  .AppendFormat("\"applied_id\":\"{0}\",", EscapeJson(x.applied_id ?? ""))
                                  .AppendFormat("\"reason\":\"{0}\",", EscapeJson(x.reason ?? ""))
                                  .AppendFormat("\"element_name\":\"{0}\"", EscapeJson(x.element_name ?? ""))
                                  .Append("}");
                                if (i < lst.Count - 1) sb.Append(",");
                            }
                            sb.Append(']');
                            return sb.ToString();
                        }

                        dto.details =
                            "{"
                            + "\"scopeA_info\":" + MakeJson(infosA) + ","
                            + "\"scopeB_info\":" + MakeJson(infosB)
                            + "}";
                    }
                    catch {   }


                   

                    return dto;
                });
            }
            catch (Exception ex)
            {
                dto.success = false;
                dto.message = ex.Message;
                LogHelper.LogError($"[CLASH][NET] FAILED: {ex}");
            }

            LogHelper.LogEvent("[CLASH][NET] RunClashAsync EXIT");
            return dto;
        }





        // ===== Helper: COM-Dokument robust holen (versch. TLB-Versionen) =====


        /// <summary>
        /// Resolves a textual <paramref name="scope"/> into a Navisworks <see cref="Selection"/>.
        /// Priority order:
        ///   1. Canonical IDs (uses <see cref="BuildSelectionFromCanonicalTokens"/> + promotion).
        ///   2. Raw item lookup (IDs resolved, then promoted to submodel roots).
        ///   3. Model root resolution (via <see cref="ResolveScopeToModelRoots"/>).
        ///   4. Fuzzy matching against root DisplayName (substring search).
        /// Behavior:
        ///   - If scope is "all" or empty: (disabled in this variant) → would expand all RootItems.
        ///   - If canonical IDs: returns exactly those items (or their geometric ancestors).
        ///   - If token matches a model root or submodel name: expands to full subtree.
        ///   - Otherwise: last-chance fuzzy matching against display names.
        /// Outputs:
        ///   - <paramref name="itemCount"/>: number of items in final selection.
        ///   - <paramref name="expanded"/>: true if scope was expanded to descendants.
        /// Notes:
        ///   - Logs detailed diagnostics (canonical promotion chain, matched roots).
        ///   - Keeps consistency with clash handling in <see cref="RunClashAsync"/>.
        /// </summary>
        private Autodesk.Navisworks.Api.Selection ResolveScopeSelection(
            Autodesk.Navisworks.Api.Document doc,
            string scope,
            out int itemCount,
            out bool expanded)
        {
            var col = new Autodesk.Navisworks.Api.ModelItemCollection();
            expanded = false;
            itemCount = 0;

            if (doc == null || doc.Models == null)
                return new Autodesk.Navisworks.Api.Selection(col);

            var tokens = (scope ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(t => t.Trim())
                                      .Where(t => !string.IsNullOrWhiteSpace(t))
                                      .ToList();

             

            if (tokens.Count > 0 && tokens.Any(LooksLikeCanonicalId))
            {
                List<ScopeAppliedInfo> infosA;  
                var sel = BuildSelectionFromCanonicalTokens(doc, tokens, out itemCount, out infosA);
                expanded = false;  
                try
                {
                    var appliedList = string.Join("; ", infosA.Select(i => $"{i.input_id} -> {i.applied_id} [{i.reason}]"));
                    LogHelper.LogInfo($"[CLASH][SCOPE] Canonical promotion: {appliedList}");
                }
                catch {   }

                
                return sel;
            }

            var rootSet = new HashSet<Autodesk.Navisworks.Api.ModelItem>();
            foreach (var token in tokens)
            {
                var items = ResolveItemsByCanonicalIds(doc, new[] { token });  
                if (items != null && items.Count > 0)
                {
                    foreach (var it in items)
                    {
                        var root = GetModelRootOf(it);  
                        if (root != null && rootSet.Add(root))
                            col.AddRange(root.DescendantsAndSelf);
                    }
                }
            }
            if (col.Count > 0)
            {
                expanded = true;
                itemCount = col.Count;
                return new Autodesk.Navisworks.Api.Selection(col);
            }

            foreach (var token in tokens)
            {
                string diag;
                var roots = ResolveScopeToModelRoots(doc, token, CancellationToken.None, out diag) ?? new List<Autodesk.Navisworks.Api.ModelItem>();
                foreach (var r in roots)
                    if (r != null && rootSet.Add(r))
                        col.AddRange(r.DescendantsAndSelf);
            }
            if (col.Count > 0)
            {
                expanded = true;
                itemCount = col.Count;
                return new Autodesk.Navisworks.Api.Selection(col);
            }

            foreach (var root in doc.Models.RootItems)
            {
                var name = root.DisplayName ?? "";
                if (tokens.Any(t => name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
                    col.AddRange(root.DescendantsAndSelf);
            }

            expanded = col.Count > 0;
            itemCount = col.Count;
            return new Autodesk.Navisworks.Api.Selection(col);
        }

        /// <summary>
        /// Builds a <see cref="Selection"/> from a textual scope.
        /// Behavior:
        ///   - If <paramref name="scope"/> contains canonical IDs, resolve via <see cref="BuildSelectionFromCanonicalTokens"/>
        ///     and promote to geometric targets.
        ///   - Otherwise, fallback to <see cref="ResolveScopeSelection"/> (legacy behavior).
        /// Outputs:
        ///   - <paramref name="itemCount"/>: number of selected items.
        ///   - <paramref name="expanded"/>: true if descendants expansion happened (only in fallback).
        ///   - <paramref name="infos"/>: applied info per input id (only for canonical paths).
        /// Notes:
        ///   - Keeps compatibility with older scope strings (e.g. "all", "submodel:xyz").
        ///   - Canonical branch: returns **only** geometric items (no descendants).
        ///   - Fallback branch: expands as needed.
        /// </summary>
        private Selection BuildSelectionWithPromotionIfCanonical(
            Document doc, string scope,
            out int itemCount, out bool expanded,
            out List<ScopeAppliedInfo> infos)
        {
            itemCount = 0; expanded = false; infos = new List<ScopeAppliedInfo>();

            var tokens = (scope ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(t => t.Trim())
                                      .Where(t => !string.IsNullOrWhiteSpace(t))
                                      .ToList();

            if (tokens.Count > 0 && tokens.Any(LooksLikeCanonicalId))
            {
                return BuildSelectionFromCanonicalTokens(doc, tokens, out itemCount, out infos);
            }

            return ResolveScopeSelection(doc, scope, out itemCount, out expanded);
        }


        /// <summary>
        /// Lists all items in the current document that contain a given property value,
        /// with optional scope restriction, model filtering, and value filtering.
        /// </summary>
        /// <param name="args">
        /// Query arguments:  
        ///   - <see cref="ListItemsToPropertyArgs.category"/>: Property category name (case-insensitive).  
        ///   - <see cref="ListItemsToPropertyArgs.property"/>: Property name (case-insensitive).  
        ///   - <see cref="ListItemsToPropertyArgs.Scope"/>: Scope token ("all", "selection", submodel id, etc.).  
        ///   - <see cref="ListItemsToPropertyArgs.ModelFilter"/>: Restricts results to specific submodels.  
        ///   - <see cref="ListItemsToPropertyArgs.ValueFilter"/>: Optional filter expression or regex.  
        ///   - <see cref="ListItemsToPropertyArgs.IgnoreCase"/>: Whether value comparisons ignore case.  
        ///   - <see cref="ListItemsToPropertyArgs.MaxResults"/>: Maximum number of results (null = unlimited).  
        /// </param>
        /// <param name="ct">Cancellation token to stop processing if requested.</param>
        /// <returns>
        /// A <see cref="PropertyItemListDto"/> containing:  
        ///   - The query parameters (echoed back).  
        ///   - A list of <see cref="PropertyItemDto"/> with canonical id, path, model metadata, and property value.  
        ///   - A <c>count</c> field with the number of matches.  
        /// </returns>
        /// <remarks>
        /// Behavior:  
        ///   - If no document is active, returns an empty result.  
        ///   - If <c>ModelFilter</c> is provided, only items from matching submodels are considered.  
        ///   - If <c>ValueFilter</c> is provided, only items whose property value matches are returned.  
        ///   - Iteration respects <paramref name="ct"/> for cancellation.  
        ///   - Results are truncated at <c>MaxResults</c> if set.  
        /// </remarks>
        public async Task<PropertyItemListDto> ListItemsToPropertyAsync(
            ListItemsToPropertyArgs args,
            CancellationToken ct)
        {
            LogHelper.LogEvent($"list_items_to_property: cat='{args.category}', prop='{args.property}', scope='{args.Scope}', modelFilter='{args.ModelFilter}', valueFilter='{args.ValueFilter}', ignoreCase={args.IgnoreCase}", "ListItemsToPropertyAsync");

            var result = new PropertyItemListDto
            {
                category = args.category,
                property = args.property,
                Scope = args.Scope,
                ModelFilter = args.ModelFilter,
                ValueFilter = args.ValueFilter,
                IgnoreCase = args.IgnoreCase,
                Items = new List<PropertyItemDto>()
            };

             
            var doc = Application.ActiveDocument;
            if (doc == null)
            {
                LogHelper.LogWarning("Kein aktives Dokument.", "ListItemsToPropertyAsync");
                return result;
            }

             
            var candidates = ResolveScopeItems(doc, args.Scope);  
            LogHelper.LogDebug($"Scope liefert {candidates.Count} Kandidaten.", "ListItemsToPropertyAsync");

             
            if (!string.IsNullOrWhiteSpace(args.ModelFilter))
            {
                var allowedRoots = GetAllowedModelRootsByModelFilter(doc, args.ModelFilter, ct);
                if (allowedRoots.Count == 0)
                {
                    LogHelper.LogInfo($"ModelFilter '{args.ModelFilter}' ergab keine Treffer (keine passenden Submodel-Roots).");
                    candidates = new List<ModelItem>();  
                }
                else
                {
                    candidates = candidates
                        .Where(mi => {
                            var r = GetModelRootOf(mi);
                            return r != null && allowedRoots.Contains(r);
                        })
                        .ToList();

                    LogHelper.LogDebug($"Nach ModelFilter '{args.ModelFilter}' verbleiben {candidates.Count} Items (Root-basiert).", "ListItemsToPropertyAsync");
                }
            }

             
            var comparer = BuildValuePredicate(args.ValueFilter, args.IgnoreCase);
            int added = 0;
            int? max = args.MaxResults;

            foreach (var item in candidates)
            {
                ct.ThrowIfCancellationRequested();

                 
                var hasValue = TryReadDisplayValue(item, args.category, args.property, out var value, out var rawVariant);
                if (!hasValue) continue;

                if (comparer == null || comparer(value))  
                {
                    var modelRoot = GetModelRootOf(item);
                    var modelCid = GetCanonicalId(modelRoot);
                    var ident = GetModelIdentityFromItem(modelRoot);
                    var modelName = ident.fileOnly ?? "";
                    var displayPath = GetPathSteps(item, includeCanonical: true, reverse: true); 
                    var cid = GetCanonicalId(item);    

                    result.Items.Add(new PropertyItemDto
                    {
                        canonical_id = cid,
                        path_from_this_object = displayPath,
                        model_name = modelName,     
                        model_canonical_id = modelCid,     
                        PropertyValue = value
                    });
                    added++;

                    if (max.HasValue && added >= max.Value) break;
                }
            }

            result.count = result.Items.Count;
            
            LogHelper.LogEvent($"list_items_to_property: {result.count} Treffer.", "ListItemsToPropertyAsync");
            return await Task.FromResult(result);
        }

        /// <summary>
        /// Resolves which model roots are allowed based on a textual <paramref name="modelFilter"/>.
        /// </summary>
        /// <param name="doc">
        /// The current <see cref="Document"/> from which submodels are scanned.  
        /// If <c>null</c>, the method returns an empty set.
        /// </param>
        /// <param name="modelFilter">
        /// Filter expression; may contain one or more tokens separated by comma, semicolon, or newline.  
        /// Each token is matched against:
        ///   - Submodel canonical id (GUID or p:hash)  
        ///   - Submodel file name (with extension)  
        ///   - Submodel display name  
        ///   - Submodel extension (e.g. ".ifc")  
        /// </param>
        /// <param name="ct">Cancellation token to stop scanning submodels early.</param>
        /// <returns>
        /// A <see cref="HashSet{ModelItem}"/> containing the matching submodel roots.  
        /// If no tokens match, the set is empty.
        /// </returns>
        /// <remarks>
        /// Behavior:  
        ///   - Token comparison is case-insensitive.  
        ///   - Stops checking a submodel as soon as the first token matches.  
        ///   - Uses <c>ScanSubModels</c> with <c>includeContainers=false</c> (container roots are skipped).  
        /// </remarks> 
        private HashSet<ModelItem> GetAllowedModelRootsByModelFilter(Document doc, string modelFilter, CancellationToken ct)
        {
            var allowed = new HashSet<ModelItem>();
            if (string.IsNullOrWhiteSpace(modelFilter) || doc == null) return allowed;

             
            var tokens = modelFilter
                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (tokens.Count == 0) return allowed;

            var subs = ScanSubModels(doc, ct, false);  
            foreach (var sm in subs)
            {
                 
                var cid = sm.CanonicalId ?? "";
                var fileOnly = sm.FileOnly ?? "";
                var display = sm.Display ?? "";
                var ext = sm.Ext ?? "";

                foreach (var tk in tokens)
                {
                     
                    if (!string.IsNullOrEmpty(cid) && cid.Equals(tk, StringComparison.OrdinalIgnoreCase))
                    { allowed.Add(sm.Root); break; }

                     
                    if (!string.IsNullOrEmpty(fileOnly) && fileOnly.Equals(tk, StringComparison.OrdinalIgnoreCase))
                    { allowed.Add(sm.Root); break; }

                     
                    if (!string.IsNullOrEmpty(display) && display.Equals(tk, StringComparison.OrdinalIgnoreCase))
                    { allowed.Add(sm.Root); break; }

                    if (!string.IsNullOrEmpty(ext) && ext.Equals(tk, StringComparison.OrdinalIgnoreCase))
                    { allowed.Add(sm.Root); break; }
                }
            }

            return allowed;
        }

        /// <summary>
        /// Resolves the set of <see cref="ModelItem"/>s defined by a given <paramref name="scope"/> string.  
        /// </summary>
        /// <param name="doc">
        /// The current <see cref="Document"/>.  
        /// If <c>null</c> or <c>doc.Models</c> is <c>null</c>, the method returns an empty list.
        /// </param>
        /// <param name="scope">
        /// Scope expression.  
        /// Currently ignored in this simplified fallback implementation –  
        /// always returns all <c>DescendantsAndSelf</c> of each model root.
        /// </param>
        /// <returns>
        /// A <see cref="List{ModelItem}"/> containing all items in the document,  
        /// or an empty list if the document is missing or has no models.
        /// </returns>
        /// <remarks>
        /// This implementation is a placeholder:  
        ///   - Always enumerates the full model tree, regardless of <paramref name="scope"/>.  
        ///   - For real behavior, extend it to support scope tokens like "all", "selection", "submodel:XYZ", etc.  
        /// </remarks>
        private List<ModelItem> ResolveScopeItems(Document doc, string scope)
        {
             
            return doc?.Models?.RootItems?
                .SelectMany(root => root.DescendantsAndSelf)
                .ToList()
                ?? new List<ModelItem>();
        }


        /// <summary>
        /// Attempts to read the display value of a specific property from a <see cref="ModelItem"/>.  
        /// </summary>
        /// <param name="item">
        /// The <see cref="ModelItem"/> to inspect.  
        /// If <c>null</c> or has no property categories, the method returns <c>false</c>.
        /// </param>
        /// <param name="category">
        /// The display name of the property category (case-insensitive).  
        /// Only categories with a matching <c>DisplayName</c> are considered.
        /// </param>
        /// <param name="property">
        /// The display name of the property within the matched category (case-insensitive).  
        /// Only properties with a matching <c>DisplayName</c> are considered.
        /// </param>
        /// <param name="display">
        /// [out] The formatted display value of the property,  
        /// or <c>null</c> if the property is not found or has no display value.
        /// </param>
        /// <param name="rawVariantOwner">
        /// [out] The <see cref="PropertyCategory"/> object that owns the property if found;  
        /// otherwise <c>null</c>.
        /// </param>
        /// <returns>
        /// <c>true</c> if the property was found and a non-empty display value could be retrieved;  
        /// otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// - Category and property names are matched by <c>DisplayName</c>, case-insensitively.  
        /// - The actual formatting of the property value is delegated to <see cref="FormatVariantValue"/>.  
        /// - Skips null categories or properties to ensure robustness.  
        /// </remarks>
        private bool TryReadDisplayValue(ModelItem item, string category, string property, out string display, out Autodesk.Navisworks.Api.PropertyCategory rawVariantOwner)
        {
            display = null;
            rawVariantOwner = null;

            var cats = item?.PropertyCategories;
            if (cats == null) return false;

             
            foreach (PropertyCategory cat in cats)
            {
                if (cat == null || string.IsNullOrEmpty(cat.DisplayName)) continue;
                if (string.Equals(cat.DisplayName, category, StringComparison.OrdinalIgnoreCase))
                {
                    rawVariantOwner = cat;
                    foreach (DataProperty dp in cat.Properties)
                    {
                        if (dp == null || string.IsNullOrEmpty(dp.DisplayName)) continue;
                        if (string.Equals(dp.DisplayName, property, StringComparison.OrdinalIgnoreCase))
                        {
                             
                            display = FormatVariantValue(dp.Value);
                            return !string.IsNullOrEmpty(display);
                        }
                    }
                }
            }
            return false;
        }


        /// <summary>
        /// Builds a predicate function that evaluates whether a given string matches
        /// a specified filter expression.
        /// </summary>
        /// <param name="valueFilter">
        /// The filter expression to parse.  
        /// Supported formats:
        /// <list type="bullet">
        ///   <item><description><c>/regex/</c> → regex match (case-sensitive)</description></item>
        ///   <item><description><c>/regex/i</c> → regex match (case-insensitive)</description></item>
        ///   <item><description>Comparison operators with numeric RHS: <c>&gt;, &gt;=, &lt;, &lt;=</c></description></item>
        ///   <item><description>Equality operators: <c>==, !=</c></description></item>
        ///   <item><description>String match operators: <c>~=</c> (contains), <c>^=</c> (starts with), <c>$=</c> (ends with)</description></item>
        ///   <item><description>Default: substring search</description></item>
        /// </list>
        /// </param>
        /// <param name="ignoreCase">
        /// If true, comparisons and substring searches ignore case.  
        /// Regex case-sensitivity is controlled separately by the <c>/.../i</c> suffix.
        /// </param>
        /// <returns>
        /// A <see cref="Func{T, TResult}"/> delegate that evaluates a string against the filter.  
        /// Returns <c>null</c> if <paramref name="valueFilter"/> is null or whitespace.
        /// </returns>
        /// <remarks>
        /// - Attempts numeric parsing of RHS values for mathematical comparisons.  
        /// - Trims whitespace around the RHS before evaluation.  
        /// - Null or empty input strings never match except for inequality (<c>!=</c>).  
        /// - Internal helpers (<c>Norm</c>, <c>Contains</c>, <c>TryParseInvariant</c>) ensure consistent trimming and culture-invariant parsing.
        /// </remarks>
        private Func<string, bool> BuildValuePredicate(string valueFilter, bool ignoreCase)
        {
            if (string.IsNullOrWhiteSpace(valueFilter)) return null;

             
            if (valueFilter.Length >= 2 && valueFilter[0] == '/' && valueFilter.Last() == '/')
            {
                var pattern = valueFilter.Substring(1, valueFilter.Length - 2);
                var rx = new System.Text.RegularExpressions.Regex(pattern);
                return s => s != null && rx.IsMatch(s);
            }
            if (valueFilter.Length >= 3 && valueFilter.StartsWith("/") && valueFilter.EndsWith("/i"))
            {
                var pattern = valueFilter.Substring(1, valueFilter.Length - 3);
                var rx = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                return s => s != null && rx.IsMatch(s);
            }

             
            string[] ops = new[] { ">=", "<=", "==", "!=", ">", "<", "~=", "^=", "$=" };
            string op = null;
            string rhs = null;

            foreach (var candidate in ops)
            {
                if (valueFilter.StartsWith(candidate, StringComparison.Ordinal))
                {
                    op = candidate;
                    rhs = valueFilter.Substring(candidate.Length);
                    break;
                }
            }

             
            if (op == null)
            {
                var needle = ignoreCase ? valueFilter.ToLowerInvariant() : valueFilter;
                return s =>
                {
                    if (s == null) return false;
                    var hay = ignoreCase ? s.ToLowerInvariant() : s;
                    return hay.Contains(needle);
                };
            }

             
            rhs = (rhs ?? "").Trim();

             
            double rhsNum;
            bool rhsIsNum = double.TryParse(rhs, NumberStyles.Float, CultureInfo.InvariantCulture, out rhsNum);

            switch (op)
            {
                case "==":
                    return s => string.Equals(Norm(s, ignoreCase), Norm(rhs, ignoreCase), ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                case "!=":
                    return s => !string.Equals(Norm(s, ignoreCase), Norm(rhs, ignoreCase), ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                case ">":
                case ">=":
                case "<":
                case "<=":
                    if (!rhsIsNum) return _ => false;
                    return s =>
                    {
                        if (!TryParseInvariant(Norm(s, ignoreCase), out var l)) return false;
                        switch (op)
                        {
                            case ">": return l > rhsNum;
                            case ">=": return l >= rhsNum;
                            case "<": return l < rhsNum;
                            case "<=": return l <= rhsNum;
                        }
                        return false;
                    };
                case "~=":  
                    return s => Contains(Norm(s, ignoreCase), Norm(rhs, ignoreCase), ignoreCase);
                case "^=":  
                    return s =>
                    {
                        var L = Norm(s, ignoreCase);
                        var R = Norm(rhs, ignoreCase);
                        return L != null && R != null && L.StartsWith(R, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                    };
                case "$=": // endet mit
                    return s =>
                    {
                        var L = Norm(s, ignoreCase);
                        var R = Norm(rhs, ignoreCase);
                        return L != null && R != null && L.EndsWith(R, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                    };
            }
            return null;

             
            string Norm(string x, bool ic) => x?.Trim();
            bool Contains(string a, string b, bool ic)
            {
                if (a == null || b == null) return false;
                if (ic) { a = a.ToLowerInvariant(); b = b.ToLowerInvariant(); }
                return a.Contains(b);
            }
            bool TryParseInvariant(string s, out double val) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out val);
        }

        /// <summary>
        /// Resolves the root <see cref="ModelItem"/> of a given item within a model hierarchy.
        /// </summary>
        /// <param name="item">
        /// The starting <see cref="ModelItem"/>.  
        /// If <c>null</c>, the method immediately returns <c>null</c>.
        /// </param>
        /// <returns>
        /// The top-most ancestor of <paramref name="item"/> (i.e., the model root).  
        /// Returns <c>null</c> if <paramref name="item"/> is <c>null</c> or has no ancestors.
        /// </returns>
        /// <remarks>
        /// Iterates through <see cref="ModelItem.AncestorsAndSelf"/> and returns the last element,  
        /// which represents the highest node in the hierarchy.  
        /// This is commonly used to identify the root of a submodel for filtering or scoping.
        /// </remarks>
        private ModelItem GetModelRootOf(ModelItem item)
        {
            if (item == null) return null;
            ModelItem root = null;
            foreach (var n in item.AncestorsAndSelf ?? Enumerable.Empty<ModelItem>())
                root = n;  
            return root;
        }

        /// <summary>
        /// Extracts a simplified identity tuple (<c>fileOnly</c>, <c>ext</c>, <c>display</c>) from a <see cref="ModelItem"/>.
        /// </summary>
        /// <param name="item">
        /// The <see cref="ModelItem"/> for which identity information should be resolved.  
        /// If <c>null</c>, the method falls back to default values defined in <see cref="ResolveSubModelIdentity"/>.
        /// </param>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///   <item><c>fileOnly</c>: The file name (with extension), or the display name if no file is available.</item>
        ///   <item><c>ext</c>: The file extension (e.g., <c>.nwd</c>, <c>.ifc</c>), or an empty string if none could be determined.</item>
        ///   <item><c>display</c>: A user-facing display string (usually the file name without extension).</item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// Internally calls <see cref="ResolveSubModelIdentity"/> to gather identity information.  
        /// Ensures that <c>fileOnly</c> is never empty by falling back to the display name.
        /// </remarks>
        private (string fileOnly, string ext, string display) GetModelIdentityFromItem(ModelItem item)
        {
             
            var r = ResolveSubModelIdentity(item: item);
             
            var fileOnly = string.IsNullOrWhiteSpace(r.fileOnly) ? r.display : r.fileOnly;
            return (fileOnly, r.ext, r.display);
        }












        /// <summary>
        /// Retrieves the currently active <see cref="Document"/> from Navisworks,
        /// ensuring a non-crashing access point with logging.
        /// </summary>
        /// <returns>
        /// The active <see cref="Document"/> instance if available; otherwise <c>null</c>.
        /// </returns>
        /// <remarks>
        /// - Logs a warning if no active document is present.  
        /// - Logs basic metadata (<c>Title</c>, <c>FileName</c>) if a document is available.  
        /// - Catches and logs exceptions during access (e.g., if Navisworks API state is unstable).  
        /// This method should be used instead of directly accessing <c>Application.ActiveDocument</c>
        /// to provide resilience and diagnostics.
        /// </remarks>
        private Document RequireDocument()
        {
            try
            {
                var doc = Application.ActiveDocument;
                if (doc == null)
                    LogHelper.LogWarning("[FALLBACK] 🔍 Aktives Dokument: none");
                else
                    LogHelper.LogEvent($"[FALLBACK] 🔍 Aktives Dokument: Title='{doc.Title}', File='{doc.FileName}'");
                return doc;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[FALLBACK] Fehler beim Zugriff auf ActiveDocument: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Enumerates all <see cref="ModelItem"/> objects in the given <see cref="Document"/>.
        /// </summary>
        /// <param name="doc">
        /// The Navisworks <see cref="Document"/> to scan.  
        /// If <c>null</c> or <c>doc.Models</c> is <c>null</c>, the sequence is empty.
        /// </param>
        /// <returns>
        /// An <see cref="IEnumerable{T}"/> of all <see cref="ModelItem"/> instances,
        /// including each model's <c>RootItem</c> and all its descendants.
        /// </returns>
        /// <remarks>
        /// - Uses <c>DescendantsAndSelf</c> to ensure the root item is included.  
        /// - Safely skips <c>null</c> roots.  
        /// - Designed as an iterator method (<c>yield return</c>) to allow streaming large documents.  
        /// </remarks>
        private IEnumerable<ModelItem> EnumerateAllItems(Document doc)
        {
            if (doc == null || doc.Models == null) yield break;
            foreach (Model m in doc.Models)
            {
                var root = m?.RootItem;
                if (root == null) continue;
                foreach (var mi in root.DescendantsAndSelf ?? Enumerable.Empty<ModelItem>())
                    yield return mi;
            }
        }


        /// <summary>
        /// Resolves a normalized identity for a given Navisworks <see cref="Model"/> or <see cref="ModelItem"/>.
        /// </summary>
        /// <param name="model">
        /// Optional <see cref="Model"/> input.  
        /// If provided, the method derives file name, extension, and display name from <c>SourceFileName</c> or <c>FileName</c>.  
        /// </param>
        /// <param name="item">
        /// Optional <see cref="ModelItem"/> input.  
        /// If provided, the method attempts to extract the source file via known property categories
        /// ("Item → Source File", "Element → Quelldatei") or falls back to <c>DisplayName</c>.  
        /// </param>
        /// <returns>
        /// A tuple with the following elements:  
        /// - <c>fileOnly</c>: File name with extension, or empty string if unavailable.  
        /// - <c>ext</c>: File extension (e.g., ".nwd", ".ifc").  
        /// - <c>display</c>: Human-readable name (usually file name without extension or item display name).  
        /// - <c>source</c>: Diagnostic string indicating which resolution branch was taken  
        ///   ("Model.Source/File", "Model.RootItem.DisplayName", "Item.props/display", "Model.Fallback(no-ext)", or "none").  
        /// </returns>
        /// <remarks>
        /// - If both <paramref name="model"/> and <paramref name="item"/> are <c>null</c>, a default tuple is returned.  
        /// - For items without a detectable extension, IFC properties ("IFC Class") are probed to assign ".ifc".  
        /// - This method ensures consistent naming and prevents "None name" artifacts.  
        /// </remarks>
        private (string fileOnly, string ext, string display, string source)
        ResolveSubModelIdentity(Model model = null, ModelItem item = null)
        {
            try
            {
                if (model != null)
                {
                    var raw = !string.IsNullOrWhiteSpace(model?.SourceFileName) ? model.SourceFileName : model?.FileName;
                    var ext = string.IsNullOrWhiteSpace(raw) ? "" : Path.GetExtension(raw);
                    var fileOnly = string.IsNullOrWhiteSpace(raw) ? "" : Path.GetFileName(raw);
                    var display = string.IsNullOrWhiteSpace(fileOnly) ? "" : Path.GetFileNameWithoutExtension(fileOnly);
                    if (!string.IsNullOrWhiteSpace(ext))
                        return (fileOnly, ext, display, "Model.Source/File");

                    var dn = model?.RootItem?.DisplayName ?? DEFAULT_UNNAMED;
                    var ext2 = string.IsNullOrWhiteSpace(dn) ? "" : Path.GetExtension(dn);
                    if (!string.IsNullOrWhiteSpace(ext2))
                        return (Path.GetFileName(dn), ext2, Path.GetFileNameWithoutExtension(dn), "Model.RootItem.DisplayName");

                    return (dn, "", dn, "Model.Fallback(no-ext)");
                }

                if (item != null)
                {
                    string candidate =
                        TryGetPropertyValue(item, "Item", "Source File")
                        ?? TryGetPropertyValue(item, "Element", "Quelldatei")
                        ?? item.DisplayName
                        ?? "";

                    string ext = string.IsNullOrWhiteSpace(candidate) ? "" : Path.GetExtension(candidate);
                    string fileOnly = string.IsNullOrWhiteSpace(candidate) ? "" : Path.GetFileName(candidate);
                    string display = string.IsNullOrWhiteSpace(fileOnly)
                        ? (item.DisplayName ?? DEFAULT_UNNAMED)
                        : Path.GetFileNameWithoutExtension(fileOnly);

                    if (string.IsNullOrWhiteSpace(ext))
                    {
                        var ifcClass =
                            TryGetPropertyValue(item, "LcOpGeometryProperty", "IFC Class")
                            ?? TryGetPropertyValue(item, "IFC", "Class")
                            ?? TryGetPropertyValue(item, "IFC", "IFC Class");
                        if (!string.IsNullOrWhiteSpace(ifcClass))
                            ext = ".ifc";
                    }

                    return (fileOnly, ext ?? "", display, "Item.props/display");
                }
            }
            catch {   }

            return ("", "", DEFAULT_UNNAMED, "none");
        }


        /// <summary>
        /// Enumerates all submodels of a given Navisworks <see cref="Document"/> and returns
        /// normalized identity tuples for each.
        /// </summary>
        /// <param name="doc">
        /// The active <see cref="Document"/> whose models and submodels should be enumerated.  
        /// </param>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> that allows the enumeration loop to be aborted early.  
        /// </param>
        /// <returns>
        /// An <see cref="IEnumerable{T}"/> of tuples, each containing:  
        /// - <c>fileOnly</c>: File name with extension if resolvable, otherwise a fallback name.  
        /// - <c>ext</c>: File extension (e.g., ".nwc", ".nwd", ".ifc"), or empty string.  
        /// - <c>display</c>: Human-readable display name (file name without extension or item display).  
        /// - <c>item</c>: The associated <see cref="ModelItem"/> representing the submodel root.  
        /// </returns>
        /// <remarks>
        /// Branching behavior:  
        /// - **Multiple models** or single non-container model → iterate <c>doc.Models</c>.  
        /// - **Single container model (.nwd/.nwf)** → iterate <c>RootItem.Children</c>.  
        /// - **Empty document** → returns no results.  
        /// - If a container is detected, the container itself and its immediate children are included.  
        /// Logs extensive information for diagnostics and handles cancellation gracefully.  
        /// </remarks>
        private IEnumerable<(string fileOnly, string ext, string display, ModelItem item)>
        EnumerateSubModels(Document doc, CancellationToken ct)
        {
            var results = new List<(string fileOnly, string ext, string display, ModelItem item)>();

            try
            {
                var models = doc.Models;
                int mc = (models != null) ? models.Count : 0;
                LogHelper.LogInfo($"[FALLBACK] 📦 Document.Models.Count = {mc}");

                Model firstModel = null;
                if (mc > 0)
                {
                    foreach (Model m in models) { firstModel = m; break; }
                }

                bool looksLikeContainer = false;
                if (mc == 1 && firstModel != null)
                {
                    var raw1 = !string.IsNullOrWhiteSpace(firstModel.SourceFileName) ? firstModel.SourceFileName : firstModel.FileName;
                    var ext1 = string.IsNullOrWhiteSpace(raw1) ? "" : Path.GetExtension(raw1);
                    looksLikeContainer = string.IsNullOrWhiteSpace(ext1) || IsContainerExt(ext1);
                    LogHelper.LogInfo($"[FALLBACK] 🔎 Single model ext='{ext1}', looksLikeContainer={looksLikeContainer}");
                }

                if (mc > 1 || (mc == 1 && !looksLikeContainer))
                {
                    LogHelper.LogInfo("[FALLBACK] Branch: NWF/unsaved → iteriere doc.Models.");
                    foreach (Model m in models)
                    {
                        if (ct.IsCancellationRequested) { LogHelper.LogInfo("[FALLBACK] ⏹️ Abgebrochen (CT)."); break; }

                        var resolved = ResolveSubModelIdentity(model: m);
                        string fileOnly = resolved.fileOnly;
                        string ext = resolved.ext;
                        string display = string.IsNullOrWhiteSpace(resolved.display) ? fileOnly : resolved.display;

                        display = SafeNameOrDefault(display, fileOnly, DEFAULT_UNNAMED);

                        LogHelper.LogInfo($"[FALLBACK] 🧭 Model-Resolve: fileOnly='{fileOnly}', ext='{ext}', src={resolved.source}");

                        if (IsContainerExt(ext))
                        {
                            results.Add((SafeNameOrDefault(fileOnly, display, DEFAULT_UNNAMED), ext, display, m.RootItem));
                            LogHelper.LogInfo($"[FALLBACK] ➕ Container aufgenommen: '{display}' ({ext})");

                            var children = m.RootItem?.Children;
                            int cc = (children != null) ? children.Count() : 0;
                            LogHelper.LogInfo($"[FALLBACK]   ↳ Children Count = {cc}");

                            if (children != null && cc > 0)
                            {
                                foreach (ModelItem child in children)
                                {
                                    if (ct.IsCancellationRequested) { LogHelper.LogInfo("[FALLBACK] ⏹️ Abgebrochen (CT)."); break; }
                                    var cres = ResolveSubModelIdentity(item: child);
                                    var cdisplay = SafeNameOrDefault(cres.display, cres.fileOnly, DEFAULT_UNNAMED);
                                    results.Add((SafeNameOrDefault(cres.fileOnly, cdisplay, DEFAULT_UNNAMED), cres.ext, cdisplay, child));
                                }
                            }
                            continue;
                        }

                        results.Add((SafeNameOrDefault(fileOnly, display, DEFAULT_UNNAMED), ext, display, m.RootItem));
                    }
                }
                else if (mc == 1 && looksLikeContainer)
                {
                    LogHelper.LogInfo("[FALLBACK] Branch: NWD/NWF → iteriere RootItem.Children (Top-Level-Modelle).");
                    var children = firstModel?.RootItem?.Children;
                    int childCount = (children != null) ? children.Count() : 0;
                    LogHelper.LogInfo($"[FALLBACK] RootItem.Children Count = {childCount}");

                    if (children != null && childCount > 0)
                    {
                        foreach (ModelItem child in children)
                        {
                            if (ct.IsCancellationRequested) { LogHelper.LogInfo("[FALLBACK] ⏹️ Abgebrochen (CT)."); break; }
                            var resolved = ResolveSubModelIdentity(item: child);
                            var display = SafeNameOrDefault(resolved.display, resolved.fileOnly, DEFAULT_UNNAMED);
                            results.Add((SafeNameOrDefault(resolved.fileOnly, display, DEFAULT_UNNAMED), resolved.ext, display, child));
                        }
                    }
                }
                else
                {
                    LogHelper.LogInfo("[FALLBACK] ℹ️ Keine Models vorhanden (doc.Models leer).");
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogWarning($"[FALLBACK] EnumerateSubModels: Fehler beim Erfassen der Untermodelle: {ex.Message}", "FALLBACK");
            }

            return results;
        }


        /// <summary>
        /// Scans all submodels of the given <see cref="Document"/> and converts them into
        /// strongly typed <see cref="SubModel"/> objects.
        /// </summary>
        /// <param name="doc">
        /// The active <see cref="Document"/> whose submodels should be scanned.  
        /// </param>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> that allows the operation to be aborted early.  
        /// </param>
        /// <param name="includeContainers">
        /// If <c>true</c>, container formats (e.g., .nwd/.nwf) are included in the result;  
        /// if <c>false</c>, they are skipped.  
        /// </param>
        /// <returns>
        /// A <see cref="List{T}"/> of <see cref="SubModel"/> instances, each describing one submodel with:  
        /// - <c>FileOnly</c>: File name of the submodel (fallback to display if missing).  
        /// - <c>Ext</c>: File extension (normalized, never <c>null</c>).  
        /// - <c>Display</c>: Human-readable display name.  
        /// - <c>Root</c>: The <see cref="ModelItem"/> root of the submodel.  
        /// - <c>CanonicalId</c>: Canonical identifier for stable referencing.  
        /// - <c>IsContainer</c>: Flag indicating whether the submodel is a container (.nwd/.nwf).  
        /// </returns>
        /// <remarks>
        /// This method wraps <see cref="EnumerateSubModels"/> and performs normalization:  
        /// - Ensures safe names for file and display values.  
        /// - Adds canonical IDs via <see cref="GetCanonicalId"/>.  
        /// - Skips container entries unless explicitly requested.  
        /// </remarks>
        private List<SubModel> ScanSubModels(Document doc, CancellationToken ct, bool includeContainers)
        {
            var list = new List<SubModel>();
            foreach (var tup in EnumerateSubModels(doc, ct))
            {
                var isCont = IsContainerExt(tup.ext);
                if (!includeContainers && isCont) continue;

                var cid = GetCanonicalId(tup.item);
                list.Add(new SubModel
                {
                    FileOnly = SafeNameOrDefault(tup.fileOnly, tup.display, DEFAULT_UNNAMED),
                    Ext = tup.ext ?? "",
                    Display = SafeNameOrDefault(tup.display, tup.fileOnly, DEFAULT_UNNAMED),
                    Root = tup.item,
                    CanonicalId = cid,
                    IsContainer = isCont
                });
            }
            return list;
        }

        // ===================== Public RPC ============================


        /// <summary>
        /// Lists all models and submodels from the currently active Navisworks <see cref="Document"/>.  
        /// </summary>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> that allows the operation to be aborted early.  
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> returning a <see cref="DtoList{ModelDetailDto}"/> that contains:  
        /// - One synthetic entry for the active document itself (at index 0).  
        /// - One entry for each discovered submodel (with counts for children and descendants).  
        /// </returns>
        /// <remarks>
        /// Behavior:
        /// - If no document is loaded (<c>null</c>), returns an empty list.  
        /// - Uses <see cref="ScanSubModels"/> to enumerate all submodels (excluding containers).  
        /// - Each submodel is mapped into a <see cref="ModelDetailDto"/> with canonical ID,  
        ///   file name, display name, children count, and descendants count.  
        /// - Adds an additional synthetic "container" entry at the top of the list  
        ///   representing the main document (using title or fallback naming).  
        ///
        /// Logging:
        /// - Emits events for document state, number of models, and missing canonical IDs.  
        ///
        /// Notes:
        /// - Canonical IDs may be missing for certain entries → these items are not selectable.  
        /// - Ensures robustness by wrapping nulls with <c>NullSafe</c> before assignment.  
        /// </remarks>
        public Task<DtoList<ModelDetailDto>> ListModelsAsync(CancellationToken ct)
        {
            LogHelper.LogEvent("RPC list_models (Fallback) gestartet.");
            var doc = RequireDocument();

            var list = new DtoList<ModelDetailDto>();
            if (doc == null)
            {
                LogHelper.LogSuccess("[FALLBACK] list_models: 0 Modelle.");
                return Task.FromResult(list);
            }

            bool isSaved;
            string containerFileNameOnly = GetContainerFileNameOnly(doc, out isSaved);
            LogHelper.LogInfo($"[FALLBACK] 🔍 Aktives Dokument: Title='{(string.IsNullOrWhiteSpace(doc.Title) ? "Unbenannt" : doc.Title)}', File='{(isSaved ? doc.FileName : "(nicht gespeichert)")}'");

            var subs = ScanSubModels(doc, ct, false);  

            int subModelCount = 0;
            foreach (var sm in subs)
            {
                if (string.IsNullOrWhiteSpace(sm.CanonicalId))
                    LogHelper.LogInfo("[FALLBACK] ℹ️ Keine CanonicalId verfügbar → Eintrag ist (noch) nicht selektierbar.");

                list.Add(new ModelDetailDto
                {
                    canonical_id = NullSafe(sm.CanonicalId),
                     
                    FileName = NullSafe(sm.FileOnly),
                    SourceFileName = NullSafe(sm.Ext),
                    DisplayName = NullSafe(!string.IsNullOrWhiteSpace(sm.FileOnly) ? sm.FileOnly : sm.Display),
                    ChildrenCount = sm.Root?.Children?.Count() ?? 0,
                    DescendantsCount = sm.Root?.DescendantsAndSelf?.Count() ?? 0,
                    perent_canonical_id = GetCanonicalId(sm.Root?.Parent)
                });
                subModelCount++;
            }

             
            list.Insert(0, new ModelDetailDto
            {
                canonical_id = Guid.NewGuid().ToString("D"),
                DisplayName = !string.IsNullOrWhiteSpace(doc.Title)
                            ? doc.Title
                            : (isSaved ? Path.GetFileNameWithoutExtension(containerFileNameOnly) : "Unbenannt"),
                FileName = containerFileNameOnly,
                SourceFileName = Path.GetExtension(containerFileNameOnly),
                ChildrenCount = subModelCount,
                DescendantsCount = subModelCount
            });

            LogHelper.LogSuccess($"[FALLBACK] list_models: {list.Count} Eintrag(e) (inkl. {subModelCount} Untermodell(e)).");
            return Task.FromResult(list);
        }


        /// <summary>
        /// Builds an overview of the currently loaded Navisworks <see cref="Document"/>.  
        /// </summary>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> that can be used to cancel the operation early.  
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> returning a <see cref="ModelOverviewDto"/> containing:  
        /// - The active document title.  
        /// - A list of all discovered submodels (<see cref="ModelDetailDto"/>).  
        /// - Total element counts (children + descendants).  
        /// - Categories histogram (empty in fallback mode).  
        /// </returns>
        /// <remarks>
        /// Behavior:
        /// - If no active document is present, returns an empty <see cref="ModelOverviewDto"/>.  
        /// - Iterates all submodels using <see cref="ScanSubModels"/> (excluding containers).  
        /// - Each submodel contributes a <see cref="ModelDetailDto"/> with canonical ID,  
        ///   file and display names, children count, and descendants count.  
        /// - Increments <see cref="ModelOverviewDto.TotalElements"/> per submodel.  
        ///
        /// Logging:
        /// - Logs document title/file, discovered models, and canonical ID issues.  
        /// - Logs success with the number of models found.  
        ///
        /// Notes:
        /// - Submodels without a canonical ID are listed but cannot be selected.  
        /// - This fallback implementation does not populate category histograms.  
        /// </remarks>
        public Task<ModelOverviewDto> GetModelOverviewAsync(CancellationToken ct)
        {
            LogHelper.LogEvent("RPC get_model_overview (Fallback) gestartet.");
            var doc = RequireDocument();

            var dto = new ModelOverviewDto
            {
                categories_histogram = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                available_categories = new List<string>(),
                Models = new List<ModelDetailDto>(),
                total_items = 0,
                TotalElements = 0,
                ModelsCount = 0,
                DocumentTitle = doc?.Title ?? string.Empty
            };

            if (doc == null)
            {
                LogHelper.LogWarning("[FALLBACK] get_model_overview: Kein aktives Dokument.", "FALLBACK");
                return Task.FromResult(dto);
            }

            try
            {
                var title = string.IsNullOrWhiteSpace(doc.Title) ? "Unbenannt" : doc.Title;
                var file = string.IsNullOrWhiteSpace(doc.FileName) ? "(nicht gespeichert)" : doc.FileName;
                LogHelper.LogInfo($"[FALLBACK] 🔍 Aktives Dokument: Title='{title}', File='{file}'");

                var subs = ScanSubModels(doc, ct, false);  

                foreach (var sm in subs)
                {
                    if (ct.IsCancellationRequested) { LogHelper.LogInfo("[FALLBACK] ⏹️ Abgebrochen (CT)."); break; }

                    if (string.IsNullOrWhiteSpace(sm.CanonicalId))
                        LogHelper.LogError("[FALLBACK] ℹ️ Keine CanonicalId verfügbar → Modell in Übersicht, aber nicht selektierbar.", "GetModelOverviewAsync");

                    dto.TotalElements += sm.Root?.Children?.Count() ?? 0;
                    dto.Models.Add(new ModelDetailDto
                    {
                        FileName = NullSafe(!string.IsNullOrWhiteSpace(sm.FileOnly) ? sm.FileOnly : sm.Display),
                        SourceFileName = NullSafe(sm.Ext),
                        DisplayName = NullSafe(sm.Display),
                        ChildrenCount = sm.Root?.Children?.Count() ?? 0,
                        DescendantsCount = sm.Root?.DescendantsAndSelf?.Count() ?? 0,
                        canonical_id = NullSafe(sm.CanonicalId),
                        perent_canonical_id = GetCanonicalId(sm.Root?.Parent)
                    });
                }

                dto.ModelsCount = dto.Models.Count;
                LogHelper.LogInfo($"[FALLBACK] ✅ ModelsCount (aus dto.Models.Count) = {dto.ModelsCount}");
                LogHelper.LogSuccess($"[FALLBACK] get_model_overview: {dto.ModelsCount} Untermodell(e) gelistet.");
            }
            catch (Exception ex)
            {
                LogHelper.LogWarning($"[FALLBACK] get_model_overview: Fehler beim Erfassen der Untermodelle: {ex.Message}", "FALLBACK");
            }

            return Task.FromResult(dto);
        }


        /// <summary>
        /// Retrieves unit and tolerance information from the active Navisworks <see cref="Document"/>.  
        /// </summary>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> that can be used to cancel the operation early.  
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> returning a <see cref="UnitInfoDto"/> with:  
        /// - <see cref="UnitInfoDto.length_unit"/>: "ft", "in", or default "mm".  
        /// - <see cref="UnitInfoDto.area_unit"/>: always "m2".  
        /// - <see cref="UnitInfoDto.volume_unit"/>: always "m3".  
        /// - <see cref="UnitInfoDto.length_tolerance"/>: fixed to 0.001.  
        /// </returns>
        /// <remarks>
        /// Behavior:
        /// - Attempts to read the document’s <c>Units</c> string.  
        /// - Matches "feet" → "ft", "inch" → "in".  
        /// - Falls back to "mm" for all other cases.  
        ///
        /// Logging:
        /// - Logs start of the RPC call.  
        /// - Logs the resolved length unit and tolerance.  
        ///
        /// Notes:
        /// - Area and volume units are currently hardcoded.  
        /// - Tolerance is fixed (0.001).  
        /// - If no active document is available, defaults are returned.  
        /// </remarks>
        public Task<UnitInfoDto> GetUnitsAndTolerancesAsync(CancellationToken ct)
        {
            LogHelper.LogEvent("RPC get_units_and_tolerances (Fallback) gestartet.");
            var doc = RequireDocument();

            var unitsStr = doc?.Units.ToString() ?? "";
            string lengthUnit =
                unitsStr.IndexOf("feet", StringComparison.OrdinalIgnoreCase) >= 0 ? "ft" :
                unitsStr.IndexOf("inch", StringComparison.OrdinalIgnoreCase) >= 0 ? "in" :
                "mm";

            var dto = new UnitInfoDto
            {
                length_unit = lengthUnit,
                area_unit = "m2",
                volume_unit = "m3",
                length_tolerance = 0.001
            };

            LogHelper.LogSuccess($"[FALLBACK] get_units_and_tolerances: length='{dto.length_unit}', tol={dto.length_tolerance.ToString(CultureInfo.InvariantCulture)}");
            return Task.FromResult(dto);
        }


        /// <summary>
        /// Computes a property distribution overview grouped by category for the active <see cref="Document"/>.  
        /// </summary>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> to cancel the operation early.  
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> returning an <see cref="ElementCountDto"/> with:  
        /// - <see cref="ElementCountDto.category"/>: always "(all)" in this fallback.  
        /// - <see cref="ElementCountDto.scope"/>: always "all".  
        /// - <see cref="ElementCountDto.count"/>: total number of property values counted.  
        /// - <see cref="AI_MassageDto.details"/>: JSON summary of category statistics.  
        /// - <see cref="AI_MassageDto.message"/>: Markdown summary of category statistics.  
        /// </returns>
        /// <remarks>
        /// Behavior:
        /// - If no active document is open, returns <c>success = false</c> with message "no document is open".  
        /// - Calls <c>BuildCategoryPropertyMaps</c> to build per-model and total statistics.  
        /// - Populates <c>details</c> with JSON, <c>message</c> with Markdown for reporting.  
        ///
        /// Logging:
        /// - Logs method start, including note that input "category" is ignored in this fallback.  
        /// - Logs total counted property values on success.  
        /// - Logs exceptions with full error details.  
        ///
        /// Notes:
        /// - This fallback ignores the requested category filter.  
        /// - Intended for diagnostic or overview purposes rather than exact filtering.  
        /// </remarks>
        public Task<ElementCountDto> GetPropertyDistributionByCategoryAsync(CancellationToken ct)
        {
            const string TAG = "[FALLBACK] GetPropertyDistributionByCategoryAsync / ID13453 (Model→Category→Property + Overview)";
            LogHelper.LogEvent($"{TAG} gestartet (Hinweis: 'category' wird ignoriert).");

            var doc = RequireDocument();
            var dto = new ElementCountDto
            {
                category = "(all)",
                scope = "all",
                count = 0,
                success = true,
                message = ""
            };

            if (doc == null)
            {
                dto.success = false;
                dto.message = "no document is open";
                return Task.FromResult(dto);
            }

            try
            {
                var tuple = BuildCategoryPropertyMaps(doc, ct);
                var map = tuple.perModel;
                var total = tuple.total;

                dto.count = total;
                dto.details = RenderCategoryStatsAsJson(map);
                dto.message = RenderCategoryStatsAsMarkdown(map);

                LogHelper.LogSuccess($"{TAG}: Gesamt gezählte Property-Werte = {total}");
                return Task.FromResult(dto);
            }
            catch (Exception ex)
            {
                dto.success = false;
                dto.message = $"error: {ex.Message}";
                LogHelper.LogError($"{TAG}: Fehler: {ex}");
                return Task.FromResult(dto);
            }
        }


        /// <summary>
        /// Counts how many <see cref="ModelItem"/> objects contain the given property category
        /// within the specified <paramref name="scope"/> of the active <see cref="Document"/>.  
        /// </summary>
        /// <param name="category">
        /// The property category name to search for (e.g., "Element", "IFC").  
        /// Must not be null or empty.  
        /// </param>
        /// <param name="scope">
        /// Scope restriction:  
        /// - "all" → search across the entire document.  
        /// - otherwise interpreted as a model identifier to limit the search.  
        /// </param>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> to cancel the operation early.  
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> producing an <see cref="ElementCountDto"/> with:  
        /// - <c>category</c>: the requested category.  
        /// - <c>scope</c>: the resolved scope ("all" or specific model id).  
        /// - <c>count</c>: number of items found.  
        /// - <c>message</c>: diagnostic information (warnings, scope mismatches, or not found).  
        /// - <c>success</c>: false if document is missing, category empty, or errors occurred.  
        /// </returns>
        /// <remarks>
        /// Behavior:  
        /// - If no active document is loaded, returns <c>success=false</c>.  
        /// - If category is empty, returns <c>success=false</c>.  
        /// - If a scope is given but does not resolve to any model roots, returns failure with diagnostic.  
        /// - Otherwise enumerates items (scoped or all) and counts how many contain the given category.  
        ///
        /// Logging:  
        /// - Logs start of RPC call, scope handling, and counting process.  
        /// - Logs detailed diagnostics on scope selection, visited item count, and execution time.  
        /// - Logs errors and returns failure if scope resolution or counting fails.  
        ///
        /// Notes:  
        /// - Uses <c>CountByCategory</c> helper to count items efficiently.  
        /// - Returns additional guidance in the message if the count is zero (e.g., suggest retry with scope=all).  
        /// </remarks>
        public Task<ElementCountDto> GetElementCountByCategoryAsync(string category, string scope, CancellationToken ct)
        {
            var t0 = DateTime.UtcNow;
            LogHelper.LogEvent($"RPC get_element_count_by_category (Fallback) gestartet: category='{category}', scope='{scope}'.");

            var doc = RequireDocument();
            var dto = new ElementCountDto
            {
                category = NullSafe(category),
                count = 0,
                scope = string.IsNullOrWhiteSpace(scope) ? "all" : scope,
                success = true,
                message = "no warnings"
            };

            if (doc == null)
            {
                dto.success = false;
                dto.message = "no document is open";
                LogHelper.LogWarning("[FALLBACK] get_element_count_by_category: Kein aktives Dokument.");
                return Task.FromResult(dto);
            }
            if (string.IsNullOrWhiteSpace(category))
            {
                dto.success = false;
                dto.message = "category is empty";
                LogHelper.LogWarning("[FALLBACK] get_element_count_by_category: 'category' ist leer.");
                return Task.FromResult(dto);
            }

            var scopeToken = (scope ?? "").Trim();
            var restrictToModels = !string.IsNullOrWhiteSpace(scopeToken) && !scopeToken.Equals("all", StringComparison.OrdinalIgnoreCase);
            List<ModelItem> scopedRoots = null;

            if (restrictToModels)
            {
                try
                {
                    string diag;
                    scopedRoots = ResolveScopeToModelRoots(doc, scopeToken, ct, out diag);

                    if (scopedRoots == null || scopedRoots.Count == 0)
                    {
                        dto.success = false;
                        dto.message = $"scope(model) not matched: '{scopeToken}'. {diag}";
                        LogHelper.LogWarning($"[FALLBACK] get_element_count_by_category: scope '{scopeToken}' ergab keine Modelltreffer. {diag}");
                        return Task.FromResult(dto);
                    }

                    var picked = string.Join(", ", scopedRoots.Select(r =>
                    {
                        var cid = GetCanonicalId(r);
                        var disp = SafeNameOrDefault(r?.DisplayName, r?.ClassDisplayName, DEFAULT_UNNAMED);
                        return $"{disp}|cid:{cid}";
                    }));
                    LogHelper.LogInfo($"[FALLBACK] get_element_count_by_category: Scope aktiv. {scopedRoots.Count} Modell-Root(s) selektiert → [{picked}]");
                }
                catch (Exception ex)
                {
                    dto.success = false;
                    dto.message = $"scope matching error: {ex.Message}";
                    LogHelper.LogError($"[FALLBACK] get_element_count_by_category: Fehler bei Scope-Zuordnung: {ex}");
                    return Task.FromResult(dto);
                }
            }
            else
            {
                LogHelper.LogDebug("[FALLBACK] get_element_count_by_category: Scope='all' → gesamtes Dokument wird gezählt.", "GetElementCountByCategoryAsync");
            }

            IEnumerable<ModelItem> sourceItems = restrictToModels
                ? scopedRoots.SelectMany(r => r?.DescendantsAndSelf ?? Enumerable.Empty<ModelItem>())
                : EnumerateAllItems(doc);

            int visited;
            int count;
            try
            {
                var tuple = CountByCategory(sourceItems, category, ct);
                count = tuple.count;
                visited = tuple.visited;
            }
            catch (Exception ex)
            {
                dto.success = false;
                dto.message = $"counting error: {ex.Message}";
                LogHelper.LogError($"[FALLBACK] get_element_count_by_category: Fehler während der Zählung: {ex}");
                return Task.FromResult(dto);
            }

            dto.count = count;

            var tookMs = (int)(DateTime.UtcNow - t0).TotalMilliseconds;
            if (restrictToModels && count == 0)
            {
                var modelIds = string.Join(", ", scopedRoots.Select(r => GetCanonicalId(r)));
                dto.message = $"category '{category}' not found within scope(model): '{scopeToken}'. models=[{modelIds}]";
                LogHelper.LogInfo($"[FALLBACK] get_element_count_by_category: Kategorie '{category}' im Scope '{scopeToken}' nicht gefunden. Visited={visited}, Took={tookMs}ms");
            }
            else
            {
                LogHelper.LogSuccess($"[FALLBACK] get_element_count_by_category: scope='{(restrictToModels ? scopeToken : "all")}', category='{category}' → count={count}, visited={visited}, Took={tookMs}ms");
            }

            if (dto.count == 0)
            {
                dto.message += $" (category '{category}' not found, if scope was active than maybe help to search over scope=all)";
            }

            return Task.FromResult(dto);
        }


        /// <summary>
        /// Applies a selection in the active <see cref="Document"/> based on canonical IDs.  
        /// </summary>
        /// <param name="canonical_id">
        /// A list of canonical IDs (Navisworks item identifiers) to select.  
        /// - If <c>null</c> or empty, no items are selected.  
        /// - Items that cannot be resolved are ignored.  
        /// </param>
        /// <param name="keepExistingSelection">
        /// If true, keeps the current selection and adds new items.  
        /// If false, clears the current selection before applying the new items.  
        /// </param>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> that allows the operation to be cancelled.  
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> producing a <see cref="List{T}"/> of  
        /// <see cref="SimpleItemRef"/> objects representing successfully selected items.  
        /// </returns>
        /// <remarks>
        /// Behavior:  
        /// - If no document is open, returns an empty list.  
        /// - If no IDs are provided, logs a warning and does not change the selection  
        ///   (use <c>clear_selection</c> RPC instead).  
        /// - Resolves each canonical ID to a <see cref="ModelItem"/>.  
        /// - Calls <c>ApplySelectionInternal</c> to apply the selection in Navisworks.  
        /// - Builds and returns <see cref="SimpleItemRef"/> for each matched item.  
        ///
        /// Logging:  
        /// - Logs start, input parameter details, resolution results, and outcome.  
        /// - Logs warnings if IDs cannot be resolved or if no IDs were provided.  
        /// - Logs errors if unexpected exceptions occur.  
        ///
        /// Notes:  
        /// - Items that cannot be selected are tracked in <c>notSelectable</c>,  
        ///   but currently not returned to the caller.  
        /// - Keeps Navisworks selection state in sync with backend RPC requests.  
        /// </remarks>
        public async Task<List<SimpleItemRef>> ApplySelectionAsync(List<string> canonical_id, bool keepExistingSelection, CancellationToken ct)
        {
            LogHelper.LogEvent($"RPC apply_selection (Fallback) gestartet: ids={canonical_id?.Count ?? 0}, keepExisting={keepExistingSelection}");
            var doc = RequireDocument();

            var resultList = new List<SimpleItemRef>();
            var notSelectable = new List<SimpleItemRef>();

            if (doc == null) return resultList;

            try
            {
                if (ct.IsCancellationRequested) return resultList;

                if (canonical_id == null || canonical_id.Count == 0)
                {
                    LogHelper.LogWarning("[FALLBACK] apply_selection: Keine IDs übergeben. (Hinweis: Auswahl wird NICHT geleert – dafür clear_selection benutzen.)");
                    return resultList;
                }

                var items = ResolveItemsByCanonicalIds(doc, canonical_id);  
                if (items == null || items.Count == 0)
                {
                    LogHelper.LogWarning("[FALLBACK] apply_selection: Keine der IDs auflösbar/selektierbar.");
                    return resultList;
                }

                ApplySelectionInternal(doc, items, keepExistingSelection);

                foreach (var mi in items)
                {
                    var info = Get_Simple_Item_Info(mi);
                    if (info != null) resultList.Add(info);
                }

                if (notSelectable.Count > 0)
                {
                    BuildStringListSimpleItem(notSelectable);
                }

                LogHelper.LogSuccess($"[FALLBACK] apply_selection: matched={resultList.Count}, notSelectable={notSelectable.Count}");
                return resultList;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[FALLBACK] apply_selection: Unerwarteter Fehler: {ex.Message}");
                return resultList;
            }
        }

        /// <summary>
        /// Clears the current selection in the active <see cref="Document"/>.  
        /// </summary>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> that can be used to cancel the operation.  
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> producing an <see cref="int"/> representing  
        /// the number of items that were cleared from the selection.  
        /// </returns>
        /// <remarks>
        /// Behavior:  
        /// - If no document or no current selection is available, nothing is cleared and the result is 0.  
        /// - If a valid selection exists, clears it using the <c>ComApiBridge</c> state.  
        /// - Logs how many items were removed from the selection.  
        ///
        /// Logging:  
        /// - Logs when the RPC call is started.  
        /// - Logs success with the count of cleared items.  
        /// - Logs a warning if no active selection was found.  
        /// - Logs errors if exceptions occur.  
        ///
        /// Notes:  
        /// - Returns only the count of previously selected items, not the cleared items themselves.  
        /// - Used by the <c>clear_selection</c> RPC to reset Navisworks selection state.  
        /// </remarks>
        public Task<int> ClearSelectionAsync(CancellationToken ct)
        {
            LogHelper.LogEvent("RPC clear_selection (Fallback) gestartet.");
            var doc = RequireDocument();

            int affected = 0;
            try
            {
                affected = doc?.CurrentSelection?.SelectedItems?.Count ?? 0;

                if (doc?.CurrentSelection != null)
                {
                    var state = ComApiBridge.State;
                    if (state != null)
                    {
                        state.CurrentSelection.SelectNone();
                    }
                    LogHelper.LogSuccess($"[FALLBACK] clear_selection: cleared={affected}");
                }
                else
                {
                    LogHelper.LogWarning("[FALLBACK] clear_selection: Keine aktuelle Selektion gefunden.");
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[FALLBACK] clear_selection: Fehler: {ex.Message}");
            }

            return Task.FromResult(affected);
        }

        /// <summary>
        /// Captures a snapshot of the current selection in the active <see cref="Document"/>.  
        /// </summary>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> that can be used to cancel the operation.  
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> producing a <see cref="SelectionSnapshotDto"/> that contains:  
        /// - <c>count</c>: number of selected items.  
        /// - <c>canonical_id</c>: list of canonical IDs for the selected items.  
        /// - <c>path</c>: hierarchical path strings for each selected item.  
        /// </returns>
        /// <remarks>
        /// Behavior:  
        /// - If no document or selection exists, returns an empty snapshot (count = 0).  
        /// - Iterates over the <c>CurrentSelection.SelectedItems</c> and collects identifiers and paths.  
        /// - Uses <c>GetCanonicalId</c> and <c>GetPathSteps</c> helpers to normalize output.  
        ///
        /// Logging:  
        /// - Logs when the RPC call is started.  
        /// - Logs the final count of items in the snapshot.  
        ///
        /// Usage:  
        /// - Supports the <c>get_current_selection_snapshot</c> RPC endpoint to let clients query  
        ///   which elements are currently selected in Navisworks.  
        /// </remarks>
        public Task<SelectionSnapshotDto> GetCurrentSelectionSnapshotAsync(CancellationToken ct)
        {
            LogHelper.LogEvent("RPC get_current_selection_snapshot (Fallback) gestartet.");
            var doc = RequireDocument();
            var dto = new SelectionSnapshotDto
            {
                count = 0,
                canonical_id = new List<string>(),
                path = new List<string>()
            };

            if (doc?.CurrentSelection?.SelectedItems == null)
                return Task.FromResult(dto);

            var i = 0;
            foreach (ModelItem mi in doc.CurrentSelection.SelectedItems)
            {
                dto.canonical_id.Add(GetCanonicalId(mi));
                var steps = GetPathSteps(mi, includeCanonical: false, reverse: true);
                dto.path.Add(steps.LastOrDefault()?.paths ?? "");
                i++;
            }
            dto.count = i;

            LogHelper.LogSuccess($"[FALLBACK] get_current_selection_snapshot: count={dto.count}");
            return Task.FromResult(dto);
        }


        /// <summary>
        /// Retrieves all available properties for a given model item by its canonical ID.  
        /// </summary>
        /// <param name="itemId">
        /// The canonical ID of the target <see cref="ModelItem"/>.  
        /// </param>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> that can be used to cancel the operation.  
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> producing an <see cref="ItemPropertiesDto"/> that contains:  
        /// - <c>canonical_id</c>: resolved canonical identifier of the item.  
        /// - <c>element_name</c>: display name of the element.  
        /// - <c>typ</c> / <c>interner_typ</c>: IFC or class information.  
        /// - <c>ifc_guid</c>: IfcGUID/GlobalId if available.  
        /// - <c>categories</c>: all property categories and their properties.  
        /// - <c>geometries</c>: geometry-related property blocks.  
        /// - <c>child_from_this_object</c>: child references of the item.  
        /// - <c>path_from_this_object</c>: hierarchical path steps from the root to this item.  
        /// </returns>
        /// <remarks>
        /// Behavior:  
        /// - Resolves the item from <paramref name="itemId"/> via <c>ResolveItemsByCanonicalIds</c>.  
        /// - If the item cannot be found, returns an empty DTO.  
        /// - Populates head metadata using <c>FillItemHead</c>.  
        /// - Collects properties via <c>Get_Property_Categories_To_Item</c> and geometries via <c>Get_Geometries_To_Item</c>.  
        /// - Builds hierarchical context with <c>Get_NachfolgeRecursive_From_Item</c> and <c>GetPathSteps</c>.  
        ///
        /// Logging:  
        /// - Logs start, category/geometry counts, children/path info, and success or error cases.  
        ///
        /// Usage:  
        /// - Supports the <c>list_properties_for_item</c> RPC endpoint for clients that need  
        ///   a full property dump of a single selected item.  
        /// </remarks>
        public Task<ItemPropertiesDto> Get_ListProperties_For_Item(string itemId, CancellationToken ct)
        {
            const string M = "[FALLBACK] list_properties_for_item";
            LogHelper.LogEvent($"{M} gestartet: requestId='{itemId}'");
            var doc = RequireDocument();
            if (doc == null || doc.Models == null)
            {
                LogHelper.LogWarning($"{M}: Kein aktives Dokument.");
                return Task.FromResult(new ItemPropertiesDto
                {
                    message = "",
                    details = "No active document.",
                });
            }

            var dto = new ItemPropertiesDto
            {
                canonical_id = itemId ?? string.Empty,
                element_name = string.Empty,
                categories = new Dictionary<string, List<SimplePropJson>>(StringComparer.OrdinalIgnoreCase),
                geometries = new Dictionary<string, List<SimplePropJson>>(StringComparer.OrdinalIgnoreCase),
                child_from_this_object = new List<SimpleItemRef>(),
                path_from_this_object = new List<PathStep>()
            };

            try
            {
                var itemList = ResolveItemsByCanonicalIds(doc, new List<string> { itemId });
                var item = (itemList == null || itemList.Count == 0) ? null : itemList.First();

                if (item == null)
                {
                    LogHelper.LogWarning($"{M}: Item '{itemId}' nicht gefunden.");
                    return Task.FromResult(dto);
                }

                 
                string cid, name, typ, ifcGuid;
                FillItemHead(item, out cid, out name, out typ, out ifcGuid);

                dto.canonical_id = cid;
                dto.element_name = name;
                dto.typ = typ;
                dto.interner_typ = item.ClassName ?? "";
                dto.ifc_guid = ifcGuid;

                LogHelper.LogDebug($"{M}: Head -> cid='{dto.canonical_id}', name='{dto.element_name}', typ='{dto.typ}', ifc_guid='{dto.ifc_guid}'");

                 
                var categories = Get_Property_Categories_To_Item(item);
                LogHelper.LogDebug($"{M}: Kategorien gelesen: {categories.Count}");

                var geometries = Get_Geometries_To_Item(item);
                dto.geometries = geometries;
                dto.categories = categories;

                LogHelper.LogDebug($"{M}: Geometrie-Blöcke: {dto.geometries.Count}, Kategorien (bereinigt): {dto.categories.Count}");

                dto.child_from_this_object = Get_NachfolgeRecursive_From_Item(item, 1);
                dto.path_from_this_object = GetPathSteps(item, includeCanonical: true, reverse: true);

                LogHelper.LogDebug($"{M}: Nachfahren gesamt={dto.child_from_this_object.Count}, Struktur-Einträge={dto.path_from_this_object.Count}");
                LogHelper.LogSuccess($"{M}: OK für '{dto.canonical_id}'");
                return Task.FromResult(dto);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"{M}: Fehler: {ex.Message}");
                return Task.FromResult(dto);
            }
        }

        // ========================= Core Helpers =====================


        /// <summary>
        /// Builds a hierarchical map of all properties grouped by model and category.  
        /// </summary>
        /// <param name="doc">
        /// The active <see cref="Document"/> from which models and items are scanned.  
        /// </param>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> that can cancel the operation.  
        /// </param>
        /// <returns>
        /// A tuple containing:  
        /// - <c>perModel</c>: Dictionary keyed by <c>modelId</c>, where each value is a dictionary of categories,  
        ///   and each category maps to a dictionary of property names with their occurrence counts.  
        /// - <c>total</c>: The total number of property values counted across all models.  
        /// </returns>
        /// <remarks>
        /// Behavior:  
        /// - Iterates through all submodels via <c>ScanSubModels</c>.  
        /// - For each model root, traverses <c>DescendantsAndSelf</c> items.  
        /// - Groups property categories (<see cref="PropertyCategory"/>) and their properties (<see cref="DataProperty"/>).  
        /// - Uses <c>SafeNameOrDefault</c> to normalize category/property names.  
        /// - Skips empty property values.  
        /// - Increments counts for each property value found, also updating the global <c>total</c>.  
        ///
        /// Logging / Usage:  
        /// - Typically used by <c>GetPropertyDistributionByCategoryAsync</c> to build statistical overviews.  
        /// - Ensures unique model IDs (falls back to hashed display name if no canonical ID).  
        /// </remarks>
        private (Dictionary<string, Dictionary<string, Dictionary<string, int>>> perModel, int total)
        BuildCategoryPropertyMaps(Document doc, CancellationToken ct)
        {
            var perModel = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>(StringComparer.OrdinalIgnoreCase);
            int totalCount = 0;

            foreach (var sm in ScanSubModels(doc, ct, false))  
            {
                if (ct.IsCancellationRequested) break;
                var root = sm.Root;
                if (root == null) continue;

                string modelId = sm.CanonicalId;
                if (string.IsNullOrWhiteSpace(modelId))
                {
                    var display = SafeNameOrDefault(sm.Display, sm.FileOnly, DEFAULT_UNNAMED);
                    modelId = "p:" + (display ?? "").GetHashCode().ToString("x8");
                }

                Dictionary<string, Dictionary<string, int>> catMap;
                if (!perModel.TryGetValue(modelId, out catMap))
                {
                    catMap = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
                    perModel[modelId] = catMap;
                }

                foreach (var item in root.DescendantsAndSelf ?? Enumerable.Empty<ModelItem>())
                {
                    foreach (PropertyCategory cat in item?.PropertyCategories ?? Enumerable.Empty<PropertyCategory>())
                    {
                        var catName = SafeNameOrDefault(cat?.DisplayName, cat?.Name, DEFAULT_CATEGORY);

                        Dictionary<string, int> propMap;
                        if (!catMap.TryGetValue(catName, out propMap))
                        {
                            propMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            catMap[catName] = propMap;
                        }

                        foreach (DataProperty p in cat?.Properties ?? Enumerable.Empty<DataProperty>())
                        {
                            var propName = SafeNameOrDefault(p?.DisplayName, p?.Name, DEFAULT_PROPERTY);
                            var val = FormatVal(p);
                            if (string.IsNullOrWhiteSpace(val)) continue;

                            if (!propMap.ContainsKey(propName)) propMap[propName] = 0;
                            propMap[propName]++;
                            totalCount++;
                        }
                    }
                }
            }

            return (perModel, totalCount);
        }

        /// <summary>
        /// Counts how many items in a sequence belong to or reference a given category.  
        /// </summary>
        /// <param name="sourceItems">
        /// The sequence of <see cref="ModelItem"/> instances to scan.  
        /// </param>
        /// <param name="category">
        /// The category name (or substring) to match against either  
        /// the item's class name or its property categories.  
        /// Matching is case-insensitive and partial (substring search).  
        /// </param>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> to stop iteration prematurely.  
        /// </param>
        /// <returns>
        /// A tuple containing:  
        /// - <c>count</c>: Number of items matching the given category.  
        /// - <c>visited</c>: Total number of items actually inspected before cancellation or completion.  
        /// </returns>
        /// <remarks>
        /// Matching rules:  
        /// - First checks <c>ClassDisplayName</c> or <c>ClassName</c> of each item.  
        /// - If no match, scans through all <see cref="PropertyCategory"/> names of the item.  
        /// - A match increments <c>count</c> and stops scanning further categories for that item.  
        ///
        /// Cancellation:  
        /// - If the <paramref name="ct"/> is signaled, iteration stops early,  
        ///   returning whatever counts were collected so far.  
        /// </remarks>
        private (int count, int visited) 
        CountByCategory(IEnumerable<ModelItem> sourceItems, string category, CancellationToken ct)
        {
            int count = 0, visited = 0;
            foreach (var item in sourceItems)
            {
                if (ct.IsCancellationRequested) break;
                if (item == null) continue;
                visited++;

                var cls = SafeNameOrDefault(item?.ClassDisplayName, item?.ClassName, "");
                if (!string.IsNullOrEmpty(cls) &&
                    cls.IndexOf(category, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    count++;
                    continue;
                }

                foreach (PropertyCategory cat in item.PropertyCategories ?? Enumerable.Empty<PropertyCategory>())
                {
                    var catName = SafeNameOrDefault(cat?.DisplayName, cat?.Name, "");
                    if (!string.IsNullOrEmpty(catName) &&
                        catName.IndexOf(category, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        count++;
                        break;
                    }
                }
            }
            return (count, visited);
        }

        /// <summary>
        /// Checks whether a given string token appears to be a canonical identifier.  
        /// </summary>
        /// <param name="token">
        /// The input string to test. Can be a GUID or a path-hash identifier.  
        /// </param>
        /// <returns>
        /// <c>true</c> if the token matches one of the recognized canonical ID formats;  
        /// otherwise, <c>false</c>.  
        /// </returns>
        /// <remarks>
        /// Supported formats:  
        /// - A valid <see cref="Guid"/> (parsed using <see cref="Guid.TryParse"/>).  
        /// - A hash-based ID prefixed with <c>"p:"</c>, followed by at least one character.  
        ///
        /// Tokens that are <c>null</c>, empty, or whitespace-only always return <c>false</c>.  
        /// </remarks>
        private bool LooksLikeCanonicalId(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            // GUID?
            Guid g; if (Guid.TryParse(token, out g)) return true;
            // p:<hash>
            return token.StartsWith("p:", StringComparison.OrdinalIgnoreCase) && token.Length > 2;
        }

        /// <summary>
        /// Retrieves the canonical identifier string for a given <see cref="ModelItem"/>.  
        /// </summary>
        /// <param name="item">
        /// The model item for which to compute the identifier.  
        /// </param>
        /// <returns>
        /// A canonical ID string:  
        /// - If the item has a non-empty <see cref="ModelItem.InstanceGuid"/>,  
        ///   the GUID is returned in "D" format.  
        /// - Otherwise, a hash-based ID prefixed with <c>"p:"</c>, generated via  
        ///   <see cref="ComputePathHash(ModelItem)"/>.  
        /// - If <paramref name="item"/> is <c>null</c>, an empty string.  
        /// - If an exception occurs, an error marker <c>"ERR:{message}"</c>.  
        /// </returns>
        /// <remarks>
        /// Canonical IDs provide stable references for selection, clash detection,  
        /// and other RPC operations, regardless of transient display names.  
        /// </remarks>
        private string GetCanonicalId(ModelItem item)
        {
            if (item == null) return string.Empty;

            try
            {
                var g = item.InstanceGuid;
                if (g != Guid.Empty)
                    return g.ToString("D");
                return "p:" + ComputePathHash(item);
            }
            catch (Exception ex)
            {
                return $"ERR:{ex.Message}";
            }
        }

        /// <summary>
        /// Resolves a set of canonical IDs to their corresponding <see cref="ModelItem"/> objects in the document.
        /// </summary>
        /// <param name="doc">
        /// The active <see cref="Document"/> containing models and items to search.
        /// </param>
        /// <param name="ids">
        /// A collection of canonical IDs to resolve.  
        /// Supported formats:  
        /// - <c>GUID</c> (from <see cref="ModelItem.InstanceGuid"/>)  
        /// - <c>"p:&lt;hash&gt;"</c> (path-based hash from <see cref="ComputePathHash(ModelItem)"/>).  
        /// Invalid or unrecognized formats are ignored.
        /// </param>
        /// <returns>
        /// A list of unique <see cref="ModelItem"/> objects that match the given IDs.  
        /// Returns an empty list if no matches are found, if <paramref name="doc"/> is <c>null</c>,  
        /// or if <paramref name="ids"/> is empty.
        /// </returns>
        /// <remarks>
        /// Behavior:  
        /// - Scans all items in <see cref="Document.Models"/> (including descendants).  
        /// - Maintains input order but removes duplicates.  
        /// - Provides detailed logging on preprocessing, scan progress, and results.  
        /// - For very large models, logs progress every 50,000 scanned items.  
        /// - If no matches are found for valid IDs, a warning is logged.  
        /// </remarks>
        /// <exception cref="Exception">
        /// Caught internally: Any unexpected errors during resolution are logged, and an empty list is returned.
        /// </exception>
        private List<ModelItem> ResolveItemsByCanonicalIds(Document doc, IEnumerable<string> ids)
        {
            var result = new List<ModelItem>();
            try
            {
                var incoming = ids?.ToList() ?? new List<string>();
                var totalIn = incoming.Count;
                LogHelper.LogEvent($"Lookup: ResolveItemsByCanonicalIds gestartet. incomingIds={totalIn}");

                if (doc == null)
                {
                    LogHelper.LogWarning("Lookup: Abbruch – kein aktives Dokument.");
                    return result;
                }
                if (totalIn == 0)
                {
                    LogHelper.LogWarning("Lookup: Abbruch – leere oder fehlende ID-Liste.");
                    return result;
                }

                var sw = Stopwatch.StartNew();

                var idsInOrder = incoming.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
                if (idsInOrder.Count == 0)
                {
                    LogHelper.LogWarning("Lookup: Abbruch – alle übergebenen IDs sind leer/whitespace.");
                    return result;
                }

                var resolvedById = new Dictionary<string, List<ModelItem>>(StringComparer.OrdinalIgnoreCase);
                var guidMap = new Dictionary<Guid, List<string>>();
                var hashMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                var unknownIds = new List<string>();

                foreach (var id in idsInOrder)
                {
                    resolvedById[id] = new List<ModelItem>();

                    Guid g;
                    if (Guid.TryParse(id, out g))
                    {
                        List<string> lst;
                        if (!guidMap.TryGetValue(g, out lst)) { lst = new List<string>(); guidMap[g] = lst; }
                        lst.Add(id);
                        continue;
                    }

                    if (id.StartsWith("p:", StringComparison.OrdinalIgnoreCase) && id.Length > 2)
                    {
                        var h = id.Substring(2);
                        if (!string.IsNullOrWhiteSpace(h))
                        {
                            List<string> lst;
                            if (!hashMap.TryGetValue(h, out lst)) { lst = new List<string>(); hashMap[h] = lst; }
                            lst.Add(id);
                        }
                        else unknownIds.Add(id);
                        continue;
                    }

                    unknownIds.Add(id);
                }

                LogHelper.LogDebug(
                    $"Lookup: Preprocess → incoming={totalIn}, usable={idsInOrder.Count}, guids={guidMap.Count}, pHashes={hashMap.Count}, unknown={unknownIds.Count}",
                    "ResolveItemsByCanonicalIds");

                if (unknownIds.Count > 0)
                {
                    LogHelper.LogWarning("Lookup: Unbekannte ID-Formate werden ignoriert:\n" + string.Join(", ", unknownIds.Take(20)) + (unknownIds.Count > 20 ? " ..." : ""));
                }

                IEnumerable<ModelItem> allItems =
                    (doc.Models ?? Enumerable.Empty<Model>())
                    .Where(m => m != null && m.RootItem != null)
                    .SelectMany(m => m.RootItem.DescendantsAndSelf);

                int scanned = 0;
                int matchedGuid = 0;
                int matchedHash = 0;

                foreach (var mi in allItems)
                {
                    if (mi == null) { scanned++; continue; }

                    var ig = mi.InstanceGuid;
                    if (ig != Guid.Empty)
                    {
                        List<string> guidIds;
                        if (guidMap.TryGetValue(ig, out guidIds))
                        {
                            foreach (var id in guidIds)
                                resolvedById[id].Add(mi);
                            matchedGuid++;
                        }
                    }

                    if (hashMap.Count > 0)
                    {
                        var calcHash = ComputePathHash(mi);
                        List<string> hashIds;
                        if (hashMap.TryGetValue(calcHash, out hashIds))
                        {
                            foreach (var id in hashIds)
                                resolvedById[id].Add(mi);
                            matchedHash++;
                        }
                    }

                    scanned++;
                    if ((scanned % 50000) == 0)
                    {
                        LogHelper.LogDebug($"Lookup: Progress scanned={scanned}, matchedGuid={matchedGuid}, matchedHash={matchedHash}", "ResolveItemsByCanonicalIds");
                    }
                }

                var seen = new HashSet<ModelItem>();
                foreach (var id in idsInOrder)
                {
                    List<ModelItem> list;
                    if (resolvedById.TryGetValue(id, out list))
                    {
                        foreach (var mi in list)
                        {
                            if (mi != null && seen.Add(mi))
                                result.Add(mi);
                        }
                    }
                }

                sw.Stop();
                LogHelper.LogSuccess(
                    $"Lookup: Fertig. incoming={totalIn}, usable={idsInOrder.Count}, scanned={scanned}, matchedGuidItems={matchedGuid}, matchedHashItems={matchedHash}, uniqueResult={result.Count}, durationMs={sw.ElapsedMilliseconds}");

                if (result.Count == 0 && (guidMap.Count + hashMap.Count) > 0)
                {
                    LogHelper.LogWarning("Lookup: Keine Treffer zu den übergebenen IDs gefunden.");
                }

                return result;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"Lookup: Unerwarteter Fehler in ResolveItemsByCanonicalIds: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Applies a set of <see cref="ModelItem"/> objects to the current selection in the given document.
        /// </summary>
        /// <param name="doc">
        /// The active <see cref="Document"/> containing the current selection.
        /// </param>
        /// <param name="items">
        /// The collection of <see cref="ModelItem"/> instances to select.  
        /// Null entries are ignored.
        /// </param>
        /// <param name="keepExistingSelection">
        /// If <c>true</c>, adds the items to the current selection.  
        /// If <c>false</c>, clears the current selection before applying the new items.
        /// </param>
        /// <remarks>
        /// Behavior:  
        /// - Wraps the items into a <see cref="ModelItemCollection"/>.  
        /// - If <paramref name="items"/> is empty, selection is not changed and a warning is logged.  
        /// - Uses <see cref="ComApiBridge"/> if available, otherwise falls back to <see cref="Document.CurrentSelection"/>.  
        /// - Logs the number of applied items upon success.
        /// </remarks>
        private void ApplySelectionInternal(Document doc, IEnumerable<ModelItem> items, bool keepExistingSelection)
        {
            var collection = new ModelItemCollection();
            foreach (var mi in items)
                if (mi != null) collection.Add(mi);

            if (collection.Count == 0)
            {
                LogHelper.LogWarning("[SEL/APPLY] Keine Items übergeben.", "MCP");
                return;
            }

            if (!keepExistingSelection)
                doc.CurrentSelection.Clear();

            var state = ComApiBridge.State;
            if (state != null)
            {
                var comSel = ComApiBridge.ToInwOpSelection(collection);
                state.CurrentSelection = comSel;
            }
            else
            {
                var sel = new Selection(collection);
                if (keepExistingSelection)
                    doc.CurrentSelection?.AddRange(collection);
                else
                    doc.CurrentSelection?.CopyFrom(sel);
            }

            LogHelper.LogSuccess($"[SEL/APPLY] Selektiert={collection.Count}", "MCP");
        }

        // ========================= Utilities ========================


        /// <summary>
        /// Attempts to resolve the display value of a property from a <see cref="ModelItem"/>.
        /// </summary>
        /// <param name="item">
        /// The <see cref="ModelItem"/> to inspect. May be <c>null</c>.
        /// </param>
        /// <param name="categoryDisplayOrApiName">
        /// The display name or API name of the property category to search (case-insensitive).
        /// </param>
        /// <param name="propertyDisplayOrApiName">
        /// The display name or API name of the property inside the category to search (case-insensitive).
        /// </param>
        /// <returns>
        /// The property’s display value if found, otherwise <c>null</c>.  
        /// If reading the display value fails, a raw <c>ToString()</c> fallback is attempted.
        /// </returns>
        /// <remarks>
        /// - Iterates over the property categories of the given <paramref name="item"/>.  
        /// - Category and property matching is case-insensitive, using either display name or internal name.  
        /// - Uses <see cref="VariantData.ToDisplayString"/> when possible, falling back to <c>ToString()</c>.  
        /// - Logs debug information if an exception occurs during property access.  
        /// - Returns <c>null</c> if the item has no properties or the requested property cannot be resolved.
        /// </remarks>
        private string TryGetPropertyValue(ModelItem item, string categoryDisplayOrApiName, string propertyDisplayOrApiName)
        {
            try
            {
                var cats = item?.PropertyCategories;
                if (cats == null) return null;

                foreach (PropertyCategory cat in cats)
                {
                    var cn = SafeNameOrDefault(cat?.DisplayName, cat?.Name, "");
                    if (!string.Equals(cn, categoryDisplayOrApiName, StringComparison.OrdinalIgnoreCase)) continue;

                    foreach (DataProperty p in cat?.Properties ?? Enumerable.Empty<DataProperty>())
                    {
                        var pn = SafeNameOrDefault(p?.DisplayName, p?.Name, "");
                        if (!string.Equals(pn, propertyDisplayOrApiName, StringComparison.OrdinalIgnoreCase)) continue;

                        try { return p?.Value?.ToDisplayString(); }
                        catch { return p?.Value?.ToString(); }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogDebug($"[FALLBACK] TryGetPropertyValue(): {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Returns the first non-empty string among <paramref name="a"/> and <paramref name="b"/>;  
        /// if both are empty or null, returns the specified <paramref name="fallback"/>.
        /// </summary>
        /// <param name="a">Primary candidate string (checked first).</param>
        /// <param name="b">Secondary candidate string (checked if <paramref name="a"/> is null or whitespace).</param>
        /// <param name="fallback">
        /// Fallback value to return if both <paramref name="a"/> and <paramref name="b"/> are null or whitespace.  
        /// If <c>null</c>, an empty string is returned.
        /// </param>
        /// <returns>
        /// A non-null string, guaranteed to be either <paramref name="a"/>, <paramref name="b"/>, or <paramref name="fallback"/>.  
        /// Returns <c>string.Empty</c> if all inputs are null or whitespace and <paramref name="fallback"/> is null.
        /// </returns>
        /// <remarks>
        /// This method is typically used to ensure safe display names for Navisworks model items,  
        /// preventing null or empty values from propagating in logs, paths, or UI outputs.
        /// </remarks>
        private string SafeNameOrDefault(string a, string b, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(a)) return a;
            if (!string.IsNullOrWhiteSpace(b)) return b;
            return fallback ?? "";
        }

        /// <summary>
        /// Computes a stable hash string representing the full ancestor path of a <see cref="ModelItem"/>.
        /// </summary>
        /// <param name="item">The Navisworks <see cref="ModelItem"/> to compute the path hash for.</param>
        /// <returns>
        /// A lowercase hexadecimal string (8 characters) derived from the hash code of the  
        /// item's ancestor path. Returns a deterministic identifier for items without a valid <c>InstanceGuid</c>.
        /// </returns>
        /// <remarks>
        /// - The path is built from all ancestors (including the item itself) using their display names,  
        ///   class display names, or class names.  
        /// - Each segment is sanitized via <see cref="SafeNameOrDefault"/> to avoid null/empty values.  
        /// - This hash is primarily used to generate "p:&lt;hash&gt;" canonical IDs as a fallback when  
        ///   no <c>InstanceGuid</c> is available.  
        /// - Note: As <see cref="string.GetHashCode"/> is not guaranteed to be consistent across runtimes,  
        ///   this is best used only within a single session or controlled environment.
        /// </remarks>
        private string ComputePathHash(ModelItem item)
        {
            var parts = new List<string>();
            foreach (var a in item.AncestorsAndSelf)
                parts.Add(SafeNameOrDefault(a.DisplayName, a.ClassDisplayName ?? a.ClassName, DEFAULT_UNNAMED));
            var path = string.Join("/", parts);
            return path.GetHashCode().ToString("x8", CultureInfo.InvariantCulture);
        }


        /// <summary>
        /// Extracts IFC metadata (class and GUID) from a given <see cref="ModelItem"/>.
        /// </summary>
        /// <param name="item">The Navisworks <see cref="ModelItem"/> to read IFC information from.</param>
        /// <returns>
        /// A tuple containing:  
        /// - <c>ifcClass</c>: The resolved IFC class/type name (falls back to display or class name if missing).  
        /// - <c>ifcGuid</c>: The resolved IFC GUID/GlobalId string (empty if not found).
        /// </returns>
        /// <remarks>
        /// - Searches multiple property categories in a prioritized order (e.g., <c>lcatfconsumer_Element_tab</c>, <c>IFC</c>, <c>LcOaNode</c>).  
        /// - Provides resilience against variations in property naming conventions across IFC exports.  
        /// - If no explicit IFC class is found, falls back to <see cref="ModelItem.ClassDisplayName"/> or <see cref="ModelItem.ClassName"/>.  
        /// - If no GUID is found, an empty string is returned.  
        /// - This method ensures consistent metadata extraction for downstream use in reporting,  
        ///   clash detection, or serialization workflows.
        /// </remarks>
        private (string ifcClass, string ifcGuid) GetIfcMeta(ModelItem item)
        {
            string ifcClass =
                TryGetPropertyValue(item, "lcatfconsumer_Element_tab", "lcatfconsumer_parameter_IfcClass")
                ?? TryGetPropertyValue(item, "lcatfconsumer_Element_tab", "IfcClass")
                ?? TryGetPropertyValue(item, "lcatfconsumer_Type_tab", "IfcClass")
                ?? TryGetPropertyValue(item, "IFC", "Type")
                ?? TryGetPropertyValue(item, "IFC", "Class")
                ?? TryGetPropertyValue(item, "LcOaNode", "LcOaSceneBaseClassUserName")
                ?? item?.ClassDisplayName
                ?? item?.ClassName
                ?? "";

            string ifcGuid =
                TryGetPropertyValue(item, "LcATFIFCId", "IfcGUID")
                ?? TryGetPropertyValue(item, "LcATFIFCId", "IfcGlobalId")
                ?? TryGetPropertyValue(item, "Element-ID", "IfcGUID")
                ?? TryGetPropertyValue(item, "Element-ID", "GlobalId")
                ?? TryGetPropertyValue(item, "lcatfconsumer_Element_tab", "IfcGlobalId")
                ?? TryGetPropertyValue(item, "lcatfconsumer_Element_tab", "GlobalId")
                ?? TryGetPropertyValue(item, "lcatfconsumer_Element_tab", "IfcGUID")
                ?? "";

            return (ifcClass, ifcGuid);
        }

        /// <summary>
        /// Builds a hierarchical path from the given <see cref="ModelItem"/> up through its ancestors.  
        /// </summary>
        /// <param name="node">The starting <see cref="ModelItem"/>.</param>
        /// <param name="includeCanonical">
        /// If <c>true</c>, includes the canonical ID of each ancestor in the result.  
        /// If <c>false</c>, only the textual path is returned.
        /// </param>
        /// <param name="reverse">
        /// If <c>true</c>, reverses the ancestor chain so that the path starts at the root and ends at <paramref name="node"/>.  
        /// If <c>false</c>, the order follows Navisworks' natural <c>AncestorsAndSelf</c> order.
        /// </param>
        /// <returns>
        /// A list of <see cref="PathStep"/> objects representing each level in the hierarchy.  
        /// Each step contains:
        /// - <c>canonical_id</c>: the resolved canonical ID (or empty if <paramref name="includeCanonical"/> = false).  
        /// - <c>paths</c>: the accumulated display path up to that node.
        /// </returns>
        /// <remarks>
        /// - Uses <see cref="SafeNameOrDefault"/> to ensure stable naming (avoids null/empty).  
        /// - Useful for reconstructing navigation paths, breadcrumbs, or debugging selection context.  
        /// - The path string is built incrementally (e.g. "Root/Submodel/Element").  
        /// </remarks>
        private List<PathStep> GetPathSteps(ModelItem node, bool includeCanonical = true, bool reverse = true)
        {
            var result = new List<PathStep>();
            if (node == null) return result;

            var chain = (node.AncestorsAndSelf ?? Enumerable.Empty<ModelItem>()).ToList();
            if (reverse) chain.Reverse();

            var parts = new List<string>();
            foreach (var n in chain)
            {
                parts.Add(SafeNameOrDefault(n.DisplayName, n.ClassDisplayName ?? n.ClassName, DEFAULT_UNNAMED));
                var path = string.Join("/", parts);

                if (includeCanonical)
                {
                    var cid = GetCanonicalId(n);
                    result.Add(new PathStep { canonical_id = NullSafe(cid), paths = NullSafe(path) });
                }
                else
                {
                    result.Add(new PathStep { canonical_id = "", paths = NullSafe(path) });
                }
            }
            return result;
        }

        /// <summary>
        /// Recursively collects descendant <see cref="ModelItem"/> objects up to a given depth.
        /// </summary>
        /// <param name="node">The starting <see cref="ModelItem"/> node. If null, nothing is collected.</param>
        /// <param name="acc">
        /// Accumulator list where all discovered descendants are added.  
        /// The caller is responsible for providing an initialized list.
        /// </param>
        /// <param name="currentDepth">The current recursion depth (starts at 0 for the root).</param>
        /// <param name="maxDepth">
        /// The maximum recursion depth.  
        /// - <c>1</c>: collects only direct children.  
        /// - <c>2</c>: collects children and grandchildren, etc.
        /// </param>
        /// <remarks>
        /// - Stops immediately if <paramref name="node"/> is null.  
        /// - Does not add the <paramref name="node"/> itself, only its descendants.  
        /// - Uses <see cref="ModelItem.Children"/> and short-circuits when no children are present.  
        /// - Avoids infinite recursion by bounding recursion with <paramref name="maxDepth"/>.
        /// </remarks>
        private void CollectDescendantsRecursive(ModelItem node, List<ModelItem> acc, int currentDepth, int maxDepth)
        {
            if (node == null) return;
            if (currentDepth >= maxDepth) return;

            foreach (var child in node.Children ?? Enumerable.Empty<ModelItem>())
            {
                acc.Add(child);
                CollectDescendantsRecursive(child, acc, currentDepth + 1, maxDepth);
            }
        }

        /// <summary>
        /// Collects descendant items of a given <see cref="ModelItem"/> up to a specified depth
        /// and converts them into simplified references (<see cref="SimpleItemRef"/>).
        /// </summary>
        /// <param name="root">
        /// The starting <see cref="ModelItem"/> from which to collect descendants.  
        /// If null, an empty list is returned.
        /// </param>
        /// <param name="recursiceDeep">
        /// Maximum recursion depth:  
        /// - <c>1</c>: only direct children.  
        /// - <c>2</c>: children and grandchildren, etc.
        /// </param>
        /// <returns>
        /// A list of <see cref="SimpleItemRef"/> objects representing the descendants
        /// of the <paramref name="root"/> item, up to the given depth.  
        /// Returns an empty list if <paramref name="root"/> is null.
        /// </returns>
        /// <remarks>
        /// - Internally uses <see cref="CollectDescendantsRecursive"/> to traverse hierarchy.  
        /// - Each <see cref="ModelItem"/> is mapped via <see cref="Get_Simple_Item_Info"/>.  
        /// - Excludes the <paramref name="root"/> itself; only descendants are included.
        /// </remarks>
        private List<SimpleItemRef> Get_NachfolgeRecursive_From_Item(ModelItem root, int recursiceDeep)
        {
            var result = new List<SimpleItemRef>();
            if (root == null) return result;

            var acc = new List<ModelItem>();
            CollectDescendantsRecursive(root, acc, 0, recursiceDeep);
            foreach (var mi in acc)
            {
                var r = Get_Simple_Item_Info(mi);
                if (r != null) result.Add(r);
            }
            return result;
        }

        /// <summary>
        /// Creates a simplified reference (<see cref="SimpleItemRef"/>) for a given <see cref="ModelItem"/>.  
        /// </summary>
        /// <param name="it">
        /// The <see cref="ModelItem"/> to extract basic information from.  
        /// If <c>null</c>, the method returns <c>null</c>.
        /// </param>
        /// <returns>
        /// A <see cref="SimpleItemRef"/> containing:  
        /// - <c>canonical_id</c>: unique identifier from <see cref="GetCanonicalId"/>.  
        /// - <c>element_name</c>: display or class name (fallback if missing).  
        /// - <c>typ</c>: IFC class (if available) or internal class name.  
        /// Returns <c>null</c> if input <paramref name="it"/> is <c>null</c>.
        /// </returns>
        /// <remarks>
        /// - Uses <see cref="SafeNameOrDefault"/> for robust naming.  
        /// - Uses <see cref="GetIfcMeta"/> to prefer IFC classification over internal class names.  
        /// - Provides minimal, stable information for UI display or JSON serialization.  
        /// </remarks>
        private SimpleItemRef Get_Simple_Item_Info(ModelItem it)
        {
            if (it == null) return null;
            var cid = GetCanonicalId(it);
            var name = SafeNameOrDefault(it.DisplayName, it.ClassDisplayName ?? it.ClassName, DEFAULT_UNNAMED);
            var meta = GetIfcMeta(it);
            var ifcClass = meta.ifcClass;
            var typ = !string.IsNullOrEmpty(ifcClass) ? ifcClass : (it.ClassDisplayName ?? it.ClassName ?? "");
            return new SimpleItemRef { canonical_id = NullSafe(cid), element_name = NullSafe(name), typ = NullSafe(typ) };
        }

        /// <summary>
        /// Extracts all property categories and their properties from a <see cref="ModelItem"/>  
        /// and transforms them into a normalized dictionary structure.
        /// </summary>
        /// <param name="mi">
        /// The <see cref="ModelItem"/> to inspect.  
        /// If <c>null</c> or has no properties, the result is an empty dictionary.
        /// </param>
        /// <returns>
        /// A dictionary mapping category names → list of <see cref="SimplePropJson"/> entries.  
        /// Each entry contains the property name, type (from <see cref="MapVariantType"/>),  
        /// and normalized value (from <see cref="FormatVal"/>).
        /// </returns>
        /// <remarks>
        /// - Category and property names are normalized with <see cref="SafeNameOrDefault"/>.  
        /// - Default labels (<c>(Kategorie)</c>, <c>(Property)</c>) are used if names are missing.  
        /// - Only properties with non-empty values are included.  
        /// - Case-insensitive keys ensure consistent grouping of categories.
        /// </remarks>
        private Dictionary<string, List<SimplePropJson>> Get_Property_Categories_To_Item(ModelItem mi)
        {
            var map = new Dictionary<string, List<SimplePropJson>>(StringComparer.OrdinalIgnoreCase);

            Action<string, string, string, string> AddKV = (cat, key, type, val) =>
            {
                if (string.IsNullOrWhiteSpace(cat)) cat = DEFAULT_CATEGORY;
                if (string.IsNullOrWhiteSpace(key)) key = DEFAULT_PROPERTY;
                List<SimplePropJson> lst;
                if (!map.TryGetValue(cat, out lst))
                {
                    lst = new List<SimplePropJson>();
                    map[cat] = lst;
                }
                lst.Add(BuildSimplePropJson(key, type, val));
            };

            foreach (PropertyCategory cat in mi?.PropertyCategories ?? Enumerable.Empty<PropertyCategory>())
            {
                if (cat == null) continue;

                var catName = SafeNameOrDefault(cat.DisplayName, cat.Name, DEFAULT_CATEGORY);
                foreach (DataProperty p in cat.Properties ?? Enumerable.Empty<DataProperty>())
                {
                    if (p == null) continue;

                    var pName = SafeNameOrDefault(p.DisplayName, p.Name, DEFAULT_PROPERTY);
                    var pType = MapVariantType(p);
                    var pVal = FormatVal(p);

                    // Nur Properties mit Wert führen wir
                    if (!string.IsNullOrWhiteSpace(pVal))
                        AddKV(catName, pName, pType, pVal);
                }
            }

            return map;
        }


        /// <summary>
        /// Resolves the most appropriate geometric targets for clash or selection operations,
        /// starting from a given <see cref="ModelItem"/>.
        /// </summary>
        /// <param name="start">
        /// The starting <see cref="ModelItem"/> to evaluate.
        /// </param>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///   <item><description><c>targets</c>: list of resolved geometric items (may be empty).</description></item>
        ///   <item><description><c>reason</c>: textual reason code:
        ///     <list type="bullet">
        ///       <item><c>"ok"</c>: the item itself has geometry.</item>
        ///       <item><c>"promoted:no-geometry"</c>: a geometric ancestor was used.</item>
        ///       <item><c>"demoted:nearest-descendants"</c>: nearest geometric descendants were used.</item>
        ///       <item><c>"no-geometry-in-subtree"</c>: no geometry could be found.</item>
        ///       <item><c>"fallback:none"</c>: input item was null.</item>
        ///     </list>
        ///   </description></item>
        ///   <item><description><c>steps</c>: number of ancestor steps traversed before finding geometry.</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// The algorithm prefers geometry in the following order:
        /// 1. The item itself.  
        /// 2. An ancestor with geometry (promotion).  
        /// 3. The nearest descendants with geometry (demotion).  
        /// 4. Fallback: no geometry found in the subtree.  
        /// </remarks>
        private (List<ModelItem> targets, string reason, int steps) ResolveGeometricTargets(ModelItem start)
        {
            var targets = new List<ModelItem>();
            int steps = 0;

            if (start == null) return (targets, "fallback:none", steps);

             
            if (Has_Geometry_From_Item(start))
            {
                targets.Add(start);
                return (targets, "ok", steps);
            }
 
            foreach (var anc in start.Ancestors ?? Enumerable.Empty<ModelItem>())
            {
                steps++;
                if (Has_Geometry_From_Item(anc))
                {
                    targets.Add(anc);
                    return (targets, "promoted:no-geometry", steps);
                }
            }
 
            var nearest = FindNearestGeometricDescendants(start);
            if (nearest.Count > 0)
                return (nearest, "demoted:nearest-descendants", steps);

 
            return (targets, "no-geometry-in-subtree", steps);
        }

        /// <summary>
        /// Searches the nearest descendants of a given <see cref="ModelItem"/> that contain geometry.
        /// </summary>
        /// <param name="start">
        /// The starting <see cref="ModelItem"/> whose subtree will be explored.
        /// </param>
        /// <param name="maxDepth">
        /// Maximum depth (levels of descendants) to search. Default is 5.
        /// </param>
        /// <param name="maxNodes">
        /// Maximum number of nodes to process before aborting the search. Default is 50,000.
        /// </param>
        /// <returns>
        /// A list of the first-found geometric descendants at the nearest depth level.  
        /// If none are found within the given limits, an empty list is returned.
        /// </returns>
        /// <remarks>
        /// - Performs a breadth-first search (BFS) to ensure the **closest** geometric descendants are found.  
        /// - As soon as one or more geometric items are found at the current depth, the search stops.  
        /// - Prevents infinite loops and excessive scanning using <paramref name="maxDepth"/> and <paramref name="maxNodes"/>.  
        /// - Uses <see cref="Has_Geometry_From_Item"/> to test whether a node has geometry.  
        /// </remarks>
        private List<ModelItem> FindNearestGeometricDescendants(ModelItem start, int maxDepth = 5, int maxNodes = 50000)
        {
            var result = new List<ModelItem>();
            if (start == null) return result;

            var q = new Queue<(ModelItem node, int depth)>();
            var seen = new HashSet<ModelItem>();
            q.Enqueue((start, 0)); seen.Add(start);

            while (q.Count > 0 && seen.Count <= maxNodes)
            {
                var (node, depth) = q.Dequeue();
                if (depth > maxDepth) break;

                var atThisLevel = new List<ModelItem>();
                foreach (var ch in node.Children ?? Enumerable.Empty<ModelItem>())
                {
                    if (ch == null || !seen.Add(ch)) continue;
                    if (Has_Geometry_From_Item(ch)) atThisLevel.Add(ch);
                    else q.Enqueue((ch, depth + 1));
                }

                if (atThisLevel.Count > 0)
                {
                    result.AddRange(atThisLevel);
                    break;  
                }
            }
            return result;
        }

        /// <summary>
        /// Checks whether a given <see cref="ModelItem"/> contains valid geometric data.
        /// </summary>
        /// <param name="mi">
        /// The <see cref="ModelItem"/> to test.
        /// </param>
        /// <returns>
        /// <c>true</c> if the item has a non-empty bounding box (geometry present);  
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// - Attempts to retrieve the bounding box of the item (first with world coordinates, then with defaults).  
        /// - If both attempts fail or return <c>null</c>, the item is considered non-geometric.  
        /// - A bounding box is only accepted as valid if it has non-zero dimensions in at least one axis (X, Y, Z).  
        /// - Safe-guarded with try/catch to handle cases where Navisworks API throws on invalid or disposed items.  
        /// </remarks>
        private bool Has_Geometry_From_Item(ModelItem mi)
        {
            if (mi == null) return false;
            try
            {
                BoundingBox3D bb = null;
                try { bb = mi.BoundingBox(true); } catch { }
                if (bb == null) { try { bb = mi.BoundingBox(); } catch { } }
                if (bb == null) return false;

 
                double dx = Math.Max(0.0, bb.Max.X - bb.Min.X);
                double dy = Math.Max(0.0, bb.Max.Y - bb.Min.Y);
                double dz = Math.Max(0.0, bb.Max.Z - bb.Min.Z);
                return (dx > 0 || dy > 0 || dz > 0);
            }
            catch { return false; }
        }

        /// <summary>
        /// Extracts derived geometric properties (bounding box and dimensions) from a given <see cref="ModelItem"/>.
        /// </summary>
        /// <param name="mi">
        /// The <see cref="ModelItem"/> for which to derive geometry information.
        /// </param>
        /// <returns>
        /// A dictionary mapping category names (usually <c>"Geometry (Derived)"</c>)  
        /// to lists of <see cref="SimplePropJson"/> entries containing:
        ///   - Bounding box Min/Max points  
        ///   - Size in X, Y, Z directions  
        ///   - Center point  
        /// Returns an empty dictionary if no geometry is available.
        /// </returns>
        /// <remarks>
        /// - Uses <see cref="ModelItem.BoundingBox"/> (world and local variants) to determine geometry.  
        /// - Safely handles API exceptions (invalid/disposed items).  
        /// - All values are stored as formatted strings (via <c>FormulaJson</c>) for JSON output.  
        /// - Category key defaults to <c>"Geometry (Derived)"</c>, property key falls back to <c>DEFAULT_PROPERTY</c>.  
        /// - Intended to supplement property categories with geometry metadata for inspection/export.  
        /// </remarks>
        private Dictionary<string, List<SimplePropJson>> Get_Geometries_To_Item(ModelItem mi)
        {
            var geo = new Dictionary<string, List<SimplePropJson>>(StringComparer.OrdinalIgnoreCase);

            Action<string, string, string, string> AddObj = (cat, prop, type, value) =>
            {
                if (string.IsNullOrWhiteSpace(cat)) cat = "Geometry (Derived)";
                if (string.IsNullOrWhiteSpace(prop)) prop = DEFAULT_PROPERTY;

                List<SimplePropJson> lst;
                if (!geo.TryGetValue(cat, out lst))
                {
                    lst = new List<SimplePropJson>();
                    geo[cat] = lst;
                }
                lst.Add(BuildSimplePropJson(prop, type, value));
            };

            try
            {
                BoundingBox3D bb = null;
                try { bb = mi.BoundingBox(true); } catch { }
                if (bb == null) { try { bb = mi.BoundingBox(); } catch { } }

                if (bb != null && bb.Min != null && bb.Max != null)
                {
                    var dx = Math.Max(0.0, bb.Max.X - bb.Min.X);
                    var dy = Math.Max(0.0, bb.Max.Y - bb.Min.Y);
                    var dz = Math.Max(0.0, bb.Max.Z - bb.Min.Z);
                    var cx = (bb.Max.X + bb.Min.X) * 0.5;
                    var cy = (bb.Max.Y + bb.Min.Y) * 0.5;
                    var cz = (bb.Max.Z + bb.Min.Z) * 0.5;

                    const string CAT = "Geometry (Derived)";
                    AddObj(CAT, "Min", "point3d", "(" + FormulaJson(bb.Min.X) + ", " + FormulaJson(bb.Min.Y) + ", " + FormulaJson(bb.Min.Z) + ")");
                    AddObj(CAT, "Max", "point3d", "(" + FormulaJson(bb.Max.X) + ", " + FormulaJson(bb.Max.Y) + ", " + FormulaJson(bb.Max.Z) + ")");
                    AddObj(CAT, "SizeX", "double", FormulaJson(dx));
                    AddObj(CAT, "SizeY", "double", FormulaJson(dy));
                    AddObj(CAT, "SizeZ", "double", FormulaJson(dz));
                    AddObj(CAT, "Center", "point3d", "(" + FormulaJson(cx) + ", " + FormulaJson(cy) + ", " + FormulaJson(cz) + ")");
                }
            }
            catch {}

            return geo;
        }

        /// <summary>
        /// Maps the <see cref="VariantDataType"/> of a Navisworks <see cref="DataProperty"/> 
        /// to a simplified string identifier for serialization or export.
        /// </summary>
        /// <param name="p">
        /// The <see cref="DataProperty"/> whose <see cref="DataProperty.Value"/> is inspected.
        /// </param>
        /// <returns>
        /// A simplified type string, e.g.:
        ///   - <c>"bool"</c> for Boolean  
        ///   - <c>"string"</c> for DisplayString/IdentifierString  
        ///   - <c>"datetime"</c> for DateTime  
        ///   - <c>"int"</c> for Int32  
        ///   - <c>"long"</c> for Int64  
        ///   - <c>"named"</c> for NamedConstant  
        ///   - <c>"double"</c> or other Double-based variations  
        ///   - The original <c>DataType</c> string if no mapping is found  
        ///   - <c>"unknown"</c> if type resolution fails or <paramref name="p"/> is null.
        /// </returns>
        /// <remarks>
        /// - Provides a stable abstraction layer over Navisworks variant types.  
        /// - Useful when exporting property values to JSON or custom schemas.  
        /// - Falls back gracefully to <c>"unknown"</c> on errors.
        /// </remarks>
        private string MapVariantType(DataProperty p)
        {
            try
            {
                var t = p?.Value?.DataType.ToString() ?? "unknown";
                if (t.Equals("Boolean", StringComparison.OrdinalIgnoreCase)) return "bool";
                if (t.Equals("DisplayString", StringComparison.OrdinalIgnoreCase)) return "string";
                if (t.Equals("IdentifierString", StringComparison.OrdinalIgnoreCase)) return "string";
                if (t.Equals("DateTime", StringComparison.OrdinalIgnoreCase)) return "datetime";
                if (t.Equals("Int32", StringComparison.OrdinalIgnoreCase)) return "int";
                if (t.Equals("Int64", StringComparison.OrdinalIgnoreCase)) return "long";
                if (t.Equals("NamedConstant", StringComparison.OrdinalIgnoreCase)) return "named";
                if (t.StartsWith("Double", StringComparison.OrdinalIgnoreCase)) return t.ToLowerInvariant();
                return t;
            }
            catch { return "unknown"; }
        }

        /// <summary>
        /// Converts a Navisworks <see cref="VariantData"/> into a human-readable string,
        /// with formatting adjusted for different <see cref="VariantDataType"/> values.
        /// </summary>
        /// <param name="v">
        /// The <see cref="VariantData"/> instance to format. Can be <c>null</c>.
        /// </param>
        /// <returns>
        /// A formatted string representation of the variant value, depending on its type:
        /// <list type="bullet">
        ///   <item><description><c>DisplayString</c>: Cleans the "DisplayString:" prefix if present.</description></item>
        ///   <item><description><c>DoubleLength</c>, <c>DoubleArea</c>, <c>DoubleVolume</c>, <c>DoubleAngle</c>, <c>Double</c>: Converted with current culture formatting.</description></item>
        ///   <item><description><c>Int32</c>: Converted to string.</description></item>
        ///   <item><description><c>Boolean</c>: Converted to string (<c>"True"</c>/<c>"False"</c>).</description></item>
        ///   <item><description><c>DateTime</c>: Localized string via current culture.</description></item>
        ///   <item><description>All other types: Falls back to <see cref="object.ToString"/>.</description></item>
        /// </list>
        /// Returns <c>""</c> if <paramref name="v"/> is <c>null</c> or if an exception occurs.
        /// </returns>
        /// <remarks>
        /// - Ensures consistent string conversion for property export (e.g. JSON or logs).  
        /// - Culture-sensitive formatting is applied for numbers and dates.  
        /// - Exceptions are caught silently to avoid breaking property enumeration.
        /// </remarks>
        private string FormatVariant(VariantData v)
        {
            if (v == null) return "";
            try
            {
                switch (v.DataType)
                {
                    case VariantDataType.DisplayString:
                        var s = v.ToDisplayString() ?? "";
                        const string prefix = "DisplayString:";
                        if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) s = s.Substring(prefix.Length);
                        return s;
                    case VariantDataType.DoubleLength: return v.ToDoubleLength().ToString(CultureInfo.CurrentCulture);
                    case VariantDataType.DoubleArea: return v.ToDoubleArea().ToString(CultureInfo.CurrentCulture);
                    case VariantDataType.DoubleVolume: return v.ToDoubleVolume().ToString(CultureInfo.CurrentCulture);
                    case VariantDataType.DoubleAngle: return v.ToDoubleAngle().ToString(CultureInfo.CurrentCulture);
                    case VariantDataType.Double: return v.ToDouble().ToString(CultureInfo.CurrentCulture);
                    case VariantDataType.Int32: return v.ToInt32().ToString(CultureInfo.CurrentCulture);
                    case VariantDataType.Boolean: return v.ToBoolean().ToString();
                    case VariantDataType.DateTime: return v.ToDateTime().ToString(CultureInfo.CurrentCulture);
                    default: return v.ToString();
                }
            }
            catch { return ""; }
        }

        /// <summary>
        /// Wrapper for <see cref="FormatVariant"/> that converts a <see cref="VariantData"/> 
        /// into its string representation.
        /// </summary>
        /// <param name="v">
        /// The <see cref="VariantData"/> instance to format. Can be <c>null</c>.
        /// </param>
        /// <returns>
        /// A formatted string representation of <paramref name="v"/>, identical to 
        /// <see cref="FormatVariant"/>.
        /// </returns>
        /// <remarks>
        /// This method is kept as a public entry point for consistent naming and external calls.  
        /// Internally, it delegates the logic to <see cref="FormatVariant"/>.
        /// </remarks>
        public string FormatVariantValue(VariantData v) => FormatVariant(v);

        /// <summary>
        /// Converts a <see cref="DataProperty"/> value into a normalized string.
        /// </summary>
        /// <param name="p">
        /// The <see cref="DataProperty"/> whose <see cref="VariantData"/> value will be formatted.  
        /// Can be <c>null</c>.
        /// </param>
        /// <returns>
        /// A formatted string representation of the property value.  
        /// Returns an empty string if the property or its value is <c>null</c>.
        /// </returns>
        /// <remarks>
        /// Resolution order:
        /// <list type="number">
        /// <item><description>Use <see cref="FormatVariant"/> for type-aware formatting.</description></item>
        /// <item><description>If no usable result, fallback to <see cref="VariantData.ToDisplayString"/>.</description></item>
        /// <item><description>If still empty, use <see cref="VariantData.ToString"/> as a last resort.</description></item>
        /// </list>
        /// </remarks>
        private string FormatVal(DataProperty p)
        {
            try
            {
                var v = p?.Value;
                if (v == null) return "";
                var s = FormatVariant(v);
                if (!string.IsNullOrWhiteSpace(s)) return s;
                try { s = v.ToDisplayString(); } catch { }
                if (!string.IsNullOrWhiteSpace(s)) return s;
                return v.ToString();
            }
            catch { return ""; }
        }

        /// <summary>
        /// Formats a <see cref="double"/> value as a JSON-safe string using
        /// invariant culture and up to three decimal places.
        /// </summary>
        /// <param name="d">
        /// The numeric value to format.
        /// </param>
        /// <returns>
        /// A string representation of <paramref name="d"/> with a maximum of
        /// three fractional digits, formatted using <see cref="CultureInfo.InvariantCulture"/>.
        /// </returns>
        /// <remarks>
        /// This helper is intended for embedding numeric values into JSON,
        /// ensuring predictable decimal separators ('.') regardless of system locale.
        /// Example: <c>1.23456</c> → <c>"1.235"</c>.
        /// </remarks>
        private string FormulaJson(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>
        /// Escapes a string so it can be safely embedded in JSON output.
        /// </summary>
        /// <param name="s">
        /// The input string to escape. Can be <c>null</c> or empty.
        /// </param>
        /// <returns>
        /// A JSON-safe string where special characters are replaced:
        /// <list type="bullet">
        /// <item><description><c>\</c> → <c>\\</c></description></item>
        /// <item><description><c>"</c> → <c>\"</c></description></item>
        /// <item><description>Carriage return → <c>\r</c></description></item>
        /// <item><description>Line feed → <c>\n</c></description></item>
        /// <item><description>Tab → <c>\t</c></description></item>
        /// </list>
        /// Returns an empty string if <paramref name="s"/> is <c>null</c> or empty.
        /// </returns>
        /// <remarks>
        /// This method is intended for quick JSON serialization in custom builders,
        /// not as a full JSON encoder. For robust scenarios, use a dedicated
        /// JSON library such as <c>System.Text.Json</c>.
        /// </remarks>
        private string EscapeJson(string s)
            => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

        /// <summary>
        /// Creates a <see cref="SimplePropJson"/> instance from the given components,
        /// ensuring that <c>null</c> values are replaced with safe defaults.
        /// </summary>
        /// <param name="prop">The property name (e.g. "Height").</param>
        /// <param name="type">The property type (e.g. "double", "string").</param>
        /// <param name="value">The property value as string representation.</param>
        /// <returns>
        /// A <see cref="SimplePropJson"/> object with <paramref name="prop"/>,
        /// <paramref name="type"/>, and <paramref name="value"/> normalized by
        /// <c>NullSafe</c>.
        /// </returns>
        /// <remarks>
        /// This is a helper method used by property/category collection routines
        /// to build consistent DTOs for JSON serialization.
        /// </remarks>
        private SimplePropJson BuildSimplePropJson(string prop, string type, string value)
        {
            return new SimplePropJson
            {
                property = NullSafe(prop),
                type = NullSafe(type),
                value = NullSafe(value),
            };
        }

        /// <summary>
        /// Retrieves the file name of the container document without its path.
        /// </summary>
        /// <param name="doc">The active Navisworks <see cref="Document"/>.</param>
        /// <param name="isSaved">
        /// Output flag set to <c>true</c> if the document is saved on disk, 
        /// otherwise <c>false</c>.
        /// </param>
        /// <returns>
        /// The file name (without path) if the document is saved, 
        /// or the placeholder string <c>"(nicht gespeichert)"</c> if not.
        /// </returns>
        /// <remarks>
        /// - Ensures that a valid string is always returned, even if 
        ///   <paramref name="doc"/> is <c>null</c> or an exception occurs.  
        /// - Used in overview/model-listing methods to display the container’s
        ///   file identity.
        /// </remarks>
        private string GetContainerFileNameOnly(Document doc, out bool isSaved)
        {
            isSaved = false;
            try
            {
                var full = (string)doc.FileName;
                isSaved = !string.IsNullOrWhiteSpace(full);
                return isSaved ? Path.GetFileName(full) : "(nicht gespeichert)";
            }
            catch { return "(nicht gespeichert)"; }
        }

        /// <summary>
        /// Determines whether a given file extension represents a Navisworks container format.  
        /// </summary>
        /// <param name="ext">
        /// The file extension to check (e.g., <c>".nwd"</c>, <c>".nwf"</c>).  
        /// </param>
        /// <returns>
        /// <c>true</c> if the extension corresponds to a container file format  
        /// (<c>.nwd</c> or <c>.nwf</c>), otherwise <c>false</c>.  
        /// </returns>
        /// <remarks>
        /// - Used during submodel scanning to decide whether a model is treated as a  
        ///   container that may hold other submodels.  
        /// - Comparison is case-insensitive.  
        /// - Returns <c>false</c> if <paramref name="ext"/> is <c>null</c>, empty,  
        ///   or whitespace.  
        /// </remarks>
        private bool IsContainerExt(string ext)
        {
            if (string.IsNullOrWhiteSpace(ext)) return false;
            return ext.Equals(".nwd", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".nwf", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds a JSON-formatted string that represents a list of <see cref="SimpleItemRef"/>  
        /// objects that could not be selected.  
        /// </summary>
        /// <param name="list">
        /// A list of <see cref="SimpleItemRef"/> instances to serialize into JSON.  
        /// </param>
        /// <returns>
        /// A JSON string with the structure:  
        /// { "notSelectable": [ { "canonical_id": "...", "element_name": "...", "typ": "...", "details": "..." }, ... ] }  
        /// </returns>
        /// <remarks>
        /// - Intended to report items that failed selection (e.g., invalid or non-geometric).  
        /// - Null values are replaced with empty strings.  
        /// - The JSON is constructed manually (not using a serializer).  
        /// </remarks>
        public string BuildStringListSimpleItem(List<SimpleItemRef> list)
        {
            var sb = new StringBuilder();
            sb.Append("{ \"notSelectable\": [");
            for (int i = 0; i < list.Count; i++)
            {
                var it = list[i];
                sb.Append("{");
                sb.AppendFormat("\"canonical_id\":\"{0}\",", it.canonical_id ?? "");
                sb.AppendFormat("\"element_name\":\"{0}\",", it.element_name ?? "");
                sb.AppendFormat("\"typ\":\"{0}\",", it.typ ?? "");
                sb.AppendFormat("\"details\":\"{0}\"", it.details ?? "");
                sb.Append("}");
                if (i < list.Count - 1) sb.Append(",");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// Renders category and property statistics as a Markdown string for readability.  
        /// </summary>
        /// <param name="perModel">
        /// A nested dictionary structure containing counts of properties:  
        /// - Outer key: model ID  
        /// - Inner key: category name  
        /// - Innermost key: property name → count  
        /// </param>
        /// <returns>
        /// A Markdown-formatted string that summarizes the property counts per model,  
        /// grouped by category and property.  
        /// </returns>
        /// <remarks>
        /// - Output begins with a header "📊 Properties – Übersicht".  
        /// - Each model ID is rendered as a `###` section.  
        /// - Categories are listed as bullet points (**bold**).  
        /// - Properties within a category are shown as sub-bullets with counts.  
        /// - This is intended for human-readable reporting (e.g., logging or debug output).  
        /// </remarks>
        private string RenderCategoryStatsAsMarkdown(Dictionary<string, Dictionary<string, Dictionary<string, int>>> perModel)
        {
            var sb = new StringBuilder();
            sb.AppendLine("📊 **Properties – Übersicht (pro Modell, Kategorie & Property)**");
            foreach (var modelKv in perModel)
            {
                sb.AppendLine();
                sb.AppendLine($"### Modell-ID: `{modelKv.Key}`");
                foreach (var catKv in modelKv.Value.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"- **{catKv.Key}**");
                    foreach (var propKv in catKv.Value.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        sb.AppendLine($"  - `{propKv.Key}`: {propKv.Value}");
                    }
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Renders category and property statistics into a compact JSON string.  
        /// </summary>
        /// <param name="perModel">
        /// A nested dictionary structure containing counts of properties:  
        /// - Outer key: model ID  
        /// - Inner key: category name  
        /// - Innermost key: property name → count  
        /// </param>
        /// <returns>
        /// A JSON-formatted string representing property counts grouped by model, category, and property.  
        /// </returns>
        /// <remarks>
        /// - Keys (model IDs, category names, property names) are escaped via <see cref="EscapeJson"/>.  
        /// - Produces a hierarchical JSON object in the form:  
        ///   <code>
        ///   {
        ///     "modelId": {
        ///       "categoryName": {
        ///         "propertyName": count
        ///       }
        ///     }
        ///   }
        ///   </code>
        /// - Output is intended for machine-readable serialization (e.g., API response, logging).  
        /// - This method does not pretty-print; result is a compact single-line JSON string.  
        /// </remarks>
        private string RenderCategoryStatsAsJson(Dictionary<string, Dictionary<string, Dictionary<string, int>>> perModel)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            bool firstModel = true;

            foreach (var modelKv in perModel)
            {
                if (!firstModel) sb.Append(',');
                firstModel = false;

                sb.Append('\"').Append(EscapeJson(modelKv.Key)).Append("\":{");
                bool firstCat = true;

                foreach (var catKv in modelKv.Value)
                {
                    if (!firstCat) sb.Append(',');
                    firstCat = false;

                    sb.Append('\"').Append(EscapeJson(catKv.Key)).Append("\":{");
                    bool firstProp = true;

                    foreach (var propKv in catKv.Value)
                    {
                        if (!firstProp) sb.Append(',');
                        firstProp = false;
                        sb.Append('\"').Append(EscapeJson(propKv.Key)).Append("\":").Append(propKv.Value);
                    }

                    sb.Append('}');
                }

                sb.Append('}');
            }

            sb.Append('}');
            return sb.ToString();
        }


        /// <summary>
        /// Resolves a textual scope token into matching submodel root <see cref="ModelItem"/> objects.  
        /// </summary>
        /// <param name="doc">
        /// The active <see cref="Document"/> from which submodels are scanned.  
        /// </param>
        /// <param name="scopeToken">
        /// The token used for matching. Can be one of:  
        /// - CanonicalId  
        /// - FileOnly (filename without path)  
        /// - Display (display name)  
        /// - Ext (file extension, e.g. ".nwd")  
        /// </param>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> to abort scanning early.  
        /// </param>
        /// <param name="diagnostics">
        /// Output string providing diagnostic info when no matches are found.  
        /// Lists all available submodels in the form "FileOnly|Ext|CanonicalId".  
        /// </param>
        /// <returns>
        /// A list of matching submodel root items.  
        /// Returns an empty list if no matches were found.  
        /// </returns>
        /// <remarks>
        /// - Internally uses <see cref="ScanSubModels"/> with <c>includeContainers=true</c>.  
        /// - Matching is case-insensitive.  
        /// - If no matches are found, <paramref name="diagnostics"/> provides a hint with available options.  
        /// - Intended for resolving scope restrictions in queries (e.g., <c>get_element_count_by_category</c>).  
        /// </remarks>
        private List<ModelItem> ResolveScopeToModelRoots(Document doc, string scopeToken, CancellationToken ct, out string diagnostics)
        {
            diagnostics = "";
            var subs = ScanSubModels(doc, ct, true);
            var matches = new List<ModelItem>();

            foreach (var sm in subs)
            {
                if (ct.IsCancellationRequested) break;

                if (!string.IsNullOrWhiteSpace(sm.CanonicalId) &&
                    sm.CanonicalId.Equals(scopeToken, StringComparison.OrdinalIgnoreCase))
                { matches.Add(sm.Root); continue; }

                if (!string.IsNullOrWhiteSpace(sm.FileOnly) &&
                    sm.FileOnly.Equals(scopeToken, StringComparison.OrdinalIgnoreCase))
                { matches.Add(sm.Root); continue; }

                if (!string.IsNullOrWhiteSpace(sm.Display) &&
                    sm.Display.Equals(scopeToken, StringComparison.OrdinalIgnoreCase))
                { matches.Add(sm.Root); continue; }

                if (!string.IsNullOrWhiteSpace(sm.Ext) &&
                    sm.Ext.Equals(scopeToken, StringComparison.OrdinalIgnoreCase))
                { matches.Add(sm.Root); continue; }
            }

            if (matches.Count == 0)
            {
                var available = string.Join(", ", subs.Select(s =>
                    (SafeNameOrDefault(s.FileOnly, s.Display, DEFAULT_UNNAMED)) + "|" + s.Ext + "|" + s.CanonicalId));
                diagnostics = "available=[" + available + "]";
            }
            return matches;
        }

        /// <summary>
        /// Extracts and fills the main identity metadata of a <see cref="ModelItem"/>.  
        /// </summary>
        /// <param name="item">
        /// The <see cref="ModelItem"/> to extract metadata from.  
        /// </param>
        /// <param name="cid">
        /// Output: Canonical identifier of the item (GUID or path-hash).  
        /// </param>
        /// <param name="name">
        /// Output: Display name of the item, falling back to class display name, class name, or a default placeholder.  
        /// </param>
        /// <param name="typ">
        /// Output: IFC class name if available; otherwise the Navisworks class display name or class name.  
        /// </param>
        /// <param name="ifcGuid">
        /// Output: Extracted IFC GUID (GlobalId) if available, otherwise empty.  
        /// </param>
        /// <remarks>
        /// - Combines canonical ID, display name, IFC metadata, and type for consistent item identification.  
        /// - Uses <see cref="GetCanonicalId"/> and <see cref="GetIfcMeta"/> internally.  
        /// - Provides robust defaults to avoid null or empty values.  
        /// </remarks>
        private void FillItemHead(ModelItem item, out string cid, out string name, out string typ, out string ifcGuid)
        {
            cid = GetCanonicalId(item);
            name = SafeNameOrDefault(item.DisplayName, item.ClassDisplayName ?? item.ClassName, DEFAULT_UNNAMED);
            var meta = GetIfcMeta(item);
            var cls = !string.IsNullOrEmpty(meta.ifcClass) ? meta.ifcClass : (item.ClassDisplayName ?? item.ClassName ?? "");
            typ = cls ?? "";
            ifcGuid = meta.ifcGuid ?? "";
        }

        /// <summary>
        /// Ensures that a string is never <c>null</c>.  
        /// </summary>
        /// <param name="s">
        /// Input string, which may be <c>null</c>.  
        /// </param>
        /// <returns>
        /// The original string if not <c>null</c>, otherwise an empty string (<c>""</c>).  
        /// </returns>
        /// <remarks>
        /// This utility is useful when serializing or logging strings to guarantee  
        /// non-null values and avoid <see cref="NullReferenceException"/>s.  
        /// </remarks> 
        private static string NullSafe(string s) => s ?? "";
    }
}
