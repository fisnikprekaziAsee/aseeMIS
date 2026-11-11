//using dsCreditDef.Core.Services.Interfaces;
//using dsCreditProduct.Core.Services.Interfaces;
//using Microsoft.AspNetCore.Mvc;

//namespace aseeMIS.Areas.CreditDef.Controllers
//{
//    [Area("CreditDef")]
//    public class ProductController(ICreditProductService productService) : Controller
//    {
//        [HttpGet]
//        [Produces("application/json")]
//        public async Task<IActionResult> Search([FromQuery] string q)
//        {
//            var term = (q ?? string.Empty).Trim();
//            var rows = await productService.SearchAsync(term);
//            return Json(rows);
//        }

//        [HttpGet]
//        public async Task<IActionResult> GetProductInfo(string productCode)
//        {
//            if (string.IsNullOrWhiteSpace(productCode))
//                return BadRequest();

//            var data = await productService.GetByIdAsync(productCode);
//            return Ok(data);
//        }

//    }
//}