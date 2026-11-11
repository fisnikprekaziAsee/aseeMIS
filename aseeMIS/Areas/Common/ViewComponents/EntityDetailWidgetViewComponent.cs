using dsMIS.Application.Utils;
// Areas/Common/ViewComponents/EntityDetailWidgetViewComponent.cs
using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using dsMIS.Core.Utils;
using aseeMIS.Areas.Common.DTOs;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace aseeMIS.Areas.Common.ViewComponents
{
    /// <summary>
    /// Renders a detail partial with the provided model and exposes FormSessionId to the view.
    /// Loads FK dropdown data based on ExtendedForeignKeyAttribute / UsePresetAttribute.
    /// </summary>
    [ViewComponent(Name = "EntityDetailWidget")]
    public sealed class EntityDetailWidgetViewComponent(
        IServiceProvider serviceProvider,
        ILocalizationService localizationService,
        IAppFormMenuService menuService,
        IAppFormPermissionService formAccessService,
        IUserContextService userContextService) : ViewComponent
    {

        public async Task<IViewComponentResult> InvokeAsync(EntityDetailRequest req)
        {
            // 1) authorize & resolve once
            var (isValid, appForm, formContext, projectNamespace, serviceInterface, viewModel) =
                await AppAuthorizeHelper.ValidateActionAsync(HttpContext, formAccessService, menuService, userContextService, req.FormSessionId, EumFormAccessAction.READ);

            if (!isValid || appForm is null || formContext is null)
                return Content("No access.");

            if (string.IsNullOrWhiteSpace(req.ViewPath))
                return new HtmlContentViewComponentResult(
                    new HtmlString("<div class=\"alert alert-warning mb-0\">Entity detail view path is not configured.</div>"));

            var serviceInvoker = new ServiceInvoker(serviceProvider);
            var modelType = serviceInvoker.GetViewModelType(projectNamespace, viewModel);

            // 2) FK & cascade
            var fkData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var fkDataSourceMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var fkDisplayMap = new List<object>();
            var cascade = new List<object>();

            var presetSvc = serviceProvider.GetRequiredService<IAttributePresetService>();

            // Cached per-model reflection info
            var fkMetas = ModelHelper.BuildFkMeta(modelType);

            foreach (var fkMeta in fkMetas)
            {
                // Resolve FK: direct attribute wins; else resolve preset each request (presets can change at runtime)
                var fk = fkMeta.DirectFk;

                if (fk == null && !string.IsNullOrWhiteSpace(fkMeta.PresetName))
                {
                    fk = presetSvc.TryGet(fkMeta.PresetName, fkMeta.PresetLoadingType);
                }

                // Skip if no FK, or is autocomplete
                if (fk == null || fk.IsAutoComplete)
                    continue;

                if (!string.IsNullOrWhiteSpace(fk.ColumnName))
                {
                    fkDisplayMap.Add(new
                    {
                        Field = fkMeta.PropertyName,
                        DisplayField = fk.ColumnName,
                        DataSourceKey = fkMeta.DataSourceKey
                    });
                }

                // Grid-only FKs can still reuse a shared Entity360 datasource for payload hydration.
                if (fk.LoadingType == EnumFKLoadingType.Grid)
                {
                    if (!string.IsNullOrWhiteSpace(fkMeta.DataSourceKey)
                        && !string.Equals(fkMeta.DataSourceKey, fkMeta.PropertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        fkDataSourceMap[fkMeta.PropertyName] = fkMeta.DataSourceKey;
                    }

                    continue;
                }

                if (!string.IsNullOrWhiteSpace(fkMeta.DataSourceKey)
                    && !string.Equals(fkMeta.DataSourceKey, fkMeta.PropertyName, StringComparison.OrdinalIgnoreCase))
                {
                    fkDataSourceMap[fkMeta.PropertyName] = fkMeta.DataSourceKey;

                    if (!string.IsNullOrWhiteSpace(fk.CascadeSource) && !string.IsNullOrWhiteSpace(fk.CascadeTarget))
                    {
                        cascade.Add(new
                        {
                            Source = fk.CascadeSource,
                            Target = fk.CascadeTarget,
                            TargetField = fkMeta.PropertyName
                        });
                    }

                    continue;
                }

                // Build parameters efficiently
                object[] ddlParams = Array.Empty<object>();
                if (!string.IsNullOrWhiteSpace(fk.Parameters))
                {
                    var parts = fk.Parameters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length > 0)
                    {
                        ddlParams = new object[parts.Length];
                        for (int i = 0; i < parts.Length; i++)
                        {
                            var p = parts[i];

                            if (p.Contains("@LanguageCode", StringComparison.OrdinalIgnoreCase))
                                ddlParams[i] = formContext.LanguageCode;
                            else if (p.Contains("@UserId", StringComparison.OrdinalIgnoreCase))
                                ddlParams[i] = formContext.UserId;
                            else
                                ddlParams[i] = p;
                        }
                    }
                }

                // IMPORTANT: In Entity360, your service interfaces are usually NON-generic -> do not pass modelType unless needed.
                // If fk.ServiceName points to a generic interface, you MUST provide the generic arg.
                // In your current Entity360 code you were not passing genericArg previously; keep it that way:
                var rows = await serviceInvoker.InvokeAsync(fk.ModulName, fk.ServiceName, modelType, fk.MethodName360, ddlParams);

                fkData["FK_" + fkMeta.PropertyName] = rows ?? Array.Empty<object>();

                if (!string.IsNullOrWhiteSpace(fk.CascadeSource) && !string.IsNullOrWhiteSpace(fk.CascadeTarget))
                {
                    cascade.Add(new
                    {
                        Source = fk.CascadeSource,
                        Target = fk.CascadeTarget,
                        TargetField = fkMeta.PropertyName
                    });
                }
            }

            // compute status text (avoid .Result)
            var isApprovedProp = req.Model?.GetType().GetProperty("IsApproved");
            if (isApprovedProp != null && (isApprovedProp.PropertyType == typeof(bool?) || isApprovedProp.PropertyType == typeof(bool)))
            {
                var val = isApprovedProp.GetValue(req.Model);
                if (val == null)
                    req.IsApprovedText = await localizationService.FindServerSideInformationMessageByKeyAndLanguageCodeAsync("WaitApproval", formContext.LanguageCode);
                else if ((bool)val == true)
                    req.IsApprovedText = await localizationService.FindServerSideInformationMessageByKeyAndLanguageCodeAsync("Approved", formContext.LanguageCode);
                else
                    req.IsApprovedText = await localizationService.FindServerSideInformationMessageByKeyAndLanguageCodeAsync("NotApproved", formContext.LanguageCode);
            }

            if (req.Source != EnumEntityDetailHost.Grid)
                req.IsAdd = string.IsNullOrWhiteSpace(req.EntityPrimaryKeyValue) || req.EntityPrimaryKeyValue == "0";

            ViewData["EntityDetailRequest"] = req;

            // Flatten into ViewData
            foreach (var kv in fkData)
                ViewData[kv.Key] = kv.Value;

            // If you need cascade map in view, expose it too:
            ViewData["CascadeMap"] = cascade;
            ViewData["FKDataSourceMap"] = fkDataSourceMap;
            ViewData["FKDisplayMap"] = fkDisplayMap;
            ViewData["EntityDetailViewPath"] = req.ViewPath;

            return View("~/Areas/Common/Views/Shared/Components/EntityDetailWidget/Default.cshtml", req.Model);
        }

    }
}
