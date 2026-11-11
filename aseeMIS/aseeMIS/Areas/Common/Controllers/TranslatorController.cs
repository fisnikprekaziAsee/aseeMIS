using dsMIS.Application.Utils;
using dsAppMng.Core.Services.Interfaces;
using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.Common.Utils.UI;
using aseeMIS.Services.Interfaces;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace aseeMIS.Areas.Common.Controllers
{
    [Area("Common")]
    [AppAuthorizeAttribute]
    public class TranslatorController(IAppLanguageService appLanguageService, IServiceProvider serviceProvider, ToolbarItemsProvider toolbarItemsProvider, ILocalizationService localizationService,
                                IAppFormMenuService menuService, IUserContextService userSecurityStampCacheService, IAppFormPermissionService formAccessService, IAppSessionService dsSessionService) : Controller
    {
        [HttpGet]
        public async Task<IActionResult> GetTranslator(string langCode1, string langCode2)
        {
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();
            // Step 1: Validate access permissions for this form and user
            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel)
                = await AppAuthorizeHelper.ValidateActionAsync(HttpContext, formAccessService, menuService, userSecurityStampCacheService, formSessionId, EumFormAccessAction.READ);

            if (!isValid || appForm == null || formContext == null)
                return RedirectToAction("NoAccess", "Error");

            var serviceInvoker = new ServiceInvoker(serviceProvider);

            var data = await serviceInvoker.InvokeAsync(projectNamespace, serviceInterface, "SelectForTranslatorAsync", langCode1, langCode2);

            var jsonResult = JsonConvert.SerializeObject(data, new JsonSerializerSettings { ContractResolver = new DefaultContractResolver() });
            return Content(jsonResult, "application/json");
        }


    }
}