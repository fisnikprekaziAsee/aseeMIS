using dsAspNet.Core.Services.Interfaces;
using dsAspNet.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.AspNet.Controllers
{
    [Area("AspNet")]
    public class RoleController(IAspNetRoleService service) : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> Search(string searchText)
        {
            var term = (searchText ?? string.Empty).Trim();
            var rows = await service.SearchAsync(term);
            return Json(rows);
        }
    }
}
