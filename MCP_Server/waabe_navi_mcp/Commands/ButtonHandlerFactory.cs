using System;
using System.Windows.Input;
using waabe_navi_shared;

namespace waabe_navi_mcp.Commands
{
    public static class ButtonHandlerFactory
    {
        /// <summary>
        /// Factory method that returns an ICommand implementation based on the given handler name.
        /// - Called whenever a button in the Navisworks Add-in triggers an action and the system 
        ///   needs to resolve the corresponding handler.
        /// - Input: string handlerName (e.g. "MCPButtonHandler" or "MCPServerButtonHandler").
        /// - Output: An ICommand instance that can be executed by the UI framework.
        /// - If the handlerName is unknown, it defaults to MCPButtonHandler.
        /// </summary>
        public static ICommand GetHandler(string handlerName)
        {
            switch (handlerName)
            {
                case "MCPButtonHandler": return new MCPButtonHandler();
                case "MCPServerButtonHandler": return new MCPServerButtonHandler();

                default: return new MCPButtonHandler();
            }
        }
    }
}
