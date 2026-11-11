using dsAspNet.Core.Services.Interfaces;
using dsAspNet.Core.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.AspNet.Controllers
{
    [Area("AspNet")]
    public class UserController(IAspNetUserService<AspNetUserViewModel> service) : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        //[HttpPost]
        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> Search(string searchText)
        {

            var term = (searchText ?? string.Empty).Trim();
            var rows = await service.SearchAsync(term);
            return Json(rows);


            //search ??= "";

            //// Keep it fast: only what you need for Mention
            //var result = (await service.SearchAsync(search)).ToList();
            //return Json(result);

            //if (!string.IsNullOrWhiteSpace(search))
            //{
            //    q = q.Where(u =>
            //        u.UserName.Contains(search) ||
            //        u.Email.Contains(search) ||
            //        u.FirstName.Contains(search) ||
            //        u.LastName.Contains(search));
            //}

            //var result = q
            //    .OrderBy(u => u.UserName)
            //    .Take(20)
            //    .Select(u => new
            //    {
            //        id = u.Id,
            //        name = (u.FirstName + " " + u.LastName).Trim(),
            //        username = u.UserName,
            //        email = u.Email
            //    })
            //    .ToList();

        }
    }
}
