namespace waabe_navi_mcp_server.Telemetry
{
    /// <summary>
    /// File: Telemetry/EventSourceIds.cs
    /// Provides a central list of string identifiers for telemetry and logging event sources.
    /// This helps ensure consistent naming across the system.
    /// </summary>
    public static class EventSourceIds
    {
        /// <summary>
        /// Event source identifier for selection guard operations
        /// (used to track and log guarded selection changes).
        /// </summary>
        public const string SelectionGuard = "selection_guard";
    }
}
