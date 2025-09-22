using System;

namespace waabe_navi_mcp_server.Contracts
{
    /// <summary>
    /// Data transfer object representing a pair of elements that are in clash.
    /// - Used in clash detection results to identify the two colliding items.
    /// - Properties 'a' and 'b' typically hold canonical IDs or element references.
    /// </summary>
    public sealed class ClashPairDto
    {
        public string a { get; set; }
        public string b { get; set; }
    }

    /// <summary>
    /// Arguments for running a clash detection.
    /// - scopeA / scopeB: define the search scope for clash test (e.g. "all" or selection).
    /// - tolerance_m: the clash tolerance in meters (default 0.01m).
    /// - test_name: name of the clash test to create or run.
    /// </summary>
    public sealed class ClashRunArgs
    {
        public string scopeA { get; set; } = "all";
        public string scopeB { get; set; } = "all";
        public double tolerance_m { get; set; } = 0.01;
        public string test_name { get; set; } = "MCP API Test";
    }

    /// <summary>
    /// Summary result object for a clash detection run.
    /// - Inherits from AI_MassageDto (base response class for API messages).
    /// - test_name: the name of the clash test executed.
    /// - results: number of detected clashes.
    /// </summary>
    public sealed class ClashSummaryDto : AI_MassageDto
    {
        public string test_name { get; set; }
        public int results { get; set; }
    }
}
