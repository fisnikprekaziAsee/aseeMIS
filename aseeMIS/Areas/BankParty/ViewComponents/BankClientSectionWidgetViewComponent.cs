using Microsoft.AspNetCore.Mvc;
using static aseeMIS.Areas.BankParty.ViewModels.BankClientSectionViewModel;

namespace aseeMIS.Areas.BankParty.ViewComponents
{
    [ViewComponent(Name = "BankClientSectionWidget")]
    public class BankClientSectionWidgetViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(SectionsViewModel model) => View(model);
    }
}
