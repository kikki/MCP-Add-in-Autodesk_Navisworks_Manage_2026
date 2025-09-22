// waabe_navi_mcpserver/Services/Implementations/ExportService.cs
using System.Threading;
using System.Threading.Tasks;
using waabe_navi_mcp_server.Contracts;
using waabe_navi_mcp_server.Services;
using waabe_navi_mcp_server.Services.Backends;
using waabe_navi_shared;

namespace waabe_navi_mcp_server.Services.Implementations
{
    /// <summary>
    ///  Orchestrates export functions (OBJ/FBX) and CSV dumps via the backend
    ///     (reflection to waabe_navi_mcp, otherwise fallback).
    /// </summary>
    public sealed class ExportService : IExportService
    {
        private static IWaabeNavisworksBackend BE => BackendResolver.Instance;

        /*ToDo*/
    }
}
