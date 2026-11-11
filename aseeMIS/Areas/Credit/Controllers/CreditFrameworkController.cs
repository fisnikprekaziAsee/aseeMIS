using dsMIS.Core.DTOs;

using dsMIS.Core.Exceptions;
using dsMIS.Core.Services.Interfaces;
using dsCredit.Core.DTOs;
using dsCredit.Core.Services.Interfaces;
using dsCredit.Core.ViewModels;
using dsDef.Core.Services.Interfaces;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using CoreBankPartyService = dsBankParty.Core.Services.Interfaces.IBankPartyService<dsBankParty.Core.ViewModels.BankPartyViewModel>;

namespace aseeMIS.Areas.Credit.Controllers
{
    [Area("Credit")]
    public class CreditFrameworkController(
        ICreditFrameworkService frameworkService,
        ICreditCaseService creditCaseService,
        ILocalizationService localizationService,
        IAppFormMenuService menuService,
        IUserContextService userContextService,
        IAppFormPermissionService formAccessService,
        ICreditFrameworkFacilityService facilityService,
        IDefCurrencyService currencyService,
        CoreBankPartyService bankPartyService,
        dsWfe.Core.Services.Interfaces.IWfeWorkflowCreditFrameworkExtensionService workflowCreditFrameworkExtensionService) : Controller
    {
        private const int FrameworkFormId = 321;

        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> Search(
            string searchText,
            int take = 20,
            CancellationToken cancellationToken = default)
        {
            var term = (searchText ?? string.Empty).Trim();
            if (term.Length < 2)
                return Json(Array.Empty<CreditFrameworkDto>());

            return Json(await frameworkService.SearchAsync(term, take, cancellationToken));
        }

        [HttpGet]
        public async Task<IActionResult> LoadFramework(int bankPartyId, string? hubFormSessionId = null, string? childFormSessionId = null)
        {
            var (isValid, appForm, formContext, _, _, _) = await AppAuthorizeHelper.ValidateChildReadPermissionAsync(
                HttpContext,
                menuService,
                userContextService,
                FrameworkFormId,
                hubFormSessionId,
                childFormSessionId);

            if (!isValid || appForm == null || formContext == null)
                return RedirectToAction("NoAccess", "Error");

            var existing = await frameworkService.GetFrameworkByPartyIdAsync(bankPartyId);
            var bankParty = await bankPartyService.GetByIdAsync(bankPartyId);
            if (bankParty == null)
                return NotFound($"Bank Party with id {bankPartyId} was not found.");

            var workflowStatuses = await workflowCreditFrameworkExtensionService
                .GetAvailableStatusesAsync(
                    bankParty.IsIndividual,
                    existing?.FrameworkStatusCode,
                    cancellationToken: HttpContext.RequestAborted);

            var initialStatusCode = workflowStatuses.FirstOrDefault()?.StatusCode;
            if (existing == null && string.IsNullOrWhiteSpace(initialStatusCode))
                throw new InvalidOperationException(
                    $"No initial WFE status is configured for Credit Framework ({(bankParty.IsIndividual ? "Individual" : "Legal")}).");

            var model = existing ?? new CreditFrameworkViewModel
            {
                BankPartyId = bankPartyId,
                FrameworkNo = "DRAFT",
                FrameworkTitle = "Marrëveshje gjenerale me Kontratë",
                CurrencyCode = "EUR",
                FrameworkStatusCode = initialStatusCode!,
                HasGeneralTerms = true,
                AgreementTermMonths = 12
            };

            await LoadFrameworkLookupsAsync(workflowStatuses);

            ViewBag.FormId = appForm.FormId;
            ViewBag.FormSessionId = formContext.FormSessionId;
            ViewBag.IsAdd = model.FrameworkId == 0;
            ViewBag.IsFromCreditHub = true;
            ViewBag.RenderEjsScripts = true;

            return PartialView("~/Areas/Credit/Views/Framework/_CreditFrameworkMasterPage.cshtml", model);
        }

        private async Task LoadFrameworkLookupsAsync(
            IReadOnlyList<dsWfe.Core.DTOs.WfeWorkflowStatusOptionDto> workflowStatuses)
        {
            ViewData["FK_CurrencyCode"] = await currencyService.SelectAllForDropdownAsync();
            ViewData["FK_FrameworkDecisionReasonCode"] = Array.Empty<object>();
            ViewData["FK_FrameworkStatusCode"] = workflowStatuses.Select(x => new
            {
                FrameworkStatusCode = x.StatusCode,
                FrameworkStatusName = x.StatusName,
                x.Action,
                x.IsCurrent,
                x.SetEntityActive,
                x.RequiresDecisionReason,
                x.AllowedDecisionReasonCodes
            }).ToList();
        }

        private static ContextSessionDto BuildContextSession(ContextSessionDto formContext, AppFormMenuDto appForm)
        {
            return new ContextSessionDto
            {
                FormId = formContext.FormId,
                UserId = formContext.UserId,
                RoleNames = formContext.RoleNames,
                LanguageCode = formContext.LanguageCode,
                IsAutoApprove = appForm.IsAutoApprove
            };
        }
    }
}
