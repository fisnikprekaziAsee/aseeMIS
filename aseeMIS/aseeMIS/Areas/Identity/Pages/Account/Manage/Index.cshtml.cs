// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using dsAppMng.Core.DTOs;
using dsAppMng.Core.Services.Interfaces;
using dsAspNet.Core.Services.Interfaces;
using dsAspNet.Core.ViewModels;
using dsMIS.Core.Attributes;
using dsDef.Core.DTOs;
using dsDef.Core.Services.Interfaces;
using dsIdentity.Core.Managers;
using dsIdentity.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace aseeMIS.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationUserManager _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAspNetUserService<AspNetUserProfileViewModel> _userService;
        private readonly IAppLanguageService appLanguageService;
        private readonly IDefGenderService genderService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<IndexModel> logger;

        public IndexModel(
            ApplicationUserManager userManager,
            SignInManager<ApplicationUser> signInManager,
            IAspNetUserService<AspNetUserProfileViewModel> userService,
            IAppLanguageService appLanguageService,
            IDefGenderService genderService,
            IWebHostEnvironment env,
            ILogger<IndexModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _userService = userService;
            this.appLanguageService = appLanguageService;
            this.genderService = genderService;
            _env = env;
            this.logger = logger;
        }

        public string Username { get; set; }
        public string ProfilePicturePath { get; set; }
        public IEnumerable<AppLanguageDto> Languages { get; set; }
        public IEnumerable<DefGenderDto> Genders { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public AspNetUserProfileViewModel Input { get; set; }

        private async Task LoadAsync(ApplicationUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

            Username = userName;
            ProfilePicturePath = user.ProfilePicturePath;

            Input = new AspNetUserProfileViewModel
            {
                Id = user.Id,
                PhoneNumber = phoneNumber,
                FullName = user.FullName,
                GenderId = user.GenderId,
                LanguageCode = user.LanguageCode,
                TwoFactorEnabled = user.TwoFactorEnabled,
                BirthDate = user.BirthDate,
                ProfilePicturePath = user.ProfilePicturePath,
                ProfilePicturePathBase64 = null
            };

            // Fetch ACTIVE language codes and genders
            Languages = await appLanguageService.SelectAllActiveForDropdownAsync();
            Genders = await genderService.SelectAllActiveForDropdownAsync();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string twoFactorCode = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

            // Always reload ACTIVE dropdown lists so they remain after validation errors
            Languages = await appLanguageService.SelectAllActiveForDropdownAsync();
            Genders = await genderService.SelectAllActiveForDropdownAsync();
            ProfilePicturePath = user.ProfilePicturePath;

            // Enforce Two-Factor Authentication if enabled
            if (user.TwoFactorEnabled)
            {
                if (string.IsNullOrEmpty(twoFactorCode))
                {
                    ModelState.AddModelError("twoFactorCode", "Two-Factor Authentication code is required.");
                    TempData["Require2FACode"] = true;
                    StatusMessage = "Enter your Two-Factor Authentication code before saving changes.";
                    return Page();
                }

                var isCodeValid = await _userManager.VerifyTwoFactorTokenAsync(
                    user,
                    _userManager.Options.Tokens.AuthenticatorTokenProvider,
                    twoFactorCode);

                if (!isCodeValid)
                {
                    ModelState.AddModelError("twoFactorCode", "Invalid Two-Factor Authentication code.");
                    TempData["Require2FACode"] = true;
                    StatusMessage = "Invalid Two-Factor Authentication code. Please try again.";
                    return Page();
                }
            }

            // IMPORTANT:
            // Do NOT call LoadAsync(user) here, because it overwrites posted Input (including CroppedImageBase64).
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Set the Id so service knows this is an update
            Input.Id = user.Id;

            try
            {
                // Handle CroppedImageBase64 to file conversion before calling service
                await HandleBase64ImageAsync(Input);

                // Use the new UpdateProfileAsync method which doesn't require FormId
                // This method handles profile picture updates properly (only updates if not null)
                await _userService.UpdateProfileAsync(user.Id, Input);

                await _signInManager.RefreshSignInAsync(user);
                StatusMessage = "Your profile has been updated.";
                return RedirectToPage();
            }
            catch (dsMIS.Core.Exceptions.AppException ex)
            {
                // Application-level exceptions are safe to display
                logger.LogWarning(ex, "Application error updating user profile for user {UserId}", user.Id);
                StatusMessage = "Error: " + ex.Message;
                return Page();
            }
            catch (Exception ex)
            {
                // Generic exceptions should not expose technical details
                logger.LogError(ex, "Unexpected error updating user profile for user {UserId}", user.Id);
                StatusMessage = "An unexpected error occurred while updating your profile. Please try again.";
                return Page();
            }
        }

        /// <summary>
        /// Handles base64 encoded image data from the ImageEditor.
        /// Converts CroppedImageBase64 to a file and sets ProfilePicturePath.
        /// Only updates if CroppedImageBase64 is provided.
        /// </summary>
        private async Task HandleBase64ImageAsync(AspNetUserProfileViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.ProfilePicturePathBase64))
                return;

            // Get the FileUpload attribute from ProfilePicturePath property
            var pathProp = typeof(AspNetUserProfileViewModel).GetProperty(nameof(AspNetUserProfileViewModel.ProfilePicturePath));
            var fileUploadAttr = pathProp?.GetCustomAttributes(typeof(FileUploadAttribute), true)
                .FirstOrDefault() as FileUploadAttribute;

            if (fileUploadAttr == null)
            {
                logger.LogWarning("FileUploadAttribute not found on ProfilePicturePath property. Cannot process profile picture upload.");
                return;
            }

            try
            {
                // Remove data URL prefix if present (e.g., "data:image/png;base64,")
                var base64Data = model.ProfilePicturePathBase64.Trim();
                var commaIndex = base64Data.IndexOf(',');
                if (commaIndex >= 0)
                    base64Data = base64Data.Substring(commaIndex + 1);

                // Convert base64 to bytes
                byte[] imageBytes;
                try
                {
                    imageBytes = Convert.FromBase64String(base64Data);
                }
                catch (FormatException ex)
                {
                    logger.LogError(ex, "Invalid base64 image data for user {UserId}", model.Id);
                    throw new InvalidOperationException("Invalid image data provided. Please try uploading the image again.", ex);
                }

                // Validate file size
                if (imageBytes.Length > fileUploadAttr.MaxFileSize)
                {
                    throw new InvalidOperationException($"Image size exceeds maximum allowed size of {fileUploadAttr.MaxFileSize / (1024.0 * 1024.0):F1}MB.");
                }

                // Create upload directory structure based on SubfolderStrategy
                var uploadFolder = fileUploadAttr.UploadFolder ?? "uploads";
                string uploadPath;

                if (fileUploadAttr.SubfolderStrategy == SubfolderStrategy.ByYearMonth)
                {
                    uploadPath = Path.Combine(uploadFolder,
                        DateTime.Now.Year.ToString(),
                        DateTime.Now.Month.ToString("D2"));
                }
                else
                {
                    uploadPath = uploadFolder;
                }

                var fullUploadPath = Path.Combine(_env.WebRootPath ?? "wwwroot", uploadPath);
                Directory.CreateDirectory(fullUploadPath);

                // Delete old profile picture if exists
                if (!string.IsNullOrEmpty(model.ProfilePicturePath))
                {
                    var normalizedPath = model.ProfilePicturePath.TrimStart('~', '/');
                    var oldFilePath = Path.Combine(_env.WebRootPath ?? "wwwroot", normalizedPath);

                    if (System.IO.File.Exists(oldFilePath))
                    {
                        try 
                        { 
                            System.IO.File.Delete(oldFilePath);
                            logger.LogInformation("Deleted old profile picture: {FilePath}", oldFilePath);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to delete old profile picture: {FilePath}", oldFilePath);
                        }
                    }
                }

                // Save cropped PNG with unique filename
                var fileName = $"{model.Id}_{Guid.NewGuid()}.png";
                var filePath = Path.Combine(fullUploadPath, fileName);

                await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

                // Update the ProfilePicturePath with relative path (use forward slashes for web)
                var relativePath = Path.Combine(uploadPath, fileName)
                    .Replace('\\', '/');  // Normalize to web-compatible forward slashes
                model.ProfilePicturePath = relativePath;

                // Clear the base64 property to avoid storing it
                model.ProfilePicturePathBase64 = null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process cropped image for user {UserId}", model.Id);
                throw new InvalidOperationException("Failed to process cropped image.", ex);
            }
        }
    }
}
