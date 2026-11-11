using dsMIS.Application.Utils;

using dsMIS.Core.Attributes;
using dsMIS.Core.DTOs;
using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using dsMIS.Core.Utils;
using aseeMIS.Areas.Common.DTOs;
using aseeMIS.Areas.Common.Utils.Parameters;
using aseeMIS.Areas.Common.Utils.UI;
using aseeMIS.Areas.Common.ViewModels;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace aseeMIS.Areas.Common.ViewComponents
{
    /// Base class – not discovered (abstract, no Invoke), contains the template method.
    public abstract class GridBaseWidgetViewComponent(IServiceProvider serviceProvider, ToolbarItemsProvider toolbarItemsProvider, ILocalizationService localizationService,
                                            IAppFormMenuService menuService, IAppFormPermissionService formAccessService, IUserContextService userContextService)
        : ViewComponent
    {

        public string? MethodName { get; set; }

        public ContextSessionDto? FormContext { get; set; }
        public AppFormMenuDto FormMenuDto { get; set; }

        // ---------------- TEMPLATE METHOD ----------------
        public async Task<IViewComponentResult> RunAsync(GridRequest req)
        {
            // 1) authorize & resolve once
            var authorization = req.ContextFormType == EnumContextFormType.CHILD
                                && !string.IsNullOrWhiteSpace(req.FormSessionId)
                ? await AppAuthorizeHelper.ValidateChildReadPermissionAsync(
                    HttpContext,
                    menuService,
                    userContextService,
                    req.FormId,
                    parentFormSessionId: null,
                    childFormSessionId: req.FormSessionId)
                : await AppAuthorizeHelper.ValidateReadPermissionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userContextService,
                    req.FormId,
                    req.ContextFormType);

            var (isValid, appForm, formContext, proj, svc, vmName) = authorization;

            if (!isValid || appForm is null || formContext is null)
                throw new InvalidOperationException("No access.");

            this.FormContext = formContext; // <-- set the property
            FormMenuDto = appForm;

            // AppForms.FormType is the single source of truth for read-only/log forms (SDLT)
            // and paged/translator variants. Mapping here makes child grids (Entity360) and
            // standalone grids (CRUDController) behave consistently for the same FormId.
            req.FormType = EnumFormTypeMapper.MapFromAppForm(appForm.FormType);

            var serviceInvoker = new ServiceInvoker(serviceProvider);
            var modelType = serviceInvoker.GetViewModelType(proj, vmName);

            // 2) backend method + args (overridable)
            string method = SelectMethodName();
            var args = BuildBackendArgs(req).ToArray();

            // In RemoteMode the DataManager fetches the first page itself (single small AJAX) so we don't
            // pre-fetch on the server. This keeps the wire format and the binding code paths identical for
            // page 1 and page 2+, which makes paging/sort/filter work reliably without dataSource-swap hacks.
            // For local-mode grids we still load everything as before.
            object? data;
            int? totalCount = null;
            int? pageSize = req.RemoteMode ? DefaultPageSize : (int?)null;

            if (req.RemoteMode)
            {
                data = Array.CreateInstance(modelType, 0);
            }
            else
            {
                data = await serviceInvoker.InvokeAsync(proj, svc, modelType, method, args);
            }

            // 3) FK & cascade (overridable) - using optimized BuildFkMeta helper
            Dictionary<string, object>? fkData = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string>? fkDataSourceMap = new(StringComparer.OrdinalIgnoreCase);
            List<object>? cascade = [];

            var presetSvc = serviceProvider.GetRequiredService<IAttributePresetService>();
            var fkMetas = ModelHelper.BuildFkMeta(modelType);

            foreach (var fkMeta in fkMetas)
            {
                // Resolve FK: direct attribute wins; else resolve preset each request (presets can change at runtime)
                var fk = fkMeta.DirectFk;

                if (fk == null && !string.IsNullOrWhiteSpace(fkMeta.PresetName))
                {
                    fk = presetSvc.TryGet(fkMeta.PresetName, fkMeta.PresetLoadingType);
                }

                // Skip FK data loading if:
                // 1. No FK resolved
                // 2. IsAutoComplete is true (search is done via method, no need for FK data)
                // 3. LoadingType is Entity360 (this is a Grid view, skip loading)
                if (fk == null || fk.IsAutoComplete || fk.LoadingType == EnumFKLoadingType.Entity360)
                    continue;

                if (!string.IsNullOrWhiteSpace(fkMeta.DataSourceKey)
                    && !string.Equals(fkMeta.DataSourceKey, fkMeta.PropertyName, StringComparison.OrdinalIgnoreCase))
                {
                    fkDataSourceMap[fkMeta.PropertyName] = fkMeta.DataSourceKey;

                    if (!string.IsNullOrWhiteSpace(fk.CascadeSource) && !string.IsNullOrWhiteSpace(fk.CascadeTarget))
                        cascade.Add(new { Source = fk.CascadeSource, Target = fk.CascadeTarget, TargetField = fkMeta.PropertyName });

                    continue;
                }

                var ddlParams = new List<object>();
                if (!string.IsNullOrWhiteSpace(fk.Parameters))
                {
                    foreach (var param in fk.Parameters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (param.Contains("@LanguageCode", StringComparison.OrdinalIgnoreCase))
                            ddlParams.Add(FormContext.LanguageCode);
                        else if (param.Contains("@UserId", StringComparison.OrdinalIgnoreCase))
                            ddlParams.Add(FormContext.UserId);
                        else
                            ddlParams.Add(param);
                    }
                }

                // Minimal: if this FK is the one being scoped AND we have TargetId,
                // call a narrow method that returns just that one row.
                var methodName = fk.MethodNameGrid.IsNullOrEmpty() ? SelectForeignKeysMethodName() : fk.MethodNameGrid;

                object? rows;

                if (!string.IsNullOrWhiteSpace(req.ForeignAttributeName)
                    && !string.IsNullOrWhiteSpace(req.TargetId)
                    && string.Equals(fkMeta.PropertyName, req.ForeignAttributeName, StringComparison.OrdinalIgnoreCase))
                {
                    var narrowParams = new object?[] { req.PrimaryAttributeName, req.TargetId };
                    rows = await serviceInvoker.InvokeAsync(fk.ModulName, fk.ServiceName, modelType, "GetDropdownByForeignKeyAsync", narrowParams);
                }
                else
                {
                    rows = await serviceInvoker.InvokeAsync(fk.ModulName, fk.ServiceName, modelType, methodName, [.. ddlParams]);
                }

                fkData["FK_" + fkMeta.PropertyName] = rows;

                if (!string.IsNullOrWhiteSpace(fk.CascadeSource) && !string.IsNullOrWhiteSpace(fk.CascadeTarget))
                    cascade.Add(new { Source = fk.CascadeSource, Target = fk.CascadeTarget, TargetField = fkMeta.PropertyName });
            }


            // 4) UI (overridable)
            var ui = ConfigureUi();
            var (hasColumnIndexDefaultVisibility, columnIndexDefaultVisibility) =
                ParseColumnIndexDefaultVisibility(appForm.FormUIConfiguration);

            // A CRUD grid can point to a separate Entity360 form. The editor partials
            // belong to that linked form (for example BankParty grid 14 -> detail form 16),
            // not necessarily to the grid form itself.
            AppFormMenuDto? editorForm = null;
            if (appForm.FlowForm360Id is > 0 && appForm.FlowForm360Id != appForm.FormId)
            {
                editorForm = await menuService.FindByFormIdAsync(appForm.FlowForm360Id.Value);
            }

            var editorAddPath = appForm.FlowForm360AddPath
                ?? editorForm?.FlowForm360AddPath;
            var editorEditPath = appForm.FlowForm360EditPath
                ?? editorForm?.FlowForm360EditPath;

            // 5) Toolbar/Context menu
            var toolbarItems = toolbarItemsProvider.GetToolbarItems(appForm, FormContext, req.FormType);
            var ctxMenuItems = await toolbarItemsProvider.GetContextMenuItems(FormContext);

            // 6) vm + render single shared grid view
            var gridId = string.IsNullOrWhiteSpace(req.GridName)
                ? $"GRID_{FormContext.FormSessionId}_{modelType.FullName!.Replace(".", "_")}"
                : req.GridName;

            var vm = new GridWidgetViewModel
            {
                FormId = FormContext.FormId,
                FormSessionId = FormContext.FormSessionId,
                Form360Id = appForm.FlowForm360Id ?? req.FormId,
                ContextFormType = req.ContextFormType,
                GridName = gridId,
                ModelType = modelType,
                Data = data,

                ForeignKeyData = fkData,
                ForeignKeyDataSourceMap = fkDataSourceMap,
                CascadeMap = cascade,

                AllowGrouping = ui.AllowGrouping,
                GroupBy = ui.GroupBy,
                FrozenColumns = ui.FrozenColumns,
                FreezeApproveColumn  = ui.FreezeApproveColumn,
                FrozenRows = ui.FrozenRows,

                RemoteMode = req.RemoteMode,
                RemoteUrl = req.RemoteMode ? BuildRemoteUrl(req) : null,
                TotalCount = totalCount,
                PageSize = pageSize,

                ToolbarItems = toolbarItems,
                ContextMenuItems = ctxMenuItems,

                // Partial-dialog editor is opt-in. Existing grids keep the old Syncfusion editor because these are null.
                EditorPartialViewPath = req.EditorPartialViewPath,
                EditorPartialAddPath = editorAddPath,
                EditorPartialEditPath = editorEditPath,
                EditorDialogTitle = req.EditorDialogTitle,
                EditorDialogDescription = appForm.FormDescription,
                EditorDialogLayoutJson = appForm.FormUIConfiguration,
                PreferredEditMode = req.PreferredEditMode,
                ParentForeignAttributeName = req.ForeignAttributeName,
                ParentTargetId = req.TargetId,
                ParentTargetText = req.TargetText,
                HasColumnIndexDefaultVisibility = hasColumnIndexDefaultVisibility,
                ColumnIndexDefaultVisibility = columnIndexDefaultVisibility,

                Area = appForm.ProjectNameAcronym
            };

            return View(ViewPath, vm);
        }

        // Local-mode default. Paged widgets override to render the dedicated paged view (with DataManager
        // binding + Menu filter + Enter-only search) — keeps the local Default.cshtml free of remote-mode code.
        protected virtual string ViewPath
            => "~/Areas/Common/Views/Shared/Components/GridWidget/Default.cshtml";

        // ---------------- HOOKS (override per grid type) ----------------

        // Default page size for RemoteMode initial fetch. Override per widget type if needed.
        protected virtual int DefaultPageSize => 20;

        /// <summary>
        /// Returns the URL the front-end DataManager will hit for subsequent page/sort/filter requests.
        /// Override in paged widgets (e.g. translator-paged points to a different endpoint).
        /// </summary>
        protected virtual string BuildRemoteUrl(GridRequest req) => "/api/griddata/paged";

        /// <summary>
        /// Map the "list-everything" method name to its paged counterpart for the initial render.
        /// Default convention: SelectAllAsync -> SelectPagedAsync; everything else keeps the suffix swap idea
        /// (e.g. SelectForTranslatorAsync -> SelectForTranslatorPagedAsync). Widgets can override when needed.
        /// </summary>
        protected virtual string BuildPagedMethodName(string baseMethodName)
            => baseMethodName.Equals("SelectAllAsync", StringComparison.Ordinal)
                ? "SelectPagedAsync"
                : baseMethodName.Replace("Async", "PagedAsync", StringComparison.Ordinal);

        protected virtual string SelectMethodName()
        {
            // default CRUD parent
            if (!string.IsNullOrEmpty(FormMenuDto.FlowMethodName)) return FormMenuDto.FlowMethodName;
            return "SelectAllAsync";
        }

        protected virtual IEnumerable<object> BuildBackendArgs(GridRequest req)
        {
            var args = new List<object>();
            // pass tokens if defined
            if (FormMenuDto.TokenUserId == true) args.Add(FormContext.UserId);
            if (FormMenuDto.TokenLanguageCode == true) args.Add(FormContext.LanguageCode);

            // claim-based parent
            if (FormMenuDto.TokenClaimType == true && req.ContextFormType == EnumContextFormType.PARENT)
            {
                // adjust method in caller override if needed; this base just supplies args
                var dsClaims = string.Join(";", HttpContext.User.Claims.Where(c => c.Type == "DS").Select(c => c.Value));
                args.Add(dsClaims);
            }
            return args;
        }

        protected virtual string SelectForeignKeysMethodName() => "SelectAllForDropdownAsync";

        protected virtual GridUiConfig ConfigureUi()
        {
            return new GridUiConfig
            {
                AllowGrouping = FormMenuDto.UIAllowGrouping ?? false,
                GroupBy = string.IsNullOrWhiteSpace(FormMenuDto.UIGroupBy)
                    ? []              // no grouping
                    : FormMenuDto.UIGroupBy.Split(',', StringSplitOptions.RemoveEmptyEntries),
                FrozenColumns = FormMenuDto.UIFrozenColumns ?? 0,
                FrozenRows = FormMenuDto.UIFrozenRows ?? 0,
                FreezeApproveColumn = FormMenuDto.UIFreezeApproveColumn
            };
        }

        private static (bool HasConfiguration, HashSet<int> VisibleIndexes) ParseColumnIndexDefaultVisibility(string? formUiConfiguration)
        {
            if (string.IsNullOrWhiteSpace(formUiConfiguration))
                return (false, []);

            try
            {
                using var doc = JsonDocument.Parse(formUiConfiguration);

                if (!doc.RootElement.TryGetProperty("grid", out var gridElement)
                    || !gridElement.TryGetProperty("columnIndexDefaultVisibility", out var indexesElement)
                    || indexesElement.ValueKind != JsonValueKind.Array)
                {
                    return (false, []);
                }

                var indexes = new HashSet<int>();
                foreach (var item in indexesElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var index) && index >= 0)
                        indexes.Add(index);
                }

                return (true, indexes);
            }
            catch
            {
                return (false, []);
            }
        }
    }
}
