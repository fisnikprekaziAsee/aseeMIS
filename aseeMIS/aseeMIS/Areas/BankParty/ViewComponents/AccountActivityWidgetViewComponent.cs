using Microsoft.AspNetCore.Mvc;
using static aseeMIS.Areas.BankParty.ViewModels.BankClientSectionViewModel;

namespace aseeMIS.Areas.BankParty.ViewComponents
{
    public class AccountActivityWidgetViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(AccountSummaryViewModel? model)
            => View(model);
    }
}
