// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using dsMIS.Core.Exceptions;
using dsIdentity.Core.Managers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Shared;

namespace aseeMIS.Areas.Identity.Pages.Account
{
    public class ConfirmEmailModel(ApplicationUserManager userManager) : PageModel
    {


        [TempData]
        public string StatusMessage { get; set; }

        public bool IsSuccess { get; private set; }

        // If your key is int, keep int?; if string key, change to string userId
        public async Task<IActionResult> OnGetAsync(string email, string code)
        {
            if (email is null || string.IsNullOrWhiteSpace(code))
                return RedirectToPage("/Index");
            try
            {
                var user = await userManager.ConfirmEmailConfimationAsync(email, code);
                if(user.LastLoginDateTime == null)
                {
                    user.LastLoginDateTime = DateTime.UtcNow;
                    code = await userManager.SentForgotPaswordConfimationAsync(email);
                    var callbackUrl = Url.Page("/Account/ResetPassword", pageHandler: null, values: new { area = "Identity", email = email, code = code }, protocol: Request.Scheme);

                    return Redirect(callbackUrl);
                }
                StatusMessage = "Tkank you for confirming your email.";
                IsSuccess = true;
            }
            catch (AppException ex)
            {
                IsSuccess = false;
                StatusMessage = ex.Message;
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                StatusMessage = ex.Message;
            }

            return Page();
        }
    }
}
