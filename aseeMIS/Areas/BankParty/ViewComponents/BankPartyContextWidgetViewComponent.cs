using aseeMIS.Areas.BankParty.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.BankParty.ViewComponents
{
    [ViewComponent(Name = "BankPartyContextWidget")]
    public class BankPartyContextWidgetViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(BankPartyContextWidgetOptions model)
        {
            model ??= new BankPartyContextWidgetOptions();

            return View(
                "~/Areas/BankParty/Views/Shared/Components/BankPartyContextWidget/Default.cshtml",
                model
            );
        }
    }
}