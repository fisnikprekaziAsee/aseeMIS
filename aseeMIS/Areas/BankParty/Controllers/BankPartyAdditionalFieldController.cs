//using dsBankParty.Core.Services.Interfaces;
//using dsMIS.Core.Enums;

//using dsMIS.Core.Services.Interfaces;
//using dsCrmDef.Core.Services.Interfaces;
//using aseeMIS.Utils;
//using Microsoft.AspNetCore.Mvc;

//namespace aseeMIS.Areas.BankParty.Controllers
//{
//    [Area("BankParty")]
//    public class BankPartyAdditionalFieldController(IFormAccessService formAccessService, IMenuService menuService, IUserContextService userContextService, IServiceProvider serviceProvider,
//        ICrmDefPartyAdditionalFieldRuleService additionalFieldRuleService, IBankPartyAdditionalFieldValueService additionalFieldValueService) : Controller
//    {
//        public async Task<IActionResult> Index(int formId, int bankPartyId)
//        {
//            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

//            // ✅ Validate session and permissions
//            var (isValid, appForm, contextSessionDto, projectNamespace, _, _, _) =
//                await AppAuthorizeHelper.ValidateReadPermissionAsync(
//                    HttpContext,
//                    formAccessService,
//                    menuService,
//                    userContextService,
//                    formId,
//                    ContextFormTypeEnum.PARENT
//                );

//            if (!isValid || contextSessionDto == null)
//                return (RedirectToAction("NoAccess", "Error"));

//            var additionalFields = await additionalFieldRuleService.SelectAdditionalFieldsAsync(bankPartyId);
//            return View("~/Areas/BankParty/Views/BankPartyAdditionalField.cshtml", additionalFields);
//        }
//    }
//}
