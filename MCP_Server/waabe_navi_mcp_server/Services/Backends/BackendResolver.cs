using System;
using waabe_navi_shared;

namespace waabe_navi_mcp_server.Services.Backends
{
    /// <summary>
    /// Provides a backend instance for Navisworks API access.
    /// - Prefers ReflectionBackend (connects into waabe_navi_mcp via reflection).
    /// - Falls back to FallbackBackend if reflection is unavailable or fails.
    /// - Singleton pattern using Lazy&lt;T&gt; ensures backend is resolved once and reused.
    /// </summary>
    public static class BackendResolver
    {
        /// <summary>
        /// Lazy-initialized backend instance.
        /// - First tries ReflectionBackend and checks IsAvailable.
        /// - Logs and falls back to FallbackBackend if reflection is not possible.
        /// </summary>
        private static readonly Lazy<IWaabeNavisworksBackend> _instance =
            new Lazy<IWaabeNavisworksBackend>(() =>
            {
                try
                {
                    var rb = new ReflectionBackend();
                    if (rb.IsAvailable)
                    {
                        LogHelper.LogInfo("BackendResolver: Using ReflectionBackend (waabe_navi_mcp detected).");
                        return rb;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.LogEvent("BackendResolver: Reflection init failed: " + ex.Message);
                }

                LogHelper.LogInfo("BackendResolver: Using FallbackBackend.");
                return new FallbackBackend();
            });

        /// <summary>
        /// Gets the singleton backend instance.
        /// - Use this property whenever backend access is required.
        /// - The resolved instance is either ReflectionBackend or FallbackBackend.
        /// </summary>
        public static IWaabeNavisworksBackend Instance => _instance.Value;
    }
}
