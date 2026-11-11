using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Utils;
using dsReport.Core.Services.Interfaces;
using dsReport.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Syncfusion.EJ2.Spreadsheet;
using Syncfusion.XlsIO;
using System.Text.Json;

namespace aseeMIS.Areas.Report.Controllers
{
    [Area("Report")]
    public class ReportTemplateController(IReportService reportService, IReportExcelSheetService reportExcelSheetService, IReportExcelJsonTemplateService reportTemplateService, IAppFormMenuService menuService, IUserContextService userContextService,
        IAppFormPermissionService formAccessService, IWebHostEnvironment env) : Controller
    {
        public IActionResult Index()
        {
            return View("~/Areas/Report/Views/ReportTemplate.cshtml");
        }

        public IActionResult PartialIndex(int reportId)
        {
            return PartialView("~/Areas/Report/Views/ReportTemplate.cshtml", reportId);
        }

        // -------------------------------
        // LOAD TEMPLATE (Excel → JSON)
        // -------------------------------
        [HttpPost]
        public IActionResult LoadTemplate([FromBody] FileOptions options)
        {
            try
            {
                string filePath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "files",
                    "BANK_FSDK.xlsx"
                );

                byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
                using var inputStream = new MemoryStream(fileBytes);

                using var excelEngine = new ExcelEngine();
                IApplication application = excelEngine.Excel;
                IWorkbook workbook = application.Workbooks.Open(inputStream);

                // 👉 OPTIONAL sheet logic goes here
                // e.g. remove / copy / reorder sheets

                using var outputStream = new MemoryStream();
                workbook.Version = ExcelVersion.Excel2016;
                workbook.SaveAs(outputStream);
                outputStream.Position = 0;

                IFormFile formFile = new FormFile(
                    outputStream,
                    0,
                    outputStream.Length,
                    "",
                    "template.xlsx"
                );

                OpenRequest open = new OpenRequest
                {
                    File = formFile
                };

                // 🔑 returns Syncfusion Spreadsheet JSON (string)
                var result = Workbook.Open(open);

                return Content(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveJson([FromBody] object spreadsheetJson, int reportId)
        {
            var id = 102;

            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) =
                await AppAuthorizeHelper.ValidateReadPermissionAsync(HttpContext, formAccessService, menuService, userContextService, id, EnumContextFormType.PARENT);

            if (!isValid || appForm == null || formContext == null)
                return RedirectToAction("NoAccess", "Error");


            var rawJson = spreadsheetJson.ToString();
            if (string.IsNullOrWhiteSpace(rawJson))
                return BadRequest("Empty JSON");

            JsonElement workbook;
            JsonElement sheets;

            using (var doc = JsonDocument.Parse(rawJson))
            {
                var root = doc.RootElement;

                if (!root.TryGetProperty("jsonObject", out var jsonObject))
                    return BadRequest("Missing jsonObject");

                if (!jsonObject.TryGetProperty("Workbook", out var tmpWorkbook))
                    return BadRequest("Missing Workbook");

                workbook = tmpWorkbook.Clone();

                if (!workbook.TryGetProperty("sheets", out var tmpSheets))
                    return BadRequest("Missing Workbook.sheets");

                sheets = tmpSheets.Clone();
            }

            // --------------------------------------------------
            // 1️ SAVE REPORT-LEVEL JSON (structure only)
            // --------------------------------------------------
            var reportLevel = new Dictionary<string, object?>();

            foreach (var prop in workbook.EnumerateObject())
            {
                if (prop.Name == "sheets")
                {
                    var lightSheets = new List<object>();

                    foreach (var sheet in sheets.EnumerateArray())
                    {
                        var lightSheet = new Dictionary<string, object?>();

                        foreach (var sheetProp in sheet.EnumerateObject())
                        {
                            if (sheetProp.Name is "rows" or "cells")
                                continue;

                            lightSheet[sheetProp.Name] =
                                JsonSerializer.Deserialize<object>(sheetProp.Value.GetRawText());
                        }

                        lightSheets.Add(lightSheet);
                    }

                    reportLevel["sheets"] = lightSheets;
                }
                else
                {
                    reportLevel[prop.Name] =
                        JsonSerializer.Deserialize<object>(prop.Value.GetRawText());
                }
            }

            await reportTemplateService.AddAsync(
                new ReportExcelJsonTemplateViewModel
                {
                    ReportId = reportId,
                    ReportSheetId = null,
                    JsonTemplate = JsonSerializer.Serialize(reportLevel)
                },
                formContext
            );

            // --------------------------------------------------
            // 2️ SAVE SHEET JSONs (mapped via ReportExcelSheetService)
            // --------------------------------------------------
            var reportSheets = await reportExcelSheetService.SelectAllReportAsync(reportId);
            int index = 0;

            foreach (var sheet in sheets.EnumerateArray())
            {
                if (index >= reportSheets.Count())
                    break;

                var reportSheet = reportSheets.ElementAt(index);

                await reportTemplateService.AddAsync(
                    new ReportExcelJsonTemplateViewModel
                    {
                        ReportId = reportId,
                        ReportSheetId = reportSheet.ReportSheetId, // ✅ from DB
                        JsonTemplate = sheet.GetRawText()
                    },
                    formContext
                );

                index++;
            }

            return Ok(new
            {
                message = "Report + sheet JSON split saved correctly",
                sheetsSaved = index
            });
        }

        // --------------------------------------------
        // Get ordered sheet metadata (for hydration)
        // --------------------------------------------
        [HttpGet]
        public async Task<IActionResult> GetSheets(int reportId)
        {
            var sheets = await reportExcelSheetService.SelectAllReportAsync(reportId);

            var result = sheets
                .OrderBy(s => s.OrderId)
                .Select(s => new
                {
                    reportSheetId = s.ReportSheetId,
                    sheetName = s.ExcelSheetName,
                    orderId = s.OrderId
                });

            return Json(result);
        }

        // --------------------------------------------
        // Load workbook shell JSON (no sheet data)
        // --------------------------------------------
        [HttpGet]
        public async Task<IActionResult> LoadWorkbook(int reportId)
        {
            var json = await reportTemplateService.GetWorkbookJsonAsync(reportId);

            // IMPORTANT: return as raw content, not Json()
            return Content(json, "application/json");
        }

        // --------------------------------------------
        // Load single sheet JSON (progressive)
        // --------------------------------------------
        [HttpGet]
        public async Task<IActionResult> LoadSheet(int reportSheetId)
        {
            var json = await reportTemplateService.GetSheetJsonAsync(reportSheetId);

            return Content(json, "application/json");
        }
    }
}