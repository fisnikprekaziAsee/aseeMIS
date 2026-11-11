using dsAppMng.Core.Services.Interfaces;
using dsMIS.Core.Services.Interfaces;
using dsIdentity.Core.Models;
using aseeMIS.Models;
using aseeMIS.Services.Interfaces; // ✅ Required for SessionHelper
using aseeMIS.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Security.Claims;

namespace aseeMIS.Controllers
{
    [AppAuthorizeAttribute]
    public class HomeController(
        IAppMenuService formService,
        ILocalizationService localizationService,
        SignInManager<ApplicationUser> signInManager,
        IAppSessionService dsSessionService,
        IWebHostEnvironment environment,
        ILogger<HomeController> logger) : Controller
    {
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            try
            {
                // var clientInfo = BuildClientInfo(); //
                //ViewBag.ClientInfo = clientInfo; //
                var formHierarchy = await formService.GetHrefByUser(userId, dsSessionService.GetLanguageCode());

                
                ViewData["FormHierarchyJson"] = JsonConvert.SerializeObject(formHierarchy);

                ViewData["LanguageStringsVersion"] = $"{dsSessionService.GetLanguageCode()}|{localizationService.LocalizationStringDatabaseVersion}";

                return View();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Home Index failed for user {UserId}", userId);
                if (environment.IsDevelopment())
                    throw;
                return RedirectToAction("Error", "Home");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetLanguageStrings()
        {
            var languageStrings = await localizationService.FindByLanguageCodeAsync(dsSessionService.GetLanguageCode());
            return Json(languageStrings);
        }

        [HttpGet]
        public async Task<IActionResult> GetLanguageStringsByStringType(string stringType)
        {
            var languageStrings = await localizationService.FindByStringTypeAndLanguageCodeAsync(stringType.ToUpper(), dsSessionService.GetLanguageCode());
            return Json(languageStrings);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        private object BuildClientInfo()
        {
            var request = HttpContext.Request;

            return new
            {
                IpAddress = GetClientIpAddress(),
                UserAgent = request.Headers["User-Agent"].ToString(),
                Language = request.Headers["Accept-Language"].ToString(),
                Referer = request.Headers["Referer"].ToString(),
                Host = request.Host.ToString(),
                Scheme = request.Scheme,
                BrowserInfo = GetBrowserDetails(request.Headers["User-Agent"]),
                ServerTime = DateTime.UtcNow.ToString("u")
            };
        }

        [HttpGet]
        public IActionResult GetClientInfo()
        {
            var request = HttpContext.Request;
            var connection = HttpContext.Connection;

            var clientInfo = new
            {
                IpAddress = GetClientIpAddress(),
                UserAgent = request.Headers["User-Agent"].ToString(),
                Language = request.Headers["Accept-Language"].ToString(),
                Referer = request.Headers["Referer"].ToString(),
                Host = request.Host.ToString(),
                Scheme = request.Scheme,
                BrowserInfo = GetBrowserDetails(request.Headers["User-Agent"]),
                ServerTime = DateTime.UtcNow.ToString("u")
            };

            return Ok(clientInfo);
        }

        private string GetClientIpAddress()
        {
            var forwardedHeader = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedHeader))
                return forwardedHeader;

            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        private string GetBrowserDetails(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "Unknown";

            if (userAgent.Contains("Edg")) return "Microsoft Edge";
            if (userAgent.Contains("Chrome")) return "Google Chrome";
            if (userAgent.Contains("Firefox")) return "Mozilla Firefox";
            if (userAgent.Contains("Safari") && !userAgent.Contains("Chrome")) return "Safari";
            if (userAgent.Contains("MSIE") || userAgent.Contains("Trident")) return "Internet Explorer";

            return "Other";
        }
    }
}
