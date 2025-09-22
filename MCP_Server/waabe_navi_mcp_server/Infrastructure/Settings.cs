namespace waabe_navi_mcp_server.Infrastructure
{
    /// <summary>
    /// Central configuration settings for the MCP server.
    /// - Defines global constants for timeouts, caching, and versioning.
    /// - Used throughout infrastructure and controller logic.
    /// </summary>
    public static class Settings
    {
        /// <summary>
        /// Default timeout (in milliseconds) for async operations such as queries and service calls.
        /// Example: 10000 = 10 seconds.
        /// </summary>
        public const int DefaultTimeoutMs = 10000;

        /// <summary>
        /// Enables or disables the response cache globally.
        /// true = cache responses where supported, false = disable caching.
        /// </summary>
        public const bool EnableResponseCache = true;

        /// <summary>
        /// Time-to-live (in milliseconds) for cached responses when caching is enabled.
        /// Example: 5000 = cache entries expire after 5 seconds.
        /// </summary>
        public const int ResponseCacheTtlMs = 5000;

        /// <summary>
        /// Current server version string.
        /// - Used in manifest and system info responses.
        /// - Update when releasing new server versions.
        /// </summary>
        public const string ServerVersion = "1.0.0";
    }
}
