using Autodesk.Navisworks.Api.Plugins;
using Autodesk.Windows;
using System.Linq;
using System.Windows.Forms;
using waabe_navi_mcp.Commands;
using waabe_navi_mcp.Helpers;
using waabe_navi_shared;
using waabe_navi_mcp.Services;

[Plugin("ButtonIstGeklickt", "WAABE", DisplayName = "MCP_server")]
/// <summary>
/// Main plugin class for the WAABE MCP Navisworks Add-in.
/// - Extends EventWatcherPlugin to hook into Navisworks lifecycle events.
/// - Initializes a custom Ribbon tab with buttons defined in Buttons.xml.
/// - Uses ButtonHandlerFactory to bind Ribbon buttons to ICommand handlers.
/// - Manages startup, Ribbon creation (with retry mechanism), and cleanup on unloading.
/// </summary>
public class Waabe_Navi_Mcp : EventWatcherPlugin
{
    private const int V = 5000;
    private const int G = 500;
    private System.Windows.Forms.Timer _timer;
    private bool _ribbonCreated = false;
    private int _waitedMs = 0;
    private int _intervalMs = G;
    private int _maxWaitMs = V;

    /// <summary>
    /// Called when the plugin is loaded into Navisworks.
    /// - Logs startup information.
    /// - Starts a timer to wait until the Ribbon control is available before adding custom buttons.
    /// </summary>
    public override void OnLoaded()
    {
        LogHelper.LogProjectStartup("MCP-MAIN", "1.0.0");
        LogHelper.LogInfo("Main plugin initialization started");
        StartRibbonTimer(GetTimer());
    }

    /// <summary>
    /// Returns a new WinForms timer instance.
    /// - Used internally to delay Ribbon initialization until UI is ready.
    /// </summary>
    private Timer GetTimer()
    {
        return new System.Windows.Forms.Timer();
    }

    /// <summary>
    /// Starts the Ribbon initialization timer.
    /// - Configures interval and max wait time.
    /// - On each tick, checks if the Ribbon is available.
    /// </summary>
    private void StartRibbonTimer(Timer timer)
    {
        LogHelper.LogDebug("Ribbon timer started");

        _waitedMs = 0;
        _ribbonCreated = false;
        _timer = timer;
        _timer.Interval = _intervalMs;
        _timer.Tick += Timer_Tick;
        _timer.Start();

        LogHelper.LogInfo($"Ribbon timer running (interval: {_intervalMs}ms, max wait: {_maxWaitMs}ms)");
    }

