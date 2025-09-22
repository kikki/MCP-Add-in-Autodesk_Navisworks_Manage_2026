using System;
using waabe_navi_shared;

namespace waabe_navi_mcp_server.Infrastructure
{
    /// <summary>
    /// Logging helpers with correlation and verbosity control.
    /// - Provides standardized request correlation formatting.
    /// - Supports scope-based logging for entry/exit tracking.
    /// </summary>
    public static class LoggingExtensions
    {
        /// <summary>
        /// Prepends a request ID to a log message for correlation.
        /// - Useful for tracing multiple log lines related to the same RPC request.
        /// Example: WithReq("abc123", "Starting clash test")
        /// → "[req:abc123] Starting clash test"
        /// </summary>
        public static string WithReq(string requestId, string message)
            => $"[req:{requestId}] {message}";

        /// <summary>
        /// Creates a scoped log context.
        /// - Logs an "enter" message when created.
        /// - Logs a "leave" message when disposed.
        /// - Typically used with a using() statement to track execution flow.
        /// Example:
        /// using (LoggingExtensions.Scope("get_model_overview")) { ... }
        /// </summary>
        public static IDisposable Scope(string scope)
            => new LogScope(scope);

        /// <summary>
        /// Internal log scope implementation.
        /// - Logs when scope is entered and left.
        /// </summary>
        private sealed class LogScope : IDisposable
        {
            private readonly string _scope;

            public LogScope(string scope)
            {
                _scope = scope;
                LogHelper.LogDebug($"[enter] {_scope}");
            }

            public void Dispose()
            {
                LogHelper.LogDebug($"[leave] {_scope}");
            }
        }
    }
}
