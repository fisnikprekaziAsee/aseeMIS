using dsCreditPriceList.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.CreditPriceList.Controllers
{
    [Area("CreditPriceList")]
    public class CreditPriceListController(ICreditPriceListService priceListService) : Controller
    {
        [HttpGet]
        public async Task<IActionResult> GetPriceListsByProduct(string productCode)
        {
            if (string.IsNullOrWhiteSpace(productCode))
                return BadRequest("ProductCode is required.");

            var result = await priceListService.SelectPriceListsByProductAsync(productCode);

            return Json(result);
        }
    }
}
