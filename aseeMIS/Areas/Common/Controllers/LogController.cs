using dsMIS.Application.Utils;
using aseeMIS.Utils;
using dsMIS.Core.Attributes;

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System.Security.Claims;
using dsAppMng.Core.Services.Interfaces;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.Common.Utils.UI;

namespace aseeMIS.Areas.Common.Controllers
{
   
    [Area("Common")]
    public class LogController(IServiceProvider serviceProvider, ToolbarItemsProvider toolbarItemsProvider, ILocalizationService localizationService, IAppFormMenuService menuService) : Controller
    {
        public async Task<IActionResult> Index(int id)
        {
            // Fetch the AppForm based on the claimType
            var appForm = await menuService.FindByFormIdAsync(id);
            if (appForm == null)
                return Forbid();

            // Dynamically generate the claimType based on the AppForm
            //var claimType = appForm.ClaimType;

            //if (!User.HasClaim(c => c.Type == claimType && c.Value == "Read"))
            //{
            //    return Forbid();
            //}

            try
            {
                // Use ServiceInvoker to call the SelectAllAsync method
                var serviceInvoker = new ServiceInvoker(serviceProvider);

                List<object> parameters = new List<object>(); // Store parameters dynamically

                if (!string.IsNullOrEmpty(appForm.FormUIConfiguration))
                {
                    if (appForm.FormUIConfiguration.Contains("@LangaugeCode"))
                    {
                        string? languageCode = User.FindFirstValue("LanguageCode");
                        parameters.Add(languageCode ?? string.Empty); // Add to dynamic parameters list
                    }

                    if (appForm.FormUIConfiguration.Contains("@UserId"))
                    {
                        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                        parameters.Add(userId ?? string.Empty);
                    }

                    // Add additional conditions for other dynamic parameters
                }

                // Convert list to object array and pass it dynamically
                var data = await serviceInvoker.InvokeAsync("appForm.ProjectName", "appForm.ServiceName", "SelectAllAsync", parameters.ToArray());


                var modelType = serviceInvoker.GetViewModelType("appForm.ProjectName", appForm.ViewModelName);
                // Check each property on the model type for the ExtendedForeignKey attribute.
                foreach (var property in modelType.GetProperties())
                {
                    var fkAttribute = property
                                        .GetCustomAttributes(typeof(ExtendedForeignKeyAttribute), false)
                                        .FirstOrDefault() as ExtendedForeignKeyAttribute;
                    if (fkAttribute != null)
                    {
                        // Call the foreign key service.
                        // Here we assume that the foreign key data can be retrieved by calling "SelectAllAsync"
                        // on the service specified in the attribute.
                        var fkData = await serviceInvoker.InvokeAsync(fkAttribute.ModulName, fkAttribute.ServiceName, "SelectAllForDropdownAsync");

                        // Save the foreign key data in the ViewData (or ViewBag) under a key like "FK_PropertyName"
                        // Using ViewData allows you to use a computed key.
                        ViewData["FK_" + property.Name] = fkData;
                    }
                }

                // Use the model type dynamically
                ViewBag.ControllerName = appForm.FormUIConfiguration;
                ViewBag.Type = modelType;
                ViewBag.FormId = id;
                ViewBag.Area = appForm.ProjectNameAcronym;

                // Set the toolbar and context menu items dynamically
                //ViewBag.ToolbarItems = await toolbarItemsProvider.GetToolbarItems(User, "ASdd", true, true, true);
                //ViewBag.ContextMenuItems = await toolbarItemsProvider.GetContextMenuItems(User, "ASdd");

                // Pass the data to the view
                return View("~/Areas/Common/Views/Log.cshtml", data);
            }
            catch (Exception ex)
            {
                // Log the error (you can use a logging service here)
                // Return the error view
                return View("Error");
            }
        }
    }
}
