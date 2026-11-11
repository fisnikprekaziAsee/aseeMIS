using System;
using System.Security.Claims;
using dsMIS.Core.Services.Interfaces;
using aseeMIS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace aseeMIS.Utils
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class AppAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            // 1) Must be authenticated
            if (user?.Identity?.IsAuthenticated != true)
            {
                context.Result = new RedirectToActionResult("Login", "Account", new { area = "Identity" });
                return;
            }

            // 2) Must have UserId
            if (!int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                context.Result = new RedirectToActionResult("Login", "Account", new { area = "Identity" });
                return;
            }

            // 3) Must have LanguageCode in session (your rule)
            var sessionService = context.HttpContext.RequestServices.GetRequiredService<IAppSessionService>();
            var languageCode = sessionService.GetLanguageCode();
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                context.Result = new RedirectToActionResult("Login", "Account", new { area = "Identity" });
                return;
            }

            // 4) Security stamp must match cache
            var userContextService = context.HttpContext.RequestServices.GetRequiredService<IUserContextService>();
            var userCtx = userContextService.GetUser(userId);

            var stampFromClaim = user.FindFirstValue("AspNet.Identity.SecurityStamp");

            if (userCtx == null ||
                string.IsNullOrWhiteSpace(stampFromClaim) ||
                string.IsNullOrWhiteSpace(userCtx.UserSecurityStamp) ||
                !string.Equals(stampFromClaim, userCtx.UserSecurityStamp, StringComparison.Ordinal))
            {
                context.Result = new RedirectToActionResult("Login", "Account", new { area = "Identity" });
                return;
            }

            // ✅ authorized
        }
    }
}
