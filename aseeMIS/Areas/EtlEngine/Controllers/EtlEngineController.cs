using dsEtlEngine.Services.Interfaces;
using dsEtlEngine.ViewModels;
using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace aseeMIS.Areas.EtlEngine.Controllers;

[Area("EtlEngine")]
[Route("EtlEngine/EtlEngine/[action]/{formId?}")]
public class EtlEngineController(
    IEtlEngineFileService etlEngineFileService,
    IEtlEngineFileImportLogService fileImportLogService,
    IEtlEngineExcelImportService excelImportService,
    IAppFormMenuService menuService,
    IAppFormPermissionService formAccessService,
    IUserContextService userSecurityStampCacheService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int formId)
    {
        var (isValid, appForm, contextSessionDto, _, _, _) =
            await AppAuthorizeHelper.ValidateReadPermissionAsync(
                HttpContext,
                formAccessService,
                menuService,
                userSecurityStampCacheService,
                formId,
                EnumContextFormType.PARENT);

        if (!isValid || contextSessionDto == null)
            return RedirectToAction("NoAccess", "Error");

        ViewBag.FormId = formId;
        ViewBag.FormSessionId = contextSessionDto.FormSessionId;
        ViewBag.FormConfigId = contextSessionDto.FormId;
        Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

        var (workflowFormId, workflowHref) = await EtlEngineFormNavigation.ResolveJobWorkflowFormAsync(
            menuService,
            appForm,
            id => Url.Action("Index", "EtlEngineJobWorkflow", new { area = "EtlEngine", formId = id }) ?? string.Empty);

        ViewBag.JobWorkflowFormId = workflowFormId;
        ViewBag.JobWorkflowBaseHref = workflowHref;
        ViewBag.ImportFormHref = await EtlEngineFormNavigation.ResolveImportFormHrefAsync(
            menuService,
            formId,
            id => Url.Action("Index", "EtlEngine", new { area = "EtlEngine", formId = id }) ?? string.Empty);

        return View(new List<EtlEngineFileImportLogGridViewModel>());
    }

    [HttpGet]
    public async Task<IActionResult> SearchWorkspace(DateOnly reportDate)
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
            return RedirectToAction("NoAccess", "Error");

        Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

        try
        {
            var workspace = await fileImportLogService.BuildImportWorkspaceAsync(reportDate);
            return Ok(new { ok = true, workspace, rows = workspace.ImportLogs });
        }
        catch (Exception ex)
        {
            return Ok(new { ok = false, message = ex.Message });
        }
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
                requiredPermission: EumFormAccessAction.READ);

        if (!isValid || contextSessionDto == null)
            return RedirectToAction("NoAccess", "Error");

        Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

        try
        {
            var rows = await fileImportLogService.BuildImportLogGridAsync(reportDate);

            return Ok(new { ok = true, rows });
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(
        int formId,
        int etlEngineFileId,
        DateOnly reportDate,
        IFormFile file)
    {
        var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

        var (isValid, _, contextSessionDto, _, _, _) =
            await AppAuthorizeHelper.ValidateActionAsync(
                HttpContext,
                formAccessService,
                menuService,
                userSecurityStampCacheService,
                formSessionId,
                requiredPermission: EumFormAccessAction.ADD);

        if (!isValid || contextSessionDto == null)
        {
            if (Request.Headers.ContainsKey("X-Requested-With"))
                return Ok(new { ok = false, message = "Session expired or access denied. Refresh the page, then Search again." });

            return RedirectToAction("NoAccess", "Error");
        }

        Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

        if (file == null || file.Length == 0)
            return Ok(new { ok = false, message = "Please select a file." });

        var user = User?.FindFirst(ClaimTypes.Email)?.Value ?? User?.Identity?.Name ?? "system";

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await excelImportService.ImportAsync(
                etlEngineFileId,
                reportDate,
                stream,
                file.FileName,
                user);

            return Ok(new { ok = result.Ok, message = result.Message, importLogId = result.EtlEngineFileImportLogId });
        }
        catch (Exception ex)
        {
            return Ok(new { ok = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reimport(
        int formId,
        int etlEngineFileId,
        DateOnly reportDate,
        IFormFile file)
    {
        var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

        var (isValid, _, contextSessionDto, _, _, _) =
            await AppAuthorizeHelper.ValidateActionAsync(
                HttpContext,
                formAccessService,
                menuService,
                userSecurityStampCacheService,
                formSessionId,
                requiredPermission: EumFormAccessAction.ADD);

        if (!isValid || contextSessionDto == null)
        {
            if (Request.Headers.ContainsKey("X-Requested-With"))
                return Ok(new { ok = false, message = "Session expired or access denied. Refresh the page, then Search again." });

            return RedirectToAction("NoAccess", "Error");
        }

        Response.Headers["FormSessionId"] = contextSessionDto.FormSessionId;

        if (file == null || file.Length == 0)
            return Ok(new { ok = false, message = "Please select a file." });

        var user = User?.FindFirst(ClaimTypes.Email)?.Value ?? User?.Identity?.Name ?? "system";

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await excelImportService.ReimportAsync(
                etlEngineFileId,
                reportDate,
                stream,
                file.FileName,
                user);

            return Ok(new
            {
                ok = result.Ok,
                message = result.Message,
                importLogId = result.EtlEngineFileImportLogId,
                records = result.RecordsImported
            });
        }
        catch (Exception ex)
        {
            return Ok(new { ok = false, message = ex.Message });
        }
    }
}
