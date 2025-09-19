using Freelancing.Data;
using Freelancing.Models;
using Freelancing.Models.Entities;
using Freelancing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using QRCoder;
using System.Text;
using System.Text.Encodings.Web;

namespace Freelancing.Controllers
{
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
        public async Task<IActionResult> Registration(RegistrationViewModel model)
        {
            if (ModelState.IsValid)
            {
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

                    // Send confirmation email
                    await _emailService.SendEmailConfirmationAsync(user.Email!, callbackUrl!);

                    ViewBag.Message = "Registration successful! Please check your email to confirm your account.";
                    return View(new RegistrationViewModel());
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
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{userId}'.");
            }

            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

            if (result.Succeeded)
            {
                ViewBag.Message = "Thank you for confirming your email. You can now log in.";
            }
            else
            {
                ViewBag.Error = "Error confirming your email.";
            }

            return View();
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
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
                    ModelState.AddModelError("", "Please confirm your email address before logging in.");
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
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token));
                    var result = await _userManager.ResetPasswordAsync(user, decodedToken, model.Password);

                    if (result.Succeeded)
                    {
                        ViewBag.Message = "Your password has been reset successfully. You can now log in with your new password.";
                        return View("Login");
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                }
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

            // Normalize code
            var verificationCode = model.VerificationCode?.Replace(" ", string.Empty).Replace("-", string.Empty) ?? string.Empty;

            var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);
            if (!isValid)
            {
                ModelState.AddModelError("", "Verification code is invalid.");
                // reload QR + key for view
                return await EnableAuthenticator();
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);

            var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            TempData["RecoveryCodes"] = string.Join(",", recoveryCodes);

            return RedirectToAction("TwoFactorEnabled");
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
        public async Task<IActionResult> ResetAuthenticatorConfirmed()
        {
            var user = await _userManager.GetUserAsync(User) ?? throw new InvalidOperationException("User not found.");

            await _userManager.ResetAuthenticatorKeyAsync(user);
            // turn off 2FA — force re-setup
            await _userManager.SetTwoFactorEnabledAsync(user, false);

            // After reset, redirect to EnableAuthenticator to get new key
            return RedirectToAction("EnableAuthenticator");
        }

        [Authorize]
        [HttpGet]
        public IActionResult ManageRecoveryCodes()
        {
            var codes = TempData["RecoveryCodes"]?.ToString();
            ViewBag.RecoveryCodes = codes != null ? codes.Split(',') : null;
            return View();
        }

        [Authorize]
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
