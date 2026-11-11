using dsCreditGL.Core.Services.Interfaces;
using aseeMIS.Areas.CreditGL.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.CreditGL.Controllers
{
    [Area("CreditGL")]
    public class CreditGLController(
        ICreditGLJournalHeaderService headerService,
        ICreditGLJournalLineService lineService) : Controller
    {
        public async Task<IActionResult> Index()
        {
            var model = new CreditGLJournalPageViewModel
            {
                // Change these method names based on your GenericCRUDService methods
                Headers = [.. (await headerService.SelectAllAsync())],
                Lines = [.. (await lineService.SelectAllAsync())]
            };

            return View("~/Areas/CreditGL/Views/CreditGL.cshtml", model);
        }
    }
}


