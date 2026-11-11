using Microsoft.AspNetCore.Mvc;
using Syncfusion.EJ2.Spreadsheet;

namespace aseeMIS.Areas.Report.Controllers
{
    [Area("Report")]
    public class ReportSpreadsheetController(IWebHostEnvironment env) : Controller
    {
        // Full view (NOT partial yet)
        public IActionResult Index()
        {
            // 🔒 Hardcoded for now
            ViewBag.TemplatePath = "files/template.xlsx";
            return View("~/Areas/Report/Views/_ReportSpreadsheet.cshtml");
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult Open(OpenRequest openRequest)
        {
            var path = Path.Combine(
                env.WebRootPath,
                "files",
                "template.xlsx"
            );

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);

            openRequest.File = new FormFile(
                fs,
                0,
                fs.Length,
                "file",
                "Template.xlsx"
            );

            return Content(Workbook.Open(openRequest));
        }

        [HttpGet]
        public IActionResult TemplateFile()
        {
            var path = Path.Combine(env.WebRootPath, "files", "Book1.xlsx");
            return PhysicalFile(
                path,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Template.xlsx"
            );
        }

    }
}
