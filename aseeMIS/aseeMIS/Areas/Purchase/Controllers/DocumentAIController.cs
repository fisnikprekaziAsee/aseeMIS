using aseeMIS.Areas.Purchase.Models;
using aseeMIS.Services.Interfaces;
using dsPurchase.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.Purchase.Controllers
{
    [Area("OcrAI")]
    public class DocumentAiController(IDocumentTextExtractor textExtractor, IBillAiService ai, IInvoiceWithLinesService invoiceService) : Controller
    {
   

        [HttpGet]
        public IActionResult Index()
        {
            return View("~/Areas/Purchase/Views/DocumentAI.cshtml");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnalyzeBill(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Upload a file.");

            // 1) OCR/Text extraction
            var extracted = await textExtractor.ExtractAsync(file); // returns text
            if (string.IsNullOrWhiteSpace(extracted.Text))
                return BadRequest("No text extracted.");

            // 2) AI extraction
            var result = await ai.ExtractBillFieldsAsync(extracted.Text);
            var rawJson = System.Text.Json.JsonSerializer.Serialize(result);


            return Json(new
            {
                fields = result,
                ocrText = extracted.Text, //extracted.Text.Length > 1500 ? extracted.Text[..1500] : extracted.Text
                rawJson = rawJson

            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddInvoice([FromBody] SaveInvoiceRequest request)
        {
            if (request?.Invoice == null)
                return BadRequest("Invalid invoice data.");

            var invoiceId = await invoiceService.AddFromAiAsync(
                request.Invoice,
                request.RawJson,
                request.OcrText
            );

            return Json(new
            {
                success = true,
                invoiceId = invoiceId
            });
        }


    }
}
