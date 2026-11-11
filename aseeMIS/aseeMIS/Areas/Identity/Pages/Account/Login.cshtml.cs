using dsAspNet.Core.Services.Interfaces;
using dsAspNet.Core.ViewModels;
using dsMIS.Core.Services.Interfaces;
using dsIdentity.Core.Managers;
using dsIdentity.Core.Models;
using aseeMIS.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.RegularExpressions;

namespace aseeMIS.Areas.Identity.Pages.Account
{
    public class LoginModel(ApplicationUserManager userService, SignInManager<ApplicationUser> signInManager, ILogger<LoginModel> logger,
        IUserContextService userContextService, IAppSessionService sessionService, IAspNetUserLoginLogService loginLogService) : PageModel
    {

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (!ModelState.IsValid)
                return Page();
            try
            {
                // Find user by email first, then fall back to username

                var user = await userService.FindByEmailAsync(Input.Email)
                           ?? await userService.FindByNameAsync(Input.Email);

                if (user is null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }

                if (string.IsNullOrWhiteSpace(Input.Password))
                {
                    ModelState.AddModelError(string.Empty, "Password is required!");
                    return Page();
                }

                // Try sign in (this will increment/reset counters automatically)
                var result = await signInManager.PasswordSignInAsync(
                    user.UserName, Input.Password, Input.RememberMe, lockoutOnFailure: true);


                if (result.Succeeded || result.RequiresTwoFactor)
                {
                    await InsertLoginLogAsync(user, isSuccess: true);
                    userContextService.UpdateCacheStamp(user.Id, user.SecurityStamp);
                    sessionService.SetLanguageCode(user.LanguageCode ?? "sq-AL");
                    user.LastLoginDateTime = DateTime.UtcNow;
                    user.LockoutEnd = null;
                    await userService.UpdateAsync(user);
                    if (TempData["Requires2FASetup"] is bool needs2fa && needs2fa)
                    {
                        TempData["StatusMessage"] = "Please set up Two-Factor Authentication.";
                        return RedirectToPage("/Account/Manage/EnableAuthenticator", new { area = "Identity" });
                    }
                    if (result.RequiresTwoFactor)
                        return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                    return LocalRedirect(returnUrl);
                }
                else
                {

                    if (result.IsNotAllowed)
                        ModelState.AddModelError(string.Empty, "Please confirm your email before signing in.");
                    else if (result.IsLockedOut)
                        ModelState.AddModelError(string.Empty, "Your account is locked. Please try again later or use 'Forgot your password'.");
                    else
                        ModelState.AddModelError(string.Empty, "Invalid login attempt.");

                    return Page();
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                return Page();
            }
        }

        private async Task InsertLoginLogAsync(ApplicationUser user, bool isSuccess, string loginSource = "Web", string? extraInfo = null)
        {

            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var localIp = Dns.GetHostAddresses(Dns.GetHostName())
                             .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?
                             .ToString();


            //var userAgent = Request.Headers["User-Agent"].ToString();

            (string name, string version) = ParseUserAgent(Request.Headers["User-Agent"].ToString());

            var log = new AspNetUserLoginLogViewModel
            {
                UserId = user.Id,
                IsSuccess = isSuccess,
                ClientDateTimeOffset = DateTime.Now,
                ServerDateTimeUtc = DateTime.UtcNow,
                PublicIpAddress = remoteIp,
                PrivateIpAddress = localIp,
                RoleNames = string.Join(",", await userService.GetRolesAsync(user)),
                UserAgent = $"Browser:{name} Version:{version}",
                FormId = "Login",
                ClientLocation = null, // optional: fill via GeoIP if available
                CountryCode = "XK",
                LoginSource = loginSource,
                ExtraInfoJson = extraInfo
            };

            await loginLogService.AddLogAsync(log, user.LanguageCode);
        }

        private (string Name, string Version) ParseUserAgent(string ua)
        {
            if (string.IsNullOrWhiteSpace(ua)) return ("Unknown", "");

            // Order matters: Edge includes Chrome token too
            var m = Regex.Match(ua, @"Edg/([\d\.]+)"); if (m.Success) return ("Microsoft Edge", m.Groups[1].Value);
            m = Regex.Match(ua, @"OPR/([\d\.]+)"); if (m.Success) return ("Opera", m.Groups[1].Value);
            m = Regex.Match(ua, @"Firefox/([\d\.]+)"); if (m.Success) return ("Firefox", m.Groups[1].Value);
            m = Regex.Match(ua, @"Chrome/([\d\.]+)"); if (m.Success) return ("Google Chrome", m.Groups[1].Value);
            // Safari usually has Version/x then Safari/x
            m = Regex.Match(ua, @"Version/([\d\.]+)\s+Safari/"); if (m.Success) return ("Safari", m.Groups[1].Value);

            return ("Unknown", "");
        }


    }
}
