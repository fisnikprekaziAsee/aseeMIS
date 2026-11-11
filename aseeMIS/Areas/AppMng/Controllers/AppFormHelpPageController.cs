using dsAppMng.Core.DTOs;
using dsAppMng.Core.Services;
using dsAppMng.Core.Services.Interfaces;
using dsAppMng.Core.ViewModels;
using dsMIS.Core.Enums;

using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.Common.Utils.UI;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
namespace aseeMIS.Areas.AppMng.Controllers

{
    [Area("AppMng")]
    public class AppFormHelpPageController(
        IAppFormHelpPageService helpPageService,
        IAppLanguageService languageService, IAppFormService<AppFormViewModel> formService, IAppLanguageService appLanguageService, IWebHostEnvironment env,
                                                     ToolbarItemsProvider toolbarItemsProvider, ILocalizationService localizationService,
                                                     IAppFormMenuService menuService, IUserContextService userContextService,
                                                     IAppFormPermissionService formAccessService) : Controller
    {

        public async Task<IActionResult> Index()
        {
           
            var (isValid, appForm, contextSessionDto, projectNamespace, serviceInterface, viewModel) =
                await AppAuthorizeHelper.ValidateReadPermissionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userContextService,
                    1253,
                    EnumContextFormType.PARENT
                );

            if (!isValid || contextSessionDto == null)
                return RedirectToAction("NoAccess", "Error");

           
            ViewBag.ToolbarItems = toolbarItemsProvider.GetToolbarItems(
                appForm,
                contextSessionDto,
            EnumFormType.CRUD
            );
            ViewBag.ContextMenuItems =
                await toolbarItemsProvider.GetContextMenuItems(contextSessionDto);

            ViewBag.FormSessionId = contextSessionDto.FormSessionId;

           
            ViewBag.Forms = await formService.SelectAllForDropdownAsync();
            ViewBag.Languages = await languageService.SelectAllForDropdownAsync();

            return View("~/Areas/AppMng/Views/AppFormHelpPage.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> LoadEditors(
           int formId,
           string sourceLang,
           string targetLang)
        {
            var source = await helpPageService
                .SelectByFormIdAndLanguageAsync(formId, sourceLang);

            var target = await helpPageService
                .SelectByFormIdAndLanguageAsync(formId, targetLang);

            return Json(new
            {
                sourceText = source?.HtmlContent ?? "",
                targetText = target?.HtmlContent ?? ""
            });
        }


        [HttpPost]
        public async Task<IActionResult> SaveEditors([FromBody] List<AppFormHelpPageViewModel> models)
        {
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) =
                await AppAuthorizeHelper.ValidateActionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userContextService,
                    formSessionId,
                    requiredPermission: EumFormAccessAction.UPDATE);

            if (!isValid || appForm == null || formContext == null)
                return RedirectToAction("NoAccess", "Error");

            foreach (var vm in models)
            {
                var record = await helpPageService
                    .SelectByFormIdAndLanguageAsync(vm.FormId, vm.LanguageCode);

                record.HtmlContent = vm.HtmlContent;

                await helpPageService.UpdateAsync(record, formContext);
            }

            return Ok(new { success = true });
        }


    }
}
