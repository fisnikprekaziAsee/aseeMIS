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
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using aseeMIS.Services.Interfaces;
using dsMIS.Core.Exceptions;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;

namespace aseeMIS.Areas.Identity.Pages.Account.Manage
{
    public class EmailModel( ApplicationUserManager userManager, IEmailSenderService emailSender) : PageModel
    {
        [TempData] public bool IsEmailConfirmed { get; set; }

        [TempData] public string StatusMessage { get; set; }

       
        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "New email")]
            public string NewEmail { get; set; }
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            var email = await userManager.GetEmailAsync(user);
           
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
                return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
            
            Input = new InputModel
            {
                Email = user.Email,
                NewEmail = user.Email
            };
            IsEmailConfirmed = await userManager.IsEmailConfirmedAsync(user);

            //await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostChangeEmailAsync()
        {
            if (!ModelState.IsValid) return Page();          

            try
            {
                if (string.Equals(Input.Email?.Trim(), Input.NewEmail?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    StatusMessage = "- Your new email is the same as the current email. No change required!";
                    return Page();
                }
                var code = await userManager.SendChangeEmailConfirmationAsync(Input.Email, Input.NewEmail); // tuple version
                var callbackUrl = Url.Page("/Account/ConfirmEmail", pageHandler: null, values: new { area = "Identity", email = Input.NewEmail, code = code }, protocol: Request.Scheme);

                var subject = "Confirm your new email – Dynamic Spheres MIS";
                var link = HtmlEncoder.Default.Encode(callbackUrl);

                var html = $@"
                            <div style='font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#1f2937'>
                              <h2 style='color:#0d9dda;margin:0 0 10px'>aseeMIS – Email reset</h2>

                              <p>We received a request to reset the Email for your account.</p>
                              <p>Click the button below to confirm new Email.</p>

                              <p style='margin:20px 0'>
                                <a href='{link}' style='background:#0d9dda;color:#fff;padding:10px 18px;
                                   border-radius:8px;text-decoration:none;font-weight:600'>Confirm new Email</a>
                              </p>

                              <p style='font-size:12px;color:#6b7280;margin:0 0 10px'>
                                If the button doesn’t work, copy and paste this link into your browser:<br/>{link}
                              </p>

                              <p style='font-size:12px;color:#6b7280;margin:10px 0 0'>
                                Didn’t request this? You can safely ignore this email, or contact your administrator.
                              </p>
                            </div>";

                await emailSender.SendEmailAsync(Input.NewEmail, subject, html);
                StatusMessage = "Verification email sent. Please check your email.";
                //return RedirectToPage(); // PRG so refresh doesn't resubmit
            }
            catch (AppException ex)
            {
                StatusMessage = "Error " +ex.Message;
            }
            catch (Exception ex)
            {
                StatusMessage = "Error " + "Unexpected error while sending the confirmation email.";
            }
            return Page();
        }

        public async Task<IActionResult> OnPostSendVerificationEmailAsync()
        {
            if (!ModelState.IsValid) return Page();

            try
            {
                var code = await userManager.SendEmailConfirmationAsync(Input.Email); // tuple version
                var callbackUrl = Url.Page("/Account/ConfirmEmail", pageHandler: null, values: new { area = "Identity", email = Input.Email, code }, protocol: Request.Scheme);

                var subject = "Confirm your email – Dynamic Spheres MIS";
                var link = HtmlEncoder.Default.Encode(callbackUrl);

                var html = $@"
                            <div style='font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#1f2937'>
                              <h2 style='color:#0d9dda;margin:0 0 10px'>Welcome to aseeMIS</h2>
                              <p>Thanks for signing up! Please confirm your email to activate your account.</p>
                              <p style='margin:20px 0'>
                                <a href='{link}' style='background:#0d9dda;color:#fff;padding:10px 18px;
                                   border-radius:8px;text-decoration:none;font-weight:600'>Confirm my email</a>
                              </p>
                              <p style='font-size:12px;color:#6b7280'>Or paste this link in your browser:<br/>{link}</p>
                            </div>";

                await emailSender.SendEmailAsync(Input.Email, subject, html);
                StatusMessage = "Verification email sent. Please check your email.";
                //return RedirectToPage(); // PRG so refresh doesn't resubmit
            }
            catch (AppException ex)
            {
                StatusMessage = "Error " + ex.Message;
            }
            catch (Exception ex)
            {
                StatusMessage = "Error " + "Unexpected error while sending the confirmation email.";
            }

            // show the page with the error banner
            return Page();
        }
    }
}
