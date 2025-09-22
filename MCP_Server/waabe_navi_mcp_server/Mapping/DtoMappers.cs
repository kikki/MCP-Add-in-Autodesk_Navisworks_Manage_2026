using System.Collections.Generic;

namespace waabe_navi_mcp_server.Mapping
{
    /// <summary>
    /// Utility class for DTO-related transformations and normalization.
    /// - Provides helper methods to clean up or standardize property values.
    /// - Can be extended with additional mapping functions as needed.
    /// </summary>
    public static class DtoMappers
    {
        /// <summary>
        /// Normalizes a string value.
        /// - Trims leading and trailing whitespace.
        /// - Returns null if the input is null.
        /// Example:
        ///   Input:  "  Steel  "
        ///   Output: "Steel"
        /// </summary>
        /// <param name="s">The input string to normalize.</param>
        /// <returns>The trimmed string, or null if input was null.</returns>
        public static string Normalize(string s) => s?.Trim();
    }
}
