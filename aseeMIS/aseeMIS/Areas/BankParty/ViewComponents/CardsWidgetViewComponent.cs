using Microsoft.AspNetCore.Mvc;
using static aseeMIS.Areas.BankParty.ViewModels.BankClientSectionViewModel;

namespace aseeMIS.Areas.BankParty.ViewComponents
{
    public class CardsWidgetViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(List<CardViewModel> cards)
            => View(cards);
    }
}
