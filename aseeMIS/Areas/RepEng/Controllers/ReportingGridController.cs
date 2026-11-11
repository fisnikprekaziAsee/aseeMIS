//using dsMIS.Core.Services.Interfaces;
//using aseeMIS.Areas.Common.Utils.UI;
//using aseeMIS.Services.Interfaces;
//using dsRepEng.Core.Services.Interfaces;
//using Microsoft.AspNetCore.Mvc;
//using System.Data;
//using System.Diagnostics;

//namespace dsRepEng.Areas.RepEng.Controllers
//{
//    [Area("RepEng")]
//    public class ReportingGridController(
//        IRepEngUniverseService universeService,
//        IRepEngUniverseParamService universeParamService,
//        IRepEngUniverseParamUiService universeParamUiService,
//        IServiceProvider serviceProvider,
//        IAppSessionService sessionService,
//        IFormAccessService formAccessService,
//        IMenuService menuService,
//        IUserContextService userSecurityStampCacheService,
//        IReportingGridService reportingService, ToolbarItemsProvider toolbarItemsProvider) : Controller
//    {



//        [HttpGet]
//        public async Task<IActionResult> Index(int id)
//        {
//            return BadRequest(id);
//            //  var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel, formType) =
//            //await AppAuthorizeHelper.ValidateReadPermissionAsync(
//            //    HttpContext,

//            //    formAccessService,
//            //    menuService,
//            //    userSecurityStampCacheService,
//            //    id,
//            //    ContextFormTypeEnum.PARENT
//            //);

//            //  if (!isValid || appForm == null || formContext == null)
//            //      return RedirectToAction("NoAccess", "Error");

//            //  try
//            //  {
//            //      // Get UniverseId from FormUIParameters (just a number)
//            //      int? selectedUniverseId = null;
//            //      if (!string.IsNullOrWhiteSpace(appForm.FormUIParameters) &&
//            //          int.TryParse(appForm.FormUIParameters.Trim(), out var parsedId))
//            //      {
//            //          selectedUniverseId = parsedId;
//            //      }

//            //      // Get all universes for dropdown
//            //      var allUniverses = await universeService.SelectAllAsync();
//            //      var universeList = allUniverses
//            //          .Select(u => new
//            //          {
//            //              UniverseId = u.UniverseId,
//            //              UniverseName = u.UniverseName
//            //          })
//            //          .ToList();



//            //      string storedProcedureName = string.Empty;
//            //      if (selectedUniverseId.HasValue)
//            //      {
//            //          var selectedUniverse = allUniverses.FirstOrDefault(u => u.UniverseId == selectedUniverseId.Value);
//            //          if (selectedUniverse != null && !string.IsNullOrWhiteSpace(selectedUniverse.SqlcommandText))
//            //          {
//            //              storedProcedureName = selectedUniverse.SqlcommandText.Trim();
//            //          }
//            //      }
//            //      ViewBag.StoredProcedureName = storedProcedureName;

//            //      // Load parameters immediately if we have a selected UniverseId
//            //      List<object> parameterList = new();
//            //      if (selectedUniverseId.HasValue)
//            //      {
//            //          var parameters = await universeParamService.GetParamsByUniverseIdAsync(selectedUniverseId.Value);

//            //          foreach (var param in parameters)
//            //          {
//            //              Debug.WriteLine($"[PARAM] Name={param.ParamName}, Type={param.ParamType}, IsRequired={param.IsRequired}, UIID={param.UniverseParamUiid}");

//            //              var options = new List<object>();
//            //              object paramObj = null;

//            //              string? minValue = null;
//            //              string? maxValue = null;
//            //              object uiBlock = null;

//            //              if (param.UniverseParamUiid.HasValue)
//            //              {
//            //                  var uiConfig = await universeParamUiService.GetUiByIdAsync(param.UniverseParamUiid.Value);

//            //                  if (uiConfig != null)
//            //                  {
//            //                      Debug.WriteLine($"[UICFG] UIType={uiConfig.Uitype}, Model={uiConfig.UimodelName}, Method={uiConfig.UimethodName}");

//            //                      if (uiConfig.Uitype?.ToLower() == "ddl" &&
//            //                          !string.IsNullOrEmpty(uiConfig.UimodelName) &&
//            //                          !string.IsNullOrEmpty(uiConfig.UimethodName))
//            //                      {
//            //                          try
//            //                          {
//            //                              var serviceInvoker = new ServiceInvoker(serviceProvider);

//            //                              string serviceInterfaceName = $"Services.Interfaces.I{uiConfig.UimodelName}Service";
//            //                              string projectAcronym = $"ds{uiConfig.UiprojectAcronym}.Core";

//            //                              var methodParams = new List<object>();
//            //                              if (!string.IsNullOrEmpty(uiConfig.UimethodNameParams))
//            //                              {
//            //                                  foreach (var paramValue in uiConfig.UimethodNameParams.Split(','))
//            //                                  {
//            //                                      if (paramValue.Contains("@LanguageCode"))
//            //                                          methodParams.Add(sessionService.GetLanguageCode() ?? string.Empty);
//            //                                      else if (paramValue.Contains("@UserId"))
//            //                                          methodParams.Add(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);
//            //                                      else
//            //                                          methodParams.Add(paramValue);
//            //                                  }
//            //                              }

//            //                              var result = await serviceInvoker.InvokeAsync(
//            //                                  projectAcronym,
//            //                                  serviceInterfaceName,
//            //                                  uiConfig.UimethodName,
//            //                                  methodParams.ToArray()
//            //                              );

//            //                              if (result is IEnumerable<object> enumerable)
//            //                                  options = enumerable.ToList();

