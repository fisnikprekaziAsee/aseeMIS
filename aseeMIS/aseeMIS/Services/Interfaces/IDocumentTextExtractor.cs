namespace aseeMIS.Services.Interfaces
{
    public record ExtractedTextResult(
      string Text,
      string Source,     // "PDF_TEXT", "PDF_OCR", "DOCX", "IMAGE_OCR"
      int Pages,
      bool IsOcrUsed
  );

    public interface IDocumentTextExtractor
    {
        Task<ExtractedTextResult> ExtractAsync(IFormFile file);
    }
}
