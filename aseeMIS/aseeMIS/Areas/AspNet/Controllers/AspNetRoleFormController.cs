using dsAppMng.Core.Services.Interfaces;
using dsAspNet.Core.Services.Interfaces;
using dsMIS.Core.Enums;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.AspNet.Controllers
{
    [Area("AspNet")]
    public class AspNetRoleFormController(
        IAspNetRoleFormService service, 
        IAspNetRoleService roleService, 
        IAppConstantService appConstantService,
        IAppFormPermissionService formPermissionService,
        IAppFormMenuService menuService,
        IUserContextService userContextService) : Controller
    {
        /// <summary>
        /// Full page mode: Batch Update opened from application menu
        /// </summary>
        public async Task<IActionResult> Index(int id)
        {
            return await PrepareBatchUpdateViewAsync(id, null, false, isPartial: false);
        }

        /// <summary>
        /// Partial view mode: Batch Update loaded inside Role360 modal
        /// </summary>
        public async Task<IActionResult> BatchUpdateModal(int id, int? roleId = null, bool lockRole = false)
        {
            return await PrepareBatchUpdateViewAsync(id, roleId, lockRole, isPartial: true);
        }

        /// <summary>
        /// Shared method to prepare batch update view for both modes
        /// </summary>
        private async Task<IActionResult> PrepareBatchUpdateViewAsync(int formId, int? roleId, bool lockRole, bool isPartial)
        {
            // Authorize READ permission for this form
            var (isValid, appForm, formContext, _, _, _) = 
                await AppAuthorizeHelper.ValidateReadPermissionAsync(
                    HttpContext, 
                    formPermissionService, 
                    menuService, 
                    userContextService,
                    formId,
                    EnumContextFormType.PARENT);

            if (!isValid || appForm == null || formContext == null)
            {
                return isPartial 
                    ? Content("<div class='alert alert-danger'>Access denied. You do not have permission to access this form.</div>") 
                    : Forbid();
            }

            // Store FormSessionId in response header for subsequent POST requests
            Response.Headers["FormSessionId"] = formContext.FormSessionId;

            // Populate dropdowns
            var roles = await roleService.SelectAllForDropdownAsync();
            var actions = await appConstantService.SelectForDropdownAsync("FormAction", "sq-AL");

            ViewBag.Roles = roles;
            ViewBag.Actions = actions;
            ViewBag.SelectedRoleId = roleId;
            ViewBag.LockRole = lockRole;
            ViewBag.FormSessionId = formContext.FormSessionId;

            var viewPath = "~/Areas/AspNet/Views/_AspNetRoleFormBatchUpdate.cshtml";
            
            return isPartial 
                ? PartialView(viewPath) 
                : View(viewPath);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoadFormsForBatchUpdate([FromBody] LoadPermissionsRequest request)
        {
            if (request.RoleId < 0 || string.IsNullOrWhiteSpace(request.FormAction))
                return BadRequest(new { success = false, error = "Invalid request parameters." });

            // Validate session authorization (READ permission already validated on page load)
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(formSessionId))
                return Unauthorized(new { success = false, error = "Missing authorization session." });

            try
            {
                // Get menu data using the stored procedure
                var data = await service.SelectMenuByRoleAsync(request.RoleId, "sq-AL");
                
                // Filter the data based on Form_* permission
                var filteredData = data.Where(item =>
                {
                    return request.FormAction switch
                    {
                        "IsReadable" => item.Form_IsReadable == true,
                        "IsInsertable" => item.Form_IsInsertable == true,
                        "IsUpdatable" => item.Form_IsUpdatable == true,
                        "IsDeletable" => item.Form_IsDeletable == true,
                        "IsApprovable" => item.Form_IsApprovable == true,
                        "IsExportable" => item.Form_IsExportable == true,
                        "IsPrintable" => item.Form_IsPrintable == true,
                        "IsModifiable" => item.Form_IsModifiable == true,
                        "IsPublishable" => item.Form_IsPublishable == true,
                        "IsAction1Enabled" => item.Form_IsAction1Enabled == true,
                        _ => false
                    };
                }).Select(item => new
                {
                    item.FormId,
                    item.FormParentId,
                    item.FormTitle,
                    item.FullPathTitle,
                    // Map the Role_* property based on the action
                    IsChecked = request.FormAction switch
                    {
                        "IsReadable" => item.Role_IsReadable == true,
                        "IsInsertable" => item.Role_IsInsertable == true,
                        "IsUpdatable" => item.Role_IsUpdatable == true,
                        "IsDeletable" => item.Role_IsDeletable == true,
                        "IsApprovable" => item.Role_IsApprovable == true,
                        "IsExportable" => item.Role_IsExportable == true,
                        "IsPrintable" => item.Role_IsPrintable == true,
                        "IsModifiable" => item.Role_IsModifiable == true,
                        "IsPublishable" => item.Role_IsPublishable == true,
                        "IsAction1Enabled" => item.Role_IsAction1Enabled == true,
                        _ => false
                    }
                }).ToList();

                return Json(new { success = true, data = filteredData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchUpdateSave([FromBody] BatchUpdateRequest request)
        {
            if (request == null || request.RoleId <= 0 || string.IsNullOrWhiteSpace(request.FormAction))
                return BadRequest(new { error = "Invalid request." });

            // Validate MODIFY permission using FormSessionId from header
            var formSessionId = HttpContext.Request.Headers["FormSessionId"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(formSessionId))
                return Unauthorized(new { error = "Missing authorization session." });

            var (isValid, appForm, formContext, _, _, _) = 
                await AppAuthorizeHelper.ValidateActionAsync(
                    HttpContext,
                    formPermissionService,
                    menuService,
                    userContextService,
                    formSessionId,
                    EumFormAccessAction.MODIFY);

            if (!isValid || appForm == null || formContext == null)
                return Forbid();

            try
            {
                await service.BatchUpdateRoleFormsAsync(request.RoleId, request.FormAction, request.SelectedFormIds ?? new List<int>());
                return Ok(new { message = "Permissions updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class LoadPermissionsRequest
        {
            public int RoleId { get; set; }
            public string FormAction { get; set; } = null!;
        }

        public class BatchUpdateRequest
        {
            public List<int> SelectedFormIds { get; set; } = new();
            public int RoleId { get; set; }
            public string FormAction { get; set; } = null!;
        }
    }
}
