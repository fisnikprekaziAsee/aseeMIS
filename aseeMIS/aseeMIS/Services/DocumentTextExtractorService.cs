using System.Text;
using DocumentFormat.OpenXml.Packaging;
using aseeMIS.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Tesseract;
using System.Drawing.Imaging;

namespace aseeMIS.Services
{
    using PdfiumDocument = PdfiumViewer.PdfDocument;
    using PdfPigDocument = UglyToad.PdfPig.PdfDocument;

    public class DocumentTextExtractor : IDocumentTextExtractor
    {
        private readonly IWebHostEnvironment _env;

        public DocumentTextExtractor(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<ExtractedTextResult> ExtractAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Empty file.");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            return ext switch
            {
                ".pdf" => await ExtractPdfAsync(file),
                ".docx" => await ExtractDocxAsync(file),

                ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff"
                    => await ExtractImageAsync(file),

                _ => throw new NotSupportedException("Supported: PDF, DOCX, PNG, JPG, JPEG, BMP, TIF, TIFF.")
            };
        }

        // -------------------------
        // DOCX (OpenXML)
        // -------------------------
        private static async Task<ExtractedTextResult> ExtractDocxAsync(IFormFile docx)
        {
            await using var ms = new MemoryStream();
            await docx.CopyToAsync(ms);
            ms.Position = 0;

            using var wordDoc = WordprocessingDocument.Open(ms, false);
            var body = wordDoc.MainDocumentPart?.Document?.Body;
            var text = body?.InnerText ?? "";

            text = NormalizeOcrText(text);
            text = FormatAsOrganizedBlocks(text);

            return new ExtractedTextResult(text, "DOCX", Pages: 1, IsOcrUsed: false);
        }

        // -------------------------
        // IMAGE OCR (Pix.LoadFromMemory)
        // -------------------------
        private async Task<ExtractedTextResult> ExtractImageAsync(IFormFile image)
        {
            var tessDataPath = GetTessDataPathOrThrow();

            await using var ms = new MemoryStream();
            await image.CopyToAsync(ms);
            var bytes = ms.ToArray();

            using var engine = CreateEngine(tessDataPath);

            using var pix = Pix.LoadFromMemory(bytes);
            using var page = engine.Process(pix);

            var text = NormalizeOcrText(page.GetText());
            text = FormatAsOrganizedBlocks(text);

            return new ExtractedTextResult(text, "IMAGE_OCR", Pages: 1, IsOcrUsed: true);
        }

        // -------------------------
        // PDF: PdfPig text, fallback OCR
        // -------------------------
        private async Task<ExtractedTextResult> ExtractPdfAsync(IFormFile pdf)
        {
            var (text, pages) = await TryExtractPdfTextAsync(pdf);

            if (!string.IsNullOrWhiteSpace(text) && text.Trim().Length > 50)
            {
                text = NormalizeOcrText(text);
                text = FormatAsOrganizedBlocks(text);
                return new ExtractedTextResult(text, "PDF_TEXT", pages, IsOcrUsed: false);
            }

            return await OcrPdfAsync(pdf);
        }

        private static async Task<(string Text, int Pages)> TryExtractPdfTextAsync(IFormFile pdf)
        {
            await using var ms = new MemoryStream();
            await pdf.CopyToAsync(ms);
            ms.Position = 0;

            using var document = PdfPigDocument.Open(ms);
            var sb = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                if (!string.IsNullOrWhiteSpace(page.Text))
                    sb.AppendLine(page.Text);
            }

            return (sb.ToString(), document.NumberOfPages);
        }

        private async Task<ExtractedTextResult> OcrPdfAsync(IFormFile pdf)
        {
            var tessDataPath = GetTessDataPathOrThrow();

            var tempPdf = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
            await using (var fs = File.Create(tempPdf))
                await pdf.CopyToAsync(fs);

            try
            {
                using var engine = CreateEngine(tessDataPath);
                using var pdfDoc = PdfiumDocument.Load(tempPdf);

                var sb = new StringBuilder();

                for (int i = 0; i < pdfDoc.PageCount; i++)
                {
                    // Render to Image
                    using var img = pdfDoc.Render(i, 350, 350, true);

                    // Convert rendered image -> PNG bytes
                    byte[] pngBytes;
                    using (var imgMs = new MemoryStream())
                    {
                        img.Save(imgMs, System.Drawing.Imaging.ImageFormat.Png);
                        pngBytes = imgMs.ToArray();
                    }

                    // OCR from PNG bytes
                    using var pix = Pix.LoadFromMemory(pngBytes);
                    using var page = engine.Process(pix);

                    var pageText = NormalizeOcrText(page.GetText());

                    sb.AppendLine($"========== PAGE {i + 1} ==========");
                    sb.AppendLine(pageText);
                    sb.AppendLine();
                }

                var finalText = FormatAsOrganizedBlocks(sb.ToString());
                return new ExtractedTextResult(finalText, "PDF_OCR", pdfDoc.PageCount, IsOcrUsed: true);
            }
            finally
            {
                try { File.Delete(tempPdf); } catch { }
            }
        }

        // -------------------------
        // Helpers
        // -------------------------
        private string GetTessDataPathOrThrow()
        {
            var tessDataPath = Path.Combine(_env.WebRootPath, "tessdata");

            var eng = Path.Combine(tessDataPath, "eng.traineddata");
            var sqi = Path.Combine(tessDataPath, "sqi.traineddata");

            if (!File.Exists(eng))
                throw new Exception($"Missing: {eng}");

            if (!File.Exists(sqi))
                throw new Exception($"Missing: {sqi}");

            return tessDataPath;
        }

        private static TesseractEngine CreateEngine(string tessDataPath)
        {
            var engine = new TesseractEngine(tessDataPath, "eng+sqi", EngineMode.LstmOnly);

            engine.SetVariable("preserve_interword_spaces", "1");
            engine.DefaultPageSegMode = PageSegMode.Auto;

            return engine;
        }

        private static string NormalizeOcrText(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            var s = input.Replace("\r\n", "\n").Replace("\r", "\n");

            while (s.Contains("  "))
                s = s.Replace("  ", " ");

            while (s.Contains("\n\n\n"))
                s = s.Replace("\n\n\n", "\n\n");

            // common OCR fixes
            s = s.Replace("TV5H", "TVSH")
                 .Replace("T V S H", "TVSH")
                 .Replace("N U I", "NUI");

            return s.Trim();
        }

        private static string FormatAsOrganizedBlocks(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            var lines = text.Split('\n')
                            .Select(l => l.Trim())
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .ToList();

            var sb = new StringBuilder();
            foreach (var line in lines)
                sb.AppendLine(line);

            return sb.ToString().Trim();
        }
    }
}