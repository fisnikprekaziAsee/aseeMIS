using dsAppMng.Core.Services.Interfaces;
using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.Common.Utils.Parameters;
using aseeMIS.Areas.Common.Utils.UI;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text.Json;

namespace aseeMIS.Areas.AppMng.Controllers
{
    [AppAuthorizeAttribute]
    [Area("AppMng")]
    public class AppLocalizationSyncfusionController(IAppLanguageService appLanguageService, IWebHostEnvironment env,
                                                     ToolbarItemsProvider toolbarItemsProvider, ILocalizationService localizationService,
                                                     IAppFormMenuService menuService, IUserContextService userContextService,
                                                     IAppFormPermissionService formAccessService) : Controller
    {
        public async Task<IActionResult> Index()
        {
            var (isValid, appForm, contextSessionDto, projectNamespace, serviceInterface, viewModel) =
                await AppAuthorizeHelper.ValidateReadPermissionAsync(HttpContext, formAccessService, menuService, userContextService, 1207, EnumContextFormType.PARENT);

            if (!isValid || contextSessionDto == null)
                return RedirectToAction("NoAccess", "Error");

            ViewBag.ToolbarItems = toolbarItemsProvider.GetToolbarItems(appForm, contextSessionDto, EnumFormType.CRUD);
            ViewBag.ContextMenuItems = await toolbarItemsProvider.GetContextMenuItems(contextSessionDto);
            ViewBag.FormSessionId = contextSessionDto.FormSessionId;

            // Define the directory for JSON files
            string jsonDirectory = Path.Combine(env.WebRootPath, "locale");

            // Fetch active languages
            var jsonFiles = await appLanguageService.SelectAllActiveForDropdownAsync();

            ViewBag.JsonFiles = jsonFiles;

            return View("~/Areas/AppMng/Views/LocalizationControls.cshtml");
        }

        [HttpPost]
        public IActionResult GetJsonData(string fileName, string replacementText)
        {
            var jsonFilePath = Path.Combine(env.WebRootPath, "locale", $"{fileName}.json");

            if (!System.IO.File.Exists(jsonFilePath))
            {
                // Handle missing file
                var sourceFilePath = Path.Combine(env.WebRootPath, "locale", "en-US.json");
                if (!System.IO.File.Exists(sourceFilePath))
                {
                    return NotFound("The source JSON file 'en-US.json' could not be found to create the missing file.");
                }

                try
                {
                    var jsonContent = System.IO.File.ReadAllText(sourceFilePath);
                    replacementText = fileName; // Use the file name as the replacement text
                    var updatedContent = jsonContent.Replace("en-US", replacementText);

                    System.IO.File.WriteAllText(jsonFilePath, updatedContent);
                }
                catch (Exception ex)
                {
                    return Ok(new { success = false, message = ex.Message });
                }
            }

            var jsonData = System.IO.File.ReadAllText(jsonFilePath);
            var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(jsonData);

            var flattenedData = FlattenJson(jsonObject);

            return Json(flattenedData);
        }

        private List<dynamic> FlattenJson(Dictionary<string, Dictionary<string, Dictionary<string, string>>> jsonData)
        {
            var result = new List<dynamic>();
            foreach (var languageCode in jsonData)
            {
                foreach (var component in languageCode.Value)
                {
                    foreach (var keyValuePair in component.Value)
                    {
                        result.Add(new
                        {
                            LanguageCode = languageCode.Key,
                            Component = component.Key,
                            Key = keyValuePair.Key,
                            TranslatedValue = keyValuePair.Value
                        });
                    }
                }
            }
            return result;
        }

        [HttpPost]
        public async Task<IActionResult> Update([FromBody] JsonElement updatedRecord, [FromQuery] string fileName)
        {
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();
            // ✅ Step 1: Revalidate from session context only (no formId passed from client)
            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) = await AppAuthorizeHelper.ValidateActionAsync(
                                            HttpContext, formAccessService, menuService, userContextService, formSessionId, requiredPermission: EumFormAccessAction.UPDATE);
            if (!isValid || appForm == null || formContext == null)
                return RedirectToAction("NoAccess", "Error");

            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    return Ok(new
                    {
                        success = false,
                        message = localizationService.FindServerSideErrorMessageByKeyAndLanguageCodeAsync("NotFound", formContext.LanguageCode)
                    });
                }

                if (!updatedRecord.TryGetProperty("LanguageCode", out JsonElement languageCodeElement) ||
                    !updatedRecord.TryGetProperty("Component", out JsonElement componentElement) ||
                    !updatedRecord.TryGetProperty("Key", out JsonElement keyElement) ||
                    !updatedRecord.TryGetProperty("TranslatedValue", out JsonElement translatedValueElement))
                {
                    return Ok(new
                    {
                        success = false,
                        message = localizationService.FindServerSideErrorMessageByKeyAndLanguageCodeAsync("IDP", formContext.LanguageCode)
                    });
                }

                string languageCode = languageCodeElement.GetString();
                string component = componentElement.GetString();
                string key = keyElement.GetString();
                string translatedValue = translatedValueElement.GetString();

                if (string.IsNullOrEmpty(translatedValue))
                {
                    return Ok(new
                    {
                        success = false,
                        message = localizationService.FindServerSideErrorMessageByKeyAndLanguageCodeAsync("NotFound", formContext.LanguageCode)
                    });
                }

                var jsonFilePath = Path.Combine(env.WebRootPath, "locale", $"{fileName}.json");

                if (!System.IO.File.Exists(jsonFilePath))
                {
                    return Ok(new
                    {
                        success = false,
                        message = localizationService.FindServerSideErrorMessageByKeyAndLanguageCodeAsync("NotFound", formContext.LanguageCode)
                    });
                }

                var jsonData = System.IO.File.ReadAllText(jsonFilePath);
                var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(jsonData);

                if (jsonObject.ContainsKey(languageCode) &&
                    jsonObject[languageCode].ContainsKey(component) &&
                    jsonObject[languageCode][component].ContainsKey(key))
                {
                    jsonObject[languageCode][component][key] = translatedValue;

                    var updatedJson = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
                    await System.IO.File.WriteAllTextAsync(jsonFilePath, updatedJson);

                    return Ok(new
                    {
                        success = true,
                        message = localizationService.FindServerSideSuccessMessageByKeyAndLanguageCodeAsync("Update", formContext.LanguageCode) + languageCode
                    });
                }
                return Ok(new
                {
                    success = false,
                    message = localizationService.FindServerSideErrorMessageByKeyAndLanguageCodeAsync("NotFound", formContext.LanguageCode)
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }
    }
}
