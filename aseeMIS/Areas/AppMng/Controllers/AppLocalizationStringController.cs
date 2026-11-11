using Microsoft.AspNetCore.Mvc;
using dsAppMng.Core.Repositories.Interfaces;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using dsAppMng.Core.Services;
using dsAppMng.Core.Services.Interfaces;
using dsAppMng.Core.ViewModels;
using dsMIS.Core.Exceptions;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.Common.Utils.UI;

namespace aseeMIS.Areas.AppMng.Controllers
{
    [Authorize]
    [Area("AppMng")]
    public class AppLocalizationStringController(IAppLocalizationStringService appLocalizationStringService,  IAppConstantService appConstantService, IAppLanguageService appLanguageService, ToolbarItemsProvider toolbarItemsProvider, ILocalizationService localizationService) : Controller
    {
        //private const string claimType = "AppMng.AppLocalizationString";

        //[ClaimFilter(claimType, "Read")]
        //public async Task<IActionResult> AppLocalizationString()
        //{
        //    var languages = await appLanguageService.SelectAllForDropdownAsync();

        //    ViewBag.Languages = languages;

        //    ViewBag.ToolbarItems = toolbarItemsProvider.GetToolbarItems(User, claimType, true, false, true).Result;
        //    ViewBag.ContextMenuItems = toolbarItemsProvider.GetContextMenuItems(User, claimType).Result;

        //    return View("~/Areas/AppMng/Views/AppLocalizationString.cshtml");
        //}

        //[HttpGet]
        //public async Task<IActionResult> GetTranslator(string langCode1, string langCode2)
        //{
        //    var constants = await appLocalizationStringService.SelectForTranslatorAsync(langCode1, langCode2);

        //    if (constants == null || !constants.Any())
        //    {
        //        var message = localizationService.FindServerSideErrorMessageByKeyAndLanguageCodeAsync("NotFound", User.FindFirstValue("LanguageCode")).Result;
        //        var jsonResultError = JsonConvert.SerializeObject(new { success = false, message }, new JsonSerializerSettings
        //        {
        //            ContractResolver = new DefaultContractResolver()
        //        });
        //        return Content(jsonResultError, "application/json");
        //    }

        //    var jsonResult = JsonConvert.SerializeObject(constants, new JsonSerializerSettings
        //    {
        //        ContractResolver = new DefaultContractResolver()
        //    });
        //    return Content(jsonResult, "application/json");
        //}


        //[ClaimFilter(claimType, "Update")]
        //[HttpPost]
        //public async Task<IActionResult> Update([FromBody] AppLocalizationStringViewModel appLocalizationString)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        try
        //        {
        //            var requestedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);

        //           // await appLocalizationStringService.UpdateAsync(appLocalizationString, 11006, User.FindFirstValue("LanguageCode"), int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))); // Update the entry in the repository
        //            return Ok(new
        //            {
        //                success = true,
        //                message = localizationService.FindServerSideSuccessMessageByKeyAndLanguageCodeAsync("Update", User.FindFirstValue("LanguageCode")).Result + appLocalizationString.DestinationStringKey,
        //                updatedRow = appLocalizationString

        //            });
        //        }
        //        catch (AppException ex)
        //        {
        //            return Ok(new { success = false, message = ex.Message });
        //        }
        //    }
        //    // If the model state is invalid, return an error message
        //    return Ok(new
        //    {
        //        success = false,
        //        message = localizationService.FindServerSideErrorMessageByKeyAndLanguageCodeAsync("IDP", User.FindFirstValue("LanguageCode")).Result + appLocalizationString.DestinationStringKey
        //    });
        //}

       // [HttpPost]
        //public async Task<IActionResult> Review([FromBody] StagingDataLogTableDto stagingDataLogTableViewModel)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        try
        //        {
        //            await appLocalizationStringService.ReviewAsync(stagingDataLogTableViewModel, User.FindFirstValue(ClaimTypes.NameIdentifier), User.FindFirstValue("LanguageCode")); // Update the entry in the repository
        //            return Ok(new
        //            {
        //                success = true,
        //                message = localizationService.FindServerSideSuccessMessageByKeyAndLanguageCodeAsync("Update", User.FindFirstValue("LanguageCode")).Result + stagingDataLogTableViewModel.StagingDataLogId
        //            });
        //        }
        //        catch (AppException ex)
        //        {
        //            return Ok(new { success = false, message = ex.Message });
        //        }
        //    }

        //    return Ok(new
        //    {
        //        success = false,
        //        message = localizationService.FindServerSideErrorMessageByKeyAndLanguageCodeAsync("IDP", User.FindFirstValue("LanguageCode")).Result + stagingDataLogTableViewModel.StagingDataLogId
        //    });
        //}

    }
}
