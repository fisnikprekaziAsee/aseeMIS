using dsAppMng.Core.Services.Interfaces;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Services.Interfaces;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Security.Claims;

namespace aseeMIS.Areas.Common.Controllers
{
    [Area("Common")]
    [AppAuthorizeAttribute] // Explicitly include the 'Attribute' suffix to resolve ambiguity
    public class AreaNavigationController(IAppMenuService formService, IAppFormMenuService menuService, IAppSessionService dsSessionService) : Controller
    {

        public async Task<IActionResult> Index(string FormId)
        {
            try
            {
                // var formHierarchy = await formService.SelectByParentIdAsync(id, "sq-AL");


                //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                //var formHierarchy = await formService.GetHrefByUser(userId, dsSessionService.GetLanguageCode());
                ViewBag.TargetFormKey = FormId;

                ////ViewData["FormHierarchyJson"] = JsonConvert.SerializeObject(formHierarchy);
                //ViewBag.FormHierarchy = formHierarchy;
                //return View("~/Areas/Common/Views/AreaNavigation.cshtml", formHierarchy);
                return View("~/Areas/Common/Views/AreaNavigation.cshtml");

            }
            catch
            {
                // Log the error (you can use a logging service here)
                // Return the error view
                return View("Error");
            }
        }

        public IActionResult IndexFavourites(string? FormId)
        {
            ViewBag.TargetFormKey = FormId;
            return View("~/Areas/Common/Views/AreaNavigationFavourites.cshtml");
        }
    }
}
