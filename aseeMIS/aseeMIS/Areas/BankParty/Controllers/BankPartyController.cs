using dsBankParty.Core.DTOs;
using dsBankParty.Core.Services.Interfaces;
using dsBankParty.Core.ViewModels;
using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.BankParty.ViewModels;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.BankParty.Controllers
{
    [Area("BankParty")]
    public class BankPartyController(
        IBankPartyService<BankPartyViewModel> service,
        IBankPartyIndividualService<BankPartyIndividualViewModel> serviceIndividual,
        IBankPartyLegalEntityService<BankPartyLegalEntityViewModel> serviceLegalEntity,
        IAppFormMenuService menuService,
        IUserContextService userContextService,
        IAppFormPermissionService formAccessService) : Controller
    {
        [HttpGet]
        [AppAuthorize]
        public async Task<IActionResult> Index(int id = 13, CancellationToken cancellationToken = default)
        {
            var (isValid, appForm, formContext, _, _, _) =
                await AppAuthorizeHelper.ValidateReadPermissionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userContextService,
                    id,
                    EnumContextFormType.PARENT);

            if (!isValid || appForm == null || formContext == null)
                return RedirectToAction("NoAccess", "Error");

            var bankParties = (await service.SelectAllActiveForDropdownAsync(cancellationToken))
                .OrderBy(x => x.FullName)
                .ToList();

            ViewBag.FormId = formContext.FormId;
            ViewBag.FormSessionId = formContext.FormSessionId;
            Response.Headers["FormSessionId"] = formContext.FormSessionId;

            return View(bankParties);
        }

        /// <summary>
        /// Main autocomplete endpoint for the BankParty context widget.
        /// Returns a structured payload with all fields needed for display and selection.
        /// </summary>
        [HttpGet]
        [Produces("application/json")]

        public async Task<IActionResult> Search(string searchText)
        {
            var term = (searchText ?? string.Empty);
            var rows = await service.SearchAsync(term);
            return Json(rows);
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> SearchForProfile(string searchText, int take = 20)
        {
            var term = (searchText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(term))
                return Json(Array.Empty<object>());

            var rows = await service.SearchForProfileAsync(term, take);
            var mapped = rows.Select(x => new
            {
                x.BankPartyId,
                x.FullName,
                x.PartyCode,
                x.IdentificationNumber,
                x.IsIndividual,
                DisplayText = string.Join(" | ", new[]
                {
                    x.FullName,
                    string.IsNullOrWhiteSpace(x.PartyCode) ? null : $"Code: {x.PartyCode}",
                    string.IsNullOrWhiteSpace(x.IdentificationNumber) ? null : $"ID: {x.IdentificationNumber}"
                }.Where(v => !string.IsNullOrWhiteSpace(v)))
            });

            return Json(mapped);
        }
        public async Task<IActionResult> PartyProfile()
        {

            return PartialView("~/Areas/BankParty/Views/BankPartyProfile.cshtml");
        }



        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> SearchIndividuals(string searchText)
      {
            var term = (searchText ?? string.Empty).Trim();
            var rows = await serviceIndividual.SearchAsync(term);
            return Json(rows);
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> SearchLegalEntities(string searchText)
        {
            var term = (searchText ?? string.Empty).Trim();
            var rows = await serviceLegalEntity.SearchAsync(term);
            return Json(rows);
        }

        [HttpGet]
        public async Task<IActionResult> GetIndividualContextHeader(int bankPartyId, string languageCode = "en")
        {
            if (bankPartyId <= 0)
                return BadRequest();

            var dto = await serviceIndividual.GetContextHeaderAsync(bankPartyId, languageCode);
            if (dto == null)
                return NotFound();

            return PartialView("~/Areas/BankParty/Views/Shared/_BankPartyIndividualContextHeader.cshtml", dto);
        }

        [HttpGet]
        public async Task<IActionResult> GetLegalEntityContextHeader(int bankPartyId, string languageCode = "en")
        {
            if (bankPartyId <= 0)
                return BadRequest();

            var dto = await serviceLegalEntity.GetContextHeaderAsync(bankPartyId, languageCode);
            if (dto == null)
                return NotFound();

            return PartialView("~/Areas/BankParty/Views/Shared/_BankPartyLegalEntityContextHeader.cshtml", dto);
        }


        ///// <summary>
        ///// Returns the rendered context header partial for a selected BankParty.
        ///// Used by the widget when loading a page with a pre-selected party.
        ///// </summary>
        //[HttpGet]
        //public async Task<IActionResult> GetContextHeader(int bankPartyId)
        //{
        //    if (bankPartyId <= 0)
        //        return BadRequest();

        //    var vm = await service.GetByIdAsync(bankPartyId);
        //    if (vm is not BankPartyViewModel bp)
        //        return NotFound();

        //    var dto = new BankPartyContextDto
        //    {
        //        BankPartyId = bp.BankPartyId,
        //        FullName = bp.FullName ?? "",
        //        PartyCode = bp.PartyCode ?? "",
        //        IdentificationNumber = bp.IdentificationNumber,
        //        IsIndividual = bp.IsIndividual,
        //        IsResident = bp.IsResident,
        //        MunicipalityName = bp.ContactMunicipalityName,
        //        Email = bp.ContactEmail,
        //        Phone = bp.ContactPhone
        //    };

        //    var partial = dto.IsIndividual
        //        ? "~/Areas/BankParty/Views/Shared/_BankPartyIndividualContextHeader.cshtml"
        //        : "~/Areas/BankParty/Views/Shared/_BankPartyBusinessContextHeader.cshtml";

        //    return PartialView(partial, dto);
        //}




    }
}
