using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Syncfusion.EJ2.Spreadsheet;
using Syncfusion.XlsIO;
using System.Drawing;

namespace aseeMIS.Areas.RepEng.Controllers
{
    [Area("RepEng")]
    public class IRRSpreadsheetController : Controller
    {
        private readonly string _connectionString;
        private readonly IWebHostEnvironment _env;

        public IRRSpreadsheetController(IConfiguration configuration, IWebHostEnvironment env)
        {
            _connectionString = configuration.GetConnectionString("ExcelConnection");
            _env = env;
        }
        public IActionResult Index()
        {
            return View("~/Areas/RepEng/Views/IRRSpreadsheet.cshtml");
        }

        public IActionResult Open(IFormCollection openRequest)
        {
            OpenRequest open = new OpenRequest();
            if (openRequest.Files.Count != 0)
            {
                open.File = openRequest.Files[0];
            }
            open.Password = openRequest["Password"];
            if (openRequest["SheetIndex"].Count != 0)
            {
                open.SheetIndex = int.Parse(openRequest["SheetIndex"]);
            }
            open.SheetPassword = openRequest["SheetPassword"];
            return Content(Workbook.Open(open));
        }


        [HttpPost]
        public IActionResult Save(SaveSettings saveSettings)
        {
            ExcelEngine excelEngine = new ExcelEngine();
            IApplication application = excelEngine.Excel;
            try
            {
                // Save the workbook as a stream.
                Stream fileStream = Workbook.Save<Stream>(saveSettings);

                // Open the workbook from the stream
                IWorkbook workbook = application.Workbooks.Open(fileStream);
                IWorkbook newWorkbook = application.Workbooks.Create(1); // Create a new workbook

                // Iterate through each worksheet
                foreach (IWorksheet sheet in workbook.Worksheets)
                {
                    IWorksheet newSheet = newWorkbook.Worksheets[0]; // You can create more sheets if necessary

                    // Get the used range
                    IRange usedRange = sheet.UsedRange;


                    // Iterate through the cells in the used range
                    for (int row = usedRange.Row; row <= usedRange.LastRow; row++)
                    {
                        for (int col = usedRange.Column; col <= usedRange.LastColumn; col++)
                        {
                            IRange cell = sheet[row, col];
                            // Checking for a specific RGB color
                            if (cell.CellStyle.Color.ToArgb() == Color.FromArgb(255, 255, 0).ToArgb()) // Yellow cells
                            {
                                // Copy the value to the new sheet
                                newSheet[row, col].Value = cell.Value;
                            }
                        }
                    }
                }

                // Construct the path to save the file in the wwwroot folder
                string basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "files", "filtered_template.xlsx");

                // Save the new workbook to a file
                using (FileStream newFileStream = new FileStream(basePath, FileMode.Create, FileAccess.Write))
                {
                    newWorkbook.SaveAs(newFileStream);
                }

                newWorkbook.Close();
                workbook.Close();
                fileStream.Dispose();

                return RedirectToAction("Index", "Spreadsheet");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public IActionResult LoadTemplate([FromBody] FileOptions options)
        {
            try
            {
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "files", "BANK_FSDK.xlsx");

                // Load file bytes into memory
                byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
                MemoryStream inputStream = new MemoryStream(fileBytes);

                using (ExcelEngine excelEngine = new ExcelEngine())
                {
                    IApplication application = excelEngine.Excel;
                    IWorkbook workbook = application.Workbooks.Open(inputStream);

                    // Select sheet
                    //IWorksheet worksheet = workbook.Worksheets["SGL"];
                    //workbook.Worksheets.AddCopy();

                    //for (int i = workbook.Worksheets.Count - 1; i >= 0; i--)
                    //{
                    //    if (workbook.Worksheets[i].Name == "SGL")
                    //    {
                    //        workbook.Worksheets.Remove(workbook.Worksheets[i]);
                    //    }
                    //}

                    MemoryStream outputStream = new MemoryStream();
                    workbook.Version = ExcelVersion.Excel2016;
                    workbook.SaveAs(outputStream);
                    outputStream.Position = 0;

                    IFormFile formFile = new FormFile(
                        outputStream,
                        0,
                        outputStream.Length,
                        "",
                        "BS_only.xlsx"
                    );

                    OpenRequest open = new OpenRequest { File = formFile };
                    var result = Workbook.Open(open);
                    return Content(result);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public IActionResult LoadSheetJson(string sheetName)
        {
            sheetName ??= "SGL";

            var filePath = Path.Combine(
                _env.WebRootPath,
                "files",
                "ZBK_FP_SGL_BS.xlsx"
            );

            if (!System.IO.File.Exists(filePath))
                return NotFound("Excel template not found");

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);

            // 🔥 Use EJ2 Workbook.Open (NOT XlsIO conversion)
            OpenRequest openRequest = new OpenRequest
            {
                File = new FormFile(fs, 0, fs.Length, "", "ZBK_FP_SGL_BS.xlsx")
            };

            // This returns FULL workbook JSON
            var workbookJson = Workbook.Open(openRequest);

            // Deserialize so we can extract ONE sheet
            var workbookObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(workbookJson);

            var sheets = (System.Text.Json.JsonElement)workbookObj["sheets"];

            foreach (var sheet in sheets.EnumerateArray())
            {
                if (sheet.GetProperty("name").GetString() == sheetName)
                {
                    return Content(sheet.GetRawText(), "application/json");
                }
            }

            return NotFound($"Sheet '{sheetName}' not found");
        }


        public IActionResult GenerateExcelFile()
        {
            // Fetch the sheet name from the database
            string sheetName = GetSheetName();

            // Fetch data for Excel cells
            var cellData = GetExcelCellData();

            // Define the path to the existing Excel file in the wwwroot folder
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "files", "BSD.IND.IRR.STA.BNK22m102023.xlsx");

            using (ExcelEngine excelEngine = new ExcelEngine())
            {
                // Load the Excel file into a MemoryStream
                byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
                MemoryStream inputStream = new MemoryStream(fileBytes);

                // Open the workbook from the memory stream
                IWorkbook workbook = excelEngine.Excel.Workbooks.Open(inputStream);

                // Find the worksheet by the fetched name
                IWorksheet worksheet = workbook.Worksheets[sheetName]; // Use the fetched name

                // Use the fetched Excel cell references and actual values
                foreach (var item in cellData)
                {
                    // Determine the value to set based on SQLColumnName
                    string valueToSet = string.Empty;

                    if (item.SQLColumnName == "AmountValue")
                    {
                        valueToSet = GetAmountValue(item.SQLCondition);
                    }
                    else if (item.SQLColumnName == "PercentValue")
                    {
                        valueToSet = GetPercentValue(item.SQLCondition);
                    }

                    // Update the Excel cell with the corresponding value
                    if (!string.IsNullOrEmpty(item.ExcelCell) && !string.IsNullOrEmpty(valueToSet))
                    {
                        worksheet.Range[item.ExcelCell].Text = valueToSet;
                    }
                }


                // Save the modified workbook to another MemoryStream
                MemoryStream outputStream = new MemoryStream();
                workbook.Version = ExcelVersion.Excel2016;
                workbook.SaveAs(outputStream);
                outputStream.Position = 0;

                // Return the Excel file as a downloadable file
                return File(outputStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ModifiedBook2.xlsx");
            }
        }

        // Get the sheet name
        private string GetSheetName()
        {
            string sheetName = string.Empty;

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT ExcelSheetName FROM ExcelAccount"; // Adjust your query as needed

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    try
                    {
                        connection.Open();
                        object result = command.ExecuteScalar();
                        if (result != null)
                        {
                            sheetName = result.ToString().Trim(); // Trim any extra spaces
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            return sheetName;
        }

        // Get the excel cell data
        private List<ExcelCellData> GetExcelCellData()
        {
            var cellDataList = new List<ExcelCellData>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT ExcelCell, SQLColumnName, SQLCondition FROM ExcelAccount"; // Adjust your query as needed

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    try
                    {
                        connection.Open();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (reader["ExcelCell"] != DBNull.Value && reader["SQLColumnName"] != DBNull.Value && reader["SQLCondition"] != DBNull.Value)
                                {
                                    cellDataList.Add(new ExcelCellData
                                    {
                                        ExcelCell = reader["ExcelCell"].ToString(),
                                        SQLColumnName = reader["SQLColumnName"].ToString(),
                                        SQLCondition = reader["SQLCondition"].ToString() // Store the condition as well
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            return cellDataList;
        }

        private string GetAmountValue(string sqlCondition)
        {
            // Logic to retrieve AmountValue based on the SQLCondition

            string amountValue = string.Empty;

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = $"SELECT AmountValue FROM Account WHERE {sqlCondition}";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    try
                    {
                        connection.Open();
                        object result = command.ExecuteScalar();
                        if (result != null)
                        {
                            amountValue = result.ToString();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            return amountValue;
        }

        private string GetPercentValue(string sqlCondition)
        {
            // Logic to retrieve PercentValue based on the SQLCondition
            string percentValue = string.Empty;

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = $"SELECT PercentValue FROM Account WHERE {sqlCondition}"; // Ensure SQL injection prevention

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    try
                    {
                        connection.Open();
                        object result = command.ExecuteScalar();
                        if (result != null)
                        {
                            percentValue = result.ToString();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            return percentValue;
        }
    }

    // Helper class to hold the cell data
    public class ExcelCellData
    {
        public string ExcelCell { get; set; }
        public string SQLColumnName { get; set; }
        public string SQLCondition { get; set; }
    }
}
