//using dsMIS.Core.Enums;

//using dsMIS.Core.Services.Interfaces;
//using dsCredit.Core.Services.Interfaces;
//using dsCredit.Core.ViewModels;
//using dsCreditDef.Core.Services.Interfaces;
//using dsCreditProduct.Core.Services.Interfaces;
//using dsDef.Core.Services.Interfaces;
//using aseeMIS.Areas.Credit.ViewModels;
//using aseeMIS.Utils;
//using Microsoft.AspNetCore.Mvc;

//namespace aseeMIS.Areas.Credit.Controllers
//{
//    [Area("Credit")]
//    public class CreditCaseContractFarmingController(
//        ICreditCaseService creditCaseService,
//        ICreditFrameworkService frameworkService,
//        IAppFormMenuService menuService,
//        IUserContextService userContextService,
//        IAppFormPermissionService formAccessService,
//        IDefBankService bankService,
//        ICreditProductService creditProductService,
//        ICreditProductSpecContractFarmingService contractFarmingSpecService,
//        ICreditDefPurposeservice creditLoanPurposeService) : Controller
//    {
//        private const int ContractFarmingFormId = 323;

//        [HttpGet]
//        public async Task<IActionResult> LoadContractFarming(int bankPartyId, string? hubFormSessionId = null, string? childFormSessionId = null)
//        {
//            if (bankPartyId <= 0)
//                return Content("Bank party is required before creating a contract farming application.");

//            return await LoadContractFarming360Async(bankPartyId, null, hubFormSessionId, childFormSessionId);
//        }

//        [HttpGet]
//        public async Task<IActionResult> LoadExistingContractFarming(long creditCaseId, string? hubFormSessionId = null, string? childFormSessionId = null)
//        {
//            if (creditCaseId <= 0)
//                return Content("Credit case is required.");

//            return await LoadContractFarming360Async(null, creditCaseId, hubFormSessionId, childFormSessionId);
//        }

//        [HttpGet]
//        public IActionResult LoadContractFarmingDetailsTab(long? creditCaseId, string? formSessionId = null)
//        {
//            var model = new CreditCaseCrudTabViewModel
//            {
//                CreditCaseId = creditCaseId > 0 ? creditCaseId : null,
//                FormSessionId = formSessionId,
//                FormId = 301,
//                ForeignAttributeName = "CreditCaseId",
//                PrimaryAttributeName = "CreditCaseId",
//                Title = "Contract Farming Details",
//                Description = "Manage the contract farming extension fields linked to this loan credit case.",
//                SaveFirstMessage = "Save the contract farming application first to manage the contract farming details.",
//                IconCss = "bi bi-tree"
//            };

//            return PartialView("~/Areas/Credit/Views/Shared/_CreditCaseCrudTab.cshtml", model);
//        }

//        [HttpGet]
//        public IActionResult LoadRateComponentsTab(long? creditCaseId, string? formSessionId = null)
//        {
//            var model = new CreditCaseCrudTabViewModel
//            {
//                CreditCaseId = creditCaseId > 0 ? creditCaseId : null,
//                FormSessionId = formSessionId,
//                FormId = 315,
//                ForeignAttributeName = "CreditCaseId",
//                PrimaryAttributeName = "CaseInterestTermId",
//                Title = "Rate Components",
//                Description = "Manage the rate components that apply to this contract farming loan.",
//                SaveFirstMessage = "Save the contract farming application first to manage rate components.",
//                IconCss = "bi bi-percent"
//            };

//            return PartialView("~/Areas/Credit/Views/Shared/_CreditCaseCrudTab.cshtml", model);
//        }

//        [HttpGet]
//        public IActionResult LoadInterestSubsidiesTab(long? creditCaseId, string? formSessionId = null)
//        {
//            var model = new CreditCaseCrudTabViewModel
//            {
//                CreditCaseId = creditCaseId > 0 ? creditCaseId : null,
//                FormSessionId = formSessionId,
//                FormId = 316,
//                ForeignAttributeName = "CreditCaseId",
//                PrimaryAttributeName = "CreditCaseInterestSubsidyId",
//                Title = "Interest Subsidies",
//                Description = "Manage the subsidy arrangements linked to this contract farming loan.",
//                SaveFirstMessage = "Save the contract farming application first to manage interest subsidies.",
//                IconCss = "bi bi-cash-coin"
//            };

//            return PartialView("~/Areas/Credit/Views/Shared/_CreditCaseCrudTab.cshtml", model);
//        }

//        private async Task<IActionResult> LoadContractFarming360Async(int? bankPartyId, long? creditCaseId, string? hubFormSessionId, string? childFormSessionId)
//        {
//            var (isValid, appForm, formContext, _, _, _) =
//                await AppAuthorizeHelper.ValidateChildReadPermissionAsync(
//                    HttpContext,
//                    menuService,
//                    userContextService,
//                    ContractFarmingFormId,
//                    hubFormSessionId,
//                    childFormSessionId);

