using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.BankParty.ViewComponents
{
    [ViewComponent(Name = "BankEntityHeaderWidget")]
    public class BankEntityHeaderWidgetViewComponent : ViewComponent
    {
        // Usage:
        // @await Component.InvokeAsync("BankEntityHeaderWidget", new { viewPath = "...", viewKey = "...", model = someModel })
        //
        // - If viewPath is provided -> render that partial path.
        // - Else if viewKey is provided (e.g., "IndividualPartyHeader") -> render that built-in component view.
        // - Else -> render the default fallback view.

        public IViewComponentResult Invoke(string? viewPath = null, object? model = null)
        {
            if (!string.IsNullOrWhiteSpace(viewPath))
            {
                // Render a custom partial path you pass in (relative or absolute view path works)
                return View(viewPath, model);
            }

            // Fallback header
            return View("Default", model);
        }
    }
}
