using System;
using System.Web.Script.Serialization;

namespace waabe_navi_mcp_server.Contracts
{
    /// <summary>
    /// Represents a JSON-RPC request object.
    /// - Contains request ID, method name, and parameters.
    /// - Can be deserialized from JSON.
    /// </summary>
    public sealed class RpcRequest
    {
        /// <summary>
        /// Unique request identifier.
        /// - Used to match responses with requests.
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// Name of the method to invoke on the server.
        /// </summary>
        public string method { get; set; }

        /// <summary>
        /// Parameters for the method.
        /// - Can be any JSON-serializable object.
        /// </summary>
        public object @params { get; set; }

        /// <summary>
        /// Deserializes a JSON string into an RpcRequest object.
        /// </summary>
        public static RpcRequest FromJson(string json)
            => new JavaScriptSerializer().Deserialize<RpcRequest>(json);
    }

    /// <summary>
    /// Metadata returned with an RPC response.
    /// - Contains information about server version, query time, and request IDs.
    /// </summary>
    public sealed class RpcMeta
    {
        public string model_revision { get; set; }
        public int query_ms { get; set; }
        public string server_version { get; set; }
        public string request_id { get; set; }
    }

    /// <summary>
    /// Error information for failed RPC calls.
    /// - Contains an error code and message.
    /// </summary>
    public sealed class RpcError
    {
        public string code { get; set; }
        public string msg { get; set; }
    }

    /// <summary>
    /// Static helper class for serializing objects into JSON RPC responses.
    /// </summary>
    public static class RpcResponse
    {
        /// <summary>
        /// Serializes an object into a JSON string.
        /// </summary>
        public static string ToJson(object obj)
            => new JavaScriptSerializer().Serialize(obj);
    }

    /// <summary>
    /// Generic wrapper for an RPC response.
    /// - Indicates success (ok = true) or failure (ok = false).
    /// - Provides typed response data, error details, and metadata.
    /// </summary>
    public sealed class RpcResponse<T>
    {
        /// <summary>
        /// Indicates whether the RPC call succeeded.
        /// </summary>
        public bool ok { get; set; }

        /// <summary>
        /// The data returned from a successful RPC call.
        /// </summary>
        public T data { get; set; }

        /// <summary>
        /// Error information if the RPC call failed.
        /// </summary>
        public RpcError error { get; set; }

        /// <summary>
        /// Metadata about the response (e.g., server version, query time).
        /// </summary>
        public RpcMeta meta { get; set; }

        /// <summary>
        /// Creates a success response with optional metadata.
        /// </summary>
        public static RpcResponse<T> Success(T data, RpcMeta meta = null)
            => new RpcResponse<T> { ok = true, data = data, meta = meta };

        /// <summary>
        /// Creates a failure response with an error code and message.
        /// </summary>
        public static RpcResponse<T> Fail(string code, string msg)
            => new RpcResponse<T> { ok = false, error = new RpcError { code = code, msg = msg } };
    }
}
