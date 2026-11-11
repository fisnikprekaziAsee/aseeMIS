using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Utils;
using dsReport.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Syncfusion.EJ2.Spreadsheet;
using Syncfusion.XlsIO;

namespace aseeMIS.Areas.Report.Controllers
{
    [Area("Report")]
    public class ReportJsonController(IReportService reportService, IAppFormMenuService menuService, IUserContextService userContextService, IAppFormPermissionService formAccessService, IWebHostEnvironment env) : Controller
    {
        // ⚠️ In-memory storage for testing
        private static object? _templateJson;

        public IActionResult Index()
        {
            return View("~/Areas/Report/Views/ReportJson.cshtml");
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

        // 🔹 SAVE TEMPLATE JSON INTO Report.TemplateJSON (ReportId = 1)
        [HttpPost]
        public async Task<IActionResult> SaveJson([FromBody] object spreadsheetJson)
        {
            var id = 102;

            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) =
                await AppAuthorizeHelper.ValidateReadPermissionAsync(HttpContext, formAccessService, menuService, userContextService, id, EnumContextFormType.PARENT);

            if (!isValid || appForm == null || formContext == null)
                return RedirectToAction("NoAccess", "Error");

            var report = await reportService.GetByIdAsync(1);
            if (report == null)
                return NotFound("Report not found");

            // 🔑 Store RAW JSON STRING
            report.TemplateJSON = spreadsheetJson.ToString();

            await reportService.UpdateAsync(report, formContext);

            return Ok(new { message = "Template JSON saved to report (ID = 1)" });
        }

        // 🔹 LOAD TEMPLATE JSON FROM Report.TemplateJSON (ReportId = 1)
        [HttpGet]
        public async Task<IActionResult> Load()
        {
            var report = await reportService.GetByIdAsync(1);
            if (report == null || string.IsNullOrWhiteSpace(report.TemplateJSON))
                return NotFound("No template JSON found for report");

            // 🔑 IMPORTANT: return raw JSON, not Json()
            return Content(report.TemplateJSON, "application/json");
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult DownloadExcel([FromBody] JObject spreadsheetJson)
        {
            using ExcelEngine excelEngine = new ExcelEngine();
            IApplication application = excelEngine.Excel;

            IWorkbook workbook = application.Workbooks.Create(1);

            var sheets = spreadsheetJson["jsonObject"]?["sheets"] as JArray;
            if (sheets == null || sheets.Count == 0)
                return BadRequest("Invalid spreadsheet JSON");

            for (int i = 0; i < sheets.Count; i++)
            {
                JObject sheetJson = (JObject)sheets[i];

                // 🔹 Use existing sheet for first item
                IWorksheet sheet = i == 0
                    ? workbook.Worksheets[0]
                    : workbook.Worksheets.Create();

                sheet.Name = sheetJson["name"]?.ToString() ?? $"Sheet{i + 1}";

                var rows = sheetJson["rows"] as JArray;
                if (rows == null) continue;

                int rowCount = rows.Count;
                int colCount = rows
                    .Where(r => r?["cells"] != null)
                    .Select(r => ((JArray)r["cells"]).Count)
                    .DefaultIfEmpty(0)
                    .Max();

                if (rowCount == 0 || colCount == 0)
                    continue;

                // 🔹 Bulk array (FAST)
                object[,] data = new object[rowCount, colCount];

                for (int r = 0; r < rowCount; r++)
                {
                    var cells = rows[r]?["cells"] as JArray;
                    if (cells == null) continue;

                    for (int c = 0; c < cells.Count; c++)
                    {
                        data[r, c] = cells[c]?["value"]?.ToString();
                    }
                }

                sheet.ImportArray(data, 1, 1);

                // 🔹 Example: overwrite header row
                string[] headers = { "Col1", "Col2", "Col3", "Col4" };
                object[,] headerRow = new object[1, headers.Length];
                for (int h = 0; h < headers.Length; h++)
                    headerRow[0, h] = headers[h];

                sheet.ImportArray(headerRow, 1, 1);
            }

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            ms.Position = 0;

            workbook.Close();

            return File(
                ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "template_processed.xlsx"
            );
        }
    }
}
