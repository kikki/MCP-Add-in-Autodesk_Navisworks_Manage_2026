using System;
using System.Collections.Concurrent;
using System.Linq;
using Autodesk.Windows;
using waabe_navi_shared;

namespace waabe_navi_mcp.Services
{
    /// <summary>
    /// Manages the status of MCP server instances and their corresponding Ribbon buttons.
    /// - Keeps track of running state and port per serviceKey.
    /// - Updates Ribbon button text based on server status.
    /// - Provides events to notify when server status changes.
    /// - Supports automatic registration of buttons by ID.
    /// </summary>
    public static class ServerStatusManager
    {
        // ------ Constants & Types ------
        public const string DefaultServiceKey = "waabe_navi_mcp_server";

        private class ServerState
        {
            public RibbonButton Button;
            public volatile bool IsRunning;
            public volatile int Port;
        }

        // key -> state mapping
        private static readonly ConcurrentDictionary<string, ServerState> _states =
            new ConcurrentDictionary<string, ServerState>(StringComparer.OrdinalIgnoreCase);

        // ------ Events ------
        /// <summary>
        /// Event fired whenever the status of a server changes (started or stopped).
        /// </summary>
        public static event EventHandler<ServerStatusChangedEventArgs> StatusChanged;

        // ------ Compatibility (default key) ------
        /// <summary>
        /// Indicates if the default server is currently running.
        /// </summary>
        public static bool IsServerRunning => GetState(DefaultServiceKey).IsRunning;

        /// <summary>
        /// Gets the current port of the default server.
        /// </summary>
        public static int CurrentPort => GetState(DefaultServiceKey).Port;

        // ------ Public API ------

        /// <summary>
        /// Registers a RibbonButton reference for the given serviceKey.
        /// - Stores the button in the state dictionary.
        /// - Updates button text to reflect current server status.
        /// </summary>
        public static void RegisterServerButton(RibbonButton button, string serviceKey = DefaultServiceKey)
        {
            if (button == null)
            {
                LogHelper.LogError("RegisterServerButton: button == null", "MCP");
                return;
            }

            var state = GetState(serviceKey);
            state.Button = button;

            LogHelper.LogEvent($"Server button registered for '{serviceKey}'");
            LogHelper.LogDebug($"[{serviceKey}] Button-ID: {button.Id}, Text: '{button.Text}'", "MCP");

            UpdateButtonText(serviceKey);
        }

        /// <summary>
        /// Marks the server as started for the given serviceKey.
        /// - Sets running state and port.
        /// - Updates the button text accordingly.
        /// - Fires the StatusChanged event.
        /// </summary>
        public static void SetServerStarted(int port, string serviceKey = DefaultServiceKey)
        {
            var state = GetState(serviceKey);
            state.IsRunning = true;
            state.Port = port;

            LogHelper.LogEvent($"[{serviceKey}] Server status: STARTED on port {port}");
            UpdateButtonText(serviceKey);

            StatusChanged?.Invoke(null, new ServerStatusChangedEventArgs(serviceKey, true, port));
        }

        /// <summary>
        /// Marks the server as stopped for the given serviceKey.
        /// - Resets running state and port.
        /// - Updates the button text accordingly.
        /// - Fires the StatusChanged event.
        /// </summary>
        public static void SetServerStopped(string serviceKey = DefaultServiceKey)
        {
            var state = GetState(serviceKey);
            var oldPort = state.Port;

            state.IsRunning = false;
            state.Port = 0;

            LogHelper.LogInfo($"[{serviceKey}] Server status: STOPPED (was on port {oldPort})", "MCP");
            UpdateButtonText(serviceKey);

            StatusChanged?.Invoke(null, new ServerStatusChangedEventArgs(serviceKey, false, oldPort));
        }

