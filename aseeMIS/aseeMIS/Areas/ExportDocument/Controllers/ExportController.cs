using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Services.Interfaces;
using aseeMIS.Utils;
using dsReportExport.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace aseeMIS.Areas.ExportDocument.Controllers
{
    [Area("ExportDocument")]
    [Route("ExportDocument/Export/[action]/{formId?}")]
    public class ExportController(
        IWebHostEnvironment env,
        IDwExcelGeneralDepositsReportService generalDepositsReportService,
        IAppFormMenuService menuService,
        IAppFormPermissionService formAccessService,
        IAppSessionService dsSessionService,
        IUserContextService userSecurityStampCacheService) : Controller
    {
        private readonly IWebHostEnvironment _env = env;
        private readonly IDwExcelGeneralDepositsReportService _generalDepositsReportService = generalDepositsReportService;

        private const string FixedFileName = "BANK_TEMPLATE_BLANK.xlsx";
        private const string FolderName = "fsdk-export";

        [HttpGet]
        public async Task<IActionResult> Index(int formId)
        {
            var (isValid, appForm, contextSessionDto, projectNamespace, serviceInterface, viewModel) =
                await AppAuthorizeHelper.ValidateReadPermissionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userSecurityStampCacheService,
                    formId,
                    EnumContextFormType.PARENT
                );

            if (!isValid || contextSessionDto == null)
                return RedirectToAction("NoAccess", "Error");

            ViewBag.FormId = formId;
            ViewBag.FormSessionId = contextSessionDto.FormSessionId;
            ViewBag.FormConfigId = contextSessionDto.FormId;
            Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

            return View("~/Areas/ExportDocument/Views/ReportExport.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> ListFile(int formId, DateOnly date, string reportType = "FSDK")
        {
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

            var (isValid, _, contextSessionDto, _, _, _) =
                await AppAuthorizeHelper.ValidateActionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userSecurityStampCacheService,
                    formSessionId,
                    requiredPermission: EumFormAccessAction.READ
                );

            if (!isValid || contextSessionDto == null)
                return Unauthorized(new { ok = false, message = "No access." });

            Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;
            var normalizedReportType = (reportType ?? "FSDK").Trim().ToUpper();
            Debug.WriteLine($"[Controller] ListFile called. ReportType=[{reportType}], Date=[{date}]");

            // --- existing logic unchanged ---
            var folder = Path.Combine(_env.WebRootPath, FolderName);
            var path = Path.Combine(folder, FixedFileName);

            if (!System.IO.File.Exists(path))
                return Json(new { ok = false, message = "File not found on server." });

            var displayName = $"{date:yyyy-MM-dd} - {FixedFileName}";
            var downloadName = $"{date:yyyy-MM-dd}_{FixedFileName}";

            return Json(new
            {
                ok = true,
                reportType = normalizedReportType,
                date = date.ToString("yyyy-MM-dd"),
                originalFileName = FixedFileName,
                displayName,
                downloadName,
                downloadUrl = Url.Action("Download", "Export", new
                {
                    area = "FSDK",
                    date = date.ToString("yyyy-MM-dd")
                })
            });
        }

        [HttpGet]
        public async Task<IActionResult> Download(int formId, string date, string reportType)
        {
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

            var (isValid, _, contextSessionDto, _, _, _) =
                await AppAuthorizeHelper.ValidateActionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userSecurityStampCacheService,
                    formSessionId,
                    requiredPermission: EumFormAccessAction.READ
                );

            if (!isValid || contextSessionDto == null)
                return Unauthorized("No access.");

            Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

            // --- existing logic unchanged ---
            if (!DateOnly.TryParse(date, out var parsedDate))
                return BadRequest("Invalid date.");
            Debug.WriteLine($"[Controller] Download called. ReportType=[{reportType}], Date=[{parsedDate:yyyy-MM-dd}]");

            var folder = Path.Combine(_env.WebRootPath, FolderName);
            var templatePath = Path.Combine(folder, FixedFileName);

            if (!System.IO.File.Exists(templatePath))
                return NotFound("Template file not found.");

            switch (reportType?.Trim().ToUpper())
            {
                case "IRR":
                    Debug.WriteLine("[Controller] Routing to IRR export.");
                    // TODO: IRR export logic goes here
                    return BadRequest("IRR export not yet implemented.");

                case "SBR":
                    Debug.WriteLine("[Controller] Routing to SBR export.");
                    // TODO: SBR export logic goes here
                    return BadRequest("SBR export not yet implemented.");

                case "FSDK":
                default:
                    Debug.WriteLine($"[Controller] Routing to FSDK export for date: {parsedDate:yyyy-MM-dd}");
                    try
                    {
                        var fileBytes = await _generalDepositsReportService
                            .GenerateFsdkExcelAsync(parsedDate, templatePath);
                        var downloadName = $"{parsedDate:yyyy-MM-dd}_{FixedFileName}";
                        var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        return File(fileBytes, contentType, downloadName);
                    }
                    catch (InvalidOperationException ex)
                    {
                        Debug.WriteLine($"[Controller] FSDK export failed: {ex.Message}");
                        return BadRequest(ex.Message);
                    }
            }
        }
    }
}