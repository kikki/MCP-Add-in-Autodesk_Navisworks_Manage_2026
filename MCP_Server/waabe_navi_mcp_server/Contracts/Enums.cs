namespace waabe_navi_mcp_server.Contracts
{
    /// <summary>
    /// Enumeration of supported property filter operations.
    /// - Used to define how property values are compared when querying or filtering.
    /// </summary>
    public enum PropertyOp
    {
        /// <summary>
        /// Exact match (property value must equal the filter value).
        /// </summary>
        Equals,

        /// <summary>
        /// Substring match (property value must contain the filter value).
        /// </summary>
        Contains,

        /// <summary>
        /// Pattern match using wildcard syntax (e.g., * or ?).
        /// </summary>
        Wildcard,

        /// <summary>
        /// Range comparison (property value must fall within a numeric or date range).
        /// </summary>
        Range
    }
}
