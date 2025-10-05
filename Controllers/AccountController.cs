using Freelancing.Data;
using Freelancing.Filters;
using Freelancing.Models;
using Freelancing.Models.Entities;
using Freelancing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using QRCoder;
using System.Text;
using System.Text.Encodings.Web;

namespace Freelancing.Controllers
{
    [RateLimitMessageFilter]
    public class AccountController : Controller
    {
        private readonly UserManager<UserAccount> _userManager;
        private readonly SignInManager<UserAccount> _signInManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public AccountController(
            UserManager<UserAccount> userManager,
            SignInManager<UserAccount> signInManager,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Registration()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> WhoAmI([FromServices] UserManager<UserAccount> userManager)
        {
            var user = await userManager.GetUserAsync(User);
            var roles = user != null ? await userManager.GetRolesAsync(user) : new List<string>();
            var roleClaims = User.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value);
            var claims = string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}"));
            return Content($"UserId={user?.Id}\nEmail={user?.Email}\nDBRoles=[{string.Join(", ", roles)}]\nRoleClaimsOnPrincipal=[{string.Join(", ", roleClaims)}]\nAllClaims=[{claims}]");
        }

        [HttpPost]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> Registration(RegistrationViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (string.IsNullOrWhiteSpace(model.Email))
                {
                    ModelState.AddModelError("Email", "Email address is required.");
                    return View(model);
                }

                try
                {
                    var testAddress = new System.Net.Mail.MailAddress(model.Email);
                }
                catch (FormatException)
                {
                    ModelState.AddModelError("Email", "Please enter a valid email address.");
                    return View(model);
                }

                var user = new UserAccount
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    FRole = model.Role
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Add role
                    await _userManager.AddToRoleAsync(user, model.Role);

                    // Generate email confirmation token
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                    var callbackUrl = Url.Action("ConfirmEmail", "Account",
                        new { userId = user.Id, token = encodedToken },
                        Request.Scheme);

                    try
                    {
                        // Send confirmation email
                        await _emailService.SendEmailConfirmationAsync(user.Email!, callbackUrl!);
                        ViewBag.Message = "Registration successful! Please check your email to confirm your account.";
                        return View(new RegistrationViewModel());
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Registration was successful, but we couldn't send the confirmation email. Please contact support.");
                        return View(model);
                    }
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null)
            {
                ViewBag.Error = "Invalid confirmation link.";
                return View();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ViewBag.Error = "User not found.";
                return View();
            }

