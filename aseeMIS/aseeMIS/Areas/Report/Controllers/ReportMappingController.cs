using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Utils;
using dsReport.Core.Services.Interfaces;
using dsReport.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.Report.Controllers
{
    [Area("Report")]
    public class ReportMappingController(IReportExcelMappingService mappingService, IReportExcelSheetService sheetService, IReportExcelDataSourceService dataSourceService, IAppFormMenuService menuService, IUserContextService userContextService,
        IAppFormPermissionService formAccessService, IWebHostEnvironment env) : Controller
    {
        public async Task<IActionResult> Index(int? reportId)
        {
            var formId = 1915;
            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) =
                await AppAuthorizeHelper.ValidateReadPermissionAsync(HttpContext, formAccessService, menuService, userContextService, formId, EnumContextFormType.PARENT);

            if (!isValid || appForm == null || formContext == null)
                return RedirectToAction("NoAccess", "Error");

            var model = new ReportExcelMappingViewModel();

            ViewBag.FormSessionId = formContext.FormSessionId;
            ViewBag.ExcelSheets = await sheetService.GetDropdownByForeignKeyAsync("ReportId", reportId.ToString());
            ViewBag.DataSources = await dataSourceService.GetDropdownByForeignKeyAsync("ReportId", reportId.ToString());

            return PartialView("~/Areas/Report/Views/ReportMapping.cshtml", model);
        }

        [HttpGet]
        public async Task<IActionResult> GetMappingsBySheet(int reportSheetId)
        {
            var data = await mappingService
                .SelectAllDetailAsync("ReportSheetId", reportSheetId.ToString());

            return Json(data);
        }
    }
}