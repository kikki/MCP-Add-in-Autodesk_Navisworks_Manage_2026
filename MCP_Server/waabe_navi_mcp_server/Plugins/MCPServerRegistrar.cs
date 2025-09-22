// waabe_navi_mcp_server/Plugins/MCPServerRegistrar.cs
using Autodesk.Navisworks.Api.Plugins;
using System;
using System.Threading;
using System.Threading.Tasks;
using waabe_navi_mcp;                      // ServiceRegistry, SettingsManager, IWaabeService
using waabe_navi_mcp_server.Services;     // MCPServer (network host)
using waabe_navi_mcp_server.Services.Backends;
using waabe_navi_shared;                  // LogHelper

namespace waabe_navi_mcp_server.Plugins
{
    /// <summary>
    /// Navisworks plugin that registers the MCPServerService in the shared ServiceRegistry.
    /// - Called automatically by the Navisworks plugin lifecycle.
    /// - Ensures the MCP server is available to other components (UI buttons, automation).
    /// </summary>
    [Plugin("MCPServerRegistrar", "WAABE", DisplayName = "MCP Server Registrar")]
    public class MCPServerRegistrar : EventWatcherPlugin
    {
        private static MCPServerService _serverService;

        /// <summary>
        /// Called when Navisworks loads the plugin.
        /// - Instantiates the MCPServerService.
        /// - Registers it in the shared ServiceRegistry.
        /// - Logs successful registration.
        /// </summary>
        public override void OnLoaded()
        {
            try
            {
                _serverService = new MCPServerService();
                ServiceRegistry.Register(_serverService);
                LogHelper.LogEvent("MCPServerRegistrar: MCP Server Service registered.");
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Error in MCPServerRegistrar.OnLoaded: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when Navisworks unloads the plugin.
        /// - Disposes the MCPServerService.
        /// - Logs shutdown.
        /// </summary>
        public override void OnUnloading()
        {
            try
            {
                _serverService?.Dispose();
                LogHelper.LogEvent("MCPServerRegistrar unloading.");
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Error unloading MCPServerRegistrar: {ex.Message}");
            }
        }

        /// <summary>
        /// Static accessor for retrieving the MCPServerService instance (optional use).
        /// </summary>
        public static MCPServerService GetServerService() => _serverService;
    }

    /// <summary>
    /// Service class that wraps the lifecycle of the MCP server.
    /// - Provides Reflection-compatible API for UI buttons and automation.
    /// - Manages server start/stop, settings persistence, and runtime status.
    ///
    /// Public API (Reflection-compatible):
    /// - string  ServiceName
    /// - bool    IsAvailable
    /// - bool    IsServerRunning
    /// - int     CurrentPort
    /// - Task&lt;bool&gt; StartServerAsync(int port)
    /// - Task     StopServerAsync()
    /// - string   GetServerUrl()
    /// - int      GetPortFromSettings()
    /// - void     SavePortToSettings(int port)
    /// - bool     GetServerEnabledFromSettings()
    /// - string   GetSettingsFilePath()
    /// - void     LogSettingsDebug()
    /// </summary>
    public class MCPServerService : IWaabeService, IDisposable
    {
        private MCPServer _server;
        private bool _isDisposed;
        private bool _isRunning;
        private int _port;

        /// <summary>
        /// Unique service name for registry access (kept stable for UI integration).
        /// </summary>
        public string ServiceName => "waabe_navi_mcp_server";
        public bool IsAvailable => !_isDisposed;
        public bool IsServerRunning => _isRunning;
        public int CurrentPort => _port;

        public MCPServerService()
        {
            LogHelper.LogEvent("UiThread initialized for MCPServerService");
            UiThread.InitializeFromCurrentThread();
            LogHelper.LogEvent($"[UIINIT] ctx={SynchronizationContext.Current?.GetType().FullName ?? "null"}, " +
                   $"tid={Thread.CurrentThread.ManagedThreadId}, apt={Thread.CurrentThread.GetApartmentState()}");
            LogHelper.LogEvent("MCPServerService created");

            _port = SafeGetPortFromSettings();

            // Optional auto-start if enabled in settings
            if (SafeGetServerEnabledFromSettings())
            {
                _ = StartServerAsync(_port);
            }
        }

        /// <summary>
        /// Returns the current server URL if running, otherwise empty string.
        /// </summary>
        public string GetServerUrl()
        {
            if (_isRunning && _server != null)
                return _server.ServerUrl ?? $"http://127.0.0.1:{_port}/";
            return string.Empty;
        }

        /// <summary>
        /// Starts the MCP server asynchronously on the given port.
        /// - If already running, stops the existing server first.
        /// - Persists port and status to settings.
        /// - Logs events and errors.
        /// </summary>
        /// <param name="port">Desired HTTP port.</param>
        /// <returns>True if server started successfully, false otherwise.</returns>
        public async Task<bool> StartServerAsync(int port)
        {
            try
            {
                if (_isRunning && _server != null)
                {
                    await StopServerAsync().ConfigureAwait(false);
                }

                _server = new MCPServer(port);

                // Wire events to logger
                _server.ServerMessage += (s, msg) => LogHelper.LogEvent($"[MCPServer] {msg}");
                _server.ServerError += (s, ex) => LogHelper.LogEvent($"[MCPServer-Error] {ex.Message}");

                var success = await _server.StartAsync().ConfigureAwait(false);
                _isRunning = success;

                _port = port;
                SavePortToSettings(_port);
                SettingsManager.SaveMCPServerEnabled(success);

                if (success)
                    LogHelper.LogEvent($"MCP Server started; URL={_server.ServerUrl ?? $"http://127.0.0.1:{_port}/"}");
                else
                    LogHelper.LogEvent("MCP Server failed to start (listener binding error).");

                return success;
            }
            catch (Exception ex)
            {
                _isRunning = false;
                LogHelper.LogEvent($"Error starting MCP Server: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stops the MCP server asynchronously.
        /// - Persists last port and disables server flag in settings.
        /// - Logs shutdown.
        /// </summary>
        public async Task StopServerAsync()
        {
            try
            {
                if (_server != null)
                {
                    var lastPort = _port;

                    await _server.StopAsync().ConfigureAwait(false);
                    _server.Dispose();
                    _server = null;

                    _isRunning = false;

                    SavePortToSettings(lastPort);
                    SettingsManager.SaveMCPServerEnabled(false);

                    LogHelper.LogEvent($"MCP Server stopped (Port {lastPort}).");
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Error stopping MCP Server: {ex.Message}");
            }
        }

        // ===== Settings Integration =====

        public int GetPortFromSettings() => SafeGetPortFromSettings();

        public void SavePortToSettings(int port)
        {
            try
            {
                SettingsManager.SaveMCPPort(port);
                LogHelper.LogEvent($"Port {port} saved to settings");
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Error saving port to settings: {ex.Message}");
            }
        }

        public bool GetServerEnabledFromSettings() => SafeGetServerEnabledFromSettings();

        public string GetSettingsFilePath()
        {
            try
            {
                return SettingsManager.GetSettingsFilePath();
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Error getting settings file path: {ex.Message}");
                return "n/a";
            }
        }

        public void LogSettingsDebug()
        {
            try
            {
                var port = GetPortFromSettings();
                var enabled = GetServerEnabledFromSettings();
                var path = GetSettingsFilePath();

                LogHelper.LogEvent("=== SETTINGS DEBUG ===");
                LogHelper.LogEvent($"Current Port: {port}");
                LogHelper.LogEvent($"Server Enabled: {enabled}");
                LogHelper.LogEvent($"Settings File: {path}");
                LogHelper.LogEvent($"Settings file exists: {System.IO.File.Exists(path)}");

                if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                {
                    var text = System.IO.File.ReadAllText(path);
                    LogHelper.LogEvent($"Settings content: {text.Substring(0, Math.Min(200, text.Length))}...");
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Error in settings debug: {ex.Message}");
            }
        }

        // ===== IDisposable =====
        public void Dispose()
        {
            if (_isDisposed) return;
            try
            {
                Task.Run(async () =>
                {
                    try { await StopServerAsync().ConfigureAwait(false); }
                    catch (Exception ex) { LogHelper.LogEvent($"Error during async Dispose: {ex.Message}"); }
                }).Wait(5000);
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Error disposing MCPServerService: {ex.Message}");
            }
            finally
            {
                _isDisposed = true;
            }
        }

        // ===== private Helpers =====
        private static int SafeGetPortFromSettings()
        {
            try
            {
                var port = SettingsManager.GetMCPPort();
                LogHelper.LogEvent($"Port loaded from settings: {port}");
                return port;
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Error loading port from settings: {ex.Message}");
                return 8080; // fallback
            }
        }

        private static bool SafeGetServerEnabledFromSettings()
        {
            try { return SettingsManager.GetMCPServerEnabled(); }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Error loading server enabled flag from settings: {ex.Message}");
                return false;
            }
        }
    }
}
