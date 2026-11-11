using dsMIS.Core.Services.Interfaces;
using aseeMIS.Areas.Common.DTOs;
using aseeMIS.Areas.Common.Utils.UI;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.Common.ViewComponents
{
    /// <summary>
    /// Paged parent grid: same UX as <see cref="GridParentWidgetViewComponent"/>, but the grid runs in
    /// RemoteMode (server-side paging/sort/filter/search). Activated by setting an AppForm's FormType
    /// to <c>CRUD_PAGED</c> in the database — no per-form code change is needed.
    /// <para>
    /// First page is fetched synchronously via <c>SelectPagedAsync</c> on the form's service so the grid is
    /// populated immediately; subsequent navigation hits <c>/api/griddata/paged</c>.
    /// </para>
    /// </summary>
    [ViewComponent(Name = "GridCrudPagedWidget")]
    public sealed class GridCrudPagedWidgetViewComponent(
        IServiceProvider serviceProvider,
        ToolbarItemsProvider toolbarItemsProvider,
        ILocalizationService localizationService,
        IAppFormMenuService menuService,
        IAppFormPermissionService formAccessService,
        IUserContextService userContextService)
        : GridBaseWidgetViewComponent(serviceProvider, toolbarItemsProvider, localizationService, menuService, formAccessService, userContextService)
    {
        public Task<IViewComponentResult> InvokeAsync(GridRequest req)
        {
            req.RemoteMode = true;
            return RunAsync(req);
        }

        protected override string SelectMethodName()
        {
            if (!string.IsNullOrEmpty(FormMenuDto.FlowMethodName)) return FormMenuDto.FlowMethodName;
            if (FormMenuDto.TokenClaimType == true) return "SelectAllByClaimTypeAsync";
            return "SelectAllAsync";
        }

        protected override string BuildRemoteUrl(GridRequest req) => "/api/griddata/paged";

        protected override string ViewPath
            => "~/Areas/Common/Views/Shared/Components/GridPagedWidget/Default.cshtml";
    }
}
