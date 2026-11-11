using dsMIS.Application.Utils;
using DocumentFormat.OpenXml.Drawing;
using dsBankParty.Core.Services.Interfaces;
using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.Common.Utils.Parameters;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.DocEng.Controllers
{
    [Area("DocEng")]
    public class DocumentGeneratorController(IAppFormPermissionService formAccessService, IAppFormMenuService menuService,
        IUserContextService userContextService, IServiceProvider serviceProvider) : Controller
    {
        [HttpGet]
        public async Task<IActionResult> View(int formId, string entityPrimaryKeyValue)
        {
            //var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

            //// ✅ Validate session and permissions
            //var (isValid, appForm, contextSessionDto, projectNamespace, _, _) =
            //    await AppAuthorizeHelper.ValidateActionAsync(
            //        HttpContext,
            //        formAccessService,
            //        menuService,
            //        userContextService,
            //        formSessionId,
            //        requiredPermission: EumFormAccessAction.PRINT
            //    );

            //if (!isValid || contextSessionDto == null)
            //    return (RedirectToAction("NoAccess", "Error"));

            var (isValid, appForm, formContext, proj, svc, vmName) = await AppAuthorizeHelper.ValidateReadPermissionAsync(
                                                                    HttpContext, formAccessService, menuService, userContextService,
                                                                    formId, contextFormType: EnumContextFormType.PARENT);

            if (!isValid || appForm is null || formContext is null)
                throw new InvalidOperationException("No access.");

            var invoker = new ServiceInvoker(serviceProvider);

            if (!isValid)
                return RedirectToAction("NoAccess", "Error");

            var templates = await invoker.InvokeAsync(proj, appForm.FlowDocumentRulePath, "SelectDocumentTemplatesAsync", new object[] { formId, int.Parse(entityPrimaryKeyValue) });

            ViewBag.Templates = templates;
            ViewBag.FormId = formId;
            ViewBag.EntityPrimaryKeyValue = entityPrimaryKeyValue;
            ViewBag.FormSessionId = formContext.FormSessionId;

            return PartialView("~/Areas/DocEng/Views/_DocumentGenerator.cshtml");
        }
    }
}

