

using dsMIS.Core.Exceptions;
using dsMIS.Core.Services.Interfaces;
using dsCredit.Core.Services.Interfaces;
using dsCredit.Core.ViewModels;
using dsCreditDef.Core.DTOs;
using dsCreditDef.Core.Services.Interfaces;
using dsCreditProduct.Core.Services.Interfaces;
using dsDef.Core.Services.Interfaces;
using aseeMIS.Areas.Credit.Services;
using aseeMIS.Areas.Credit.ViewModels;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.Credit.Controllers
{
    [Area("Credit")]
    public class CreditCaseFactoringController(
        ICreditCaseService creditCaseService,
        ICreditCaseFactoringService factoringService,
        dsBankParty.Core.Services.Interfaces.IBankPartyService<dsBankParty.Core.ViewModels.BankPartyViewModel> bankPartyService,
        ICreditFrameworkService frameworkService,
        IAppFormMenuService menuService,
        IUserContextService userContextService,
        IAppFormPermissionService formAccessService,
        ILocalizationService localizationService,
        ICreditProductService creditProductService,
        ICreditDefFactoringTypeService factoringTypeService,
        ICreditDefFactoringTypePolicyService factoringTypePolicyService,
        ICreditProductSpecFactoringService factoringSpecService,
        dsWfe.Core.Services.Interfaces.IWfeWorkflowCreditFactoringExtensionService workflowService,
        dsWfe.Core.Services.Interfaces.IWfeDecisionReasonService decisionReasonService,
        ICreditCaseFactoringInvoicePricingService invoicePricingService) : Controller
    {
        private const int FactoringFormId = 334;
        private const int FactoringInvoiceFormId = 335;

        [HttpGet]
        public async Task<IActionResult> LoadFactoring(int bankPartyId, string? hubFormSessionId = null, string? childFormSessionId = null)
        {
            if (bankPartyId <= 0)
                return Content("Bank party is required before creating a factoring application.");

            return await LoadFactoring360Async(bankPartyId, null, hubFormSessionId, childFormSessionId);
        }

        [HttpGet]
        public async Task<IActionResult> LoadExistingFactoring(long creditCaseId, string? hubFormSessionId = null, string? childFormSessionId = null)
        {
            if (creditCaseId <= 0)
                return Content("Credit case is required.");

            return await LoadFactoring360Async(null, creditCaseId, hubFormSessionId, childFormSessionId);
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> GetNextCaseStatusesForDropdown(string productCode, string? currentStatusCode = null)
        {
            var statuses = await LoadWorkflowStatusesAsync(productCode, currentStatusCode);
            return Ok(new { statuses = ToCaseStatuses(statuses), workflowSteps = ToCaseWorkflowSteps(statuses) });
        }

        [HttpGet]
        public IActionResult LoadFactoringInvoicesTab(long? creditCaseId, string? formSessionId = null)
        {
            var model = new CreditCaseCrudTabViewModel
            {
                CreditCaseId = creditCaseId > 0 ? creditCaseId : null,
                FormSessionId = formSessionId,
                FormId = FactoringInvoiceFormId,
                ForeignAttributeName = nameof(CreditCaseFactoringInvoiceViewModel.CreditCaseId),
                PrimaryAttributeName = nameof(CreditCaseFactoringInvoiceViewModel.FactoringInvoiceId),
                Title = "Imported Invoices",
                Description = "Manage factoring invoices linked to this credit case.",
                SaveFirstMessage = "Save the factoring application first to manage invoices.",
                IconCss = "bi bi-table"
            };

            return PartialView("~/Areas/Credit/Views/Shared/_CreditCaseCrudTab.cshtml", model);
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> GetPoliciesByFactoringType(string? factoringTypeCode)
        {
            var policies = await factoringTypePolicyService.SelectPoliciesByFactoringTypeAsync(factoringTypeCode);
            return Ok(policies);
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> GetCounterpartyBankParties(CancellationToken cancellationToken = default)
        {
            var rows = (await bankPartyService.SelectAllActiveForDropdownAsync(cancellationToken))
                .OrderBy(x => x.FullName)
                .Select(x => new
                {
                    x.BankPartyId,
                    x.FullName,
                    x.DisplayText,
                    x.PartyCode,
                    x.IdentificationNumber,
                    x.IsIndividual
                });

            return Ok(rows);
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> GetInvoicePricingContext(
            long creditCaseId,
            CancellationToken cancellationToken = default)
        {
            var result = await invoicePricingService.GetPricingContextAsync(creditCaseId, cancellationToken);

            if (result == null)
                return NotFound(new { message = "Factoring pricing context was not found for this credit case." });

            return Ok(result);
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> CalculateInvoicePricing(
            long creditCaseId,
            decimal invoiceAmount,
            decimal? requestedAmount,
            decimal? dilutionAmount,
            DateOnly? disbursementDate,
            DateOnly bankDueDate,
            CancellationToken cancellationToken = default)
        {
            var result = await invoicePricingService.CalculateAsync(
                creditCaseId,
                invoiceAmount,
                requestedAmount,
                dilutionAmount,
                disbursementDate,
                bankDueDate,
                cancellationToken);

            if (result == null)
                return NotFound(new { message = "No matching factoring price list item was found for this invoice." });

            return Ok(result);
        }

        [HttpGet]
        public IActionResult LoadInterestSubsidiesTab(long? creditCaseId, string? formSessionId = null)
        {
            var model = new CreditCaseCrudTabViewModel
            {
                CreditCaseId = creditCaseId > 0 ? creditCaseId : null,
                FormSessionId = formSessionId,
                FormId = 304,
                ForeignAttributeName = "CreditCaseId",
                PrimaryAttributeName = "CreditCaseInterestSubsidyId",
                Title = "Interest Subsidies",
                Description = "Manage the subsidy arrangements linked to this factoring credit case.",
                SaveFirstMessage = "Save the factoring application first to manage interest subsidies.",
                IconCss = "bi bi-cash-coin"
            };

            return PartialView("~/Areas/Credit/Views/Shared/_CreditCaseCrudTab.cshtml", model);
        }

        private async Task<IActionResult> LoadFactoring360Async(int? bankPartyId, long? creditCaseId, string? hubFormSessionId, string? childFormSessionId)
        {
            if (await menuService.FindByFormIdAsync(FactoringFormId) == null)
                return NotFound($"Factoring form {FactoringFormId} is not available in the active menu configuration. Check AppForms and publish the menu or restart the application after updating its configuration.");

            var (isValid, appForm, formContext, _, _, _) =
                await AppAuthorizeHelper.ValidateChildReadPermissionAsync(
                    HttpContext,
                    menuService,
                    userContextService,
                    FactoringFormId,
                    hubFormSessionId,
                    childFormSessionId);

            if (!isValid || appForm == null || formContext == null)
                return StatusCode(StatusCodes.Status403Forbidden, "No access to factoring application.");

            CreditCaseFactoringApplicationViewModel model;

            if (creditCaseId.HasValue && creditCaseId.Value > 0)
            {
                var creditCase = await creditCaseService.GetByIdAsync(creditCaseId.Value);
                var factoringDetails = await factoringService.GetByIdAsync(creditCaseId.Value);
                model = new CreditCaseFactoringApplicationViewModel
                {
                    CreditCase = creditCase,
                    FactoringDetails = factoringDetails
                };

                if (model.CreditCase.CreditCaseId <= 0)
                    return Content("Factoring application was not found.");
            }
            else
            {
                var framework = await frameworkService.GetFrameworkByPartyIdAsync(bankPartyId!.Value);

                model = new CreditCaseFactoringApplicationViewModel
                {
                    CreditCase = new CreditCaseViewModel
                    {
                        BankPartyId = bankPartyId.Value,
                        FrameworkId = framework?.FrameworkId,
                        CurrencyCode = string.IsNullOrWhiteSpace(framework?.CurrencyCode) ? "EUR" : framework!.CurrencyCode,
                        StageCode = "A0",
                        CreditStatusCode = string.Empty,
                        StartDateRequested = DateOnly.FromDateTime(DateTime.Today),
                        MaturityDateRequested = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
                        CaseNumber = string.Empty
                    },
                    FactoringDetails = new CreditCaseFactoringViewModel
                    {
                        IsActive = true
                    }
                };
            }

            model.FactoringDetails ??= new CreditCaseFactoringViewModel
            {
                CreditCaseId = model.CreditCase.CreditCaseId,
                IsActive = true
            };
            model.FactoringDetails.CreditCaseViewModel = model.CreditCase;

            var bankParty = await bankPartyService.GetByIdAsync(model.CreditCase.BankPartyId);
            model.CreditCase.IsIndividual = bankParty.IsIndividual;
            await PopulateLookupsAsync(bankParty.IsIndividual, model.CreditCase.ProductCode, model.CreditCase.CreditStatusCode, model.FactoringDetails.FactoringTypeCode);

            ViewBag.FormId = appForm.FormId;
            ViewBag.FormSessionId = formContext.FormSessionId;
            ViewBag.IsAdd = !creditCaseId.HasValue || creditCaseId.Value <= 0;
            ViewBag.FactoringInvoicesUrl = Url.Action("LoadFactoringInvoicesTab", "CreditCaseFactoring", new { area = "Credit" });
            ViewBag.InterestSubsidiesUrl = Url.Action("LoadInterestSubsidiesTab", "CreditCaseFactoring", new { area = "Credit" });

            return PartialView("~/Areas/Credit/Views/Factoring/_CreditCaseFactoring360.cshtml", model);
        }

        private async Task PopulateLookupsAsync(bool isIndividual, string? productCode, string? currentStatusCode, string? factoringTypeCode)
        {
            var products = await creditProductService.SelectAllAsync();
            var allowedProductCodes = (await factoringSpecService.SelectAllAsync())
                .Where(x => x.IsActive == true && x.IsApproved == true)
                .Select(x => x.ProductCode)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            ViewData["FK_ProductCode"] = products
                .Where(x => x.IsActive && (x.IsApproved ?? false) && allowedProductCodes.Contains(x.ProductCode))
                .Where(x => x.IsIndividual == null || x.IsIndividual == isIndividual)
                .OrderBy(x => x.ProductName)
                .ToList();

            var statuses = await LoadWorkflowStatusesAsync(productCode, currentStatusCode);
            ViewData["FK_StatusCode"] = ToCaseStatuses(statuses);
            ViewData["CaseWorkflowSteps"] = ToCaseWorkflowSteps(statuses);
            ViewData["FK_DecisionReasonCode"] = (await decisionReasonService.SelectAllActiveForDropdownAsync())
                .Select(x => new { CreditDecisionReasonCode = x.DecisionReasonCode, CreditDecisionReasonName = x.DecisionReasonName }).ToList();
            ViewData["FK_FactoringTypeCode"] = (await factoringTypeService.SelectAllActiveForDropdownAsync())
                .OrderBy(x => x.FactoringTypeName)
                .Select(x => new
                {
                    x.FactoringTypeCode,
                    x.FactoringTypeName
                })
                .ToList();
            ViewData["FK_FactoringPolicyCode"] = await LoadFactoringPoliciesAsync(factoringTypeCode);
        }

        private async Task<IReadOnlyList<dsWfe.Core.DTOs.WfeWorkflowStatusOptionDto>> LoadWorkflowStatusesAsync(string? productCode, string? currentStatusCode)
        {
            if (string.IsNullOrWhiteSpace(productCode)) return [];
            var product = (await creditProductService.SelectAllAsync()).FirstOrDefault(x => x.ProductCode == productCode);
            if (string.IsNullOrWhiteSpace(product?.WorkflowCode)) return [];
            return await workflowService.GetAvailableStatusesAsync(product.WorkflowCode, currentStatusCode, HttpContext.RequestAborted);
        }

        private async Task<IEnumerable<object>> LoadCaseWorkflowStepsAsync(string? productCode)
            => ToCaseWorkflowSteps(await LoadWorkflowStatusesAsync(productCode, null));

        private static IEnumerable<object> ToCaseStatuses(
            IEnumerable<dsWfe.Core.DTOs.WfeWorkflowStatusOptionDto> statuses)
            => statuses.Select(x => new
            {
                CaseStatusCode = x.StatusCode,
                CaseStatusName = x.StatusName,
                x.CategoryStatusCode,
                x.Action,
                x.IsCurrent,
                x.SetEntityActive,
                x.RequiresDecisionReason,
                x.AllowedDecisionReasonCodes
            }).Cast<object>().ToList();

        private static IEnumerable<object> ToCaseWorkflowSteps(
            IEnumerable<dsWfe.Core.DTOs.WfeWorkflowStatusOptionDto> statuses)
            => statuses.Select(x => new
            {
                CaseStatusCode = x.StatusCode,
                x.CategoryStatusCode,
                x.StepCode,
                x.IsEditable,
                x.AllowsStatusChange,
                x.IsTerminal,
                x.SetEntityActive,
                x.SortOrder
            }).Cast<object>().ToList();

        private async Task<List<object>> LoadFactoringPoliciesAsync(string? factoringTypeCode)
        {
            var policies = await factoringTypePolicyService.SelectPoliciesByFactoringTypeAsync(factoringTypeCode);
            return policies
                .Select(x => new
                {
                    x.FactoringTypeCode,
                    FactoringPolicyCode = x.FactoringPolicyCode,
                    FactoringPolicyName = x.FactoringPolicyName,
                    x.RiskBearerCode,
                    x.IsDefault,
                    x.NotificationRequired,
                    Description = x.FactoringPolicyName
                })
                .Cast<object>()
                .ToList();
        }
    }
}
