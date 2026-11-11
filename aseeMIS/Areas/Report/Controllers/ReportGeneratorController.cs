using dsReport.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.Report.Controllers
{
    [Area("Report")]
    public class ReportGeneratorController(IReportService reportService) : Controller
    {
        public IActionResult Index(int reportId)
        {
            ViewBag.ReportId = reportId;
            return View("~/Areas/Report/Views/ReportGenerator.cshtml");
        }

        [HttpPost]
        public async Task<IActionResult> Generate(int reportId)
        {
            reportId = 1;
            var result = await reportService.GenerateExcelReportAsync(
                reportId,
                languageCode: "en-US",
                parameters: new Dictionary<string, object>() // no params for now
            );

            return File(
                result.FileContent,
                result.ContentType,
                result.FileName
            );
        }
    }
}
