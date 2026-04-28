using AppointmentSystem.Bll.Services.Interfaces;
using AppointmentSystem.Data.Entities;
using AppointmentSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DomainService = AppointmentSystem.Data.Entities.Service;

namespace AppointmentSystem.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly IAppointmentService _appointments;
    private readonly IUserAdminService _users;
    private readonly IServiceCatalogService _services;

    public AdminController(IAppointmentService appointments, IUserAdminService users, IServiceCatalogService services)
    {
        _appointments = appointments;
        _users = users;
        _services = services;
    }

    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        from ??= DateTime.UtcNow.Date.AddDays(-7);
        to ??= DateTime.UtcNow.Date.AddDays(7);

        var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek + 1);
        var weekAppts = await _appointments.GetInRangeAsync(weekStart, weekStart.AddDays(7));

        return View(new AdminDashboardViewModel
        {
            FromDate = from.Value, ToDate = to.Value,
            DailyCounts = await _appointments.GetDailyCountsAsync(from.Value, to.Value.AddDays(1)),
            Utilization = await _appointments.GetUtilizationAsync(from.Value, to.Value.AddDays(1)),
            TotalThisWeek = weekAppts.Count()
        });
    }

    public async Task<IActionResult> Users() => View(await _users.GetAllAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(int id, string role)
    {
        try { await _users.UpdateRoleAsync(id, role); TempData["Success"] = "Role updated."; }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int id, bool active)
    {
        await _users.SetActiveAsync(id, active);
        return RedirectToAction(nameof(Users));
    }

    public async Task<IActionResult> Services() => View(await _services.GetAllAsync(includeInactive: true));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateService(string name, string? description, int durationMinutes, decimal price)
    {
        if (!string.IsNullOrWhiteSpace(name) && durationMinutes > 0)
        {
            await _services.CreateAsync(new DomainService { Name = name, Description = description, DurationMinutes = durationMinutes, Price = price });
            TempData["Success"] = "Service created.";
        }
        return RedirectToAction(nameof(Services));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleService(int id, bool active)
    {
        await _services.SetActiveAsync(id, active);
        return RedirectToAction(nameof(Services));
    }
}