    /// <summary>
    /// Event handler for the timer tick.
    /// - If Ribbon is available and not yet created: builds Ribbon tab, panels, and buttons.
    /// - Loads button definitions from Buttons.xml and binds them to ICommand handlers.
    /// - If Ribbon is not available within max wait, shows a retry dialog to the user.
    /// </summary>
    private void Timer_Tick(object sender, System.EventArgs e)
    {
        var ribbonControl = ComponentManager.Ribbon;
        if (ribbonControl != null && !_ribbonCreated)
        {
            LogHelper.LogSuccess("Navisworks Ribbon found, creating UI");

            _ribbonCreated = true;
            _timer.Stop();
            _timer.Dispose();

            // Load buttons from XML
            var buttonDefs = ButtonsXmlLoader.LoadButtonDefinitions();

            var tabName = "waabe";
            var myTab = ribbonControl.Tabs.FirstOrDefault(t => t.Title == tabName);
            if (myTab == null)
            {
                myTab = new RibbonTab { Title = tabName, Id = tabName };
                ribbonControl.Tabs.Add(myTab);
            }
            else
            {
                LogHelper.LogInfo($"Ribbon tab '{tabName}' already exists");
            }

            var myPanel = myTab.Panels.FirstOrDefault(p => p.Source.Title == "Aktionen");
            if (myPanel == null)
            {
                var panelSource = new RibbonPanelSource { Title = "Aktionen" };
                myPanel = new RibbonPanel { Source = panelSource };
                myTab.Panels.Add(myPanel);

                LogHelper.LogInfo("Start creating buttons");

                foreach (var def in buttonDefs)
                {
                    LogHelper.LogDebug($"Creating button: {def.Id} ('{def.Text}')");

                    var btn = new RibbonButton
                    {
                        Text = def.Text ?? "Button",
                        ShowText = true,
                        Id = def.Id,
                        ToolTip = def.Description,
                        Size = RibbonItemSize.Large,
                        LargeImage = waabe_navi_mcp.Helpers.ImageLoader.LoadImage(def.LargeImage),
                        Image = waabe_navi_mcp.Helpers.ImageLoader.LoadImage(def.SmallImage),

                        // Use ButtonHandlerFactory instead of direct handler
                        CommandHandler = GetCommandHandler(def.Id)
                    };
                    panelSource.Items.Add(btn);

                    // Register server button for status updates
                    if (def.Id == "BTN_MCP_SERVER")
                    {
                        waabe_navi_mcp.Services.ServerStatusManager.RegisterServerButton(btn);
                        LogHelper.LogEvent("Server button registered for status updates");
                    }
                    

                    LogHelper.LogSuccess($"Button '{def.Text}' created successfully");
                }

                LogHelper.LogSuccess($"All {buttonDefs.Count} buttons created and added to the Ribbon");
            }
        }
        else
        {
            _waitedMs += _intervalMs;
            if (_waitedMs >= _maxWaitMs)
            {
                LogHelper.LogWarning($"Ribbon timeout after {_maxWaitMs / 1000.0:F1} seconds");

                _timer.Stop();
                _timer.Dispose();

                var result = MessageBox.Show(
                    $"The Navisworks Ribbon was not found after {_maxWaitMs / 1000.0:F1} seconds.\nTry again?",
                    "Ribbon not found",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    LogHelper.LogInfo("User chose to restart Ribbon timer");
                    StartRibbonTimer(GetTimer());
                }
                else
                {
                    LogHelper.LogWarning("User aborted Ribbon initialization");
                }
            }
            else if (_waitedMs % 1000 == 0) // log every second
            {
                LogHelper.LogDebug($"Waiting for Ribbon... ({_waitedMs / 1000.0:F1}s of {_maxWaitMs / 1000.0:F1}s)");
            }
        }
    }

    /// <summary>
    /// Maps button IDs to ICommand handlers using ButtonHandlerFactory.
    /// - Ensures that each Ribbon button executes the correct command.
    /// - Provides a fallback handler if no match is found.
    /// </summary>
    private System.Windows.Input.ICommand GetCommandHandler(string buttonId)
    {
        switch (buttonId)
        {
            
            case "BTN_MCP":
                return ButtonHandlerFactory.GetHandler("MCPButtonHandler");
            case "BTN_MCP_SERVER":
                return ButtonHandlerFactory.GetHandler("MCPServerButtonHandler");
            default:
                return ButtonHandlerFactory.GetHandler("MCPButtonHandler"); // fallback
        }
    }

    /// <summary>
    /// Called when the plugin is unloaded from Navisworks.
    /// - Stops and disposes the Ribbon timer.
    /// - Removes the Ribbon tab if it was created.
    /// - Logs shutdown information.
    /// </summary>
    public override void OnUnloading()
    {
        LogHelper.LogInfo("Main plugin shutting down");

        _timer?.Stop();
        _timer?.Dispose();

        var ribbonControl = ComponentManager.Ribbon;
        if (ribbonControl == null)
        {
            LogHelper.LogWarning("Ribbon control not available during shutdown");
            return;
        }

        var tabName = "Dateien";
        var myTab = ribbonControl.Tabs.FirstOrDefault(t => t.Title == tabName);
        if (myTab != null)
        {
            ribbonControl.Tabs.Remove(myTab);
            LogHelper.LogInfo($"Ribbon tab '{tabName}' removed");
        }

        LogHelper.LogProjectShutdown("MCP-MAIN");
    }
}
