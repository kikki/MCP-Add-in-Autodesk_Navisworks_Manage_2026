// waabe_navi_mcp_server/Services/MCPServer.cs
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using waabe_navi_mcp_server.Contracts;       
using waabe_navi_mcp_server.Infrastructure;  
using waabe_navi_mcp_server.Mapping;         
using waabe_navi_shared;                     

namespace waabe_navi_mcp_server.Services
{
    /// <summary>
    /// Datei/File: Services/MCPServer.cs | Klasse/Class: MCPServer
    ///   HTTP adapter for JSON-RPC and helper routes (/health, /manifest).
    /// </summary> 
    public sealed class MCPServer : IDisposable
    {
        private readonly int _port;
        private readonly HttpListener _listener = new HttpListener();
        private readonly JavaScriptSerializer _jss = new JavaScriptSerializer();
        private readonly RpcRouter _router;
        private CancellationTokenSource _cts;
        private Task _acceptLoopTask;

        /// <summary>
        /// Fired when the server emits an informational message (e.g. startup, shutdown).
        /// </summary>
        public event EventHandler<string> ServerMessage;

        /// <summary>
        /// Fired when the server encounters an error.
        /// </summary>
        public event EventHandler<Exception> ServerError;

        /// <summary>
        /// Gets whether the server is currently running.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// The port number the server is configured to listen on.
        /// </summary>
        public int Port => _port;

        /// <summary>
        /// Returns the base server URL (127.0.0.1:{port}).
        /// </summary> 
        public string ServerUrl => $"http://127.0.0.1:{_port}/";

        /// <summary>
        /// Creates a new MCPServer instance for the given port.
        /// Initializes the RPC router and registers routes.
        /// </summary>
        public MCPServer(int port)
        {
            _port = port;

            _router = new RpcRouter();
            try
            {
                 
                RpcMap.Register(_router.Routes);
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"[MCPServer] Warnung: RpcMap.Register konnte nicht aufgerufen werden: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts the HTTP listener asynchronously on the configured port.
        /// </summary>
        /// <returns>
        /// True if the listener started successfully, false otherwise.
        /// </returns>
        public async Task<bool> StartAsync()
        {
            if (IsRunning) return true;

            try
            {
                var urlA = $"http://127.0.0.1:{_port}/";
                var urlB = $"http://localhost:{_port}/";

                _listener.Prefixes.Clear();
                _listener.Prefixes.Add(urlA);
                if (!string.Equals(urlA, urlB, StringComparison.OrdinalIgnoreCase))
                    _listener.Prefixes.Add(urlB);

                _listener.Start();
                _cts = new CancellationTokenSource();
                _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_cts.Token), _cts.Token);

                IsRunning = true;
                OnServerMessage($"Listening on: {urlA} (and localhost)");
                return true;
            }
            catch (HttpListenerException hex)
            {
                IsRunning = false;
                OnServerError(hex);
                OnServerMessage("Hinweis/Note: Prüfe URL-ACL (netsh) oder ob ein anderer Prozess den Port belegt.");
                return false;
            }
            catch (Exception ex)
            {
                IsRunning = false;
                OnServerError(ex);
                return false;
            }
        }

        /// <summary>
        /// Stops the HTTP listener and cancels the accept loop.
        /// </summary>
        public async Task StopAsync()
        {
            if (!IsRunning) return;

            try
            {
                _cts?.Cancel();

                try { _listener.Stop(); } catch { /* ignore */ }

                if (_acceptLoopTask != null)
                {
                    try { await _acceptLoopTask.ConfigureAwait(false); } catch { /* ignore */ }
                }
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>
        /// Background loop that continuously accepts incoming HTTP requests until cancellation.
        /// </summary>
        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext ctx = null;
                try
                {
                    ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                    _ = Task.Run(() => HandleRequestAsync(ctx), ct);
                }
                catch (ObjectDisposedException) { break; }
                catch (HttpListenerException)
                {
                    if (ct.IsCancellationRequested) break;
                     
                }
                catch (Exception ex)
                {
                    OnServerError(ex);
                }
            }
        }

