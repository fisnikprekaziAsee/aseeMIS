using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using dsHrmOrg.Core.Services.Interfaces;
using dsHrmOrg.Core.ViewModels;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.HrmOrg.Controllers
{
    [Area("HrmOrg")]
    public class OrganizationUnitController(IAppFormPermissionService formAccessService, IAppFormMenuService menuService, IUserContextService userContextService,
        IHrmOrgOrganizationUnitService organizationUnitService, IHrmOrgOrganizationUnitTypeService organizationUnitTypeService, IHrmOrgCostCenterService costCenterService) : Controller
    {
        public async Task<IActionResult> Index(int id)
        {
            // ✅ Validate session and permissions
            var (isValid, appForm, contextSessionDto, projectNamespace, _, _) =
                await AppAuthorizeHelper.ValidateReadPermissionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userContextService,
                    id,
                    contextFormType: EnumContextFormType.PARENT
                );

            if (!isValid || contextSessionDto == null)
                return (RedirectToAction("NoAccess", "Error"));

            // Get all organization units from your database
            var nodes = await organizationUnitService.SelectAllForDiagramAsync();

            return View("~/Areas/HrmOrg/Views/OrganizationUnit.cshtml", nodes);
        }

        [HttpGet]
        public async Task<IActionResult> GetNodes(int id)
        {
            // VALIDATION still applied
            var (isValid, _, _, _, _, _) =
                await AppAuthorizeHelper.ValidateReadPermissionAsync(
                    HttpContext, formAccessService, menuService, userContextService,
                    id, EnumContextFormType.PARENT);

            if (!isValid)
                return Unauthorized();

            var nodes = await organizationUnitService.SelectAllForDiagramAsync();

            return Json(nodes);
        }

        public async Task<IActionResult> LoadEditForm(int parentId, int? id)
        {
            var formId = 3126;

            // ✅ Validate session and permissions
            var (isValid, appForm, contextSessionDto, projectNamespace, _, _ ) =
                await AppAuthorizeHelper.ValidateReadPermissionAsync(
                    HttpContext,
                    formAccessService,
                    menuService,
                    userContextService,
                    formId,
                    contextFormType: EnumContextFormType.PARENT
                );

            if (!isValid || contextSessionDto == null)
                return (RedirectToAction("NoAccess", "Error"));

            // Load dropdown types
            var types = await organizationUnitTypeService.SelectAllForDropdownAsync();
            var costCenters = await costCenterService.SelectAllForDropdownAsync();
            var parentOrganizationUnits = await organizationUnitService.SelectAllForDropdownAsync();


            ViewBag.OrganizationTypes = types;
            ViewBag.CostCenters = costCenters;
            ViewBag.ParentOrganizationUnits = parentOrganizationUnits;
            ViewBag.FormId = contextSessionDto.FormId;
            ViewBag.FormSessionId = contextSessionDto.FormSessionId;

            // EDIT MODE
            if (id.HasValue && id.Value > 0)
            {
                var existing = await organizationUnitService.GetByIdAsync(id.Value);
                if (existing == null)
                    return NotFound();

                return PartialView(
                    "~/Areas/HrmOrg/Views/_OrganizationUnitEdit.cshtml",
                    existing
                );
            }

            // NEW MODE
            var model = new HrmOrgOrganizationUnitViewModel
            {
                OrganizationUnitId = 0,
                OrganizationUnitParentId = parentId
            };

            return PartialView(
                "~/Areas/HrmOrg/Views/_OrganizationUnitEdit.cshtml",
                model
            );
        }
    }
}
