using dsAppMng.Core.Services.Interfaces; // IAppLanguageService
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.Common.ViewComponents
{
    [ViewComponent(Name = "TranslatorWidget")]
    public class TranslatorWidgetViewComponent(IAppLanguageService appLanguageService) : ViewComponent
    {
        private readonly IAppLanguageService _appLanguageService = appLanguageService;

        public async Task<IViewComponentResult> InvokeAsync(string gridName)
        {
            ViewBag.GridName = gridName;
            ViewBag.Languages = await _appLanguageService.SelectAllActiveForDropdownAsync();
            // Renders Areas/Common/Views/Shared/Components/TranslatorWidget/Default.cshtml
            return View();
        }
    }
}
