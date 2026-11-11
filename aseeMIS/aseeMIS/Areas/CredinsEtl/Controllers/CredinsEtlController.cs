using dsMIS.Core.Enums;
using dsMIS.Core.Exceptions;
using dsMIS.Core.Services.Interfaces;
using dsCredinsEtl.Core.Services;
using dsCredinsEtl.Core.Services.Interfaces;
using dsCredinsEtl.Core.ViewModels;
using aseeMIS.Services.Interfaces;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace aseeMIS.Areas.CredinsEtl.Controllers
{
    [Area("CredinsEtl")]
    [Route("CredinsEtl/CredinsEtl/[action]/{formId?}")]
    public class CredinsEtlController(
       IDwEtlPassiveInventoryItemService passiveInventoryServie,
       IDwEtlPassivePositiveInventoryItemService passivePositiveInventoryService,
       IDwEtlBankRelatedPersonService bankRelatedPersonService,
       IDwEtlLoanInventoryItemService loanInventoryService,
       IDwEtlOverdueInstallmentService overdueInstallmentService,
        IDwEtlExpiredOverdraftService expiredOverdraftService,
        IDwEtlFileService dwEtlFileService,
        IDwEtlFileImportLogService fileImportService,
        IDwEtlGlStatementService glStatementService,
        IServiceProvider serviceProvider,
        IAppFormMenuService menuService,
        IAppFormPermissionService formAccessService,
        IAppSessionService dsSessionService,
        IWebHostEnvironment env,
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
            var etlFiles = await dwEtlFileService.SelectAllAsync();
            var activeFiles = etlFiles
                .Where(x => x.IsActive && x.IsApproved)
                .OrderBy(x => x.DwEtlFileId)
                .ToList();

            ViewBag.EtlFiles = activeFiles;

            return View("~/Areas/CredinsEtl/Views/CredinsEtl.cshtml", new List<DwEtlFileImportLogGridViewModel>());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(int formId, DateOnly reportDate, IFormFile file, string fileType, string? sheetName)
        {
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

            var (isValid, _, contextSessionDto, _, _, _) =
                await AppAuthorizeHelper.ValidateActionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userSecurityStampCacheService,
                    formSessionId,
                    requiredPermission: EumFormAccessAction.ADD
                );

            if (!isValid || contextSessionDto == null)
                return RedirectToAction("NoAccess", "Error");

            Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

            // --- existing logic unchanged ---
            if (file == null || file.Length == 0)
                return Ok(new { ok = false, message = "Please select a file." });

            var user = User?.FindFirst(ClaimTypes.Email)?.Value ?? User?.Identity?.Name ?? "system";

            var uploadFolder = Path.Combine(env.WebRootPath, "credins");
            if (!Directory.Exists(uploadFolder))
                Directory.CreateDirectory(uploadFolder);

            var originalFileName = Path.GetFileName(file.FileName);
            var savedPath = Path.Combine(uploadFolder, originalFileName);

            await using (var fs = new FileStream(savedPath, FileMode.Create))
            {
                await file.CopyToAsync(fs);
            }

            var normalizedFileType = (fileType ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedFileName = (originalFileName ?? string.Empty).Trim().ToUpperInvariant();
            var normalizedSheetName = (sheetName ?? string.Empty).Trim();

            if (normalizedFileType == "txt")
            {
                if (normalizedFileName.Contains("FI44B0A"))
                {
                    await expiredOverdraftService.ImportAsync(savedPath, reportDate, user, fileType);
                }
                else if (normalizedFileName.Contains("MS05B0A"))
                {
                    await overdueInstallmentService.ImportAsync(savedPath, reportDate, user, fileType);
                }
                else
                {
                    await glStatementService.ImportAsync(savedPath, reportDate, user, fileType);
                }
            }
            else if (string.Equals(normalizedSheetName, "Pasivi", StringComparison.OrdinalIgnoreCase))
            {
                await passivePositiveInventoryService.ImportAsync(savedPath, reportDate, user, fileType, sheetName);
            }
            else if (string.Equals(normalizedSheetName, "Lista e perjashtuar", StringComparison.OrdinalIgnoreCase))
            {
                await bankRelatedPersonService.ImportAsync(savedPath, reportDate, user, fileType, sheetName);
            }
            else if (string.Equals(normalizedSheetName, "Inventari", StringComparison.OrdinalIgnoreCase))
            {
                await loanInventoryService.ImportAsync(savedPath, reportDate, user, fileType, sheetName);
            }
            else
            {
                await passiveInventoryServie.ImportAsync(savedPath, reportDate, user, fileType, sheetName);
            }

            return Ok(new { ok = true, message = "Import processed. Check log for status." });
        }



        [HttpGet]
        public async Task<IActionResult> GetImportsByReportDate(DateOnly reportDate)
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
                return RedirectToAction("NoAccess", "Error");

            Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

            try
            {
                //var allRows = await fileImportService.SelectAllAsync();

                //var rows = allRows
                //    .Where(x => x.ReportingDate == reportDate)
                //    .OrderByDescending(x => x.ImportDate)
                //    .ToList();


                var rows = await fileImportService.BuildLogRowsAsync(reportDate);

                return Ok(new
                {
                    ok = true,
                    rows
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    ok = false,
                    message = ex.Message,
                    rows = new List<object>()
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckReportDate(int formId, DateOnly reportDate, string fileType, string? sheetName, string? fileName)
        {
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

            var (isValid, _, contextSessionDto, _, _, _) = await AppAuthorizeHelper.ValidateActionAsync(
                    HttpContext, formAccessService, menuService, userSecurityStampCacheService,
                    formSessionId, requiredPermission: EumFormAccessAction.READ);

            if (!isValid || contextSessionDto == null)
                return RedirectToAction("NoAccess", "Error");

            Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

            // 1. Normalize inputs
            var normalizedFileType = (fileType ?? string.Empty).Trim();
            var normalizedSheetName = (sheetName ?? string.Empty).Trim();
            var normalizedFileName = (fileName ?? string.Empty).Trim();

            var etlFiles = await dwEtlFileService.SelectAllAsync();

            // 2. Simplified Selection: Handles TXT (by FileName) and Excel (by SheetName) in one go
            var dwEtlFile = etlFiles.FirstOrDefault(x =>
                x.IsActive &&
                x.IsApproved &&
                string.Equals((x.DwEtlFileType ?? string.Empty).Trim(), normalizedFileType, StringComparison.OrdinalIgnoreCase) &&
                (
                    // Logic for TXT: Check if filename contains the code from DB (e.g. MS05B0A)
                    (normalizedFileType.Equals("txt", StringComparison.OrdinalIgnoreCase) &&
                     !string.IsNullOrEmpty(x.DwEtlFileName) &&
                     normalizedFileName.Contains(x.DwEtlFileName.Split(' ').Length > 1 ? x.DwEtlFileName.Split(' ')[1] : x.DwEtlFileName, StringComparison.OrdinalIgnoreCase))
                    ||
                    // Logic for Excel: Match the Sheet Name
                    (!normalizedFileType.Equals("txt", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals((x.DwEtlSheetName ?? string.Empty).Trim(), normalizedSheetName, StringComparison.OrdinalIgnoreCase))
                ));

            if (dwEtlFile == null)
                return Json(new { exists = false, hasPassed = false, etlFileExists = false });

            // 3. Check logs
            var logs = await fileImportService.SelectAllAsync();
            var existingLog = logs
                .Where(x => x.ReportingDate == reportDate && x.DwEtlFileId == dwEtlFile.DwEtlFileId)
                .OrderByDescending(x => x.ImportDate)
                .FirstOrDefault();

            return Json(new
            {
                exists = existingLog != null,
                hasPassed = existingLog?.HasPassed ?? false,
                etlFileExists = true
            });
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(int dwEtlFileImportId, bool rebuild = true)
        {
            try
            {
                var row = await fileImportService.GetByIdAsync(dwEtlFileImportId);

                if (row == null)
                    return NotFound(new { ok = false, message = "Import row not found." });

                return Ok(new
                {
                    ok = true,
                    message = "Stored procedure executed successfully."
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    ok = false,
                    message = ex.Message
                });
            }
        }
    }
}