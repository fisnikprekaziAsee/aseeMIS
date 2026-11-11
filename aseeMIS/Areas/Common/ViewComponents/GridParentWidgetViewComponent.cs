using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using aseeMIS.Areas.Common.DTOs;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Services.Interfaces;
using aseeMIS.Areas.Common.Utils.UI;

namespace aseeMIS.Areas.Common.ViewComponents
{
    [ViewComponent(Name = "GridParentWidget")]
    public sealed class GridParentWidgetViewComponent(IServiceProvider serviceProvider, ToolbarItemsProvider toolbarItemsProvider, ILocalizationService localizationService,
                                        IAppFormMenuService menuService, IAppFormPermissionService formAccessService, IUserContextService userContextService) 
        : GridBaseWidgetViewComponent(serviceProvider, toolbarItemsProvider, localizationService, menuService, formAccessService, userContextService)
    {
        public Task<IViewComponentResult> InvokeAsync(GridRequest req) => RunAsync(req);

        protected override string SelectMethodName()
        {
            if (!string.IsNullOrEmpty(base.FormMenuDto.FlowMethodName)) return base.FormMenuDto.FlowMethodName;
            // claim-type override for parent
            if (base.FormMenuDto.TokenClaimType == true) return "SelectAllByClaimTypeAsync";
            return "SelectAllAsync";
        }

        //protected override IEnumerable<object> BuildBackendArgs(GridRequest req, BuildContext ctx)
        //{
        //    var args = base.BuildBackendArgs(req, ctx).ToList();
        //    if (ctx.FormParams.Tokens.ClaimType == true && req.ContextFormType == ContextFormType.PARENT)
        //    {
        //        var dsClaims = string.Join(";", HttpContext.User.Claims.Where(c => c.Type == "DS").Select(c => c.Value));
        //        // base already added userid/lang; just ensure claims last
        //        if (!args.Contains(dsClaims)) args.Add(dsClaims);
        //    }
        //    return args;
        //}
    }
}
