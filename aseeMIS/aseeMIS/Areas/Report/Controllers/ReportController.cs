using dsMIS.Application.Utils;
using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.Report.Models;
using aseeMIS.Utils;
using dsReport.Core.DTOs;
using dsReport.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Syncfusion.EJ2.Spreadsheet;

namespace aseeMIS.Areas.Report.Controllers
{
    [Area("Report")]
    public class ReportController(IReportService reportService, IReportUniverseService reportUniverseService, IReportUniverseParamService universeParamService,
        IServiceProvider serviceProvider, IAppFormMenuService menuService, IUserContextService userContextService, IAppFormPermissionService formAccessService, IWebHostEnvironment env) : Controller
    {
        public async Task<IActionResult> ReportDesigner(int id)
        {
            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) =
                await AppAuthorizeHelper.ValidateReadPermissionAsync(HttpContext, formAccessService, menuService, userContextService, id, EnumContextFormType.PARENT);

            if (!isValid || appForm == null || formContext == null)
                return RedirectToAction("NoAccess", "Error");

            var reports = await reportService.SelectAllForDropdownAsync();
            var reportUniverses = await reportUniverseService.SelectAllForDropdownAsync();

            ViewBag.FormSessionId = formContext.FormSessionId;
            ViewBag.Reports = reports;
            ViewBag.ReportUniverses = reportUniverses;
            return View("~/Areas/Report/Views/ReportDesigner.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> GetUniverseParams(int universeId)
        {
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

            // 1) authorize & resolve once
            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) = await AppAuthorizeHelper.ValidateActionAsync(
                                            HttpContext, formAccessService, menuService, userContextService, formSessionId, EumFormAccessAction.READ);

            if (universeId == 0)
                return BadRequest("Invalid universe id.");

            var parameters = await universeParamService.SelectUniverseParamsForUiAsync(universeId, formContext.LanguageCode) ?? Enumerable.Empty<ReportUniverseParamUIDto>();
            return Json(parameters);
        }

        [HttpPost]
        public async Task<IActionResult> GetUniverseParamDropdownData([FromBody] UniverseParamDropdownRequest request)
        {
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(formSessionId))
                return BadRequest("Missing FormSessionId.");

            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) = await AppAuthorizeHelper.ValidateActionAsync(
                                            HttpContext, formAccessService, menuService, userContextService, formSessionId, EumFormAccessAction.READ);

            if (!isValid || formContext == null)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.ProjectAcronym) ||
                string.IsNullOrWhiteSpace(request.ModelName) ||
                string.IsNullOrWhiteSpace(request.MethodName))
                return BadRequest("Invalid dropdown metadata.");

            var serviceInvoker = new ServiceInvoker(serviceProvider);

            // Resolve parameters dynamically (same idea as grid FK)
            var methodParams = new List<object?>();

            if (!string.IsNullOrWhiteSpace(request.MethodParams))
            {
                foreach (var p in request.MethodParams.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (p.Equals("@LanguageCode", StringComparison.OrdinalIgnoreCase))
                        methodParams.Add(formContext.LanguageCode);
                    else if (p.Equals("@UserId", StringComparison.OrdinalIgnoreCase))
                        methodParams.Add(formContext.UserId);
                    else
                        methodParams.Add(p);
                }
            }

            var data = await serviceInvoker.InvokeAsync(request.ProjectAcronym, request.ServiceName ?? request.ModelName, null, request.MethodName, methodParams.ToArray());

            return Json(data ?? Array.Empty<object>());
        }

        [HttpPost]
        public IActionResult OpenReportTemplate(string path, OpenRequest openRequest)
        {
            if (string.IsNullOrWhiteSpace(path))
                return BadRequest("Template path is missing.");

            path = path.TrimStart('/').Replace("\\", "/");

            var fullPath = Path.Combine(env.WebRootPath, path);

            if (!System.IO.File.Exists(fullPath))
                return NotFound($"File not found: {path}");

            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);

            var formFile = new FormFile(
                stream,
                0,
                stream.Length,
                "file",
                Path.GetFileName(fullPath)
            );

            openRequest.File = formFile;

            var json = Workbook.Open(openRequest);
            return Content(json);
        }

        [HttpPost]
        public async Task<IActionResult> ExecuteUniverseCommand([FromBody] ExecuteUniverseRequest request)
        {
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

            // 1) authorize & resolve once
            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) = await AppAuthorizeHelper.ValidateActionAsync(
                                            HttpContext, formAccessService, menuService, userContextService, formSessionId, EumFormAccessAction.READ);
            if (request.UniverseId == 0)
                return BadRequest("Invalid universe id.");

            var universe = await reportUniverseService.GetByIdAsync(request.UniverseId);
            if (universe is null)
                return NotFound("Universe not found.");

            request.Parameters ??= new Dictionary<string, object>();

            // ---- Validate required params (if any)
            var expectedParams = await universeParamService.SelectByUniverseAsync(request.UniverseId);

            foreach (var p in expectedParams.Where(x => x.IsRequired))
            {
                if (!request.Parameters.ContainsKey(p.ParamName) ||
                    request.Parameters[p.ParamName] is null ||
                    string.IsNullOrWhiteSpace(request.Parameters[p.ParamName]?.ToString()))
                {
                    return BadRequest($"Missing required parameter: {p.ParamName}");
                }
            }

            var result = await reportUniverseService.ExecuteUniverseAsync(request.UniverseId, request.Parameters);

            if (result is null)
                return Json(Array.Empty<object>());

            return Json(result);
        }
    }
}
