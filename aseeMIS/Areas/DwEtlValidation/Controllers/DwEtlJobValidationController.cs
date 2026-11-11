using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using dsCredinsEtl.Core.Services;
using dsCredinsEtl.Core.Services.Interfaces;
using dsDwJob.Core.Services;
using dsDwJob.Core.Services.Interfaces;
using dsDwReport.Core.Services;
using dsDwReport.Core.Services.Interfaces;
using aseeMIS.Areas.DwEtlValidation.ViewModels;
using aseeMIS.Services.Interfaces;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace aseeMIS.Areas.DwEtlValidation.Controllers
{
    [Area("DwEtlValidation")]
    [Route("DwEtlValidation/DwEtlJobValidation/[action]/{formId?}")]
    public class DwEtlJobValidationController(
        IDwEtlJobValidationTypeService validationService,
        IDwEtlFileImportLogService fileImportLogService,
        IDwEtlJobValidationExecution dwEtlJobValidationExecution,
        IDwEtlJobValidationLogService validationLogService,
        IDwEtlJobValidationLogDetailService validationLogDetailService,
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
            var pageVm = new DwEtlValidationPageViewModel
            {
                Validations = await validationService.SelectAllAsync(),
            };

            return View("~/Areas/DwEtlValidation/Views/DwEtlJobValidation.cshtml", pageVm);
        }

        [HttpGet]
        public async Task<IActionResult> GetValidationLogDetails(int formId, DateOnly reportingDate)
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
            var allLogs = await validationLogService.SelectAllAsync();
            var logs = allLogs
                .Where(l => l.ReportingDate == reportingDate)
                .OrderByDescending(l => l.ExecutionDate)
                .ToList();

            var logIds = logs.Select(l => l.DwEtlJobValidationLogId).ToList();

            var allDetails = await validationLogDetailService.SelectAllAsync();
            var details = allDetails
                .Where(d => logIds.Contains(d.DwEtlJobValidationLogId))
                .ToList();

            var revalidateMap = logs
                .GroupBy(l => l.DwEtlJobValidationTypeId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Any(x => x.HasPassed)
                );

            return Json(new { logs, details, revalidateMap });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunValidation(int formId, int validationId, DateOnly reportingDate)
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

            var validation = await validationService.GetByIdAsync(validationId);
            if (validation == null)
                return Json(new { ok = false, message = "Validation not found." });

            var email = User?.FindFirst(ClaimTypes.Email)?.Value ?? User?.Identity?.Name ?? "system";

            try
            {
                await dwEtlJobValidationExecution.ExecuteAsync(
                    validation.DwEtlValidationStoredProcedureName,
                    reportingDate,
                    email);

                return Json(new { ok = true, message = "Validation executed successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = ex.Message });
            }
        }
    }
}