        /// <summary>
        /// Refreshes server status by querying the actual server service via reflection.
        /// - Reads IsServerRunning and CurrentPort properties.
        /// - Updates the internal state and button accordingly.
        /// </summary>
        public static void RefreshFromServerService(string serviceKey = DefaultServiceKey)
        {
            try
            {
                LogHelper.LogDebug($"[{serviceKey}] Refreshing server status from service", "MCP");

                var mcpService = ServiceRegistry.GetService(serviceKey);
                if (mcpService == null)
                {
                    LogHelper.LogEvent($"[{serviceKey}] Server service not available for status refresh");
                    return;
                }

                var t = mcpService.GetType();
                var isRunningProperty = t.GetProperty("IsServerRunning");
                var currentPortProperty = t.GetProperty("CurrentPort");

                if (isRunningProperty != null && currentPortProperty != null)
                {
                    var isRunning = (bool)isRunningProperty.GetValue(mcpService);
                    var port = (int)currentPortProperty.GetValue(mcpService);

                    if (isRunning)
                        SetServerStarted(port, serviceKey);
                    else
                        SetServerStopped(serviceKey);

                    LogHelper.LogEvent($"[{serviceKey}] Status refresh: Server {(isRunning ? "running" : "stopped")} on port {port}");
                }
                else
                {
                    LogHelper.LogError($"[{serviceKey}] Properties IsServerRunning/CurrentPort not found", "MCP");
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"[{serviceKey}] Error during status refresh: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempts to locate a RibbonButton by its ID and register it for the given serviceKey.
        /// - Searches through all tabs, panels, and items in the Ribbon.
        /// </summary>
        public static bool TryRegisterServerButtonById(string ribbonButtonId, string serviceKey = DefaultServiceKey)
        {
            try
            {
                var ribbon = ComponentManager.Ribbon;
                if (ribbon == null)
                {
                    LogHelper.LogWarning($"[{serviceKey}] Ribbon not available (TryRegisterServerButtonById)", "MCP");
                    return false;
                }

                foreach (var tab in ribbon.Tabs)
                {
                    foreach (var panel in tab.Panels)
                    {
                        foreach (var item in panel.Source.Items)
                        {
                            if (item is RibbonButton btn && btn.Id == ribbonButtonId)
                            {
                                RegisterServerButton(btn, serviceKey);
                                LogHelper.LogEvent($"[{serviceKey}] Button '{ribbonButtonId}' found & registered");
                                return true;
                            }
                        }
                    }
                }

                LogHelper.LogEvent($"[{serviceKey}] Button-ID '{ribbonButtonId}' not found");
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"[{serviceKey}] Error in TryRegisterServerButtonById('{ribbonButtonId}'): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Searches for known button IDs and registers the first match.
        /// - Used for automatic server button detection during startup.
        /// </summary>
        public static void FindAndRegisterServerButton(
            string serviceKey = DefaultServiceKey,
            params string[] candidateButtonIds)
        {
            try
            {
                LogHelper.LogDebug($"[{serviceKey}] Searching for server button for auto-registration", "MCP");

                var ids = (candidateButtonIds != null && candidateButtonIds.Length > 0)
                    ? candidateButtonIds
                    : new[] { "BTN_MCP_SERVER", "Old_BTN_MCP_SERVER" };

                foreach (var id in ids)
                {
                    if (TryRegisterServerButtonById(id, serviceKey))
                        return;
                }

                LogHelper.LogEvent($"[{serviceKey}] No matching server button found");
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"[{serviceKey}] Error while auto-finding button: {ex.Message}");
            }
        }

        // ------ Private Helpers ------

        /// <summary>
        /// Retrieves the current state object for a given serviceKey, creating it if necessary.
        /// </summary>
        private static ServerState GetState(string serviceKey)
        {
            return _states.GetOrAdd(serviceKey ?? DefaultServiceKey, _ => new ServerState
            {
                Button = null,
                IsRunning = false,
                Port = 0
            });
        }

        /// <summary>
        /// Updates the RibbonButton text to reflect the current server status.
        /// - Shows "Server: [Port]" if running, otherwise "Server starten".
        /// </summary>
        private static void UpdateButtonText(string serviceKey)
        {
            try
            {
                var state = GetState(serviceKey);

                if (state.Button == null)
                {
                    LogHelper.LogDebug($"[{serviceKey}] No button registered – skipping text update", "MCP");
                    return;
                }

                string newText = state.IsRunning ? $"Server: {state.Port}" : "Server starten";
                state.Button.Text = newText;

                LogHelper.LogSuccess($"[{serviceKey}] Button text updated: '{newText}'", "MCP");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[{serviceKey}] Error updating button text: {ex.Message}", "MCP");
            }
        }
    }

    /// <summary>
    /// Event arguments for ServerStatusManager.StatusChanged event.
    /// - Provides service key, running state, and port.
    /// </summary>
    public class ServerStatusChangedEventArgs : EventArgs
    {
        public string ServiceKey { get; }
        public bool IsRunning { get; }
        public int Port { get; }

        public ServerStatusChangedEventArgs(string serviceKey, bool isRunning, int port)
        {
            ServiceKey = serviceKey;
            IsRunning = isRunning;
            Port = port;
        }
    }
}
