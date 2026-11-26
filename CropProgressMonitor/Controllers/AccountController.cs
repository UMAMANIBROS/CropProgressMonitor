using CropProgressMonitor.Data;
using CropProgressMonitor.Models;
using CropProgressMonitor.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CropProgressMonitor.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<Farmer> _userManager;
    private readonly SignInManager<Farmer> _signInManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<Farmer> userManager,
        SignInManager<Farmer> signInManager,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var farmer = new Farmer
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            Name = model.Name.Trim(),
            Location = model.Location.Trim(),
            PreferredLanguage = model.PreferredLanguage is "hi" or "pa" ? model.PreferredLanguage : "en",
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(farmer, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(farmer, DbInitializer.FarmerRole);
            await _signInManager.SignInAsync(farmer, isPersistent: false);
            _logger.LogInformation("New farmer registered: {Email}", farmer.Email);
            return RedirectToAction("Index", "Dashboard");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.Email.Trim(),
            model.Password,
            model.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            _logger.LogInformation("Farmer logged in: {Email}", model.Email);
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }
            return RedirectToAction("Index", "Dashboard");
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
