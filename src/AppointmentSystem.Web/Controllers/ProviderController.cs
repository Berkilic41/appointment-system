using System.Security.Claims;
using AppointmentSystem.Bll.Services.Interfaces;
using AppointmentSystem.Data.Entities;
using AppointmentSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentSystem.Web.Controllers;

[Authorize(Roles = "Provider")]
public class ProviderController : Controller
{
    private readonly IAppointmentService _appointments;
    private readonly IScheduleService _schedule;
    private readonly IServiceCatalogService _services;
    private readonly IProviderService _providers;
    private readonly IRatingService _ratings;

    public ProviderController(IAppointmentService appointments, IScheduleService schedule,
        IServiceCatalogService services, IProviderService providers, IRatingService ratings)
    {
        _appointments = appointments;
        _schedule = schedule;
        _services = services;
        _providers = providers;
        _ratings = ratings;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Dashboard(DateTime? week)
    {
        var weekStart = (week ?? DateTime.UtcNow.Date).Date;
        weekStart = weekStart.AddDays(-(int)weekStart.DayOfWeek + 1);
        var weekEnd = weekStart.AddDays(7);

        var weekAppts = (await _appointments.GetForProviderAsync(CurrentUserId, weekStart, weekEnd)).ToList();
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var monthAppts = await _appointments.GetForProviderAsync(CurrentUserId, monthStart, monthStart.AddMonths(1));

        var profile = await _providers.GetByIdAsync(CurrentUserId);
        var ratings = await _ratings.GetForProviderAsync(CurrentUserId);

        return View(new ProviderDashboardViewModel
        {
            WeekStart = weekStart,
            WeeklyAppointments = weekAppts,
            Pending = weekAppts.Count(a => a.Status == "Pending"),
            Confirmed = weekAppts.Count(a => a.Status == "Confirmed"),
            CompletedThisMonth = monthAppts.Count(a => a.Status == "Completed"),
            AverageRating = profile?.AverageRating ?? 0,
            RatingCount = profile?.RatingCount ?? 0,
            RecentRatings = ratings.Take(5)
        });
    }

    public async Task<IActionResult> Schedule()
    {
        var profile = await _providers.GetByIdAsync(CurrentUserId);
        return View(new ProviderScheduleViewModel
        {
            WorkingHours = await _schedule.GetWorkingHoursAsync(CurrentUserId),
            AllServices = await _services.GetAllAsync(),
            SelectedServiceIds = await _services.GetProviderServiceIdsAsync(CurrentUserId),
            Upcoming = await _schedule.GetTimeOffAsync(CurrentUserId, DateTime.UtcNow, DateTime.UtcNow.AddYears(1)),
            Bio = profile?.Bio,
            Specialty = profile?.Specialty
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProfile(string? bio, string? specialty, int[]? serviceIds)
    {
        await _providers.UpsertProfileAsync(CurrentUserId, bio, specialty);
        await _services.SetProviderServicesAsync(CurrentUserId, serviceIds ?? []);
        TempData["Success"] = "Profile updated.";
        return RedirectToAction(nameof(Schedule));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveWorkingHours(int[] dayOfWeek, int[] startMinutes, int[] endMinutes)
    {
        var hours = new List<WorkingHour>();
        for (int i = 0; i < dayOfWeek.Length; i++)
        {
            if (i >= startMinutes.Length || i >= endMinutes.Length) break;
            if (startMinutes[i] >= endMinutes[i]) continue;
            hours.Add(new WorkingHour
            {
                ProviderId = CurrentUserId,
                DayOfWeek = (byte)dayOfWeek[i],
                StartMinutes = (short)startMinutes[i],
                EndMinutes = (short)endMinutes[i]
            });
        }
        await _schedule.ReplaceWorkingHoursAsync(CurrentUserId, hours);
        TempData["Success"] = "Working hours updated.";
        return RedirectToAction(nameof(Schedule));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTimeOff(DateTime startUtc, DateTime endUtc, string? reason)
    {
        if (startUtc >= endUtc)
        {
            TempData["Error"] = "Time-off end must be after start.";
        }
        else
        {
            await _schedule.CreateTimeOffAsync(new TimeOff
            {
                ProviderId = CurrentUserId, StartUtc = startUtc, EndUtc = endUtc, Reason = reason
            });
            TempData["Success"] = "Time-off added.";
        }
        return RedirectToAction(nameof(Schedule));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTimeOff(int id)
    {
        await _schedule.DeleteTimeOffAsync(id, CurrentUserId);
        return RedirectToAction(nameof(Schedule));
    }
}
