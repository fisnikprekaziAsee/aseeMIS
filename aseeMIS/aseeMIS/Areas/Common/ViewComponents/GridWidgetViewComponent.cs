using Microsoft.AspNetCore.Mvc;
using aseeMIS.Areas.Common.ViewModels;

namespace aseeMIS.Areas.Common.ViewComponents
{
    [ViewComponent(Name = "GridWidget")]
    public sealed class GridWidgetViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(GridWidgetViewModel model)
        {
            // Renders: /Areas/Common/Views/Shared/Components/GridWidget/Default.cshtml
            return View(model);
        }
    }
}