        /// <summary>
        /// Handles a single incoming HTTP request.
        /// Supports helper endpoints (/health, /manifest, /mcp_manifest.json) and /rpc for JSON-RPC.
        /// </summary>
        private async Task HandleRequestAsync(HttpListenerContext ctx)
        {
             
            ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
            ctx.Response.Headers["Access-Control-Allow-Headers"] = "content-type";
            ctx.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";

            try
            {
                var req = ctx.Request;
                var res = ctx.Response;
                var path = (req.Url?.AbsolutePath ?? "/").TrimEnd('/').ToLowerInvariant();
                var method = req.HttpMethod?.ToUpperInvariant();

                 
                if (method == "OPTIONS")
                {
                    res.StatusCode = 204;
                    res.Close();
                    return;
                }

                 
                if (method == "GET" && (path == "" || path == "/"))
                {
                    await WriteTextAsync(res, 200, "WAABE MCP Server is running. Try POST /rpc").ConfigureAwait(false);
                    return;
                }

                 
                if (method == "GET" && path == "/health")
                {
                    await WriteTextAsync(res, 200, "OK").ConfigureAwait(false);
                    return;
                }

                 
                if (method == "GET" && path == "/manifest")
                {
                    var baseUrl = GetBaseUrl(req);
                    var doc = ManifestBuilder.Build(baseUrl);

                     
                    ManifestBuilder.TryWriteToDisk(doc, out _);

                    var json = _jss.Serialize(doc);
                    await WriteRawAsync(res, 200, "application/json; charset=utf-8", json).ConfigureAwait(false);
                    return;
                }

                 
                if (method == "GET" && path == "/mcp_manifest.json")
                {
                    var baseUrl = GetBaseUrl(req);
                    var doc = ManifestBuilder.Build(baseUrl);
                    ManifestBuilder.TryWriteToDisk(doc, out var fullPath);

                     
                    if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                    {
                        var fileJson = File.ReadAllText(fullPath, Encoding.UTF8);
                        await WriteRawAsync(res, 200, "application/json; charset=utf-8", fileJson).ConfigureAwait(false);
                    }
                    else
                    {
                        var json = _jss.Serialize(doc);
                        await WriteRawAsync(res, 200, "application/json; charset=utf-8", json).ConfigureAwait(false);
                    }
                    return;
                }

                 
                if (method == "POST" && (path == "/rpc" || path == ""))
                {
                    string body;
                    using (var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8))
                        body = await reader.ReadToEndAsync().ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(body))
                    {
                        await WriteJsonAsync(res, 400, new { error = "empty body" }).ConfigureAwait(false);
                        return;
                    }

                    RpcRequest rpcReq;
                    try
                    {
                        rpcReq = _jss.Deserialize<RpcRequest>(body);
                    }
                    catch (Exception dex)
                    {
                        await WriteJsonAsync(res, 400, new { error = "invalid json", detail = dex.Message }).ConfigureAwait(false);
                        return;
                    }

                    object rpcResp;
                    try
                    {
                        rpcResp = _router.Dispatch(rpcReq);
                    }
                    catch (Exception ex)
                    {
                        OnServerError(ex);
                         
                        var fail = RpcResponse<object>.Fail("NVX_INTERNAL", ex.Message);
                         
                        rpcResp = fail;
                    }

                    var json = _jss.Serialize(rpcResp);
                    await WriteRawAsync(res, 200, "application/json; charset=utf-8", json).ConfigureAwait(false);
                    return;
                }

                 
                await WriteTextAsync(ctx.Response, 404, "Not Found").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                OnServerError(ex);
                try
                {
                    await WriteJsonAsync(ctx.Response, 500, new { error = ex.Message }).ConfigureAwait(false);
                }
                catch {   }
            }
        }

        /// <summary>
        /// Builds a base URL string from the given request (scheme, host, port).
        /// </summary>
        private static string GetBaseUrl(HttpListenerRequest req)
        {
            try
            {
                var scheme = req.Url?.Scheme ?? "http";
                var host = req.UserHostName ?? "127.0.0.1";
                var port = req.Url?.Port ?? 80;
                return $"{scheme}://{host}:{port}/";
            }
            catch
            {
                return "http://127.0.0.1/";
            }
        }

        /// <summary>
        /// Writes plain text to the HTTP response with the given status code.
        /// </summary>
        private static async Task WriteTextAsync(HttpListenerResponse res, int code, string text)
            => await WriteRawAsync(res, code, "text/plain; charset=utf-8", text);

        /// <summary>
        /// Serializes the given object as JSON and writes it to the HTTP response.
        /// </summary>
        private static async Task WriteJsonAsync(HttpListenerResponse res, int code, object obj)
        {
            var jss = new JavaScriptSerializer();
            var json = jss.Serialize(obj);
            await WriteRawAsync(res, code, "application/json; charset=utf-8", json);
        }

        /// <summary>
        /// Writes a raw string payload to the HTTP response with the specified content type.
        /// </summary>
        private static async Task WriteRawAsync(HttpListenerResponse res, int code, string contentType, string payload)
        {
            var bytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);
            res.StatusCode = code;
            res.ContentType = contentType;
            res.ContentEncoding = Encoding.UTF8;
            res.ContentLength64 = bytes.LongLength;
            using (var os = res.OutputStream)
            {
                await os.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Raises <see cref="ServerMessage"/> and logs the message.
        /// </summary>
        private void OnServerMessage(string msg)
        {
            try { ServerMessage?.Invoke(this, msg); } catch {   }
            LogHelper.LogEvent($"[MCP] {msg}");
        }

        /// <summary>
        /// Raises <see cref="ServerError"/> and logs the exception.
        /// </summary>
        private void OnServerError(Exception ex)
        {
            try { ServerError?.Invoke(this, ex); } catch {   }
            LogHelper.LogEvent($"[MCP-ERROR] {ex.GetType().Name}: {ex.Message}");
        }

        /// <summary>
        /// Cleans up server resources (listener, cancellation tokens).
        /// </summary>
        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Close(); } catch { }
            try { _cts?.Dispose(); } catch { }
        }
    }
}
