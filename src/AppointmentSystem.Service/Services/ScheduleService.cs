using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;
using AppointmentSystem.Bll.DTOs;
using AppointmentSystem.Bll.Services.Interfaces;

namespace AppointmentSystem.Bll.Services;

public class ScheduleService : IScheduleService
{
    private readonly IScheduleRepository _schedule;
    private readonly IAppointmentRepository _appts;
    private readonly IServiceRepository _services;

    public ScheduleService(IScheduleRepository schedule, IAppointmentRepository appts, IServiceRepository services)
    {
        _schedule = schedule;
        _appts = appts;
        _services = services;
    }

    public Task<IEnumerable<WorkingHour>> GetWorkingHoursAsync(int providerId)
        => _schedule.GetWorkingHoursAsync(providerId);

    public Task ReplaceWorkingHoursAsync(int providerId, IEnumerable<WorkingHour> hours)
        => _schedule.ReplaceWorkingHoursAsync(providerId, hours);

    public Task<IEnumerable<TimeOff>> GetTimeOffAsync(int providerId, DateTime fromUtc, DateTime toUtc)
        => _schedule.GetTimeOffAsync(providerId, fromUtc, toUtc);

    public Task<int> CreateTimeOffAsync(TimeOff entry) => _schedule.CreateTimeOffAsync(entry);
    public Task DeleteTimeOffAsync(int id, int providerId) => _schedule.DeleteTimeOffAsync(id, providerId);

    /// <summary>
    /// Build the slot list for one day:
    ///  1. Read provider's WorkingHours intervals for the day-of-week.
    ///  2. Subtract any TimeOff windows that intersect that day.
    ///  3. Subtract existing Pending/Confirmed appointments.
    ///  4. Step through remaining intervals at the service's duration.
    /// </summary>
    public async Task<IEnumerable<TimeSlot>> GetAvailableSlotsAsync(int providerId, int serviceId, DateTime localDate)
    {
        var service = await _services.GetByIdAsync(serviceId)
            ?? throw new InvalidOperationException("Service not found.");

        // We treat localDate as a UTC date for simplicity (timezone normalization is out of scope).
        var dayStart = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var hours = (await _schedule.GetWorkingHoursAsync(providerId))
            .Where(h => (int)h.DayOfWeek == (int)dayStart.DayOfWeek)
            .OrderBy(h => h.StartMinutes)
            .ToList();

        if (!hours.Any()) return [];

        // Run appointments and time-off queries in parallel — independent queries, no reason to serialize
        var apptTask    = _appts.GetForProviderAsync(providerId, dayStart, dayEnd);
        var timeOffTask = _schedule.GetTimeOffAsync(providerId, dayStart, dayEnd);
        await Task.WhenAll(apptTask, timeOffTask);

        var existing = apptTask.Result
            .Where(a => a.Status is "Pending" or "Confirmed")
            .ToList();

        var timeOff = timeOffTask.Result.ToList();

        var slots = new List<TimeSlot>();
        var duration = TimeSpan.FromMinutes(service.DurationMinutes);

        foreach (var window in hours)
        {
            var winStart = dayStart.AddMinutes(window.StartMinutes);
            var winEnd   = dayStart.AddMinutes(window.EndMinutes);

            for (var t = winStart; t.Add(duration) <= winEnd; t = t.Add(duration))
            {
                var slotEnd = t.Add(duration);

                bool blockedByOff = timeOff.Any(o => o.StartUtc < slotEnd && o.EndUtc > t);
                bool taken        = existing.Any(a => a.StartUtc < slotEnd && a.EndUtc > t);
                bool inPast       = t <= DateTime.UtcNow;

                slots.Add(new TimeSlot(t, slotEnd, !blockedByOff && !taken && !inPast));
            }
        }
        return slots;
    }
}
