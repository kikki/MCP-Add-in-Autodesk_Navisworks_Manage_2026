// waabe_navi_mcp_server/Contracts/DtoContracts.cs
using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace waabe_navi_mcp_server.Contracts
{
    /// <summary>
    /// Represents a single property item result.
    /// - Contains canonical ID, model info, property value, and path information.
    /// - Used in property search responses.
    /// </summary>
    public sealed class PropertyItemDto
    {
        public string canonical_id { get; set; }
        public List<PathStep> path_from_this_object { get; set; } = new List<PathStep>();
        public string model_name { get; set; }
        public string model_canonical_id { get; set; }
        public string ModelFilter { get; set; }
        public string PropertyValue { get; set; }
    }

    /// <summary>
    /// DTO representing a list of property items.
    /// - Echoes search parameters.
    /// - Contains matched items with count and optional filters.
    /// </summary>
    public sealed class PropertyItemListDto
    {
        public string category { get; set; }
        public string property { get; set; }
        public string Scope { get; set; }
        public string ModelFilter { get; set; }
        public string ValueFilter { get; set; }
        public bool IgnoreCase { get; set; }
        public int count { get; set; }
        public List<PropertyItemDto> Items { get; set; }
    }

    /// <summary>
    /// Input arguments for listing items to property values.
    /// - Provides clean parameter passing via BackendResolver.
    /// - Supports scope, value filters, and limits.
    /// </summary>
    public sealed class ListItemsToPropertyArgs
    {
        public string category { get; set; }
        public string property { get; set; }
        public string Scope { get; set; }
        public string ModelFilter { get; set; }
        public string ValueFilter { get; set; }
        public bool IgnoreCase { get; set; } = true;
        public int? MaxResults { get; set; }
    }

    /// <summary>
    /// Represents a loaded sub-model in the Navisworks document.
    /// </summary>
    public sealed class SubModel
    {
        public string FileOnly { get; set; }
        public string Ext { get; set; }
        public string Display { get; set; }
        public ModelItem Root { get; set; }
        public string CanonicalId { get; set; }
        public bool IsContainer { get; set; }
    }

    /// <summary>
    /// Information about how a scope string was resolved to actual IDs.
    /// - Contains input, resolved, and applied IDs with reason and element name.
    /// </summary>
    public sealed class ScopeAppliedInfo
    {
        public string input_id { get; set; }
        public string resolved_id { get; set; }
        public string applied_id { get; set; }
        public string reason { get; set; }
        public string element_name { get; set; }
    }

    /// <summary>
    /// Base message type for API responses.
    /// - Contains success flag, message, and optional details.
    /// </summary>
    public class AI_MassageDto
    {
        [DefaultValue(true)]
        public bool success { get; set; } = true;

        [DefaultValue("no warnings")]
        public string message { get; set; } = "no warnings";

        public string details { get; set; } = "";
    }

    /// <summary>
    /// Simple ping/pong response object.
    /// </summary>
    public sealed class PongDto
    {
        public string message { get; set; } = "";
    }

    /// <summary>
    /// Information about the MCP server version and API.
    /// </summary>
    public sealed class ServerInfoDto
    {
        public string version { get; set; } = "";
        public string api { get; set; } = "";
    }

    /// <summary>
    /// Details about a single model in the Navisworks document.
    /// - Includes IDs, filenames, display names, and element counts.
    /// </summary>
    public sealed class ModelDetailDto
    {
        public string canonical_id { get; set; } = "";
        public string perent_canonical_id { get; set; } = "";
        public string FileName { get; set; } = "";
        public string SourceFileName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int ChildrenCount { get; set; }
        public int DescendantsCount { get; set; }
    }

    /// <summary>
    /// Overview of the currently loaded Navisworks document.
    /// - Inherits from AI_MassageDto.
    /// - Includes model details, categories, histograms, and total element counts.
    /// </summary>
    public sealed class ModelOverviewDto : AI_MassageDto
    {
        public int ModelsCount { get; set; }
        public int TotalElements { get; set; }
        public string DocumentTitle { get; set; } = "";
        public List<ModelDetailDto> Models { get; set; } = new List<ModelDetailDto>();
        public List<string> available_categories { get; set; } = new List<string>();
        public Dictionary<string, int> categories_histogram { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public int total_items { get; set; }
    }

    /// <summary>
    /// Element count result for a given category and scope.
    /// </summary>
    public sealed class ElementCountDto : AI_MassageDto
    {
        public string category { get; set; } = "";
        public int count { get; set; }
        public string scope { get; set; } = "all";
    }

    /// <summary>
    /// Generic DTO list wrapper that can include an AI_MassageDto with status information.
    /// </summary>
    public sealed class DtoList<T> : List<T>
    {
        public AI_MassageDto ai_message { get; set; } = new AI_MassageDto();
    }

    /// <summary>
    /// Unit and tolerance information.
    /// - Used for reporting measurement units and tolerances of the document.
    /// </summary>
    public sealed class UnitInfoDto
    {
        public string length_unit { get; set; }//= "mm";
        public string area_unit { get; set; }// = "m2";
        public string volume_unit { get; set; }// = "m3";
        public double length_tolerance { get; set; }// = 0.001;
    }

    /// <summary>
    /// Full property set for a single item.
    /// - Includes categories, geometries, child references, and path information.
    /// - Inherits from AI_MassageDto for status reporting.
    /// </summary>
    public sealed class ItemPropertiesDto : AI_MassageDto
    {
        public string element_name { get; set; } = "";
        public Dictionary<string, List<SimplePropJson>> categories { get; set; } =
            new Dictionary<string, List<SimplePropJson>>(StringComparer.OrdinalIgnoreCase);
        public string canonical_id { get; set; } = "";
        public string typ { get; set; } = "";
        public string interner_typ { get; set; } = "";
        public string ifc_guid { get; set; } = "";
        public Dictionary<string, List<SimplePropJson>> geometries { get; set; } =
            new Dictionary<string, List<SimplePropJson>>(StringComparer.OrdinalIgnoreCase);
        public List<SimpleItemRef> child_from_this_object { get; set; } = new List<SimpleItemRef>();
        public List<PathStep> path_from_this_object { get; set; } = new List<PathStep>();
    }

    /// <summary>
    /// Step in the hierarchical path of an element.
    /// - Inherits from AI_MassageDto for consistency.
    /// </summary>
    public sealed class PathStep : AI_MassageDto
    {
        public string canonical_id { get; set; } = "";
        public string paths { get; set; } = "";
    }

    /// <summary>
    /// Simple reference to an item (minimal information).
    /// - Inherits from AI_MassageDto for status compatibility.
    /// </summary>
    public sealed class SimpleItemRef : AI_MassageDto
    {
        public string canonical_id { get; set; } = "";
        public string element_name { get; set; } = "";
        public string typ { get; set; } = "";
    }

    /// <summary>
    /// Simplified property representation in JSON format.
    /// - Contains property name, type, and value as strings.
    /// </summary>
    public sealed class SimplePropJson
    {
        public string property { get; set; } = "";
        public string type { get; set; } = "";
        public string value { get; set; } = "";
    }

    /// <summary>
    /// Snapshot of the current selection.
    /// - Provides count, canonical IDs, and paths.
    /// - Inherits from AI_MassageDto for status reporting.
    /// </summary>
    public sealed class SelectionSnapshotDto : AI_MassageDto
    {
        public int count { get; set; }
        public List<string> canonical_id { get; set; } = new List<string>();
        public List<string> path { get; set; } = new List<string>();
    }

    /// <summary>
    /// Result of applying a selection command.
    /// - Reports how many items were affected.
    /// - Inherits from AI_MassageDto for success and message details.
    /// </summary>
    public sealed class ApplyResultDto : AI_MassageDto
    {
        public int affected { get; set; }
    }
}
