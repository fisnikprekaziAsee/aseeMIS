using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.Common.DTOs;
using aseeMIS.Areas.Common.Utils.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace aseeMIS.Areas.Common.ViewComponents
{
    [ViewComponent(Name = "GridTranslatorWidget")]
    public sealed class GridTranslatorWidgetViewComponent(
        IServiceProvider serviceProvider,
        ToolbarItemsProvider toolbarItemsProvider,
        ILocalizationService localizationService,
        IAppFormMenuService menuService,
        IAppFormPermissionService formAccessService,
        IUserContextService userContextService,
        IConfiguration configuration)
        : GridBaseWidgetViewComponent(serviceProvider, toolbarItemsProvider, localizationService, menuService, formAccessService, userContextService)
    {
        // Fallback defaults preserve legacy behavior when "Localization" section is missing in appsettings.
        private const string DefaultSourceLanguageCode = "sq-AL";
        private const string DefaultDestinationLanguageCode = "en-US";

        public Task<IViewComponentResult> InvokeAsync(GridRequest req)
            => RunAsync(req);

        protected override string SelectMethodName()
            => string.IsNullOrEmpty(base.FormMenuDto.FlowMethodName) ? "SelectForTranslatorAsync" : base.FormMenuDto.FlowMethodName;

        protected override IEnumerable<object> BuildBackendArgs(GridRequest req)
        {
            // Source/destination languages are configurable via appsettings.json:
            //   "Localization": { "SourceLanguageCode": "sq-AL", "DestinationLanguageCode": "en-US" }
            // Falls back to legacy hardcoded values if the section/keys are absent so existing
            // deployments keep working without configuration changes.
            var source = configuration["Localization:SourceLanguageCode"];
            if (string.IsNullOrWhiteSpace(source)) source = DefaultSourceLanguageCode;

            var destination = configuration["Localization:DestinationLanguageCode"];
            if (string.IsNullOrWhiteSpace(destination)) destination = DefaultDestinationLanguageCode;

            return new List<object> { source, destination };
        }

        //protected override bool ShouldLoadForeignKeys() => false;

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
