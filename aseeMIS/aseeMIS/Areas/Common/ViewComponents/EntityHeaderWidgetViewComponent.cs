using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.Common.ViewComponents
{
    [ViewComponent(Name = "EntityHeaderWidget")]
    public class EntityHeaderWidgetViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(string viewPath, object? model)
        {
            // If viewPath is null/empty, render nothing (no header)
            if (string.IsNullOrWhiteSpace(viewPath))
            {
                return Content(string.Empty);
            }
            return View(viewPath, model); // Renders the partial you configured in JSON
        }
    }
}
