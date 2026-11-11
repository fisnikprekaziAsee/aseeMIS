using dsCreditPriceList.Core.Services.Interfaces;
using Google.Api;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.CreditPriceList.Controllers
{
    [Area("CreditPriceList")]
    public class CreditPriceListItemLoanController(ICreditPriceListItemLoanService priceListItemLoanService) : Controller
    {
        [HttpGet]
        public async Task<IActionResult> SelectLoanPriceListByProduct(string productCode, int priceListId)
        {
            if (string.IsNullOrWhiteSpace(productCode))
                return BadRequest("ProductCode is required.");

            var result = await priceListItemLoanService.SelectLoanPriceListByProductAsync(productCode, priceListId);

            return Json(result);
        }
    }
}