//            if (!isValid || appForm == null || formContext == null)
//                return RedirectToAction("NoAccess", "Error");

//            CreditCaseLoanViewModel model;

//            if (creditCaseId.HasValue && creditCaseId.Value > 0)
//            {
//                var loanApplication = await creditCaseService.GetLoanApplicationByCaseIdAsync(creditCaseId.Value);
//                if (loanApplication?.CreditCase == null)
//                    return Content("Contract farming application was not found.");

//                model = loanApplication.LoanDetails ?? new CreditCaseLoanViewModel();
//                model.CreditCaseId = loanApplication.CreditCase.CreditCaseId;
//                model.CreditCaseViewModel = loanApplication.CreditCase;
//                ApplyDisbursementDescriptionMetadata(model.CreditCaseViewModel);
//            }
//            else
//            {
//                var framework = await frameworkService.GetFrameworkByPartyIdAsync(bankPartyId!.Value);

//                model = new CreditCaseLoanViewModel
//                {
//                    CreditCaseViewModel = new CreditCaseViewModel
//                    {
//                        BankPartyId = bankPartyId.Value,
//                        FrameworkId = framework?.FrameworkId,
//                        CurrencyCode = string.IsNullOrWhiteSpace(framework?.CurrencyCode) ? "EUR" : framework!.CurrencyCode,
//                        StageCode = "A0",
//                        StatusCode = string.Empty
//                    }
//                };
//            }

//            await PopulateLookupsAsync(model.CreditCaseViewModel?.ProductCode, model.CreditCaseViewModel?.StatusCode);

//            ViewBag.FormId = appForm.FormId;
//            ViewBag.FormSessionId = formContext.FormSessionId;
//            ViewBag.IsAdd = !creditCaseId.HasValue || creditCaseId.Value <= 0;
//            ViewBag.ShowContractFarmingTab = true;
//            ViewBag.ContractFarmingDetailsUrl = Url.Action("LoadContractFarmingDetailsTab", "CreditCaseContractFarming", new { area = "Credit" });
//            ViewBag.RateComponentsUrl = Url.Action("LoadRateComponentsTab", "CreditCaseContractFarming", new { area = "Credit" });
//            ViewBag.InterestSubsidiesUrl = Url.Action("LoadInterestSubsidiesTab", "CreditCaseContractFarming", new { area = "Credit" });

//            return PartialView("~/Areas/Credit/Views/ContractFarming/_CreditCaseContractFarming360.cshtml", model);
//        }

//        private async Task PopulateLookupsAsync(string? productCode, string? currentStatusCode)
//        {
//            var products = await creditProductService.SelectAllAsync();
//            var allowedProductCodes = (await contractFarmingSpecService.SelectAllAsync())
//                .Where(x => x.IsActive && x.IsApproved)
//                .Select(x => x.ProductCode)
//                .ToHashSet(StringComparer.OrdinalIgnoreCase);

//            ViewData["FK_ProductCode"] = products
//                .Where(x => x.IsActive && (x.IsApproved ?? false) && allowedProductCodes.Contains(x.ProductCode))
//                .OrderBy(x => x.ProductName)
//                .ToList();

//            ViewData["FK_CreditPurposeCode"] = await creditLoanPurposeService.SelectAllForDropdownAsync();
//            ViewData["FK_StatusCode"] = await LoadCaseStatusesAsync(productCode, currentStatusCode);
//            ViewData["FK_DisbursementBankCode"] = await LoadKosovoBanksAsync();
//        }

//        private Task<IEnumerable<object>> LoadCaseStatusesAsync(string? productCode, string? currentStatusCode)
//            => Task.FromResult(Enumerable.Empty<object>());

//        private async Task<IEnumerable<object>> LoadKosovoBanksAsync()
//        {
//            var activeBanks = (await bankService.SelectAllAsync())
//                .Where(x => x.IsActive && (x.IsApproved ?? false))
//                .OrderBy(x => x.BankName)
//                .ToList();

//            var kosovoBanks = activeBanks
//                .Where(x => string.Equals(x.CountryCode, "XK", StringComparison.OrdinalIgnoreCase)
//                         || string.Equals(x.CountryCode, "KS", StringComparison.OrdinalIgnoreCase))
//                .Cast<object>()
//                .ToList();

//            return kosovoBanks.Count > 0
//                ? kosovoBanks
//                : activeBanks.Cast<object>().ToList();
//        }

//        //private static void ApplyDisbursementDescriptionMetadata(CreditCaseViewModel? creditCase)
//        //{
//        //    if (creditCase == null)
//        //        return;

//        //    var parsed = CreditCaseDisbursementDescriptionHelper.Parse(creditCase.CaseDescription);
//        //    creditCase.CaseDescription = parsed.CleanCaseDescription;
//        //    creditCase.DisbursementBankCode = parsed.BankCode;
//        //    creditCase.DisbursementBankName = parsed.BankName;
//        //    creditCase.DisbursementAccountNumber = parsed.AccountNumber;
//        //}
//    }
//}
