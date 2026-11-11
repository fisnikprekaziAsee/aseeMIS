using DocumentFormat.OpenXml.Office2010.Excel;
using dsBankParty.Core.Services.Interfaces;
using dsBankParty.Core.ViewModels;

using dsMIS.Core.Enums;
using dsMIS.Core.Exceptions;
using dsMIS.Core.Services.Interfaces;
using dsCredit.Core.DTOs;
using dsCredit.Core.Services.Interfaces;
using dsCredit.Core.ViewModels;
using dsCreditDef.Core.Services;
using dsCreditDef.Core.Services.Interfaces;
using dsCreditProduct.Core.Services;
using dsCreditProduct.Core.Services.Interfaces;
using dsDef.Core.Services.Interfaces;
using aseeMIS.Areas.Credit.ViewModels;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.Credit.Controllers
{
    /// <summary>
    /// Controller for the Credit Case domain.
    ///
    /// Business flow:
    ///   1. Create / save a Credit Case Framework (the limit/umbrella record).
    ///   2. Under an existing framework, create a Credit Case application.
    ///   3. The product type determines the application sub-type:
    ///      - Products that support amortization plans ? Loan application flow.
    ///      - Factoring products ? Factoring application flow.
    ///
    /// All mutating endpoints expect a "FormSessionId" header for permission validation
    /// (following the same convention as the rest of the platform).
    /// </summary>
    [Area("Credit")]
    public class CreditCaseController(
        ICreditCaseService creditCaseService,
        ICreditCaseLoanService creditCaseLoanService,
        ICreditProductSpecLoanService productSpecLoanService,
        ILocalizationService localizationService,
        IAppFormMenuService menuService,
        IUserContextService userContextService,
        IAppFormPermissionService formAccessService,
        IDefCurrencyService currencyService,
        ICreditProductService creditProductService,
        ICreditDefPurposeservice creditLoanPurposeService,
        IBankPartyService<BankPartyViewModel> bankPartyService) : Controller
    {
        private const int CreditHubFormId = 31;

        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> Search(
            string searchText,
            int take = 20,
            CancellationToken cancellationToken = default)
        {
            var term = (searchText ?? string.Empty).Trim();
            if (term.Length < 2)
                return Json(Array.Empty<CreditCaseSearchDto>());

            return Json(await creditCaseService.SearchAsync(term, take, cancellationToken));
        }

        // ---------------------------------------------------------------------
        // INDEX / GRID
        // ---------------------------------------------------------------------

        /*
        // Legacy route. Credit Hub opens through CreditController.IndexAsync and uses LoadPartyCreditCases.
        public IActionResult Index()
        {
            return View("~/Areas/CreditCase/Views/CreditCase/_PartyCreditCaseMasterPage.cshtml");
        }
        */

        [HttpGet]
        public async Task<IActionResult> LoadPartyCreditCases(int bankPartyId, string? hubFormSessionId = null, string? caseFilter = null)
        {
            bool? onlyActive = string.Equals(caseFilter, "all", StringComparison.OrdinalIgnoreCase)
                ? null : !string.Equals(caseFilter, "historical", StringComparison.OrdinalIgnoreCase);
            var existing = await creditCaseService.SelectForm31ClientCreditHUB(bankPartyId, onlyActive);

            ViewBag.HubFormSessionId = hubFormSessionId;
            ViewBag.BankPartyId = bankPartyId;
            ViewBag.CaseFilter = onlyActive == null ? "all" : onlyActive.Value ? "active" : "historical";

            return PartialView("~/Areas/Credit/Views/_PartyCreditCaseMasterPage.cshtml", existing);
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> GetPartyCreditCases(int bankPartyId, string? caseFilter = null)
        {
            if (bankPartyId <= 0)
                return Json(Array.Empty<dsCredit.Core.DTOs.FN.CreditCaseProductByBankPartyDto>());

            bool? onlyActive = string.Equals(caseFilter, "all", StringComparison.OrdinalIgnoreCase)
                ? null : !string.Equals(caseFilter, "historical", StringComparison.OrdinalIgnoreCase);
            var existing = await creditCaseService.SelectForm31ClientCreditHUB(bankPartyId, onlyActive);
            return Json(existing);
        }

        //[HttpGet]
        //public async Task<IActionResult> BankPartyProfile(int? bankPartyId = null)
        //{
        //    var (isValid, appForm, formContext, _, _, _) =
        //        await AppAuthorizeHelper.ValidateReadPermissionAsync(
        //            HttpContext,
        //            formAccessService,
        //            menuService,
        //            userContextService,
        //            CreditHubFormId,
        //            EnumContextFormType.PARENT);

        //    if (!isValid || appForm == null || formContext == null)
        //        return RedirectToAction("NoAccess", "Error");

        //    var vm = new CreditBankPartyProfilePageViewModel
        //    {
        //        SelectedBankPartyId = bankPartyId > 0 ? bankPartyId : null
        //    };

        //    return View("~/Areas/Credit/Views/BankPartyProfile.cshtml", vm);
        //}

        //[HttpGet]
        //public async Task<IActionResult> LoadBankPartyProfile(int bankPartyId)
        //{
        //    if (bankPartyId <= 0)
        //        return Content("A valid customer must be selected.");

        //    var (isValid, appForm, formContext, _, _, _) =
        //        await AppAuthorizeHelper.ValidateReadPermissionAsync(
        //            HttpContext,
        //            formAccessService,
        //            menuService,
        //            userContextService,
        //            CreditHubFormId,
        //            EnumContextFormType.PARENT);

        //    if (!isValid || appForm == null || formContext == null)
        //        return RedirectToAction("NoAccess", "Error");

        //    var profile = await creditCaseService.GetBankPartyCaseProfileAsync(bankPartyId);
        //    if (profile == null)
        //        return Content("Customer profile could not be found.");

        //    var bankParty = await bankPartyService.GetByIdAsync(bankPartyId);
        //    var activeProducts = await creditCaseService.SelectCreditCaseProductsByBankPartyAsync(bankPartyId, true);

        //    var vm = new CreditBankPartyProfileViewModel
        //    {
        //        Profile = profile,
        //        BankParty = bankParty,
        //        ActiveProducts = activeProducts
        //            .OrderByDescending(x => x.OutstandingAmount ?? x.OutstandingExposureAmount ?? 0m)
        //            .ThenByDescending(x => x.CreditCaseId)
        //            .ToList()
        //    };

        //    return PartialView("~/Areas/Credit/Views/_BankPartyCaseProfile.cshtml", vm);
        //}

        //[HttpGet]
        //public async Task<IActionResult> LoadExisitingCreditCase(long creditCaseId)
        //{
        //    var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) =
        //        await AppAuthorizeHelper.ValidateReadPermissionAsync(
        //            HttpContext, formAccessService, menuService, userContextService,
        //            322, EnumContextFormType.PARENT);

        //    if (!isValid || appForm == null || formContext == null)
        //        return RedirectToAction("NoAccess", "Error");

        //    var model = await creditCaseLoanService.GetByIdAsync(creditCaseId);

        //    if (model == null || model.CreditCaseId == 0)
        //        return Content("Credit Case doesnt exist");


        //    ViewBag.FormId = appForm.FormId;
        //    ViewBag.FormSessionId = formContext.FormSessionId;
        //    ViewBag.IsAdd = true;

        //    return PartialView(
        //        "~/Areas/CreditCase/Views/CreditCase/_CreditCaseLoanUpsert.cshtml",
        //        model);
        //}


    }
}
