//using aseeMIS.Areas.Common.ViewModels;
//using dsProduct.Core.Services.Interfaces;
//using Microsoft.AspNetCore.Mvc;

//namespace aseeMIS.Areas.Product.Controllers
//{
//    [Area("Product")]
//    [Route("Product/[action]")]
//    public class ProductController(IWebHostEnvironment env, IProductTypeService productTypeService) : Controller
//    {
//        public IActionResult Index()
//        {
//            return View();
//        }

//        [HttpPost]
//        public async Task<IActionResult> UploadImage(
//            IFormFile file,
//            string productCategoryName,
//            string productCode)
//        {
//            if (file == null || file.Length == 0)
//                return BadRequest("No file selected.");

//            // CATEGORY NAME NEEDS CLEANING (remove spaces, special chars)
//            string safeCategoryName = productCategoryName
//                .Trim()
//                .Replace(" ", "-")
//                .Replace("/", "-")
//                .Replace("\\", "-")
//                .Replace(".", "-");

//            // Build folder path
//            string folderPath = Path.Combine(
//                env.WebRootPath,
//                "product",
//                safeCategoryName,
//                productCode
//            );

//            // Create folder if not exists
//            if (!Directory.Exists(folderPath))
//                Directory.CreateDirectory(folderPath);

//            // Save file
//            string filePath = Path.Combine(folderPath, file.FileName);

//            using (var stream = new FileStream(filePath, FileMode.Create))
//            {
//                await file.CopyToAsync(stream);
//            }

//            // Return RELATIVE path to save in DB
//            string relativePath = $"product/{safeCategoryName}/{productCode}/{file.FileName}";

//            return Ok(new { imagePath = relativePath });
//        }

//        [HttpGet]
//        public async Task<IActionResult> GetProductTypeCapabilities(int productTypeId)
//        {
//            var vm = await productTypeService.SelectProductTypeCapabilitiesAsync(productTypeId);
//            if (vm == null)
//                return NotFound();

//            return Json(vm);
//        }


//        [HttpGet]
//        public IActionResult ConfigurableGrid(string sessionId, int pk)
//        {
//            var model = new Entity360PageViewModel
//            {
//                FormSessionId = sessionId,
//                PrimaryKey = pk.ToString()
//            };

//            return PartialView("~/Areas/Product/Views/_ProductConfigurableGrids.cshtml", model);
//        }

//        [HttpGet]
//        public IActionResult AssembledGrid(string sessionId, int pk)
//        {
//            var model = new Entity360PageViewModel
//            {
//                FormSessionId = sessionId,
//                PrimaryKey = pk.ToString()
//            };

//            return PartialView("~/Areas/Product/Views/_ProductAssembledGrids.cshtml", model);
//        }

//    }
//}
