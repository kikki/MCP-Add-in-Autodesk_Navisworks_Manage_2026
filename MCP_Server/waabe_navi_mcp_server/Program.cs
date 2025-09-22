using System;
using waabe_navi_mcp_server.Infrastructure;
using waabe_navi_shared; // LogHelper

namespace waabe_navi_mcp_server
{
    /// <summary>
    /// Entry point for hosting the MCP server inside the Navisworks Add-in process.
    /// Provides lifecycle methods to start and stop the <see cref="RpcHost"/>.
    /// </summary>
    public static class Program
    {
        private static RpcHost _host;

        /// <summary>
        /// Starts the MCP server by creating and initializing a <see cref="RpcHost"/>.
        /// </summary>
        public static void Start()
        {
            LogHelper.LogInfo("MCPServer starting...", "[waabe_navi_mcpserver/Programm]");
            _host = new RpcHost();
            _host.Start();
            LogHelper.LogInfo("MCPServer started.", "[waabe_navi_mcpserver/Programm]");
        }

        /// <summary>
        /// Stops the MCP server and releases resources.
        /// </summary>
        public static void Stop()
        {
            try
            {
                _host?.Dispose();
                LogHelper.LogInfo("MCPServer stopped.", "[waabe_navi_mcpserver/Programm]");
            }
            catch (Exception ex)
            {
                LogHelper.LogError("Error while stopping MCPServer: " + ex, "[waabe_navi_mcpserver/Programm]");
            }
        }
    }
}
