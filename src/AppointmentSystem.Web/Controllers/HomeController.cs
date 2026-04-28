using System.Security.Claims;
using AppointmentSystem.Bll.Services.Interfaces;
using AppointmentSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentSystem.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IProviderService _providers;
    private readonly IAppointmentService _appointments;

    public HomeController(IProviderService providers, IAppointmentService appointments)
    {
        _providers = providers;
        _appointments = appointments;
    }

    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("Admin"))    return RedirectToAction("Index", "Admin");
        if (User.IsInRole("Provider")) return RedirectToAction("Dashboard", "Provider");

        var providers = await _providers.GetAllAsync();
        ViewBag.Providers = providers;

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var all = (await _appointments.GetForCustomerAsync(userId)).ToList();
        var now = DateTime.UtcNow;
        return View(new CustomerHomeViewModel
        {
            Upcoming = all.Where(a => a.EndUtc >= now).OrderBy(a => a.StartUtc),
            Past = all.Where(a => a.EndUtc < now).OrderByDescending(a => a.StartUtc).Take(20)
        });
    }
}
