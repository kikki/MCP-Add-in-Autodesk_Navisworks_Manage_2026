using System.Collections.Generic;

namespace waabe_navi_mcp_server.Contracts
{
    /// <summary>
    /// Query to request all items from a given category.
    /// - category: API/IFC name of the property category.
    /// - scope: either "all" or a specific model_id.
    /// </summary>
    public sealed class CategoryQuery
    {
        public string category { get; set; }   // API/IFC name
        public string scope { get; set; }      // "all" | model_id
    }

    /// <summary>
    /// Query to search for items by property value.
    /// - Supports different operations (Equals, Contains, Wildcard, Range).
    /// - Can limit and paginate results.
    /// </summary>
    public sealed class PropertySearchQuery
    {
        /// <summary>
        /// Name of the property category (e.g. IFC property set).
        /// </summary>
        public string category { get; set; }

        /// <summary>
        /// Name of the property to filter by.
        /// </summary>
        public string property { get; set; }

        /// <summary>
        /// Operator to use (Equals, Contains, Wildcard, Range).
        /// </summary>
        public PropertyOp op { get; set; }

        /// <summary>
        /// Filter value to match against.
        /// </summary>
        public string value { get; set; }

        /// <summary>
        /// Scope of the query ("all" or a specific model_id).
        /// </summary>
        public string scope { get; set; }  // "all" | model_id

        /// <summary>
        /// Maximum number of results to return (default 200).
        /// </summary>
        public int limit { get; set; } = 200;

        /// <summary>
        /// Offset for pagination (default 0).
        /// </summary>
        public int offset { get; set; } = 0;

        /// <summary>
        /// Whether to ignore case when comparing strings (default true).
        /// </summary>
        public bool ignore_case { get; set; } = true;
    }

    /// <summary>
    /// Reference to a single item in the model.
    /// - Typically holds a canonical item ID.
    /// </summary>
    public sealed class ItemRef
    {
        public string item_id { get; set; }
    }

    /// <summary>
    /// Query to retrieve a portion of the model tree.
    /// - model_id: which model to query.
    /// - depth: how many hierarchy levels to expand (default 1).
    /// - limit: number of nodes to return (default 200).
    /// - cursor: continuation token for paging.
    /// </summary>
    public sealed class ModelTreeQuery
    {
        public string model_id { get; set; }
        public int depth { get; set; } = 1;
        public int limit { get; set; } = 200;
        public string cursor { get; set; }
    }

    /// <summary>
    /// Query to suggest property values.
    /// - Used for auto-completion of values when filtering.
    /// </summary>
    public sealed class ValueSuggestQuery
    {
        public string category { get; set; }
        public string property { get; set; }
        public string prefix { get; set; }
        public int topN { get; set; } = 20;
    }

    /// <summary>
    /// Options for exporting model data.
    /// - format: export format ("obj", "fbx", "csv").
    /// - scope: which elements to export ("selection" or "all").
    /// - path_hint: optional file path or directory hint.
    /// </summary>
    public sealed class ExportOptions
    {
        public string format { get; set; } // "obj" | "fbx" | "csv"
        public string scope { get; set; }  // "selection" | "all"
        public string path_hint { get; set; }
    }

    /// <summary>
    /// Command to apply a new selection in the model.
    /// - item_ids: list of item IDs to select.
    /// - keepExistingSelection: whether to preserve current selection.
    /// </summary>
    public sealed class SelectionApplyCommand
    {
        public List<string> item_ids { get; set; }
        public bool keepExistingSelection { get; set; }
    }

    /// <summary>
    /// Query to request details for specific items.
    /// - item_ids: list of item IDs to fetch.
    /// </summary>
    public sealed class ItemsQuery
    {
        public List<string> item_ids { get; set; }
    }
}
