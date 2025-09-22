using System;
using System.Collections.Concurrent;

namespace waabe_navi_mcp_server.Infrastructure.Caching
{
    /// <summary>
    /// Simple in-memory cache with TTL (time-to-live).
    /// - Stores responses keyed by string.
    /// - Automatically invalidates entries older than Settings.ResponseCacheTtlMs.
    /// - Thread-safe implementation using ConcurrentDictionary.
    /// </summary>
    public sealed class ResponseCache
    {
        private readonly ConcurrentDictionary<string, (DateTime ts, object value)> _mem =
            new ConcurrentDictionary<string, (DateTime ts, object value)>();

        /// <summary>
        /// Attempts to get a cached value for the given key.
        /// - Returns true and outputs the cached value if it exists and has not expired.
        /// - Returns false if the entry is missing, expired, or cannot be cast to T.
        /// - Expired entries are removed automatically.
        /// </summary>
        /// <typeparam name="T">Expected type of the cached value.</typeparam>
        /// <param name="key">Cache key.</param>
        /// <param name="value">Output value if found and valid, otherwise default(T).</param>
        /// <returns>True if a valid cached value was found, otherwise false.</returns>
        public bool TryGet<T>(string key, out T value)
        {
            value = default;
            if (_mem.TryGetValue(key, out var entry))
            {
                if ((DateTime.UtcNow - entry.ts).TotalMilliseconds < Settings.ResponseCacheTtlMs)
                {
                    if (entry.value is T t)
                    {
                        value = t;
                        return true;
                    }
                }
                else
                {
                    // Remove expired entry
                    _mem.TryRemove(key, out _);
                }
            }
            return false;
        }

        /// <summary>
        /// Adds or updates a cache entry with the given key and value.
        /// - Overwrites any existing entry for the same key.
        /// - Timestamp is set to current UTC time for TTL tracking.
        /// </summary>
        /// <param name="key">Cache key.</param>
        /// <param name="value">Value to store in the cache.</param>
        public void Set(string key, object value)
            => _mem[key] = (DateTime.UtcNow, value);
    }
}
