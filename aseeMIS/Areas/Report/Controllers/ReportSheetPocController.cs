using Microsoft.AspNetCore.Mvc;
using Syncfusion.EJ2.Spreadsheet;
using Syncfusion.XlsIO;

namespace aseeMIS.Areas.Report.Controllers
{
    [Area("Report")]
    public class ReportSheetPocController : Controller
    {
        // -----------------------------
        // 🔴 STATIC IN-MEMORY STORAGE
        // -----------------------------
        private static readonly Dictionary<int, SheetStore> _sheets = new();

        // Fake ReportId = 1
        private const int REPORT_ID = 1;

        public IActionResult Index()
        {
            return View("~/Areas/Report/Views/ReportSheetPoc.cshtml");
        }

        private static byte[]? _currentWorkbook; // TEMP for POC

        [HttpPost]
        public IActionResult Open(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            using var ms = new MemoryStream();
            file.CopyTo(ms);
            var uploadedBytes = ms.ToArray();

            // First file → normal open
            if (_currentWorkbook == null)
            {
                _currentWorkbook = uploadedBytes;

                OpenRequest open = new OpenRequest
                {
                    File = file
                };

                return Content(Workbook.Open(open));
            }

            // Subsequent files → append
            _currentWorkbook = AppendSheets(_currentWorkbook, uploadedBytes);

            // Return merged workbook
            OpenRequest mergedOpen = new OpenRequest
            {
                File = new FormFile(
                    new MemoryStream(_currentWorkbook),
                    0,
                    _currentWorkbook.Length,
                    "",
                    "merged.xlsx"
                )
            };

            return Content(Workbook.Open(mergedOpen));
        }

        public byte[] AppendSheets(byte[] baseFile, byte[] importFile)
        {
            using var engine = new ExcelEngine();
            var app = engine.Excel;

            IWorkbook baseWb = app.Workbooks.Open(new MemoryStream(baseFile));
            IWorkbook importWb = app.Workbooks.Open(new MemoryStream(importFile));

            foreach (IWorksheet sheet in importWb.Worksheets)
            {
                // Clone sheet into base workbook
                baseWb.Worksheets.AddCopy(sheet);
            }

            using var outStream = new MemoryStream();
            baseWb.SaveAs(outStream);
            return outStream.ToArray();
        }

        // -----------------------------
        // LOAD SINGLE SHEET
        // -----------------------------
        [HttpGet]
        public IActionResult LoadSheet(int sheetId)
        {
            if (!_sheets.TryGetValue(sheetId, out var sheet))
                return NotFound("Sheet not found");

            return Json(sheet);
        }

        // -----------------------------
        // SAVE SINGLE SHEET
        // -----------------------------
        [HttpPost]
        public IActionResult SaveSheet([FromBody] SheetStore sheet)
        {
            _sheets[sheet.SheetId] = sheet;
            return Ok(new { message = "Sheet saved", sheet.SheetId });
        }

        // -----------------------------
        // LIST SHEETS FOR REPORT
        // -----------------------------
        [HttpGet]
        public IActionResult GetSheets()
        {
            return Json(_sheets.Values.OrderBy(s => s.Order));
        }
    }

    // -----------------------------
    // SIMPLE SHEET DTO
    // -----------------------------
    public class SheetStore
    {
        public int SheetId { get; set; }
        public int ReportId { get; set; }
        public string SheetName { get; set; } = "";
        public int Order { get; set; }

        // 🔑 ONLY SHEET CONTENT
        public object SheetJson { get; set; } = null!;
    }
}
