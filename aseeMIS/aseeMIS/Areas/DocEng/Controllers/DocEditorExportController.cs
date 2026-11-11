using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using Syncfusion.Pdf;

namespace aseeMIS.Areas.DocEng.Controllers
{
    [Area("DocEng")]
    [Route("DocEng/DocEditorExport/[action]")]
    public class DocEditorExportController(
        IAppFormMenuService menuService,
        IAppFormPermissionService formAccessService,
        IUserContextService userContextService) : Controller
    {
        [HttpPost]
        public async Task<IActionResult> ExportPdf(IFormFile document)
        {
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();
            var (isValid, _, contextSessionDto, _, _, _) = await AppAuthorizeHelper.ValidateActionAsync(
                HttpContext,
                formAccessService,
                menuService,
                userContextService,
                formSessionId,
                requiredPermission: EumFormAccessAction.READ);

            if (!isValid || contextSessionDto == null)
                return Unauthorized();

            if (document is null || document.Length == 0)
                return BadRequest("A document is required.");

            await using var input = document.OpenReadStream();
            using var wordDocument = new WordDocument(input, FormatType.Docx);
            using var renderer = new DocIORenderer();
            using var pdfDocument = renderer.ConvertToPDF(wordDocument);
            await using var output = new MemoryStream();
            pdfDocument.Save(output);

            return File(output.ToArray(), "application/pdf", "document.pdf");
        }
    }
}
