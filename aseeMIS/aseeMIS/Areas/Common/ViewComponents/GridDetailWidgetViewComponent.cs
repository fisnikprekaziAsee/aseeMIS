using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.Common.DTOs;
using aseeMIS.Areas.Common.Utils.UI;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.Common.ViewComponents
{
    [ViewComponent(Name = "GridDetailWidget")]
    public sealed class GridDetailWidgetViewComponent(IServiceProvider serviceProvider, ToolbarItemsProvider toolbarItemsProvider, ILocalizationService localizationService,
                                        IAppFormMenuService menuService, IAppFormPermissionService formAccessService, IUserContextService userContextService)
        : GridBaseWidgetViewComponent(serviceProvider, toolbarItemsProvider, localizationService, menuService, formAccessService, userContextService)
    {
        public Task<IViewComponentResult> InvokeAsync(GridRequest req)
        {
            // Force CHILD context here regardless of caller. FormType is derived from
            // AppForms.FormType inside GridBaseWidget.RunAsync so SDLT/CRUD_PAGED/etc.
            // are respected even when rendered as a child grid inside Entity360.
            req.ContextFormType = EnumContextFormType.CHILD;

            return RunAsync(req);
        }

        protected override string SelectMethodName()
         => string.IsNullOrEmpty(base.FormMenuDto.FlowMethodName) ? "SelectAllDetailAsync" : base.FormMenuDto.FlowMethodName;

        //protected override string SelectForeignKeysMethodName() => "GetForDropdownAsync";

        protected override IEnumerable<object> BuildBackendArgs(GridRequest req)
        {
            var args = new List<object>();

            if (!string.IsNullOrEmpty(req.ForeignAttributeName))
                args.Add(req.ForeignAttributeName!); // primary key first

            if (!string.IsNullOrEmpty(req.TargetId))
                args.Add(req.TargetId!); // foreign key second

            return args;
        }


        protected override GridUiConfig ConfigureUi()
            => new GridUiConfig
            {
                AllowGrouping = false,
                GroupBy = null,
                FrozenColumns = 0,
                FrozenRows = 0,
                FreezeApproveColumn = FormMenuDto.UIFreezeApproveColumn
            };
    }
}
