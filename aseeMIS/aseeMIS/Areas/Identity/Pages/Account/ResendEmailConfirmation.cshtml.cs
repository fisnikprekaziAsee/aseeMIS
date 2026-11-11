using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using dsIdentity.Core.Managers;
using aseeMIS.Services.Interfaces;
using dsMIS.Core.Exceptions;

namespace aseeMIS.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ResendEmailConfirmationModel(ApplicationUserManager userManager, IEmailSenderService emailSender) : PageModel
    {

        [BindProperty]
        public InputModel Input { get; set; }
        [TempData] public string Message { get; set; }
        [TempData] public bool IsSuccess { get; set; } = true;
        public class InputModel
        {
            [Required, EmailAddress]
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
                var code = await userManager.SendEmailConfirmationAsync(Input.Email); // tuple version
                var callbackUrl = Url.Page( "/Account/ConfirmEmail", pageHandler: null, values: new { area = "Identity", email = Input.Email, code }, protocol: Request.Scheme);

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
