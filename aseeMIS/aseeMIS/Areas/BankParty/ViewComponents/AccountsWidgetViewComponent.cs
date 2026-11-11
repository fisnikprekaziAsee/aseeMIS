using Microsoft.AspNetCore.Mvc;
using static aseeMIS.Areas.BankParty.ViewModels.BankClientSectionViewModel;

namespace aseeMIS.Areas.BankParty.ViewComponents
{
    public class AccountsWidgetViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(List<AccountViewModel> accounts)
            => View(accounts);
    }
}
