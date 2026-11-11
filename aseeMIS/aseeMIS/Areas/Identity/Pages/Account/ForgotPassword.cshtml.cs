// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using dsIdentity.Core.Models;

using dsIdentity.Core.Managers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using aseeMIS.Services.Interfaces;
using dsMIS.Core.Exceptions;

namespace aseeMIS.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel(ApplicationUserManager userManager, IEmailSenderService emailSender) : PageModel
    {


        [BindProperty]
        public InputModel Input { get; set; }
        [TempData] public string Message { get; set; }
        [TempData] public bool IsSuccess { get; set; } = true;

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public void OnGet()
        {
            Message = "";
        }
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            try
            {
                var code = await userManager.SentForgotPaswordConfimationAsync(Input.Email);
                var callbackUrl = Url.Page("/Account/ResetPassword", pageHandler: null, values: new { area = "Identity", email = Input.Email, code = code }, protocol: Request.Scheme);
                var subject = "Reset your password – Dynamic Spheres MIS";
                var link = HtmlEncoder.Default.Encode(callbackUrl);

               var html = $@"
                            <div style='font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#1f2937'>
                              <h2 style='color:#0d9dda;margin:0 0 10px'>aseeMIS – Password reset</h2>

                              <p>We received a request to reset the password for your account.</p>
                              <p>Click the button below to choose a new password.</p>

                              <p style='margin:20px 0'>
                                <a href='{link}' style='background:#0d9dda;color:#fff;padding:10px 18px;
                                   border-radius:8px;text-decoration:none;font-weight:600'>Reset my password</a>
                              </p>

                              <p style='font-size:12px;color:#6b7280;margin:0 0 10px'>
                                If the button doesn’t work, copy and paste this link into your browser:<br/>{link}
                              </p>

                              <p style='font-size:12px;color:#6b7280;margin:10px 0 0'>
                                Didn’t request this? You can safely ignore this email, or contact your administrator.
                              </p>
                            </div>";

                await emailSender.SendEmailAsync(Input.Email, subject, html);
                IsSuccess = true;
                Message = "Verification email sent. Please check your email.";
                //return RedirectToPage(); // PRG so refresh doesn't resubmit
            }
            catch (AppException ex)
            {
                IsSuccess = false;
                Message = ex.Message;
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                Message = "Unexpected error while sending the confirmation email.";
            }

            // show the page with the error banner
            return Page();
        }
    }
}