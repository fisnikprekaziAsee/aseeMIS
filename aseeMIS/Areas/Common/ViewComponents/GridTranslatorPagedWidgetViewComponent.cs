using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.Common.DTOs;
using aseeMIS.Areas.Common.Utils.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace aseeMIS.Areas.Common.ViewComponents
{
    /// <summary>
    /// Paged translator grid: same UX as <see cref="GridTranslatorWidgetViewComponent"/>, but the grid
    /// runs in RemoteMode (server-side paging/sort/filter/search). Activated by setting an AppForm's
    /// FormType to <c>TRANSLATOR_PAGED</c> in the database — no per-form code change is needed.
    /// First page is fetched synchronously through <c>GridBaseWidget.RunAsync</c> so the grid is populated
    /// immediately; subsequent navigation hits <c>/api/griddata/translator-paged</c>.
    /// </summary>
    [ViewComponent(Name = "GridTranslatorPagedWidget")]
    public sealed class GridTranslatorPagedWidgetViewComponent(
        IServiceProvider serviceProvider,
        ToolbarItemsProvider toolbarItemsProvider,
        ILocalizationService localizationService,
        IAppFormMenuService menuService,
        IAppFormPermissionService formAccessService,
        IUserContextService userContextService,
        IConfiguration configuration)
        : GridBaseWidgetViewComponent(serviceProvider, toolbarItemsProvider, localizationService, menuService, formAccessService, userContextService)
    {
        private const string DefaultSourceLanguageCode = "sq-AL";
        private const string DefaultDestinationLanguageCode = "en-US";

        public Task<IViewComponentResult> InvokeAsync(GridRequest req)
        {
            req.RemoteMode = true;
            return RunAsync(req);
        }

        protected override string SelectMethodName()
            => string.IsNullOrEmpty(FormMenuDto.FlowMethodName) ? "SelectForTranslatorAsync" : FormMenuDto.FlowMethodName;

        protected override IEnumerable<object> BuildBackendArgs(GridRequest req)
        {
            // Same as legacy GridTranslatorWidget — these args are forwarded to the first-page fetch in the
            // base; later DataManager calls hit /api/griddata/translator-paged which re-reads config itself.
            var source = configuration["Localization:SourceLanguageCode"];
            if (string.IsNullOrWhiteSpace(source)) source = DefaultSourceLanguageCode;

            var destination = configuration["Localization:DestinationLanguageCode"];
            if (string.IsNullOrWhiteSpace(destination)) destination = DefaultDestinationLanguageCode;

            return new List<object> { source, destination };
        }

        protected override string BuildRemoteUrl(GridRequest req) => "/api/griddata/translator-paged";

        protected override string ViewPath
            => "~/Areas/Common/Views/Shared/Components/GridPagedWidget/Default.cshtml";

        protected override GridUiConfig ConfigureUi()
            => new GridUiConfig
            {
                AllowGrouping = false,
                GroupBy = null,
                FrozenColumns = FormMenuDto.UIFrozenColumns ?? 0,
                FreezeApproveColumn = true,
                FrozenRows = FormMenuDto.UIFrozenRows ?? 0
            };
    }
}
