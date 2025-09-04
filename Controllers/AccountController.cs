using Freelancing.Data;
using Freelancing.Models;
using Freelancing.Models.Entities;
using Freelancing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
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

                var result = await _signInManager.PasswordSignInAsync(userNameToSignIn, model.Password, false, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    var user = await _userManager.FindByNameAsync(userNameToSignIn);

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
                            // Admin: go to admin area / page
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
                    return RedirectToAction("LoginWith2fa", new { returnUrl });
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

                ModelState.AddModelError("", "Invalid login attempt.");
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> LogOut()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
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

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
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
    }
}
