// Controllers/ExportController.cs
using System.Threading;
using System.Web.Script.Serialization;
using waabe_navi_mcp_server.Contracts;
using waabe_navi_mcp_server.Infrastructure;
using waabe_navi_mcp_server.Services;
using waabe_navi_mcp_server.Services.Implementations;
using static waabe_navi_mcp_server.Infrastructure.ErrorHandlingMiddleware;

namespace waabe_navi_mcp_server.Controllers
{

    public sealed class ExportController
    {
        private readonly IExportService _svc = new ExportService();
        private static readonly JavaScriptSerializer _jss = new JavaScriptSerializer();

       /*ToDo*/
    }
}