            // Check if email is already confirmed
            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                ViewBag.Message = "Your email has already been confirmed. You can now log in.";
                return View();
            }

            try
            {
                var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
                var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

                if (result.Succeeded)
                {
                    ViewBag.Message = "Thank you for confirming your email. You can now log in.";
                    ViewBag.ShowLoginButton = true;
                }
                else
                {
                    // Check if the error is due to an invalid/expired token
                    if (result.Errors.Any(e =>
                        string.Equals(e.Code, "InvalidToken", StringComparison.OrdinalIgnoreCase) ||
                        (e.Description?.Contains("expired", StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (e.Description?.Contains("Invalid token", StringComparison.OrdinalIgnoreCase) ?? false)))
                    {
                        ViewBag.Error = "Your email confirmation link has expired or is invalid.";
                        ViewBag.ShowResendOption = true;
                        ViewBag.UserId = userId;
                        ViewBag.UserEmail = user.Email;
                    }
                    else
                    {
                        ViewBag.Error = "Error confirming your email. Please try again or contact support.";
                    }
                }
            }
            catch (Exception)
            {
                ViewBag.Error = "Your email confirmation link is invalid or has expired.";
                ViewBag.ShowResendOption = true;
                ViewBag.UserId = userId;
                ViewBag.UserEmail = user.Email;
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ResendEmailConfirmation(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ViewBag.Error = "User not found.";
                return View();
            }

            // Check if email is already confirmed
            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                ViewBag.Message = "Your email has already been confirmed. You can log in now.";
                return View();
            }

            var model = new ResendEmailConfirmationViewModel
            {
                UserId = userId,
                Email = user.Email!
            };

            return View(model);
        }

        [HttpPost]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> ResendEmailConfirmation(ResendEmailConfirmationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                ViewBag.Error = "User not found.";
                return View(model);
            }

            // Check if email is already confirmed
            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                ViewBag.Message = "Your email has already been confirmed. You can log in now.";
                return View(model);
            }

            // Check if email has changed
            if (user.Email != model.Email)
            {
                // Validate the new email format
                if (!IsValidEmail(model.Email))
                {
                    ModelState.AddModelError("Email", "Please enter a valid email address.");
                    return View(model);
                }

                // Check if new email is already in use by another user
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null && existingUser.Id != user.Id)
                {
                    ModelState.AddModelError("Email", "This email address is already registered.");
                    return View(model);
                }

                // Update the user's email
                user.Email = model.Email;
                user.NormalizedEmail = model.Email.ToUpperInvariant();
                user.EmailConfirmed = false; // Reset confirmation status

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    ModelState.AddModelError("", "Failed to update email address.");
                    return View(model);
                }
            }

            try
            {
                // Generate new email confirmation token
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var callbackUrl = Url.Action("ConfirmEmail", "Account",
                    new { userId = user.Id, token = encodedToken },
                    Request.Scheme);

                // Send new confirmation email to the (possibly updated) email address
                await _emailService.SendEmailConfirmationAsync(user.Email!, callbackUrl!);

                ViewBag.Message = "A confirmation email has been sent to your email address. Please check your email and follow the instructions.";
            }
            catch (Exception)
            {
                ViewBag.Error = "We couldn't send the confirmation email. Please try again later or contact support.";
            }

            return View(model);
        }

        // Helper method for email validation
        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        [HttpGet]
        public async Task<IActionResult> CancelRegistration(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ViewBag.Error = "User not found.";
                return View();
            }

            // Only allow cancellation if email is not confirmed
            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                ViewBag.Error = "Cannot cancel a confirmed account. Please use the normal account deletion process.";
                return View();
            }

            var model = new CancelRegistrationViewModel
            {
                UserId = userId,
                Email = user.Email!,
                UserName = user.UserName!
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> CancelRegistrationConfirmed(CancelRegistrationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("CancelRegistration", model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                ViewBag.Error = "User not found.";
                return View("CancelRegistration", model);
            }

            // Only allow cancellation if email is not confirmed
            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                ViewBag.Error = "Cannot cancel a confirmed account.";
                return View("CancelRegistration", model);
            }

            try
            {
                // Delete the unconfirmed user account
                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    ViewBag.Message = "Your registration has been cancelled successfully. You can now register again with the same email address if you wish.";
                    ViewBag.ShowRegistrationButton = true;
                }
                else
                {
                    ViewBag.Error = "An error occurred while cancelling your registration. Please contact support.";
                }
            }
            catch (Exception)
            {
                ViewBag.Error = "An error occurred while cancelling your registration. Please contact support.";
            }

            // Return the CancelRegistration view (not looking for CancelRegistrationConfirmed.cshtml)
            return View("CancelRegistration", model);
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                string userNameToSignIn = model.UserNameorEmail;

                if (model.UserNameorEmail.Contains("@"))
                {
                    var userByEmail = await _userManager.FindByEmailAsync(model.UserNameorEmail);
                    if (userByEmail != null)
                    {
                        userNameToSignIn = userByEmail.UserName;
                    }
                }

                var user = await _userManager.FindByNameAsync(userNameToSignIn);

                // Check if account is soft deleted and within recovery period
                if (user != null && user.IsDeleted)
                {
                    var daysSinceDeletion = user.DeletedAt.HasValue ?
                        (DateTime.UtcNow - user.DeletedAt.Value).TotalDays : 31;

                    if (daysSinceDeletion <= 30)
                    {
                        // Account can be recovered
                        return RedirectToAction("RecoverAccount", new { userId = user.Id });
                    }
                    else
                    {
                        // Account is permanently deleted
                        ModelState.AddModelError("", "This account has been permanently deleted and cannot be recovered.");
                        return View(model);
                    }
                }

                var result = await _signInManager.PasswordSignInAsync(userNameToSignIn, model.Password, model.RememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    if (user != null)
                    {
                        var roles = await _userManager.GetRolesAsync(user);
                        var role = roles.FirstOrDefault();

                        // Redirect based on role
                        if (role?.ToLower() == "client")
                        {
                            return RedirectToAction("Dashboard", "Client");
                        }
                        else if (role?.ToLower() == "freelancer")
                        {
                            return RedirectToAction("Dashboard", "Freelancer");
                        }
                        else if (role?.ToLower() == "admin")
                        {
                            return RedirectToAction("IdentityVerification", "Admin");
                        }
                        else
                        {
                            return RedirectToAction("AccessDenied");
                        }
                    }
                }

                if (result.RequiresTwoFactor)
                {
                    TempData["ReturnUrl"] = returnUrl;
                    TempData["RememberMe"] = model.RememberMe;
                    return RedirectToAction("LoginWith2fa");
                }

                if (result.IsLockedOut)
                {
                    ModelState.AddModelError("", "Account locked out due to too many failed attempts.");
                    return View(model);
                }

                var userForCheck = await _userManager.FindByNameAsync(userNameToSignIn);
                if (userForCheck != null && !await _userManager.IsEmailConfirmedAsync(userForCheck))
                {
                    // Instead of ModelState.AddModelError with HTML, use ViewBag
                    ViewBag.EmailNotConfirmed = true;
                    ViewBag.ResendConfirmationUrl = Url.Action("ResendEmailConfirmation", new { userId = userForCheck.Id });
                    ViewBag.CancelRegistrationUrl = Url.Action("CancelRegistration", new { userId = userForCheck.Id });
                    /*ModelState.AddModelError("", "Please confirm your email address before logging in.");*/
                    return View(model);
                }

                ModelState.AddModelError("", "That didn’t work. Check your credentials and try again.");
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> RecoverAccount(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return NotFound();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || !user.IsDeleted)
                return NotFound();

            var daysSinceDeletion = user.DeletedAt.HasValue ?
                (DateTime.UtcNow - user.DeletedAt.Value).TotalDays : 31;

            if (daysSinceDeletion > 30)
            {
                ViewBag.Message = "This account has been permanently deleted and cannot be recovered.";
                return View("AccountRecoveryExpired");
            }

            ViewBag.DaysRemaining = Math.Max(0, 30 - (int)daysSinceDeletion);
            ViewBag.UserName = user.UserName;
            return View(new RecoverAccountViewModel { UserId = userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> RecoverAccount(RecoverAccountViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null || !user.IsDeleted)
                return NotFound();

            var daysSinceDeletion = user.DeletedAt.HasValue ?
                (DateTime.UtcNow - user.DeletedAt.Value).TotalDays : 31;

            if (daysSinceDeletion > 30)
            {
                ViewBag.Message = "This account has been permanently deleted and cannot be recovered.";
                return View("AccountRecoveryExpired");
            }

            // Verify password
            var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!passwordValid)
            {
                ModelState.AddModelError("Password", "Invalid password.");
                return View(model);
            }

            // Restore account
            user.IsDeleted = false;
            user.DeletedAt = null;
            user.DeletionReason = null;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);

                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault();

                TempData["Message"] = "Welcome back! Your account has been successfully recovered.";

                if (role?.ToLower() == "client")
                {
                    return RedirectToAction("Dashboard", "Client");
                }
                else if (role?.ToLower() == "freelancer")
                {
                    return RedirectToAction("Dashboard", "Freelancer");
                }
            }

            ModelState.AddModelError("", "An error occurred while recovering your account.");
            return View(model);
        }

        [HttpGet]
        public IActionResult AccountDeleted()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> LoginWith2fa()
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                // If no 2FA process is in flight, redirect to login
                return RedirectToAction("Login");
            }

            var model = new LoginWith2faViewModel
            {
                RememberMe = TempData.ContainsKey("RememberMe") && bool.TryParse(TempData["RememberMe"]?.ToString(), out var rm) ? rm : false,
                ReturnUrl = TempData["ReturnUrl"]?.ToString()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> LoginWith2fa(LoginWith2faViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                ModelState.AddModelError("", "Unable to load two-factor authentication user.");
                return View(model);
            }

            var code = model.TwoFactorCode?.Replace(" ", string.Empty).Replace("-", string.Empty) ?? string.Empty;

            // rememberMe (persist cookie) comes from model.RememberMe (preserved as hidden input)
            // rememberClient (remember this browser/device) comes from model.RememberDevice
            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(code, model.RememberMe, rememberClient: model.RememberDevice);

            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }

                return RedirectToAction("Index", "Home");
            }
            if (result.IsLockedOut)
            {
                ModelState.AddModelError("", "User account locked out.");
                return View(model);
            }

            ModelState.AddModelError("", "Invalid authentication code.");
            return View(model);
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null && await _userManager.IsEmailConfirmedAsync(user))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                    var callbackUrl = Url.Action("ResetPassword", "Account",
                        new { email = model.Email, token = encodedToken },
                        Request.Scheme);

                    await _emailService.SendPasswordResetAsync(user.Email!, callbackUrl!);
                }

                // Always show the same message to prevent email enumeration
                ViewBag.Message = "If your email is registered, you will receive a password reset link.";
                return View();
            }

            return View(model);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> AccountSettings(bool showAuthenticator = false)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var model = new AccountSettingsViewModel
            {
                IsTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user),
                // show the QR/setup UI only when requested and 2FA isn't already enabled
                ShowEnableAuthenticator = showAuthenticator && !await _userManager.GetTwoFactorEnabledAsync(user)
            };

            if (model.ShowEnableAuthenticator)
            {
                await PopulateAuthenticatorFieldsAsync(model, user);
            }

            // If EnableAuthenticator POST put recovery codes in TempData, pick them up
            if (TempData.ContainsKey("RecoveryCodes"))
            {
                var codesCsv = TempData["RecoveryCodes"]?.ToString();
                if (!string.IsNullOrEmpty(codesCsv))
                {
                    model.RecoveryCodes = codesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries);
                }
            }

            return View(model);
        }

        private async Task PopulateAuthenticatorFieldsAsync(AccountSettingsViewModel model, UserAccount user)
        {
            // Ensure an authenticator key exists
            var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(unformattedKey))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            }

            model.SharedKey = FormatKey(unformattedKey);
            model.AuthenticatorUri = GenerateQrCodeUri(user.Email ?? user.UserName ?? "user", unformattedKey, "GIGHub");

            // generate QR base64 image
            using var qrGenerator = new QRCoder.QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(model.AuthenticatorUri, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var png = new QRCoder.PngByteQRCode(qrData);
            var bytes = png.GetGraphic(20);
            model.QrCodeImageUrl = $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
        }

        [Authorize]
        [HttpGet]
        public async Task<JsonResult> GetAuthenticatorSetup()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { ok = false });

            var vm = new AccountSettingsViewModel();
            await PopulateAuthenticatorFieldsAsync(vm, user);

            return Json(new
            {
                ok = true,
                sharedKey = vm.SharedKey,
                qrImage = vm.QrCodeImageUrl,
                authenticatorUri = vm.AuthenticatorUri
            });
        }


        // POST endpoint to change password
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(AccountSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Return to the same view with validation messages
                return View("AccountSettings", model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge(); // or NotFound
            }

            // Prevent reusing current password as new password
            var isSameAsCurrent = await _userManager.CheckPasswordAsync(user, model.NewPassword);
            if (isSameAsCurrent)
            {
                ModelState.AddModelError(nameof(model.NewPassword), "New password cannot be the same as the current password.");
                return View("AccountSettings", model);
            }

            // Attempt to change password (this validates the current password and enforces password policy)
            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["ChangePasswordSuccess"] = "Your password has been changed successfully.";
                return RedirectToAction("AccountSettings");
            }

            // Handle failures (invalid current password, password policy violations, etc.)
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View("AccountSettings", model);
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            if (email == null || token == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var model = new ResetPasswordViewModel
            {
                Email = email,
                Token = token
            };

            return View(model);
        }

        [HttpPost]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (string.IsNullOrWhiteSpace(model.Token) || string.IsNullOrWhiteSpace(model.Email))
            {
                ModelState.AddModelError("", "The password reset link is invalid. Please request a new reset link.");
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Don't reveal existence — keep same UX as ForgotPassword
                ViewBag.Message = "If your email is registered, you will receive a password reset confirmation.";
                return View("Login");
            }

            // Prevent using the current password
            var isSameAsCurrent = await _userManager.CheckPasswordAsync(user, model.Password);
            if (isSameAsCurrent)
            {
                ModelState.AddModelError("Password", "New password cannot be the same as your previous password.");
                return View(model);
            }

            string decodedToken;
            try
            {
                var tokenBytes = WebEncoders.Base64UrlDecode(model.Token);
                decodedToken = Encoding.UTF8.GetString(tokenBytes);
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "The password reset link is invalid or malformed. Please request a new reset link.");
                return View(model);
            }

            var result = await _userManager.ResetPasswordAsync(user, decodedToken, model.Password);
            if (result.Succeeded)
            {
                ViewBag.Message = "Your password has been reset successfully. You can now log in with your new password.";
                return View("Login");
            }

            // Detect invalid/expired token and show a friendly message
            if (result.Errors.Any(e =>
                    string.Equals(e.Code, "InvalidToken", StringComparison.OrdinalIgnoreCase)
                    || (e.Description?.Contains("expired", StringComparison.OrdinalIgnoreCase) ?? false)
                    || (e.Description?.Contains("Invalid token", StringComparison.OrdinalIgnoreCase) ?? false)))
            {
                ModelState.AddModelError("", "The password reset link is invalid or has expired. Please request a new reset link.");
                return View(model);
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> EnableAuthenticator()
        {
            var user = await _userManager.GetUserAsync(User) ?? throw new InvalidOperationException("User not found.");

            // Ensure a key exists
            var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(unformattedKey))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            }

            var model = new EnableAuthenticatorViewModel
            {
                SharedKey = FormatKey(unformattedKey),
                AuthenticatorUri = GenerateQrCodeUri(user.Email ?? user.UserName ?? "user", unformattedKey, "GIGHub")
            };

            // generate QR base64 image
            using var qrGenerator = new QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(model.AuthenticatorUri, QRCodeGenerator.ECCLevel.Q);
            using var png = new PngByteQRCode(qrData);
            var bytes = png.GetGraphic(20);
            model.QrCodeImageUrl = $"data:image/png;base64,{Convert.ToBase64String(bytes)}";

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableAuthenticator(EnableAuthenticatorViewModel model)
        {
            var user = await _userManager.GetUserAsync(User) ?? throw new InvalidOperationException("User not found.");

            var verificationCode = model.VerificationCode?.Replace(" ", string.Empty).Replace("-", string.Empty) ?? string.Empty;

            var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);
            if (!isValid)
            {
                // Return the AccountSettings view with the QR + validation error inline
                var vm = new AccountSettingsViewModel
                {
                    ShowEnableAuthenticator = true
                };
                await PopulateAuthenticatorFieldsAsync(vm, user);
                ModelState.AddModelError("", "Verification code is invalid.");
                return View("AccountSettings", vm);
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);

            var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            // keep codes in TempData for the view render
            TempData["RecoveryCodes"] = string.Join(",", recoveryCodes);

            // Return AccountSettings view showing recovery codes inline
            var resultVm = new AccountSettingsViewModel
            {
                IsTwoFactorEnabled = true,
                RecoveryCodes = recoveryCodes.ToArray()
            };

            return View("AccountSettings", resultVm);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmRecoveryCodesSaved()
        {
            // remove transient codes and return to settings (2FA is enabled)
            TempData.Remove("RecoveryCodes");
            return RedirectToAction("AccountSettings");
        }

        [Authorize]
        [HttpGet]
        public IActionResult TwoFactorEnabled()
        {
            var codes = TempData["RecoveryCodes"]?.ToString();
            ViewBag.RecoveryCodes = codes != null ? codes.Split(',') : null;
            return View();
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ResetAuthenticator()
        {
            var user = await _userManager.GetUserAsync(User) ?? throw new InvalidOperationException("User not found.");
            // Show confirmation UI
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetAuthenticatorConfirmed(string? returnUrl = null)
        {
            var user = await _userManager.GetUserAsync(User) ?? throw new InvalidOperationException("User not found.");

            await _userManager.ResetAuthenticatorKeyAsync(user);
            // turn off 2FA — force re-setup
            await _userManager.SetTwoFactorEnabledAsync(user, false);

            // Provide a transient success message
            TempData["Message"] = "Two-factor authenticator has been reset. You will need to re-enable it to generate new recovery codes.";

            // If a local returnUrl was provided, redirect back there — otherwise go to AccountSettings
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("AccountSettings");
        }

        [HttpGet]
        public IActionResult ManageRecoveryCodes()
        {
            var codes = TempData["RecoveryCodes"]?.ToString();
            ViewBag.RecoveryCodes = codes != null ? codes.Split(',') : null;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateNewRecoveryCodes()
        {
            var user = await _userManager.GetUserAsync(User) ?? throw new InvalidOperationException("User not found.");
            if (!await _userManager.GetTwoFactorEnabledAsync(user))
            {
                TempData["Error"] = "Cannot generate recovery codes for an account without 2FA enabled.";
                return RedirectToAction("ManageRecoveryCodes");
            }

            var codes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            TempData["RecoveryCodes"] = string.Join(",", codes);
            return RedirectToAction("ManageRecoveryCodes");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> LogOut()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        // Helpers
        private static string FormatKey(string unformattedKey)
        {
            // group into blocks for display
            int currentPosition = 0;
            var result = "";
            while (currentPosition + 4 <= unformattedKey.Length)
            {
                result += unformattedKey.Substring(currentPosition, 4) + " ";
                currentPosition += 4;
            }
            if (currentPosition < unformattedKey.Length)
            {
                result += unformattedKey.Substring(currentPosition);
            }
            return result.Trim().ToLowerInvariant();
        }

        private static string GenerateQrCodeUri(string email, string unformattedKey, string issuer)
        {
            // otpauth URI format for authenticator apps
            return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}?secret={unformattedKey}&issuer={Uri.EscapeDataString(issuer)}&digits=6";
        }
    }
}