using System.Security.Claims;
using dsAppMng.Core.Services.Interfaces;
using aseeMIS.Services.Interfaces;
using aseeMIS.Utils;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.AppMng.Controllers
{
    [Area("AppMng")]
    [AppAuthorizeAttribute]
    public class AppFormFavoriteController(
        IAppFormFavoriteService favoriteService,
        IAppMenuService menuService,
        IAppSessionService sessionService) : Controller
    {
        [HttpGet]
        public async Task<IActionResult> GetByCurrentUser()
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized();

            var allowedMenus = await menuService.GetHrefByUser(
                userId.ToString(),
                sessionService.GetLanguageCode());
            var parentFormIds = allowedMenus
                .Where(x => x.FormParentId.HasValue)
                .Select(x => x.FormParentId!.Value)
                .ToHashSet();
            var allowedFormIds = allowedMenus
                .Where(x => !parentFormIds.Contains(x.FormId) && !string.IsNullOrWhiteSpace(x.FormHref))
                .Select(x => x.FormId)
                .ToHashSet();

            var favoriteIds = allowedMenus
                .Where(x => x.IsFavorite && allowedFormIds.Contains(x.FormId))
                .Select(x => x.FormId);

            return Json(favoriteIds);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add([FromBody] FavoriteRequest request, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized();

            var allowedMenus = await menuService.GetHrefByUser(
                userId.ToString(),
                sessionService.GetLanguageCode());
            var selectedMenu = allowedMenus.FirstOrDefault(x => x.FormId == request.FormId);
            var isParent = allowedMenus.Any(x => x.FormParentId == request.FormId);

            if (selectedMenu == null || isParent || string.IsNullOrWhiteSpace(selectedMenu.FormHref))
                return BadRequest(new { success = false, message = "Menuja nuk është valide për favourites." });

            await favoriteService.AddByUserAndFormAsync(userId, request.FormId, ct);
            return Ok(new { success = true, formId = request.FormId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove([FromBody] FavoriteRequest request, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized();

            await favoriteService.RemoveByUserAndFormAsync(userId, request.FormId, ct);
            return Ok(new { success = true, formId = request.FormId });
        }

        private bool TryGetCurrentUserId(out int userId)
            => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

        public sealed record FavoriteRequest(int FormId);
    }
}
