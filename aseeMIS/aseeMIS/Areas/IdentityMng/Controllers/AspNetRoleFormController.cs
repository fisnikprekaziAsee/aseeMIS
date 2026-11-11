using dsAppMng.Core.Services.Interfaces;
using dsAspNet.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.IdentityMng.Controllers
{
    [Area("IdentityMng")]
    public class AspNetRoleFormController(
        IAspNetRoleFormService service,
        IAspNetRoleService roleService,
        IAppConstantService appConstantService) : Controller
    {
        public async Task<IActionResult> AspNetRoleForm()
        {
            var roles = await roleService.SelectAllForDropdownAsync();
            var actions = await appConstantService.SelectForDropdownAsync("FormAction", "sq-AL");

            ViewBag.Roles = roles;
            ViewBag.Actions = actions;

            return View("~/Areas/IdentityMng/Views/AspNetRoleForm.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetData([FromBody] LoadPermissionsRequest request)
        {
            if (request.RoleId <= 0 || string.IsNullOrWhiteSpace(request.FormAction))
                return BadRequest(new { success = false, error = "Invalid request parameters." });

            try
            {
                var data = await service.SelectAllRoleFormsAsync("sq-AL", request.RoleId, request.FormAction);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Modify([FromBody] ModifyPermissionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FormAction))
                return BadRequest(new { success = false, error = "Invalid request." });

            try
            {
                await service.ModifyAsync(request.FormIds ?? new List<int>(), request.RoleId, request.FormAction);
                return Ok(new { message = "Permissions updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Batch Update view
        public async Task<IActionResult> BatchUpdate()
        {
            var roles = await roleService.SelectAllForDropdownAsync();
            var actions = await appConstantService.SelectForDropdownAsync("FormAction", "sq-AL");

            ViewBag.Roles = roles;
            ViewBag.Actions = actions;

            return View("~/Areas/IdentityMng/Views/AspNetRoleFormBatchUpdate.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoadFormsForBatchUpdate([FromBody] LoadPermissionsRequest request)
        {
            if (request.RoleId < 0 || string.IsNullOrWhiteSpace(request.FormAction))
                return BadRequest(new { success = false, error = "Invalid request parameters." });

            try
            {
                var data = await service.GetAllAvailableFormsForRoleAsync("sq-AL", request.RoleId, request.FormAction);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> BatchUpdateSave([FromBody] BatchUpdateRequest request)
        {
            if (request == null || request.RoleId <= 0 || string.IsNullOrWhiteSpace(request.FormAction))
                return BadRequest(new { error = "Invalid request." });

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

        public class ModifyPermissionRequest
        {
            public List<int> FormIds { get; set; } = new();
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
