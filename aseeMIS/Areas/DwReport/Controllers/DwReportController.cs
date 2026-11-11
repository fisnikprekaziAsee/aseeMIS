using dsCredinsEtl.Core.Services;
using dsCredinsEtl.Core.Services.Interfaces;
using dsDwReport.Core.Services;
using dsDwReport.Core.Services.Interfaces;
using dsDwReport.Core.ViewModels;
using aseeMIS.Areas.DwReport.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace aseeMIS.Areas.DwReport.Controllers
{

    [Area("DwReport")]
    public class DwReportController(IDwReportTypeService dwReportTypeService, IDwReportService dwReportService, IDwEtlFileImportLogService dwEtlFileImportService, IDwReportExecutionService dwReportExecutionService) : Controller
    {


        public async Task<IActionResult> Index()
        {
            var pageVm = new DwReportPageViewModel
            {
                ReportTypes = await dwReportTypeService.SelectAllAsync(),
                Reports = await dwReportService.SelectAllAsync()
            };

            return View("~/Areas/DwReport/Views/DwReport.cshtml", pageVm);
        }


        [HttpGet]
        public async Task<IActionResult> CheckImportsByDate(DateOnly reportingDate)
        {
            var imports = await dwEtlFileImportService.SelectAllAsync();

            var validTypes = imports
                .Where(x => x.ReportingDate == reportingDate
                            && !string.IsNullOrWhiteSpace(x.DwEtlFileName))
                .Select(x => x.DwEtlFileName.Trim().ToUpper())
                .Distinct()
                .ToList();

            return Json(validTypes);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunReport(int reportId, DateOnly reportingDate)
        {
            var report = await dwReportService.GetByIdAsync(reportId);

            if (report == null)
            {
                return Json(new
                {
                    ok = false,
                    message = "Report not found."
                });
            }

            try
            {
                var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? "system";

                await dwReportExecutionService.ExecuteReportAsync(
                    report.StoredProcedureName,
                    reportId,
                    reportingDate,
                    userId
                );

                return Json(new
                {
                    ok = true,
                    message = "Report executed successfully."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    message = ex.Message
                });
            }
        }
    }
}

