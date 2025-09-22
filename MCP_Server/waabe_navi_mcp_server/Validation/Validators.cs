using System;

namespace waabe_navi_mcp_server.Validation
{
    /// <summary>
    /// Provides common validation helpers for request parameters.
    /// Each method throws an <see cref="ArgumentException"/> when validation fails.
    /// </summary>
    public static class Validators
    {
        /// <summary>
        /// Ensures that a string parameter is not null, empty, or whitespace.
        /// </summary>
        /// <param name="name">The parameter name (used in the exception message).</param>
        /// <param name="value">The string value to check.</param>
        /// <exception cref="ArgumentException">Thrown if the string is null, empty, or whitespace.</exception>
        public static void EnsureNonEmpty(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"Parameter '{name}' must not be empty.");
        }

        /// <summary>
        /// Ensures that a limit value is within the allowed range (1–5000).
        /// </summary>
        /// <param name="limit">The numeric limit to validate.</param>
        /// <exception cref="ArgumentException">Thrown if the limit is outside the range.</exception>
        public static void EnsureLimit(int limit)
        {
            if (limit <= 0 || limit > 5000)
                throw new ArgumentException("limit must be between 1 and 5000.");
        }

        /// <summary>
        /// Ensures that an offset value is non-negative (≥ 0).
        /// </summary>
        /// <param name="offset">The offset value to validate.</param>
        /// <exception cref="ArgumentException">Thrown if the offset is negative.</exception>
        public static void EnsureOffset(int offset)
        {
            if (offset < 0) throw new ArgumentException("offset must be >= 0.");
        }
    }
}
