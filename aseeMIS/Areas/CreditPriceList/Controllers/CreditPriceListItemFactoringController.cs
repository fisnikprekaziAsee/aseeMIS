using dsCreditPriceList.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.CreditPriceList.Controllers
{
    [Area("CreditPriceList")]
    public class CreditPriceListItemFactoringController(ICreditPriceListItemFactoringService priceListItemFactoringService)
        : Controller
    {
        [HttpGet]
        public async Task<IActionResult> SelectFactoringPriceListByProduct(string productCode, int priceListId)
        {
            if (string.IsNullOrWhiteSpace(productCode))
                return BadRequest("ProductCode is required.");

            var result = await priceListItemFactoringService.SelectFactoringPriceListByProductAsync(productCode, priceListId);
            return Json(result);
        }
    }
}
