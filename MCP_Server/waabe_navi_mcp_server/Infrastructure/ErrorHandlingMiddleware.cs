using System;
using waabe_navi_mcp_server.Contracts;

namespace waabe_navi_mcp_server.Infrastructure
{
    /// <summary>
    /// Maps exceptions to a unified RpcError object for consistent error handling.
    /// - Provides a Wrap method that executes an action inside a try/catch block.
    /// - Converts common exceptions into standardized RpcResponse failures with error codes.
    /// - Ensures that controllers always return RpcResponse<T> objects (success or failure).
    /// </summary>
    public static class ErrorHandlingMiddleware
    {
        /// <summary>
        /// Executes the given action and wraps it in error handling logic.
        /// - Returns the action result if successful.
        /// - Catches known exceptions and maps them to RpcResponse<T>.Fail with proper error codes:
        ///   NVX_TIMEOUT → TimeoutException
        ///   NVX_INVALID_ARG → ArgumentException
        ///   NVX_CANCELED → OperationCanceledException
        ///   NVX_UNEXPECTED → Any other unhandled exception
        /// - Ensures that controllers never throw raw exceptions back to clients.
        /// </summary>
        /// <typeparam name="T">The type of the response data.</typeparam>
        /// <param name="action">The action to execute, returning RpcResponse&lt;T&gt;.</param>
        /// <returns>A successful RpcResponse&lt;T&gt; or a failure response with RpcError.</returns>
        public static RpcResponse<T> Wrap<T>(Func<RpcResponse<T>> action)
        {
            try
            {
                return action();
            }
            catch (TimeoutException tex)
            {
                return RpcResponse<T>.Fail("NVX_TIMEOUT", tex.Message);
            }
            catch (ArgumentException aex)
            {
                return RpcResponse<T>.Fail("NVX_INVALID_ARG", aex.Message);
            }
            catch (OperationCanceledException)
            {
                return RpcResponse<T>.Fail("NVX_CANCELED", "Operation was canceled.");
            }
            catch (Exception ex)
            {
                return RpcResponse<T>.Fail("NVX_UNEXPECTED", ex.ToString());
            }
        }
    }
}
