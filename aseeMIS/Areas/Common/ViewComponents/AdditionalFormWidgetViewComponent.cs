using dsMIS.Application.Utils;
using dsAppMng.Core.DTOs;
using dsMIS.Core.Attributes;
using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.Common.ViewModels;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace aseeMIS.Areas.Common.ViewComponents
{
    [ViewComponent(Name = "AdditionalFormWidget")]
    public sealed class AdditionalFormWidgetViewComponent(
        IServiceProvider serviceProvider,
        IAppFormPermissionService formAccessService,
        IAppFormMenuService menuService,
        IUserContextService userContextService,
        ILocalizationService localizationService) : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync(int formId, string? targetId, string? parentFormSessionId, string? childFormSessionId = null)
        {
            var (isValid, _, formContext, _, _, _) =
                await AppAuthorizeHelper.ValidateActionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userContextService,
                    parentFormSessionId ?? string.Empty,
                    EumFormAccessAction.READ);

            var appForm = await menuService.FindByFormIdAsync(formId);
            if (appForm == null)
                return Content("No access.");

            var projectNamespace = $"ds{appForm.ProjectNameAcronym}.Core";
            var serviceInterface = $"Services.Interfaces.I{appForm.ModelName}Service";
            var viewModel = $"ViewModels.{appForm.ViewModelName}ViewModel";

            if (!isValid || formContext == null)
                return Content("No access.");

            var serviceInvoker = new ServiceInvoker(serviceProvider);
            var presetService = serviceProvider.GetRequiredService<IAttributePresetService>();
            var sections = new List<AdditionalFormSectionViewModel>();
            var debugMessages = new List<string>();
            var layoutIds = ParseChildIds(appForm.FlowFormChildIds);

            if (layoutIds.Count > 0)
            {
                foreach (var layoutId in layoutIds)
                {
                    var section = await BuildSectionAsync(
                        serviceInvoker,
                        presetService,
                        layoutId,
                        appForm,
                        formContext.FormSessionId,
                        formContext.LanguageCode,
                        formContext.UserId,
                        projectNamespace,
                        serviceInterface,
                        viewModel,
                        targetId);

                    if (section != null)
                        sections.Add(section);
                    else
                        debugMessages.Add($"Layout {layoutId}: no active additional fields found.");
                }
            }
            else
            {
                var section = await BuildSectionAsync(
                    serviceInvoker,
                    presetService,
                    formId,
                    appForm,
                    formContext.FormSessionId,
                    formContext.LanguageCode,
                    formContext.UserId,
                    projectNamespace,
                    serviceInterface,
                    viewModel,
                    targetId);

                if (section != null)
                    sections.Add(section);
                else
                    debugMessages.Add($"Form {formId}: no active additional fields found.");
            }

            var vm = new AdditionalFormWidgetViewModel
            {
                FormId = formId,
                FormSessionId = formContext.FormSessionId,
                ParentFormSessionId = parentFormSessionId ?? string.Empty,
                TargetId = targetId,
                Title = appForm.FormTitle,
                Description = appForm.FormDescription,
                FlowForeignKeyAttributeName = appForm.FlowForeignKeyAttributeName ?? string.Empty,
                Sections = sections,
                DebugMessage = debugMessages.Count > 0 ? string.Join(" ", debugMessages) : null
            };

            return View("~/Areas/Common/Views/Shared/Components/AdditionalFormWidget/Default.cshtml", vm);
        }

        private async Task<AdditionalFormSectionViewModel?> BuildSectionAsync(
            ServiceInvoker serviceInvoker,
            IAttributePresetService presetService,
            int formId,
            dsMIS.Core.DTOs.AppFormMenuDto appForm,
            string formSessionId,
            string languageCode,
            int userId,
            string projectNamespace,
            string serviceInterface,
            string viewModelName,
            string? targetId)
        {
            var runtimeRowsObj = await serviceInvoker.InvokeAsync(
                "dsAppMng.Core",
                "Services.Interfaces.IAppAdditionalLayoutService",
                "SelectRuntimeByFormIdAsync",
                formId,
                languageCode);

            var runtimeRows = (runtimeRowsObj as IEnumerable<AppAdditionalFieldRuntimeDto>)?.ToList()
                ?? new List<AppAdditionalFieldRuntimeDto>();

            if (runtimeRows.Count == 0)
                return null;

            var fieldValues = await LoadCurrentValuesAsync(
                serviceInvoker,
                projectNamespace,
                serviceInterface,
                viewModelName,
                appForm.FlowForeignKeyAttributeName,
                targetId);

            var fields = new List<AdditionalFormFieldViewModel>(runtimeRows.Count);

            foreach (var row in runtimeRows)
            {
                fieldValues.TryGetValue(row.AdditionalFieldId, out var currentValue);
                var fieldVm = new AdditionalFormFieldViewModel
                {
                    AdditionalFieldId = row.AdditionalFieldId,
                    ValueId = currentValue.ValueId,
                    DisplayName = row.DisplayName,
                    FieldOrder = row.FieldOrder,
                    FieldTypeCode = row.FieldTypeCode,
                    HelpText = row.HelpText,
                    EnumJson = row.EnumJson,
                    MinValueDecimal = row.MinValueDecimal,
                    MaxValueDecimal = row.MaxValueDecimal,
                    ValueMinLength = row.ValueMinLength,
                    ValueMaxLength = row.ValueMaxLength,
                    RegexPattern = row.RegexPattern,
                    ForeignKeyPresetName = row.ForeignKeyPresetName,
                    ViewRowNo = row.ViewRowNo,
                    ViewColumnWidth = row.ViewColumnWidth,
                    IsMandatory = row.IsMandatory,
                    IsActive = row.IsActive,
                    ValueText = currentValue.ValueText,
                    EnumOptions = ParseEnumOptions(row.EnumJson, languageCode)
                };

                if (!string.IsNullOrWhiteSpace(row.ForeignKeyPresetName))
                {
                    var preset = presetService.TryGet(row.ForeignKeyPresetName, EnumFKLoadingType.Both);
                    if (preset != null)
                    {
                        fieldVm.ForeignKeyAttribute = preset;
                        fieldVm.ForeignKeyRows = await LoadForeignKeyRowsAsync(serviceInvoker, preset, languageCode, userId);
                    }
                }

                fields.Add(fieldVm);
            }

            var first = runtimeRows[0];
            return new AdditionalFormSectionViewModel
            {
                FormId = formId,
                FormSessionId = formSessionId,
                LayoutCode = first.LayoutCode,
                LayoutTitle = first.LayoutTitle,
                LayoutDescription = first.LayoutDescription,
                RowWidth = first.RowWidth,
                IconClass = first.IconClass,
                Fields = fields
            };
        }

        private static List<int> ParseChildIds(string? flowFormChildIds)
        {
            if (string.IsNullOrWhiteSpace(flowFormChildIds))
                return [];

            return flowFormChildIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var id) ? id : 0)
                .Where(x => x > 0)
                .Distinct()
                .ToList();
        }

        private async Task<Dictionary<int, (long? ValueId, string? ValueText)>> LoadCurrentValuesAsync(
            ServiceInvoker serviceInvoker,
            string projectNamespace,
            string serviceInterface,
            string viewModelName,
            string? foreignKeyAttributeName,
            string? targetId)
        {
            var map = new Dictionary<int, (long? ValueId, string? ValueText)>();
            if (string.IsNullOrWhiteSpace(foreignKeyAttributeName) || string.IsNullOrWhiteSpace(targetId))
                return map;

            var modelType = serviceInvoker.GetViewModelType(projectNamespace, viewModelName);
            var result = await serviceInvoker.InvokeAsync(
                projectNamespace,
                serviceInterface,
                modelType,
                "SelectAllDetailAsync",
                foreignKeyAttributeName,
                targetId);

            if (result is not System.Collections.IEnumerable rows)
                return map;

            foreach (var row in rows.Cast<object?>().Where(x => x != null))
            {
                var rowType = row!.GetType();
                var fieldId = ReadInt(row, rowType, "AdditionalFieldId") ?? ReadInt(row, rowType, "FieldId");
                if (!fieldId.HasValue)
                    continue;

                var valueId = ReadLong(row, rowType, "ValueId");
                var valueText = ReadString(row, rowType, "ValueText");
                map[fieldId.Value] = (valueId, valueText);
            }

            return map;
        }

        private async Task<IEnumerable<object>> LoadForeignKeyRowsAsync(
            ServiceInvoker serviceInvoker,
            ExtendedForeignKeyAttribute preset,
            string languageCode,
            int userId)
        {
            var args = BuildPresetParameters(preset.Parameters, languageCode, userId);
            var result = await serviceInvoker.InvokeAsync(
                preset.ModulName,
                preset.ServiceName,
                preset.MethodName360,
                args);

            return (result as System.Collections.IEnumerable)?.Cast<object>().ToList()
                ?? Enumerable.Empty<object>();
        }

        private static object[] BuildPresetParameters(string? rawParameters, string languageCode, int userId)
        {
            if (string.IsNullOrWhiteSpace(rawParameters))
                return Array.Empty<object>();

            var values = new List<object>();
            foreach (var token in rawParameters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (token.Contains("@LanguageCode", StringComparison.OrdinalIgnoreCase))
                    values.Add(languageCode);
                else if (token.Contains("@UserId", StringComparison.OrdinalIgnoreCase))
                    values.Add(userId);
                else
                    values.Add(token);
            }

            return values.ToArray();
        }

        private static IReadOnlyList<KeyValuePair<string, string>> ParseEnumOptions(
            string? enumJson,
            string languageCode)
        {
            if (string.IsNullOrWhiteSpace(enumJson))
                return Array.Empty<KeyValuePair<string, string>>();

            try
            {
                var token = JToken.Parse(enumJson);
                if (token.Type == JTokenType.Array)
                {
                    return token
                        .Children()
                        .Select(item =>
                        {
                            if (item.Type == JTokenType.Object)
                            {
                                var value = item["value"]?.ToString() ?? item["Value"]?.ToString() ?? item["key"]?.ToString() ?? item["Key"]?.ToString() ?? item.ToString();
                                var text = item["text"]?.ToString() ?? item["Text"]?.ToString() ?? item["label"]?.ToString() ?? item["Label"]?.ToString() ?? value;
                                return new KeyValuePair<string, string>(value, text);
                            }

                            var raw = item.ToString();
                            return new KeyValuePair<string, string>(raw, raw);
                        })
                        .ToList();
                }

                if (token.Type == JTokenType.Object)
                {
                    return token.Children<JProperty>()
                        .Select(x => new KeyValuePair<string, string>(
                            x.Name,
                            ResolveLocalizedEnumText(x.Value, languageCode)))
                        .ToList();
                }
            }
            catch
            {
                // Fallback to plain-text option when JSON is malformed.
            }

            return new[] { new KeyValuePair<string, string>(enumJson, enumJson) };
        }

        private static string ResolveLocalizedEnumText(JToken value, string languageCode)
        {
            if (value.Type != JTokenType.Object)
                return value.ToString();

            var translations = (JObject)value;
            var selected =
                translations.Properties().FirstOrDefault(x =>
                    string.Equals(x.Name, languageCode, StringComparison.OrdinalIgnoreCase))
                ?? translations.Properties().FirstOrDefault(x =>
                    string.Equals(x.Name, "en-US", StringComparison.OrdinalIgnoreCase))
                ?? translations.Properties().FirstOrDefault();

            return selected?.Value.ToString() ?? string.Empty;
        }

        private static int? ReadInt(object row, Type rowType, string propertyName)
        {
            var prop = rowType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null)
                return null;

            var value = prop.GetValue(row);
            if (value == null)
                return null;

            return Convert.ToInt32(value);
        }

        private static long? ReadLong(object row, Type rowType, string propertyName)
        {
            var prop = rowType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null)
                return null;

            var value = prop.GetValue(row);
            if (value == null)
                return null;

            return Convert.ToInt64(value);
        }

        private static string? ReadString(object row, Type rowType, string propertyName)
        {
            var prop = rowType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return prop?.GetValue(row)?.ToString();
        }
    }
}
