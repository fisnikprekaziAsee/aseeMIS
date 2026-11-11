using dsBankParty.Core.Services.Interfaces;
using dsBankParty.Core.ViewModels;
using dsMIS.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.BankParty.Controllers
{
    [Area("BankParty")]
    public class BankPartyIndividualController(IBankPartyIndividualService<BankPartyIndividualViewModel> service, ILocalizationService localizationService,
                                IAppFormMenuService menuService, IUserContextService userContextService,
                                IAppFormPermissionService formAccessService, IWebHostEnvironment env) : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        //[HttpPost]
        //public async Task<IActionResult> AddIndividualAsync([FromBody] BankPartyIndividualResidentDetailViewModel vm)
        //{
        //    // 1) Revalidate from session context only (no formId from client)
        //    var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

        //    var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModelName)
        //        = await AppAuthorizeHelper.ValidateActionAsync(
        //            HttpContext, formAccessService, menuService, userContextService,
        //            formSessionId, requiredPermission: FormAccessAction.ADD);

        //    if (!isValid || appForm == null || formContext == null)
        //        return Ok(new { success = false, message = "No access" });

        //    // 2) Basic model validation (client may have validated already)
        //    if (!ModelState.IsValid || vm == null)
        //    {
        //        var msgInvalid = await localizationService
        //            .FindServerSideErrorMessageByKeyAndLanguageCodeAsync("NotFound", formContext.LanguageCode);
        //        return Ok(new { success = false, message = msgInvalid });
        //    }

        //    try
        //    {
        //        // 4) Call the concrete service (does 3 inserts in a transaction)
        //        //    Note: vm.BankPartyId will be populated after insert.
        //        await service.AddIndividualAsync(vm, new ContextSessionDto
        //        {
        //            FormId = formContext.FormId,
        //            UserId = formContext.UserId,
        //            RoleNames = formContext.RoleNames,
        //            LanguageCode = formContext.LanguageCode,
        //            IsAutoApprove = appForm.IsAutoApprove // if you carry this flag
        //        });

        //        // 5) Localized success
        //        var msgOk = await localizationService
        //            .FindServerSideSuccessMessageByKeyAndLanguageCodeAsync("ADD", formContext.LanguageCode);

        //        return Ok(new
        //        {
        //            success = true,
        //            message = msgOk,
        //            updatedRow = vm // now contains the generated BankPartyId
        //        });
        //    }
        //    catch (AppException ex) // your domain/business exceptions
        //    {
        //        return Ok(new { success = false, message = ex.Message, updatedRow = vm });
        //    }
        //    catch (Exception ex)
        //    {
        //        return Ok(new { success = false, message = ex.Message, updatedRow = vm });
        //    }
        //}
    }
}
