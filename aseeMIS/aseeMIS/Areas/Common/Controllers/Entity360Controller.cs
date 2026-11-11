using dsMIS.Application.Utils;
using dsAppMng.Core.DTOs;
using dsAppMng.Core.Services.Interfaces;
using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.Common.DTOs;
using aseeMIS.Areas.Common.Utils.UI;
using aseeMIS.Areas.Common.ViewModels;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace aseeMIS.Areas.Common.Controllers
{
    [Area("Common")]
    [AppAuthorize]
    public class Entity360Controller(
        IServiceProvider serviceProvider,
        ToolbarItemsProvider toolbarItemsProvider,
        ILocalizationService localizationService,
        IAppConstantService appConstantService,
        IAppFormMenuService menuService,
        IUserContextService userContextService,
        IAppFormPermissionService formAccessService,
        IAppFormLanguageService appFormLanguageService
    ) : Controller
    {
        public async Task<IActionResult> Index(int id, string? entityPrimaryKeyValue)
        {
            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) =
                await AppAuthorizeHelper.ValidateReadPermissionAsync(HttpContext, formAccessService, menuService, userContextService, id, EnumContextFormType.PARENT);

            if (!isValid || appForm == null || formContext == null)
                return RedirectToAction("NoAccess", "Error");

            var invoker = new ServiceInvoker(serviceProvider);
            var modelType = invoker.GetViewModelType(projectNamespace, viewModel);

            object? entityModel = null;

            if (!string.IsNullOrEmpty(entityPrimaryKeyValue))
            {
                entityModel = await invoker.InvokeAsync(projectNamespace, serviceInterface, modelType, "GetByPrimaryKeyAsync", new object[] { entityPrimaryKeyValue });
                if (entityModel == null)
                    ViewBag.EntityLoadWarning = $"No record found for key '{entityPrimaryKeyValue}'.";
            }
            else
            {
                entityModel = Activator.CreateInstance(modelType);
            }

            // Figure out language (adapt to however you store it)
            var languageCode = formContext.LanguageCode;

            int[]? childIds = string.IsNullOrWhiteSpace(appForm.FlowFormChildIds)
                ? null
                : appForm.FlowFormChildIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => int.Parse(x.Trim()))
                    .ToArray();

            List<AppFormEntityChildDto>? childForms = null;

            if (childIds != null)
            {
                childForms = await appFormLanguageService
                    .SelectTitlesByFormIdsAsync(childIds, languageCode);
            }

            var constantValues = await appConstantService
                .SelectForDropdownAsync("SDLRejectType", formContext.LanguageCode);
            ViewBag.ConstantValues = constantValues;

            var form360AddPath = appForm.FlowForm360AddPath;
            var form360EditPath = appForm.FlowForm360EditPath;
            var resolvedDetailPath = !string.IsNullOrWhiteSpace(form360EditPath) ? form360EditPath : form360AddPath;
            var resolvedHeaderPath = !string.IsNullOrWhiteSpace(form360AddPath) ? form360AddPath : form360EditPath;

            var vm = new Entity360PageViewModel
            {
                FormId = formContext.FormId,
                FormSessionId = formContext.FormSessionId,
                ForeignAttribute = appForm.FlowForeignKeyAttributeName,
                PrimaryKeyAttribute = !string.IsNullOrWhiteSpace(appForm.FlowPrimaryKeyAttributeName) ? appForm.FlowPrimaryKeyAttributeName : appForm.FlowForeignKeyAttributeName,
                PrimaryKey = entityPrimaryKeyValue,
                HeaderPath = resolvedHeaderPath,
                DetailPath = resolvedDetailPath,
                ChildForms = childForms,
                Area = appForm.ProjectNameAcronym,
                EntityModelData = entityModel
            };

            return View("~/Areas/Common/Views/Entity360.cshtml", vm);
        }

        [HttpPost]
        public async Task<IActionResult> EntityDetail([FromBody] EntityDetailRequest request)
        {
            try
            {
                var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();

                var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) =
                    await AppAuthorizeHelper.ValidateActionAsync(
                        HttpContext,
                        formAccessService,
                        menuService,
                        userContextService,
                        formSessionId,
                        EumFormAccessAction.READ);

                if (!isValid || appForm == null || formContext == null)
                    return StatusCode(StatusCodes.Status403Forbidden, "No access.");

                if (request.Source == EnumEntityDetailHost.Grid &&
                    request.FormId > 0 &&
                    request.FormId != formContext.FormId)
                {
                    var (childValid, childAppForm, childFormContext, childProjectNamespace, childServiceInterface, childViewModel) =
                        await AppAuthorizeHelper.ValidateChildReadPermissionAsync(
                            HttpContext,
                            menuService,
                            userContextService,
                            request.FormId,
                            formSessionId);

                    if (!childValid || childAppForm == null || childFormContext == null)
                        return StatusCode(StatusCodes.Status403Forbidden, "No access.");

                    appForm = childAppForm;
                    formContext = childFormContext;
                    projectNamespace = childProjectNamespace;
                    serviceInterface = childServiceInterface;
                    viewModel = childViewModel;
                }

                Response.Headers["FormSessionId"] = formContext.FormSessionId;

                var isAdd = string.Equals(request.GridEditorAction, "add", StringComparison.OrdinalIgnoreCase)
                            || request.IsAdd;

                var viewPath = isAdd
                    ? appForm.FlowForm360AddPath
                    : appForm.FlowForm360EditPath;

                if (string.IsNullOrWhiteSpace(viewPath))
                    viewPath = appForm.FlowForm360AddPath ?? appForm.FlowForm360EditPath;

                if (string.IsNullOrWhiteSpace(viewPath))
                    return BadRequest("No entity detail path is configured for this form.");

                var invoker = new ServiceInvoker(serviceProvider);
                var modelType = invoker.GetViewModelType(projectNamespace, viewModel);

                var entityModel = await ResolveEntityDetailModelAsync(
                    invoker,
                    projectNamespace,
                    serviceInterface,
                    modelType,
                    request,
                    isAdd);

                var primaryKeyValue = modelType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null)
                    ?.GetValue(entityModel)
                    ?.ToString();

                var vm = new Entity360PageViewModel
                {
                    FormId = formContext.FormId,
                    FormSessionId = formContext.FormSessionId,
                    PrimaryKey = primaryKeyValue ?? request.EntityPrimaryKeyValue,
                    HeaderPath = viewPath,
                    DetailPath = viewPath,
                    Area = appForm.ProjectNameAcronym,
                    EntityModelData = entityModel
                };

                ViewData["EntityDetailSource"] = request.Source;
                ViewData["EntityDetailGridName"] = request.GridName;
                ViewData["EntityDetailAction"] = request.GridEditorAction;
                ViewData["EntityDetailIsAdd"] = isAdd;

                return PartialView("~/Areas/Common/Views/EntityDetail.cshtml", vm);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        private static async Task<object> ResolveEntityDetailModelAsync(
            ServiceInvoker invoker,
            string projectNamespace,
            string serviceInterface,
            Type modelType,
            EntityDetailRequest request,
            bool isAdd)
        {
            if (!isAdd && !string.IsNullOrWhiteSpace(request.EntityPrimaryKeyValue))
            {
                return await invoker.InvokeAsync(
                           projectNamespace,
                           serviceInterface,
                           modelType,
                           "GetByPrimaryKeyAsync",
                           new object[] { request.EntityPrimaryKeyValue })
                       ?? Activator.CreateInstance(modelType)
                       ?? throw new InvalidOperationException($"Could not create instance of {modelType.Name}.");
            }

            var model = Activator.CreateInstance(modelType)
                ?? throw new InvalidOperationException($"Could not create instance of {modelType.Name}.");

            SetParentForeignKey(model, request.ParentForeignAttributeName, request.ParentTargetId);
            await SetParentForeignKeyDisplayAsync(
                invoker,
                modelType,
                model,
                request.ParentForeignAttributeName,
                request.ParentTargetId,
                request.ParentTargetText);

            return model;
        }

        private static void SetParentForeignKey(object model, string? parentForeignAttributeName, string? parentTargetId)
        {
            if (string.IsNullOrWhiteSpace(parentForeignAttributeName) || string.IsNullOrWhiteSpace(parentTargetId))
                return;

            var prop = model.GetType().GetProperty(
                parentForeignAttributeName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (prop == null || !prop.CanWrite)
                return;

            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            var token = JToken.FromObject(parentTargetId);

            try
            {
                prop.SetValue(model, ConvertScalarToken(token, targetType, prop.PropertyType));
            }
            catch
            {
                // Detail-grid add can pass a parent key from another form. If conversion fails,
                // normal model validation will report the exact missing/invalid field.
            }
        }

        private static async Task SetParentForeignKeyDisplayAsync(
            ServiceInvoker invoker,
            Type modelType,
            object model,
            string? parentForeignAttributeName,
            string? parentTargetId,
            string? parentTargetText)
        {
            if (string.IsNullOrWhiteSpace(parentForeignAttributeName))
                return;

            var idProp = model.GetType().GetProperty(
                parentForeignAttributeName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (idProp == null)
                return;

            var fk = idProp.GetCustomAttribute<dsMIS.Core.Attributes.ExtendedForeignKeyAttribute>();
            var displayPropertyName = fk?.ColumnName;
            if (string.IsNullOrWhiteSpace(displayPropertyName))
                return;

            var displayProp = model.GetType().GetProperty(
                displayPropertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (displayProp == null || !displayProp.CanWrite)
                return;

            var resolvedText = parentTargetText;

            if (string.IsNullOrWhiteSpace(resolvedText) && !string.IsNullOrWhiteSpace(parentTargetId))
            {
                try
                {
                    var rows = await invoker.InvokeAsync(
                        fk!.ModulName,
                        fk.ServiceName,
                        modelType,
                        "GetDropdownByForeignKeyAsync",
                        new object?[] { parentForeignAttributeName, parentTargetId });

                    if (rows is System.Collections.IEnumerable enumerable)
                    {
                        var first = enumerable.Cast<object?>().FirstOrDefault(x => x != null);
                        if (first != null)
                        {
                            resolvedText = ReadDisplayValue(first, displayPropertyName);
                        }
                    }
                }
                catch
                {
                    // Best-effort hydration only. The FK id is already set and the editor can still save.
                }
            }

            if (!string.IsNullOrWhiteSpace(resolvedText))
            {
                displayProp.SetValue(model, resolvedText);
            }
        }

        private static string? ReadDisplayValue(object row, string displayPropertyName)
        {
            var props = row.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var displayProp = props.FirstOrDefault(p =>
                string.Equals(p.Name, displayPropertyName, StringComparison.OrdinalIgnoreCase));
            if (displayProp != null)
                return displayProp.GetValue(row)?.ToString();

            if (props.Length >= 2)
                return props[1].GetValue(row)?.ToString();

            return null;
        }

        private static object? ConvertScalarToken(JToken scalar, Type targetType, Type propertyType)
        {
            if (scalar.Type == JTokenType.Null && Nullable.GetUnderlyingType(propertyType) != null)
                return null;

            if (targetType == typeof(DateOnly))
                return DateOnly.Parse(scalar.Value<string>()!);

            if (targetType == typeof(TimeOnly))
                return TimeOnly.Parse(scalar.Value<string>()!);

            if (targetType.IsEnum)
                return Enum.Parse(targetType, scalar.Value<string>()!, true);

            return scalar.ToObject(targetType);
        }
    }
}

