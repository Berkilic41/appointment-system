using System.Security.Claims;
using AppointmentSystem.Bll.Services.Interfaces;
using AppointmentSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentSystem.Web.Controllers;

[Authorize]
public class AppointmentsController : Controller
{
    private readonly IProviderService _providers;
    private readonly IServiceCatalogService _services;
    private readonly IScheduleService _schedule;
    private readonly IAppointmentService _appointments;
    private readonly IRatingService _ratings;

    public AppointmentsController(
        IProviderService providers,
        IServiceCatalogService services,
        IScheduleService schedule,
        IAppointmentService appointments,
        IRatingService ratings)
    {
        _providers = providers;
        _services = services;
        _schedule = schedule;
        _appointments = appointments;
        _ratings = ratings;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Provider booking page with weekly grid (customer view).</summary>
    [HttpGet("/book/{providerId:int}")]
    public async Task<IActionResult> Book(int providerId, DateTime? week, int? serviceId)
    {
        var provider = await _providers.GetByIdAsync(providerId);
        if (provider is null) return NotFound();

        var weekStart = (week ?? DateTime.UtcNow.Date).Date;
        weekStart = weekStart.AddDays(-(int)weekStart.DayOfWeek + 1); // Monday
        if (weekStart > DateTime.UtcNow.Date.AddDays(60)) weekStart = DateTime.UtcNow.Date;

        var services = await _services.GetForProviderAsync(providerId);
        var existing = await _appointments.GetForProviderAsync(providerId, weekStart, weekStart.AddDays(7));

        return View(new BookingPageViewModel
        {
            Provider = provider,
            Services = services,
            WeekStart = weekStart,
            WeeklyAppointments = existing,
            PreselectedServiceId = serviceId
        });
    }

    /// <summary>AJAX endpoint: returns slot list for a specific provider+service+date.</summary>
    [HttpGet("/api/availability")]
    public async Task<IActionResult> Availability(int providerId, int serviceId, DateTime date)
    {
        var slots = await _schedule.GetAvailableSlotsAsync(providerId, serviceId, date);
        return Json(slots.Select(s => new {
            startUtc = s.StartUtc.ToString("u"),
            endUtc = s.EndUtc.ToString("u"),
            available = s.Available,
            label = s.StartUtc.ToString("HH:mm")
        }));
    }

    [HttpPost("/book/{providerId:int}/confirm"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int providerId, int serviceId, DateTime startUtc, string? notes)
    {
        var result = await _appointments.BookAsync(CurrentUserId, providerId, serviceId, startUtc, notes);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Book), new { providerId });
        }
        TempData["Success"] = "Booking submitted! Awaiting provider confirmation.";
        return RedirectToAction(nameof(Details), new { id = result.AppointmentId });
    }

    public async Task<IActionResult> Details(int id)
    {
        var appt = await _appointments.GetByIdAsync(id);
        if (appt is null) return NotFound();

        bool authorized = User.IsInRole("Admin")
            || appt.CustomerId == CurrentUserId
            || appt.ProviderId == CurrentUserId;
        if (!authorized) return Forbid();

        ViewBag.ExistingRating = await _ratings.GetByAppointmentAsync(id);
        return View(appt);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, string status)
    {
        try
        {
            var role = User.FindFirstValue(ClaimTypes.Role)!;
            await _appointments.ChangeStatusAsync(id, CurrentUserId, role, status);
            TempData["Success"] = $"Appointment {status.ToLower()}.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitRating(int appointmentId, int stars, string? comment)
    {
        try
        {
            await _ratings.SubmitAsync(appointmentId, CurrentUserId, stars, comment);
            TempData["Success"] = "Thanks for your rating!";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id = appointmentId });
    }
}
