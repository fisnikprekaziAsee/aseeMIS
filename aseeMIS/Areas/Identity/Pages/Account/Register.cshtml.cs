using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using dsAppMng.Core.Repositories.Interfaces;
using dsIdentity.Core.Models;

using dsIdentity.Core.Managers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using dsAppMng.Core.Models;
using dsAppMng.Core.Services.Interfaces;
using aseeMIS.Services.Interfaces;

namespace aseeMIS.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAppLanguageService appLanguageService;
        private readonly ApplicationUserManager _applicationUserManager;
        private readonly IEmailSenderService _emailSenderService;

        public RegisterModel(
               UserManager<ApplicationUser> userManager,
               SignInManager<ApplicationUser> signInManager,
               IAppLanguageService appLanguageService,
               ApplicationUserManager applicationUserManager,
               IEmailSenderService emailSenderService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            this.appLanguageService = appLanguageService;
            _applicationUserManager = applicationUserManager;
            _emailSenderService = emailSenderService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IEnumerable<object> Languages { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "First Name")]
            public string FirstName { get; set; }

            [Required]
            [Display(Name = "Last Name")]
            public string LastName { get; set; }

            [Required]
            [DataType(DataType.Date)]
            [Display(Name = "Date of Birth")]
            public DateTime DateOfBirth { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            [Display(Name = "Gender")]
            public string Gender { get; set; }

            [Phone]
            [Display(Name = "Phone Number")]
            public string PhoneNumber { get; set; }

            [Display(Name = "Language")]
            public string LanguageCode { get; set; }

            [Display(Name = "Profile Picture")]
            public IFormFile? ProfilePicture { get; set; }


            [Display(Name = "Enable Two-Factor Authentication")]
            public bool TwoFactorEnabled { get; set; }
        }

        public async Task OnGetAsync()
        {
            try
            {
                // Fetch all language codes and populate LanguageCodes list
                Languages = await appLanguageService.SelectAllForDropdownAsync();
            }
            catch (Exception ex)
            {
                // Handle errors gracefully
                ModelState.AddModelError(string.Empty, $"Error fetching language codes: {ex.Message}");
            }
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                var user = CreateUser();
                byte[] profilePictureBytes = null;

                // Process profile picture if uploaded
                if (Input.ProfilePicture != null)
                {
                    try
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            await Input.ProfilePicture.CopyToAsync(memoryStream);
                            profilePictureBytes = memoryStream.ToArray();
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError(string.Empty, $"Error processing profile picture: {ex.Message}");
                        return Page();
                    }
                }


                //try
                //{
                //    // Create the user
                //    var applicationUser = await _userManager.CreateAsync(user, Input.Password, null, null);

                //    if (applicationUser != null)
                //    {
                //        // Generate and send email confirmation token
                //        var generatedToken = await _userService.GenerateEmailConfirmationTokenAsync(applicationUser.Id);
                //        await _userTokenService.AddUserTokenAsync(applicationUser.Id, "MyApp", "EmailConfirmationToken", generatedToken);
                //        var callbackUrl = Url.Page(
                //            "/Account/ConfirmEmail",
                //            pageHandler: null,
                //            values: new { area = "Identity", userId = applicationUser.Id, code = generatedToken },
                //            protocol: Request.Scheme);

                //        await _emailSenderService.SendEmailAsync(
                //            applicationUser.Email,
                //            "Confirm your email",
                //            $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                //        // Redirect to enable 2FA setup if the user opted for 2FA
                //        if (Input.TwoFactorEnabled)
                //        {
                //            // Redirect user to enable 2FA setup after login
                //            TempData["Requires2FASetup"] = true;
                //        }

                //        // Redirect to login page
                //        TempData["StatusMessage"] = "Registration successful. Please log in.";
                //        return RedirectToPage("/Account/Login", new { area = "Identity" });
                //    }

                //    // Handle errors
                //    //foreach (var error in result.Errors)
                //    //{
                //    //    ModelState.AddModelError(string.Empty, error.Description);
                //    //}
                //}
                //catch (Exception ex)
                //{
                //    ModelState.AddModelError(string.Empty, $"Error creating user: {ex.Message}");
                //}
            }

            // If we get this far, something failed; redisplay form
            return Page();
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(IdentityUser)}'. " +
                    $"Ensure that '{nameof(IdentityUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

    }
}
