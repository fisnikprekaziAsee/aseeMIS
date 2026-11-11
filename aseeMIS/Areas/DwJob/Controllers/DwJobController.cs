using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using dsCredinsEtl.Core.Services;
using dsCredinsEtl.Core.Services.Interfaces;
using dsDwJob.Core.Services;
using dsDwJob.Core.Services.Interfaces;
using dsDwJob.Core.ViewModels;
using aseeMIS.Areas.DwJob.ViewModels;
using aseeMIS.Services.Interfaces;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace aseeMIS.Areas.DwJob.Controllers
{
    [Area("DwJob")]
    [Route("DwJob/DwJob/[action]/{formId?}")]
    public class DwJobController(
        IDwJobService dwReportService,
        IDwJobLogService dwjobLogService,
        IDwEtlJobValidationLogService validationLogService,
        IDwEtlFileImportLogService dwEtlFileImportService,
        IDwJobExecutionService dwReportExecutionService,
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
            var pageVm = new DwJobPageViewModel
            {
                Jobs = await dwReportService.SelectAllAsync(),
            };

            return View("~/Areas/DwJob/Views/DwJob.cshtml", pageVm);
        }

        [HttpGet]
        public async Task<IActionResult> GetValidationJobLogs(int formId, DateOnly reportingDate)
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
            var allValidationLogs = await validationLogService.SelectAllAsync();
            var passed = allValidationLogs.Any(l => l.ReportingDate == reportingDate && l.HasPassed);

            var allJobs = await dwReportService.SelectAllAsync();
            var allJobLogs = await dwjobLogService.SelectAllAsync();

            var logsForDate = allJobLogs
                .Where(l => l.ReportingDate == reportingDate)
                .OrderByDescending(l => l.ExecutionDate)
                .ToList();

            foreach (var log in logsForDate)
            {
                var job = allJobs.FirstOrDefault(j => j.DwJobId == log.DwJobId);
                log.DwJobName = job?.DwJobName;
            }

            var executedJobIds = logsForDate
                .Where(l => l.IsExecuted)
                .Select(l => l.DwJobId)
                .Distinct()
                .ToHashSet();

            var rerunMap = allJobs.ToDictionary(
                j => j.DwJobId.ToString(),
                j => executedJobIds.Contains(j.DwJobId)
            );

            return Json(new { passed, logs = logsForDate, rerunMap });
        }

        [HttpGet]
        public async Task<IActionResult> GetValidationStatus(int formId, DateOnly reportingDate)
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
            var passed = allLogs.Any(l => l.ReportingDate == reportingDate && l.HasPassed);
            return Json(new { passed });
        }

        [HttpGet]
        public async Task<IActionResult> GetJobLogs(int formId, DateOnly reportingDate)
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
            var allLogs = await dwjobLogService.SelectAllAsync();

            var filtered = allLogs
                .Where(l => l.ReportingDate == reportingDate)
                .OrderByDescending(l => l.ExecutionDate)
                .ToList();

            return Json(filtered);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunReport(int formId, int reportId, DateOnly reportingDate)
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
            var report = await dwReportService.GetByIdAsync(reportId);
            if (report == null)
                return Json(new { ok = false, message = "Report not found." });

            try
            {
                var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";

                await dwReportExecutionService.ExecuteReportAsync(
                    report.StoredProcedureName,
                    reportId,
                    reportingDate,
                    userId
                );

              //  await dwjobLogService.LogAsync(reportId, reportingDate, true, null);

                return Json(new { ok = true, message = "Report executed successfully." });
            }
            catch (Exception ex)
            {
                await dwjobLogService.LogAsync(reportId, reportingDate, false, ex.Message);

                return Json(new { ok = false, message = ex.Message });
            }
        }
    }
}