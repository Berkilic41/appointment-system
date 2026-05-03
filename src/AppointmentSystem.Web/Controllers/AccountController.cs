using System.Security.Claims;
using AppointmentSystem.Bll.Services.Interfaces;
using AppointmentSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AppointmentSystem.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _auth;
    public AccountController(IAuthService auth) => _auth = auth;

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var result = await _auth.LoginAsync(vm.Email, vm.Password);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError("", result.Error ?? "Login failed.");
            return View(vm);
        }
        await SignInAsync(result.Value!);
        if (!string.IsNullOrWhiteSpace(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
            return Redirect(vm.ReturnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var result = await _auth.RegisterCustomerAsync(vm.Username, vm.Email, vm.Password, vm.FullName, vm.Phone);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError("", result.Error ?? "Registration failed.");
            return View(vm);
        }
        await SignInAsync(result.Value!);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private async Task SignInAsync(AppointmentSystem.Data.Entities.User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new("FullName", user.FullName ?? user.Username)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14) });
    }
}
