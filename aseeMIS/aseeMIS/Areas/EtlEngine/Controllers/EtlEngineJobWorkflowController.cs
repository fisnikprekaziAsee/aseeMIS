using System.Security.Claims;
using System.Text;
using aseeMIS.Areas.Common.Utils;
using aseeMIS.Utils;
using dsEtlEngine.Services.Interfaces;
using dsEtlEngine.ViewModels;
using dsMIS.Core.DTOs;
using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Syncfusion.EJ2.Base;

namespace aseeMIS.Areas.EtlEngine.Controllers;

[Area("EtlEngine")]
[Route("EtlEngine/EtlEngineJobWorkflow/[action]/{formId?}")]
public class EtlEngineJobWorkflowController(
    IEtlEngineJobWorkflowService workflowService,
    IEtlEngineJobExecutionService executionService,
    IAppFormMenuService menuService,
    IAppFormPermissionService formAccessService,
    IUserContextService userSecurityStampCacheService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int formId, DateOnly? reportingDate)
    {
        var (isValid, _, contextSessionDto, _, _, _) =
            await AppAuthorizeHelper.ValidateReadPermissionAsync(
                HttpContext,
                formAccessService,
                menuService,
                userSecurityStampCacheService,
                formId,
                EnumContextFormType.PARENT);

        if (!isValid || contextSessionDto == null)
            return RedirectToAction("NoAccess", "Error");

        await SetViewContextAsync(formId, contextSessionDto.FormSessionId);

        var page = await CreateShellPageAsync(formId);
        if (reportingDate.HasValue)
        {
            page.ReportingDate = reportingDate.Value;
        }

        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadWorkflow(int formId, int etlEngineJobId, DateOnly reportingDate)
    {
        var formSessionId = ResolveFormSessionId();

        var auth = await ValidateWorkflowSessionAsync(formSessionId, EumFormAccessAction.READ);
        if (!auth.IsValid || auth.Context == null)
        {
            return Ok(new { ok = false, message = auth.ErrorMessage ?? "Session expired. Refresh the page (F5)." });
        }

        Response.Headers["FormSessionId"] = auth.Context.FormSessionId;

        if (etlEngineJobId <= 0)
        {
            return Ok(new { ok = false, message = "Select a job." });
        }

        var page = await workflowService.BuildWorkflowPageAsync(etlEngineJobId, reportingDate);
        if (page == null)
        {
            return Ok(new { ok = false, message = "Job was not found." });
        }

        var shell = await CreateShellPageAsync(formId);
        page.FormId = formId;
        page.ImportFormId = shell.ImportFormId;
        page.ImportFormHref = shell.ImportFormHref;

        return Ok(new { ok = true, data = page });
    }

    [HttpPost]
    public async Task<IActionResult> ReferenceIssues(
        [FromBody] DataManagerRequest dm,
        int etlEngineJobId,
        DateOnly reportingDate,
        [FromQuery] string? importLogIds)
    {
        var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

        var (isValid, _, contextSessionDto, _, _, _) =
            await AppAuthorizeHelper.ValidateActionAsync(
                HttpContext,
                formAccessService,
                menuService,
                userSecurityStampCacheService,
                formSessionId,
                requiredPermission: EumFormAccessAction.READ);

        if (!isValid || contextSessionDto == null)
            return Unauthorized();

        Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

        var logIds = ParseImportLogIds(importLogIds);
        var (rows, openCount) = await workflowService.GetReferenceIssuesAsync(
            etlEngineJobId,
            reportingDate,
            logIds);

        return Ok(new { result = rows, count = rows.Count, openCount });
    }

    [HttpPost]
    public async Task<IActionResult> ExecutionHistory([FromBody] DataManagerRequest dm, long processRunId)
    {
        var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

        var (isValid, _, contextSessionDto, _, _, _) =
            await AppAuthorizeHelper.ValidateActionAsync(
                HttpContext,
                formAccessService,
                menuService,
                userSecurityStampCacheService,
                formSessionId,
                requiredPermission: EumFormAccessAction.READ);

        if (!isValid || contextSessionDto == null)
            return Unauthorized();

        Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

        var rows = await workflowService.GetExecutionHistoryAsync(processRunId);
        return Ok(new { result = rows, count = rows.Count });
    }

    [HttpPost]
    public async Task<IActionResult> StepDetails([FromQuery] long processRunStepId, [FromQuery] string? severity)
    {
        var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

        var (isValid, _, contextSessionDto, _, _, _) =
            await AppAuthorizeHelper.ValidateActionAsync(
                HttpContext,
                formAccessService,
                menuService,
                userSecurityStampCacheService,
                formSessionId,
                requiredPermission: EumFormAccessAction.READ);

        if (!isValid || contextSessionDto == null)
            return Unauthorized();

        Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

        if (processRunStepId <= 0)
        {
            return Ok(new { result = Array.Empty<object>(), count = 0 });
        }

        var dm = await ReadDataManagerRequestAsync(cancellationToken: default);
        var page = dm.ToPageRequest(
            typeof(EtlEngineProcessRunStepDetailGridViewModel),
            defaultOrderBy: "EtlEngineProcessRunStepDetailId");

        if (dm.Select is { Count: > 0 })
        {
            var field = dm.Select[^1];
            var choiceTake = dm.Take > 0 ? dm.Take : 10_000;
            var (choices, choiceCount) = await workflowService.GetStepDetailFilterChoicesAsync(
                processRunStepId,
                page,
                field,
                choiceTake,
                severity);
            return Ok(new { result = choices, count = choiceCount });
        }

        var (rows, total) = await workflowService.GetStepDetailsPageAsync(
            processRunStepId,
            page,
            severity);
        return Ok(new { result = rows, count = total });
    }

    private async Task<DataManagerRequest> ReadDataManagerRequestAsync(CancellationToken cancellationToken)
    {
        HttpContext.Request.EnableBuffering();
        HttpContext.Request.Body.Position = 0;

        using var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
        var json = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new DataManagerRequest();
        }

        return JsonConvert.DeserializeObject<DataManagerRequest>(json) ?? new DataManagerRequest();
    }

    [HttpPost]
    public async Task<IActionResult> StepSummary(long processRunStepId)
    {
        var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

        var (isValid, _, contextSessionDto, _, _, _) =
            await AppAuthorizeHelper.ValidateActionAsync(
                HttpContext,
                formAccessService,
                menuService,
                userSecurityStampCacheService,
                formSessionId,
                requiredPermission: EumFormAccessAction.READ);

        if (!isValid || contextSessionDto == null)
            return Unauthorized();

        Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

        var summary = await workflowService.GetProcessRunStepSummaryAsync(processRunStepId);
        return Ok(new { ok = summary != null, data = summary });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunStep(int etlEngineJobStepId, DateOnly reportingDate)
    {
        var formSessionId = ResolveFormSessionId();

        var auth = await ValidateWorkflowSessionAsync(formSessionId, EumFormAccessAction.UPDATE);
        if (!auth.IsValid || auth.Context == null)
        {
            return Ok(new
            {
                ok = false,
                message = auth.ErrorMessage
                    ?? "Session expired or access denied. Press F5, click Search, then Run. You need Update permission on this form."
            });
        }

        Response.Headers["FormSessionId"] = auth.Context.FormSessionId;

        var user = User?.FindFirst(ClaimTypes.Email)?.Value ?? User?.Identity?.Name ?? "system";
        var result = await executionService.ExecuteStepAsync(etlEngineJobStepId, reportingDate, user);

        return Ok(new
        {
            ok = result.Ok,
            message = result.Message,
            processRunStepId = result.EtlEngineProcessRunStepId,
            validationCompletedWithIssues = result.ValidationCompletedWithIssues,
            proceedWithValidationWarnings = result.ProceedWithValidationWarnings
        });
    }

    private string? ResolveFormSessionId() =>
        HttpContext.Request.Headers["FormSessionId"].FirstOrDefault()
        ?? Request.Form["formSessionId"].FirstOrDefault();

    private async Task<(bool IsValid, ContextSessionDto? Context, string? ErrorMessage)> ValidateWorkflowSessionAsync(
        string? formSessionId,
        EumFormAccessAction requiredPermission)
    {
        if (string.IsNullOrWhiteSpace(formSessionId))
        {
            return (false, null, "Session expired. Refresh the page (F5) and click Search again.");
        }

        var (isValid, _, contextSessionDto, _, _, _) =
            await AppAuthorizeHelper.ValidateActionAsync(
                HttpContext,
                formAccessService,
                menuService,
                userSecurityStampCacheService,
                formSessionId,
                requiredPermission);

        if (isValid && contextSessionDto != null)
        {
            return (true, contextSessionDto, null);
        }

        if (requiredPermission == EumFormAccessAction.UPDATE)
        {
            (isValid, _, contextSessionDto, _, _, _) =
                await AppAuthorizeHelper.ValidateActionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userSecurityStampCacheService,
                    formSessionId,
                    EumFormAccessAction.MODIFY);

            if (isValid && contextSessionDto != null)
            {
                return (true, contextSessionDto, null);
            }
        }

        return (false, null,
            "Session expired or you lack Update/Modify permission on ETL Job Workflow (9902). Refresh (F5), Search again, then Run.");
    }

    private static IReadOnlyList<long> ParseImportLogIds(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Array.Empty<long>();
        }

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, out var id) ? id : (long?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
    }

    private Task SetViewContextAsync(int formId, string formSessionId)
    {
        ViewBag.FormId = formId;
        ViewBag.FormSessionId = formSessionId;
        Response.Headers["FormSessionId"] = formSessionId;
        return Task.CompletedTask;
    }

    private async Task<EtlEngineJobWorkflowPageViewModel> CreateShellPageAsync(int formId)
    {
        var jobs = await workflowService.GetActiveApprovedJobsAsync();
        var (importFormId, importHref) = await EtlEngineFormNavigation.ResolveImportFormAsync(
            menuService,
            formId,
            id => Url.Action("Index", "EtlEngine", new { area = "EtlEngine", formId = id }) ?? string.Empty);

        return new EtlEngineJobWorkflowPageViewModel
        {
            FormId = formId,
            ImportFormId = importFormId,
            ImportFormHref = importHref,
            Jobs = jobs
        };
    }
}
