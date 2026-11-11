using dsMIS.Core.Enums;

using dsMIS.Core.Services.Interfaces;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace aseeMIS.Areas.Credit.Controllers
{
    [Area("Credit")]
    public class CreditController(IAppFormMenuService menuService,IUserContextService userContextService,IAppFormPermissionService formAccessService) : Controller
    {
        public async Task<IActionResult> IndexAsync()
        {
            var (isValid, appForm, formContext, _, _, _) = await AppAuthorizeHelper.ValidateReadPermissionAsync(HttpContext,
                                                          formAccessService,
                                                          menuService,
                                                          userContextService,
                                                          31,
                                                          EnumContextFormType.PARENT);

            if (!isValid || appForm == null || formContext == null)
                return RedirectToAction("NoAccess", "Error");

            ViewBag.FormSessionId = formContext.FormSessionId;
            ViewBag.FormId = appForm.FormId;
            ViewBag.ChildFormSessionsJson = JsonSerializer.Serialize(
                await CreateCreditHubChildSessionsAsync(appForm, formContext.FormSessionId));

            return View("~/Areas/Credit/Views/PartyCreditHub.cshtml");
        }

        [HttpGet]
        public IActionResult LoadImportedDocuments(int bankPartyId, string? formSessionId)
        {
            if (bankPartyId <= 0)
                return BadRequest("A valid Bank Party is required.");

            return PartialView(
                "~/Areas/Credit/Views/_PartyImportedDocuments.cshtml",
                new ViewModels.PartyImportedDocumentsViewModel
                {
                    BankPartyId = bankPartyId,
                    FormSessionId = formSessionId
                });
        }

        private async Task<IReadOnlyDictionary<int, string>> CreateCreditHubChildSessionsAsync(dsMIS.Core.DTOs.AppFormMenuDto hubForm, string parentFormSessionId)
        {
            var childFormIds = await ResolveCreditHubChildFormIdsAsync(hubForm);

            return await AppAuthorizeHelper.CreateChildSessionMapFromParentAsync(
                HttpContext,
                menuService,
                userContextService,
                parentFormSessionId,
                childFormIds);
        }

        private async Task<IReadOnlyList<int>> ResolveCreditHubChildFormIdsAsync(dsMIS.Core.DTOs.AppFormMenuDto hubForm)
        {
            const int importedDocumentsFormId = 102;

            if (!string.IsNullOrWhiteSpace(hubForm.FlowFormChildIds))
            {
                return hubForm.FlowFormChildIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(x => int.TryParse(x, out var id) ? id : 0)
                    .Where(x => x > 0)
                    .Append(importedDocumentsFormId)
                    .Distinct()
                    .ToList();
            }

            var allForms = await menuService.SelectAllAsync();
            var detailRoot = allForms
                .Where(x => x.FormParentId == hubForm.FormParentId)
                .OrderBy(x => x.FormOrderId)
                .ThenBy(x => x.FormId)
                .FirstOrDefault(x =>
                    string.Equals(x.FormType, "Menu", StringComparison.OrdinalIgnoreCase) &&
                    x.FormTitle.Contains("Detail", StringComparison.OrdinalIgnoreCase));

            if (detailRoot == null)
                return Array.Empty<int>();

            var descendants = await menuService.SelectDescendantsAsync(detailRoot.FormId);

            return descendants
                .Where(x => string.Equals(x.FormType, "CRUD", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.FormId)
                .Append(importedDocumentsFormId)
                .Distinct()
                .ToList();
        }
    }
}