//            //                              Debug.WriteLine($"[OPTIONS] Loaded {options.Count} options for {param.ParamName}");
//            //                          }
//            //                          catch (Exception ex)
//            //                          {
//            //                              Debug.WriteLine($"[ERROR] Service invocation failed: {ex.Message}");
//            //                          }
//            //                      }

//            //                      string paramType = param.ParamType?.ToLower() ?? "";
//            //                      bool isRangeSupported = paramType is "int" or "decimal" or "float" or "datetime" or "date" or "ddl";

//            //                      if (isRangeSupported)
//            //                      {
//            //                          minValue = uiConfig.UiminValue;
//            //                          maxValue = uiConfig.UimaxValue;

//            //                          // 🕒 Handle datetime edge case: Min cannot be after Max
//            //                          if (paramType == "datetime" && paramType == "date" &&
//            //                              DateTime.TryParse(minValue, out var minDate) &&
//            //                              DateTime.TryParse(maxValue, out var maxDate) &&
//            //                              minDate > maxDate)
//            //                          {
//            //                              Debug.WriteLine($"[WARNING] {param.ParamName} has Min > Max for datetime. Clearing MinValue.");
//            //                              minValue = null;
//            //                          }

//            //                          // 📌 DDL: Sometimes value is string, but we assume they represent numeric IDs
//            //                          if (paramType == "ddl" &&
//            //                              double.TryParse(minValue, out var minNum) &&
//            //                              double.TryParse(maxValue, out var maxNum) &&
//            //                              minNum > maxNum)
//            //                          {
//            //                              Debug.WriteLine($"[WARNING] {param.ParamName} has Min > Max for DDL. Clearing MinValue.");
//            //                              minValue = null;
//            //                          }

//            //                          Debug.WriteLine($"[RANGE] {param.ParamName} Min={minValue}, Max={maxValue}");
//            //                      }


//            //                      uiBlock = new
//            //                      {
//            //                          uiConfig.UiparamName,
//            //                          uiConfig.Uitype,
//            //                          uiConfig.UidefaultValue,
//            //                          uiConfig.UiminValue,
//            //                          uiConfig.UimaxValue,
//            //                          uiConfig.UimodelName,
//            //                          uiConfig.UimethodName,
//            //                          uiConfig.UimethodNameParams
//            //                      };
//            //                  }
//            //              }

//            //              // ✅ Always build paramObj, even if no UI config is present
//            //              paramObj = new
//            //              {
//            //                  ParamName = param.ParamName,
//            //                  ParamType = param.ParamType,
//            //                  IsRequired = param.IsRequired,
//            //                  DefaultValue = param.DefaultValue,
//            //                  MinValue = minValue,
//            //                  MaxValue = maxValue,
//            //                  UI = uiBlock,
//            //                  Options = options
//            //              };

//            //              Debug.WriteLine($"[paramObj BUILT] Name={param.ParamName}, Min={minValue}, Max={maxValue}, OptionsCount={options.Count}");

//            //              parameterList.Add(paramObj);
//            //          }

//            //      }

//            //      ViewBag.AllUniverses = universeList;
//            //      ViewBag.FormId = id;
//            //      ViewBag.SelectedUniverseId = selectedUniverseId;
//            //      ViewBag.ParameterList = parameterList;

//            //      return View("~/Areas/RepEng/Views/ReportingGrid.cshtml");
//            //  }
//            //  catch (AppException ex)
//            //  {
//            //      TempData["ErrorMessage"] = ex.Message;
//            //      return RedirectToAction("ShowError", "Error");
//            //  }
//            //  catch (Exception)
//            //  {
//            //      return View("Error");
//            //  }
//        }



//        [HttpPost]
//        public async Task<IActionResult> Execute()
//        {
//            try
//            {
//                // Read raw JSON request body
//                using var reader = new StreamReader(Request.Body);
//                var body = await reader.ReadToEndAsync();

//                Debug.WriteLine("📥 Raw body: " + body); // Debug log (optional)

//                var json = System.Text.Json.JsonDocument.Parse(body);
//                var root = json.RootElement;

//                // ✅ Check if property exists first (safe access)
//                if (!root.TryGetProperty("storedProcedureName", out var spNameElement))
//                    return BadRequest(new { error = "Missing 'storedProcedureName' in JSON body" });


//                var storedProcedureName = spNameElement.GetString();

//                var paramValues = root.GetProperty("paramValues");


//                // Prepare parameters
//                var parameters = new Dictionary<string, object>();
//                foreach (var param in paramValues.EnumerateObject())
//                {
//                    parameters[param.Name] = param.Value.GetString();
//                }

//                // Execute stored procedure
//                var dataTable = await reportingService.GetReportAsync(storedProcedureName, parameters);

//                // Extract columns
//                var columns = dataTable.Columns
//                    .Cast<DataColumn>()
//                    .Select(col => new
//                    {
//                        field = col.ColumnName,
//                        headerText = col.ColumnName
//                    }).ToList();

//                // Extract rows
//                var rows = new List<Dictionary<string, object>>();
//                foreach (DataRow row in dataTable.Rows)
//                {
//                    var dict = new Dictionary<string, object>();
//                    foreach (DataColumn col in dataTable.Columns)
//                    {
//                        dict[col.ColumnName] = row[col];
//                    }
//                    rows.Add(dict);
//                }

//                // ✅ Return both columns and rows
//                return Json(new
//                {
//                    columns,
//                    rows
//                });
//            }
//            catch (Exception ex)
//            {
//                return BadRequest(new { error = ex.Message });
//            }
//        }

//    }
//}
