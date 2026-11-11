using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using dsIdentity.Core.Managers;
using dsMIS.Core.Exceptions;

namespace aseeMIS.Areas.Identity.Pages.Account
{
    public class ResetPasswordModel(ApplicationUserManager userManager) : PageModel
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

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            [Required]
            public string Code { get; set; }
        }

        public IActionResult OnGet(string code = null, string email = null)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(email))
                return BadRequest("A code and email must be supplied for password reset.");

            Input = new InputModel
            {
                Email = email,
                Code = code 
            };
            Message = "";
            IsSuccess = true;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();
            try
            {
                var result = await userManager.ConfirmForgotPaswordConfimationAsync(Input.Email, Input.Code, Input.Password);
                if (result.Equals(""))
                {
                    IsSuccess = true;
                    Message = "Password has been reset successfully. Please go to Login Page";
                    //return RedirectToPage("./ResetPasswordConfirmation");
                }
                else
                {
                    IsSuccess = false;
                    if(result.Equals("Invalid token."))
                        Message = "Password reset token expired or invalid. Please request a new password reset.";
                    else
                        Message = result;
                }
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

            return Page();


        }
    }
}
