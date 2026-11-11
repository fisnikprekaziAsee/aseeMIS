using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using dsCredinsEtl.Core.Services;
using dsCredinsEtl.Core.Services.Interfaces;
using aseeMIS.Services.Interfaces;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace aseeMIS.Areas.CredinsEtl.Controllers
{
    [Area("CredinsEtl")]
    [Route("CredinsEtl/DwEtlFileDataViewer/[action]/{formId?}")]
    public class DwEtlFileDataViewerController(
        IDwEtlFileService fileService,
        IDwEtlPassiveInventoryItemService passiveInventoryService,
        IDwEtlPassivePositiveInventoryItemService passivePositiveInventoryService,
        IDwEtlBankRelatedPersonService bankRelatedPersonService,
        IDwEtlLoanInventoryItemService loanInventoryService,
         IDwEtlOverdueInstallmentService overdueInstallmentService,
        IDwEtlExpiredOverdraftService expiredOverdraftService,
        IDwEtlGlStatementService glStatementService,
        IDwEtlFileImportLogService dwEtlFileImportLogService,
        IAppFormMenuService menuService,
        IAppFormPermissionService formAccessService,
        IAppSessionService dsSessionService,
        IUserContextService userSecurityStampCacheService) : Controller
    {

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

            // --- existing logic unchanged ---
            var allFiles = await fileService.SelectAllAsync();

            ViewBag.FilesDropdown = allFiles
                .Where(f => f.IsActive && f.IsApproved)
                .OrderBy(f => f.DwEtlFileName)
                .Select(f => new SelectListItem
                {
                    Value = f.DwEtlFileId.ToString(),
                    Text = string.IsNullOrWhiteSpace(f.DwEtlSheetName)
                        ? f.DwEtlFileName
                        : $"{f.DwEtlFileName} — {f.DwEtlSheetName}"
                })
                .ToList();

            return View("~/Areas/CredinsEtl/Views/DwEtlFileDataViewer.cshtml");
        }

        [HttpPost]
        public async Task<IActionResult> Process(int formId, int dwEtlFileId, string reportingDate)
        {
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

            var (isValid, _, contextSessionDto, _, _, _) =
                await AppAuthorizeHelper.ValidateActionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userSecurityStampCacheService,
                    formSessionId,
                    requiredPermission: EumFormAccessAction.UPDATE
                );

            if (!isValid || contextSessionDto == null)
                return Unauthorized(new { ok = false, message = "No access." });

            Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

            // --- existing logic unchanged ---
            try
            {
                if (dwEtlFileId <= 0 || string.IsNullOrWhiteSpace(reportingDate))
                    return Json(new { ok = false, message = "Invalid process request." });

                if (!DateOnly.TryParse(reportingDate, out var parsedDate))
                    return Json(new { ok = false, message = "Invalid reporting date." });

                var logs = await dwEtlFileImportLogService.SelectAllAsync();

                var log = logs
                    .Where(x => x.DwEtlFileId == dwEtlFileId && x.ReportingDate == parsedDate)
                    .OrderByDescending(x => x.ImportDate)
                    .FirstOrDefault();

                if (log == null)
                    return Json(new { ok = false, message = "No log found for the selected file and reporting date." });

                var updatedLog = await dwEtlFileImportLogService.UpdateLogAsync(
                    log.DwEtlFileImportLogId,
                    log.RecordsCount,
                    true,
                    "Processed successfully.");

                if (updatedLog == null)
                    return Json(new { ok = false, message = "Failed to update process status." });

                return Json(new { ok = true, message = "Log marked as processed successfully." });
            }
            catch (Exception)
            {
                return Json(new { ok = false, message = "An unexpected error occurred while processing." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> IsProcessed(int formId, int dwEtlFileId, string reportingDate)
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

            // --- existing logic unchanged ---
            if (dwEtlFileId <= 0 || !DateOnly.TryParse(reportingDate, out var parsedDate))
                return Json(new { processed = false });

            var logs = await dwEtlFileImportLogService.SelectAllAsync();

            var isProcessed = logs.Any(x => x.DwEtlFileId == dwEtlFileId
                                         && x.ReportingDate == parsedDate
                                         && x.HasPassed == true);

            return Json(new { processed = isProcessed });
        }

        [HttpGet]
        public async Task<IActionResult> Search(int formId, int? dwEtlFileId, string? reportingDate)
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

            try
            {
                if (dwEtlFileId is null || string.IsNullOrWhiteSpace(reportingDate))
                    return Json(new { ok = false, message = "Please select both a reporting date and a file." });

                if (!DateOnly.TryParse(reportingDate, out var parsedDate))
                    return Json(new { ok = false, message = "Invalid date format." });

                var allFiles = await fileService.SelectAllAsync();
                var selectedFile = allFiles.FirstOrDefault(f => f.DwEtlFileId == dwEtlFileId.Value);

                if (selectedFile is null)
                    return Json(new { ok = false, message = "Selected file not found." });

                var fileType = (selectedFile.DwEtlFileType ?? "").Trim().ToLowerInvariant();
                var sheetName = (selectedFile.DwEtlSheetName ?? "").Trim();
                var fileCodeOrName = (selectedFile.DwEtlFileName ?? "").Trim().ToUpperInvariant();

                object data;
                int count;

                switch (fileType)
                {
                    case "xlsx":
                        if (sheetName.Equals("Pasivi", StringComparison.OrdinalIgnoreCase))
                        {
                            var items = await passivePositiveInventoryService.SelectAllAsync();
                            var filtered = items.Where(x => x.DwEtlFileId == dwEtlFileId.Value && x.ReportingDate == parsedDate).ToList();
                            data = filtered;
                            count = filtered.Count;
                        }
                        else if (sheetName.Equals("Inv llog rrj+dep", StringComparison.OrdinalIgnoreCase))
                        {
                            var items = await passiveInventoryService.SelectAllAsync();
                            var filtered = items.Where(x => x.DwEtlFileId == dwEtlFileId.Value && x.ReportingDate == parsedDate).ToList();
                            data = filtered;
                            count = filtered.Count;
                        }
                        else if (sheetName.Equals("Lista e perjashtuar", StringComparison.OrdinalIgnoreCase))
                        {
                            var items = await bankRelatedPersonService.SelectAllAsync();
                            var filtered = items.Where(x => x.DwEtlFileId == dwEtlFileId.Value && x.ReportingDate == parsedDate).ToList();
                            data = filtered;
                            count = filtered.Count;
                        }
                        else if (sheetName.Equals("Inventari", StringComparison.OrdinalIgnoreCase))
                        {
                            var items = await loanInventoryService.SelectAllAsync();
                            var filtered = items.Where(x => x.DwEtlFileId == dwEtlFileId.Value && x.ReportingDate == parsedDate).ToList();
                            data = filtered;
                            count = filtered.Count;
                        }
                        else
                        {
                            return Json(new { ok = false, message = $"Unknown sheet configuration: '{sheetName}'." });
                        }
                        break;

                    case "txt":
                        if (fileCodeOrName.Contains("MS05B0A"))
                        {
                            var items = await overdueInstallmentService.SelectAllAsync();
                            var filtered = items.Where(x => x.DwEtlFileId == dwEtlFileId.Value && x.ReportingDate == parsedDate).ToList();
                            data = filtered;
                            count = filtered.Count;
                        }
                        else if (fileCodeOrName.Contains("FI44B0A"))
                        {
                            var items = await expiredOverdraftService.SelectAllAsync();
                            var filtered = items.Where(x => x.DwEtlFileId == dwEtlFileId.Value && x.ReportingDate == parsedDate).ToList();
                            data = filtered;
                            count = filtered.Count;
                        }
                        else
                        {
                            var items = await glStatementService.SelectAllAsync();
                            var filtered = items.Where(x => x.DwEtlFileId == dwEtlFileId.Value && x.ReportingDate == parsedDate).ToList();
                            data = filtered;
                            count = filtered.Count;
                        }
                        break;

                    default:
                        return Json(new { ok = false, message = $"Unsupported file type: '{fileType}'." });
                }

                if (count == 0)
                    return Json(new { ok = true, message = "No data found.", data = Array.Empty<object>(), totalCount = 0 });

                return Json(new { ok = true, message = $"{count:N0} record(s) loaded.", data, totalCount = count });
            }
            catch (Exception)
            {
                return Json(new { ok = false, message = "An unexpected error occurred." });
            }
        }


    }
}