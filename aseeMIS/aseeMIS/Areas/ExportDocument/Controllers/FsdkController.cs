using dsReportExport.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.Fsdk.Controllers
{
    [Area("FSDK")]
    public class FsdkController(
        IDwFsdkGeneralDepositsReportService fullService
    ) : Controller
    {
        [HttpGet]
        public async Task<IActionResult> Index(DateOnly? dateKey)
        {
            var data = await fullService.SelectReportDataAsync(dateKey);
            return View("~/Areas/Fsdk/Views/Fsdk.cshtml", data);
        }


        [HttpGet]
        public async Task<IActionResult> LoadByDate(DateOnly dateKey)
        {
            var data = await fullService.SelectReportDataAsync(dateKey);
            return Json(new { result = data, count = data.Count });
        }
    }
}
