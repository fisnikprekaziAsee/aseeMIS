using dsMIS.Application.Utils;
using dsMIS.Core.Attributes;
using dsMIS.Core.Enums;
using dsMIS.Core.Exceptions;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.Common.Utils.UI;
using aseeMIS.Services.Interfaces;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace aseeMIS.Areas.Common.Controllers
{
    //[AuthorizeWithLanguageCode]
    [Area("Common")]
    [AppAuthorizeAttribute]
    public class FormBuilderController(IServiceProvider serviceProvider, 
                                IAppFormMenuService menuService, IUserContextService userSecurityStampCacheService, IAppFormPermissionService formAccessService, IAppSessionService dsSessionService) : Controller
    {
        private string methodName = "GetByIdAsync";
        /// <summary>
        /// Generic CRUD Index action that handles form authorization, dynamic service invocation,
        /// foreign key loading, and dynamic toolbar/context menu generation for a given form ID.
        /// </summary>
        /// <param name="id">Form ID</param>
        /// <returns>Dynamic View for the CRUD form, or redirects to NoAccess/Error if unauthorized or failure occurs</returns>
        public async Task<IActionResult> Index(int id, object? entityPrimaryKeyValue)
        {
            // Step 1: Validate access permissions for this form and user
            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel)
            = await AppAuthorizeHelper.ValidateReadPermissionAsync(HttpContext, formAccessService, menuService, userSecurityStampCacheService, id, EnumContextFormType.PARENT);
            if (!isValid || appForm == null || formContext == null)
                return RedirectToAction("NoAccess", "Error");

            try
            {
                // Step 2: Prepare dynamic service invoker to call SelectAllAsync from the appropriate service
                var serviceInvoker = new ServiceInvoker(serviceProvider);
                List<object> parameters = new List<object>(); // Collect automatic parameters dynamically
                // Add the primary key value to parameters
                if (entityPrimaryKeyValue != null)
                    parameters.Add(entityPrimaryKeyValue); // Add the primary key value to parameters
                // Step 3: Add dynamic parameters from AppForm definition (e.g., @LanguageCode, @UserId)
                if (!string.IsNullOrEmpty(appForm.FormUIConfiguration))
                {
                    if (appForm.FormUIConfiguration.Contains("@ClaimType"))
                    {
                        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                        parameters.Add(userId ?? string.Empty);
                        parameters.Add(dsSessionService.GetLanguageCode() ?? string.Empty);
                        methodName = "SelectAllByClaimTypeAsync";

                        var userClaimsString = string.Join(";", HttpContext.User.Claims.Where(c => c.Type == "DS").Select(c => c.Value));
                        parameters.Add(userClaimsString ?? string.Empty);

                        // Add other dynamic placeholders if needed in the future
                    }
                    else
                    {
                        if (appForm.FormUIConfiguration.Contains("@UserId"))
                        {
                            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                            parameters.Add(userId ?? string.Empty);
                        }
                        if (appForm.FormUIConfiguration.Contains("@LanguageCode"))
                        {
                            parameters.Add(dsSessionService.GetLanguageCode() ?? string.Empty);
                        }
                    }
                }


                // Step 4: Dynamically invoke SelectAllAsync from the correct module, service, and model
                var data = await serviceInvoker.InvokeAsync(projectNamespace, serviceInterface, methodName, parameters.ToArray());

                // Step 5: Resolve the correct ViewModel type dynamically
                var modelType = serviceInvoker.GetViewModelType(projectNamespace, viewModel);
                var cascadeMap = new List<object>();
                // Step 6: Loop through ViewModel properties to populate foreign key dropdowns
                foreach (var property in modelType.GetProperties())
                {
                    var fkAttribute = property
                        .GetCustomAttributes(typeof(ExtendedForeignKeyAttribute), false)
                        .FirstOrDefault() as ExtendedForeignKeyAttribute;

                    if (fkAttribute != null)
                    {
                        List<object> ddlParameters = new List<object>();

                        // Step 6a: Collect foreign key method parameters (e.g., @LanguageCode, @UserId)
                        if (fkAttribute.Parameters.Length > 0)
                        {
                            var methodParameters = fkAttribute.Parameters.Split(',');

                            foreach (var parameter in methodParameters)
                            {
                                if (parameter.Contains("@LanguageCode"))
                                {
                                    ddlParameters.Add(dsSessionService.GetLanguageCode() ?? string.Empty);
                                }
                                else if (parameter.Contains("@UserId"))
                                {
                                    string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                                    ddlParameters.Add(userId ?? string.Empty);
                                }
                                else
                                {
                                    ddlParameters.Add(parameter);
                                }
                            }
                        }

                        // Step 6b: Invoke the foreign key service and store result in ViewData
                        var fkData = await serviceInvoker.InvokeAsync(fkAttribute.ModulName, fkAttribute.ServiceName, fkAttribute.MethodName360, ddlParameters.ToArray());
                        ViewData["FK_" + property.Name] = fkData;

                        if (!string.IsNullOrWhiteSpace(fkAttribute.CascadeSource) && !string.IsNullOrWhiteSpace(fkAttribute.CascadeTarget))
                        {
                            cascadeMap.Add(new
                            {
                                Source = fkAttribute.CascadeSource,
                                Target = fkAttribute.CascadeTarget,
                                TargetField = property.Name // the actual field that is being rendered
                            });
                        }
                    }
                }

                // Step 7: Pass model metadata and control flags to the view
                ViewBag.CascadeMap = cascadeMap;
                ViewBag.Type = modelType;
                ViewBag.Area = appForm.ProjectNameAcronym.ToUpper();
                ViewBag.FormId = id;




                // Step 9: Render the dynamic CRUD view with the loaded data
                return View("~/Areas/Common/Views/FormBuilder.cshtml", data);

            }
            catch (AppException ex)
            {
                // Optional: log the exception if needed

                // Store the error message temporarily (e.g., in TempData or ViewBag)
                TempData["ErrorMessage"] = ex.Message;

                // Redirect to a shared error page (e.g., ErrorController -> ShowError action)
                return RedirectToAction("ShowError", "Error");
            }
            catch
            {
                // Step 10: Log the error (optional) and show generic error view
                // TODO: Add logging logic here
                return View("Error");
            }
        }
    }
}

