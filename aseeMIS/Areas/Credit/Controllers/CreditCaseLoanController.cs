using dsBankParty.Core.Services;
using dsBankParty.Core.Services.Interfaces;
using dsBankParty.Core.ViewModels;
using dsMIS.Core.DTOs;
using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using dsCredit.Core.Services.Interfaces;
using dsCredit.Core.ViewModels;
using dsCreditDef.Core.DTOs;
using dsCreditDef.Core.Services.Interfaces;
using dsCreditProduct.Core.Services.Interfaces;
using dsDef.Core.Services.Interfaces;
using aseeMIS.Areas.Credit.Services.Interfaces;
using aseeMIS.Areas.Credit.ViewModels;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.Credit.Controllers
{
    [Area("Credit")]
    public class CreditCaseLoanController(
        ICreditCaseLoanService creditCaseLoanService,
        ICreditCaseLoanAmortPlanService creditCaseLoanAmortPlanService,
        dsCredit.Core.Repositories.Interfaces.ICreditCaseLoanRepository creditCaseLoanRepository,
        ICreditFrameworkService frameworkService,
        IAppFormMenuService menuService,
        IUserContextService userContextService,
        IAppFormPermissionService formAccessService,
        IDefCurrencyService currencyService,
        IDefBankService bankService,
        ICreditProductService creditProductService,
        ICreditProductSpecLoanService creditProductSpecLoanService,
        IBankPartyService<BankPartyViewModel> bankPartyService,
        ICreditDefPurposeservice creditLoanPurposeService,
        ICreditDefAmortizationTemplateService amortizationTemplateService,
        dsAppMng.Core.Services.Interfaces.IAppCreditConstantService appCreditConstantService,
        dsWfe.Core.Services.Interfaces.IWfeDecisionReasonService decisionReasonService,
        dsWfe.Core.Services.Interfaces.IWfeWorkflowCreditLoanExtensionService workflowCreditLoanExtensionService) : Controller
    {
        private const int LoanFormId = 332;

        // ADD flow:
        // 1) Form posts CreditCaseLoan + CreditCase + CreditCaseAmortPlanVersion (engine settings).
        // 2) RegenerateAsync reads Requested terms, builds installments in-memory on the view model.
        // 3) AddAsync persists CreditCase/CreditCaseLoan, then AfterInsertAsync saves version + installments.
        [HttpPost]
        [Produces("application/json")]
        public async Task<IActionResult> Add([FromBody] CreditCaseLoanViewModel model)
        {
            model ??= new CreditCaseLoanViewModel();

            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();
            var (isValid, appForm, formContext, _, _, _) =
                await AppAuthorizeHelper.ValidateActionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userContextService,
                    formSessionId ?? string.Empty,
                    EumFormAccessAction.ADD);

            if (!isValid || appForm == null || formContext == null)
                return Ok(new { success = false, message = "No access to add a loan application." });

            var context = BuildContextSession(formContext, appForm);
            model.CreditCase ??= new CreditCaseViewModel();
            model.CreditCase.LastUpdatedUserId = context.UserId;

            try
            {
                model.IsWorkflowStepEditable = true;

                await creditCaseLoanAmortPlanService.RegenerateAsync(
                    model,
                    context.Username,
                    HttpContext.RequestAborted);

                // One Add only. CreditCaseLoanService.AfterInsertAsync persists
                // the version and installments using the generated CreditCaseId.
                await creditCaseLoanService.AddAsync(model, context, HttpContext.RequestAborted);

                return Ok(new
                {
                    success = true,
                    message = "Loan application and amortization plan were saved successfully.",
                    creditCaseId = model.CreditCaseId,
                    caseNumber = model.CreditCase.CaseNumber,
                    updatedRow = model
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
        }

        // UPDATE flow:
        // 1) Form posts IsWorkflowStepEditable (true=Requested, false=Approved) with the loan VM.
        // 2) Controller ensures the flag is set, then RegenerateAsync picks the active term set.
        // 3) UpdateAsync persists loan/case; ReplaceAmortizationPlanAsync replaces installments.
        [HttpPost]
        [Produces("application/json")]
        public async Task<IActionResult> Update([FromBody] CreditCaseLoanViewModel model)
        {
            if (model == null || model.CreditCaseId <= 0)
                return Ok(new { success = false, message = "Credit case is required for update." });

            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();
            var (isValid, appForm, formContext, _, _, _) =
                await AppAuthorizeHelper.ValidateActionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userContextService,
                    formSessionId ?? string.Empty,
                    EumFormAccessAction.UPDATE);

            if (!isValid || appForm == null || formContext == null)
                return Ok(new { success = false, message = "No access to update the loan application." });

            var context = BuildContextSession(formContext, appForm);
            model.CreditCase ??= new CreditCaseViewModel();
            model.CreditCase.CreditCaseId = model.CreditCaseId;
            model.CreditCase.LastUpdatedUserId = context.UserId;

            try
            {
                await EnsureWorkflowStepEditableAsync(model, HttpContext.RequestAborted);

                await creditCaseLoanAmortPlanService.RegenerateAsync(
                    model,
                    context.Username,
                    HttpContext.RequestAborted);

                await creditCaseLoanService.UpdateAsync(model, context, HttpContext.RequestAborted);

                var isDisbursement = string.Equals(
                    model.CreditCase.CreditStatusCode?.Trim(),
                    "DISBURSED",
                    StringComparison.OrdinalIgnoreCase);

                return Ok(new
                {
                    success = true,
                    message = isDisbursement
                        ? "Loan was updated and the disbursement was posted successfully."
                        : "Loan application and amortization plan were updated successfully.",
                    creditCaseId = model.CreditCaseId,
                    caseNumber = model.CreditCase.CaseNumber,
                    updatedRow = model
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> LoadLoan(int bankPartyId, string? hubFormSessionId = null, string? childFormSessionId = null)
        {
            if (bankPartyId <= 0)
                return Content("Bank party is required before creating a loan application.");

            return await LoadLoan360Async(bankPartyId, null, hubFormSessionId, childFormSessionId);
        }

        [HttpGet]
        public async Task<IActionResult> LoadExistingLoan(long creditCaseId, string? hubFormSessionId = null, string? childFormSessionId = null)
        {
            if (creditCaseId <= 0)
                return Content("Credit case is required.");

            return await LoadLoan360Async(null, creditCaseId, hubFormSessionId, childFormSessionId);
        }

        private async Task<IActionResult> LoadLoan360Async(int? bankPartyId, long? creditCaseId, string? hubFormSessionId, string? childFormSessionId)
        {
            var (isValid, appForm, formContext, _, _, _) =
                await AppAuthorizeHelper.ValidateChildReadPermissionAsync(
                    HttpContext,
                    menuService,
                    userContextService,
                    LoanFormId,
                    hubFormSessionId,
                    childFormSessionId);

            if (!isValid || appForm == null || formContext == null)
                return StatusCode(StatusCodes.Status403Forbidden, "No access to loan application.");

            CreditCaseLoanViewModel model;
            int resolvedBankPartyId;

            // 🔹 EDIT MODE
            if (creditCaseId.HasValue && creditCaseId.Value > 0)
            {
                var loadedModel = await creditCaseLoanRepository.SelectByIdAsync(creditCaseId.Value, HttpContext.RequestAborted);
                if (loadedModel?.CreditCase == null)
                    return Content("Loan application was not found.");

                model = loadedModel;
                resolvedBankPartyId = model.CreditCase.BankPartyId;
            }
            // 🔹 NEW MODE
            else
            {
                if (!bankPartyId.HasValue)
                    return Content("BankPartyId is required.");

                resolvedBankPartyId = bankPartyId.Value;

                var framework = await frameworkService.GetFrameworkByPartyIdAsync(resolvedBankPartyId);

                model = new CreditCaseLoanViewModel
                {
                    CreditCase = new CreditCaseViewModel
                    {
                        BankPartyId = resolvedBankPartyId,
                        FrameworkId = framework?.FrameworkId,
                        CurrencyCode = string.IsNullOrWhiteSpace(framework?.CurrencyCode) ? "EUR" : framework!.CurrencyCode,
                        StageCode = "A0",
                        CreditStatusCode = string.Empty
                    }
                };
            }

            // 🔥 GET BANK PARTY (CRITICAL STEP)
            var bankParty = await bankPartyService.GetByIdAsync(resolvedBankPartyId);

            if (bankParty == null)
                return Content("BankParty not found.");

            model.CreditCase.IsIndividual = bankParty.IsIndividual;

            // 🔥 FILTERED LOOKUPS
            await PopulateLookupsAsync(
                bankParty.IsIndividual,
                model.CreditCase?.ProductCode,
                model.CreditCase?.CreditStatusCode,
                formContext.LanguageCode);

            ViewBag.FormId = appForm.FormId;
            ViewBag.FormSessionId = formContext.FormSessionId;
            ViewBag.IsAdd = !creditCaseId.HasValue || creditCaseId.Value <= 0;
            ViewBag.IsFromCreditHub = true;

            model.IsWorkflowStepEditable = await ResolveIsWorkflowStepEditableAsync(model, HttpContext.RequestAborted);

            return PartialView("~/Areas/Credit/Views/Loan/_CreditCaseLoan360.cshtml", model);
        }

        [HttpGet]
        public IActionResult LoadRateComponentsTab(long? creditCaseId, string? formSessionId = null)
        {
            var model = new CreditCaseCrudTabViewModel
            {
                CreditCaseId = creditCaseId > 0 ? creditCaseId : null,
                FormSessionId = formSessionId,
                FormId = 315,
                ForeignAttributeName = "CreditCaseId",
                PrimaryAttributeName = "CaseInterestTermId",
                Title = "Rate Components",
                Description = "Manage the loan rate components that apply to this credit case.",
                SaveFirstMessage = "Save the loan application first to manage rate components.",
                IconCss = "bi bi-percent"
            };

            return PartialView("~/Areas/Credit/Views/Shared/_CreditCaseCrudTab.cshtml", model);
        }

        [HttpGet]
        public IActionResult LoadInterestSubsidiesTab(long? creditCaseId, string? formSessionId = null)
        {
            var model = new CreditCaseCrudTabViewModel
            {
                CreditCaseId = creditCaseId > 0 ? creditCaseId : null,
                FormSessionId = formSessionId,
                FormId = 316,
                ForeignAttributeName = "CreditCaseId",
                PrimaryAttributeName = "CreditCaseInterestSubsidyId",
                Title = "Interest Subsidies",
                Description = "Manage the subsidy arrangements linked to this loan credit case.",
                SaveFirstMessage = "Save the loan application first to manage interest subsidies.",
                IconCss = "bi bi-cash-coin"
            };

            return PartialView("~/Areas/Credit/Views/Shared/_CreditCaseCrudTab.cshtml", model);
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> GetProductsForBankParty(int bankPartyId)
        {
            if (bankPartyId <= 0)
                return Ok(Array.Empty<object>());

            var bankParty = await bankPartyService.GetByIdAsync(bankPartyId);
            if (bankParty == null)
                return Ok(Array.Empty<object>());

            var products = await creditProductSpecLoanService.SelectCreditProductLoansAsync(
                bankParty.IsIndividual);

            return Ok(products);
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> GetDecisionReasons(CancellationToken cancellationToken = default)
        {
            var reasons = await decisionReasonService.SelectAllActiveForDropdownAsync(cancellationToken);
            return Ok(reasons.Select(x => new
            {
                CreditDecisionReasonCode = x.DecisionReasonCode,
                CreditDecisionReasonName = x.DecisionReasonName
            }));
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> GetNextCaseStatusesForDropdown(
            string workflowCode,
            string? currentStatusCode = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(workflowCode))
                return Ok(new { statuses = Array.Empty<object>(), workflowSteps = Array.Empty<object>() });

            var statuses = await workflowCreditLoanExtensionService.GetAvailableStatusesAsync(
                workflowCode,
                currentStatusCode,
                cancellationToken);

            return Ok(new
            {
                statuses = ToCaseStatuses(statuses),
                workflowSteps = ToCaseWorkflowSteps(statuses)
            });
        }

        private async Task PopulateLookupsAsync(
            bool isIndividual,
            string? productCode,
            string? currentStatusCode,
            string languageCode)
        {
            var products = await creditProductSpecLoanService.SelectCreditProductLoansAsync(isIndividual);
            var workflowCode = products
                .FirstOrDefault(x => string.Equals(x.ProductCode, productCode, StringComparison.OrdinalIgnoreCase))
                ?.WorkflowCode;
            var statuses = string.IsNullOrWhiteSpace(workflowCode)
                ? []
                : (await workflowCreditLoanExtensionService.GetAvailableStatusesAsync(
                    workflowCode,
                    currentStatusCode,
                    HttpContext.RequestAborted)).ToList();

            ViewData["FK_CurrencyCode"] = await currencyService.SelectAllForDropdownAsync();
            ViewData["FK_ProductCode"] = products;
            ViewData["FK_CreditPurposeCode"] = await creditLoanPurposeService.SelectAllForDropdownAsync();
            ViewData["FK_StatusCode"] = ToCaseStatuses(statuses);
            ViewData["CaseWorkflowSteps"] = ToCaseWorkflowSteps(statuses);
            ViewData["FK_DisbursementBankCode"] = await LoadKosovoBanksAsync();
            ViewData["FK_AmortizationTemplateCode"] = await LoadAmortizationTemplatesAsync();
            ViewData["FK_OtherIncomeTypeId"] = (await appCreditConstantService
                    .SelectForDropdownAsync("BankPartyOtherIncomeType", languageCode))
                .Select(x => new
                {
                    OtherIncomeTypeId = int.TryParse(x.ConstantKey, out var id) ? (int?)id : null,
                    OtherIncomeTypeName = x.ConstantValue
                })
                .Where(x => x.OtherIncomeTypeId.HasValue)
                .ToList();
        }

        private async Task<IEnumerable<object>> LoadAmortizationTemplatesAsync()
        {
            var templates = await amortizationTemplateService.SelectAllAsync();
            var options = new List<object>
            {
                new { TemplateCode = string.Empty, TemplateName = "(No Template - Custom Parameters)" }
            };

            options.AddRange(templates
                .Where(x => x.IsActive && (x.IsApproved ?? false))
                .OrderBy(x => x.TemplateName)
                .Select(x => (object)new
                {
                    TemplateCode = x.TemplateCode,
                    TemplateName = x.TemplateName
                }));

            return options;
        }

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
                x.SortOrder
            }).Cast<object>().ToList();

        private async Task<IEnumerable<object>> LoadKosovoBanksAsync()
        {
            return (await bankService.SelectAllAsync())
                .Where(x => x.IsActive
                         && (x.IsApproved ?? false)
                         && string.Equals(x.CountryCode, "XK", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.BankName)
                .Cast<object>()
                .ToList();
        }

        private async Task EnsureWorkflowStepEditableAsync(
            CreditCaseLoanViewModel model,
            CancellationToken cancellationToken)
        {
            if (model.IsWorkflowStepEditable.HasValue)
                return;

            model.IsWorkflowStepEditable = await ResolveIsWorkflowStepEditableAsync(model, cancellationToken);
        }

        private async Task<bool> ResolveIsWorkflowStepEditableAsync(
            CreditCaseLoanViewModel model,
            CancellationToken cancellationToken)
        {
            var creditCase = model.CreditCase;
            if (creditCase == null)
                return true;

            var products = await creditProductSpecLoanService.SelectCreditProductLoansAsync(creditCase.IsIndividual);
            var workflowCode = products
                .FirstOrDefault(x => string.Equals(x.ProductCode, creditCase.ProductCode, StringComparison.OrdinalIgnoreCase))
                ?.WorkflowCode;

            if (string.IsNullOrWhiteSpace(workflowCode))
            {
                return string.Equals(creditCase.CreditStatusCode, "DRAFT", StringComparison.OrdinalIgnoreCase);
            }

            var statuses = await workflowCreditLoanExtensionService.GetAvailableStatusesAsync(
                workflowCode,
                creditCase.CreditStatusCode,
                cancellationToken);

            var currentStep = statuses.FirstOrDefault(x =>
                string.Equals(x.StatusCode, creditCase.CreditStatusCode, StringComparison.OrdinalIgnoreCase));

            if (currentStep != null)
                return currentStep.IsEditable;

            return string.Equals(creditCase.CreditStatusCode, "DRAFT", StringComparison.OrdinalIgnoreCase);
        }

        private static ContextSessionDto BuildContextSession(
            ContextSessionDto formContext,
            AppFormMenuDto appForm)
        {
            return new ContextSessionDto
            {
                FormId = LoanFormId,
                UserId = formContext.UserId,
                Username = formContext.Username,
                RoleNames = formContext.RoleNames,
                LanguageCode = formContext.LanguageCode,
                IsAutoApprove = appForm.IsAutoApprove,
                Permissions = formContext.Permissions
            };
        }
    }
}
