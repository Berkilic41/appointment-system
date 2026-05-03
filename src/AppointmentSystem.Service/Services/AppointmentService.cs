using AppointmentSystem.Bll.Common;
using AppointmentSystem.Bll.Services.Interfaces;
using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;

namespace AppointmentSystem.Bll.Services;

public class AppointmentService : IAppointmentService
{
    private readonly IAppointmentRepository _appts;
    private readonly IServiceRepository _services;
    private readonly IUserRepository _users;
    private readonly INotificationRepository _notifications;

    public AppointmentService(
        IAppointmentRepository appts,
        IServiceRepository services,
        IUserRepository users,
        INotificationRepository notifications)
    {
        _appts = appts;
        _services = services;
        _users = users;
        _notifications = notifications;
    }

    public async Task<Result<int>> BookAsync(int customerId, int providerId, int serviceId, DateTime startUtc, string? notes)
    {
        var service = await _services.GetByIdAsync(serviceId);
        if (service is null) return Result<int>.Fail("Service not found.");

        var provider = await _users.GetByIdAsync(providerId);
        if (provider is null || provider.Role != "Provider")
            return Result<int>.Fail("Provider not found.");

        if (startUtc < DateTime.UtcNow)
            return Result<int>.Fail("Cannot book a slot in the past.");

        var endUtc = startUtc.AddMinutes(service.DurationMinutes);

        var appt = new Appointment
        {
            ProviderId = providerId,
            CustomerId = customerId,
            ServiceId = serviceId,
            StartUtc = startUtc,
            EndUtc = endUtc,
            Status = "Pending",
            Notes = notes
        };

        try
        {
            var id = await _appts.CreateWithConflictCheckAsync(appt);

            await _notifications.CreateAsync(new Notification
            {
                UserId = providerId,
                Title = "New booking request",
                Body = $"New {service.Name} requested at {startUtc:yyyy-MM-dd HH:mm} UTC. Awaiting confirmation.",
                RelatedAppointmentId = id
            });
            await _notifications.CreateAsync(new Notification
            {
                UserId = customerId,
                Title = "Booking submitted",
                Body = $"Your {service.Name} on {startUtc:yyyy-MM-dd HH:mm} UTC is pending provider confirmation.",
                RelatedAppointmentId = id
            });

            return Result<int>.Ok(id);
        }
        catch (BookingConflictException ex)
        {
            return Result<int>.Fail(ex.Message);
        }
    }

    public Task<Appointment?> GetByIdAsync(int id) => _appts.GetByIdAsync(id);

    public Task<IEnumerable<Appointment>> GetForProviderAsync(int providerId, DateTime fromUtc, DateTime toUtc)
        => _appts.GetForProviderAsync(providerId, fromUtc, toUtc);

    public Task<IEnumerable<Appointment>> GetForCustomerAsync(int customerId)
        => _appts.GetForCustomerAsync(customerId);

    public Task<IEnumerable<Appointment>> GetInRangeAsync(DateTime fromUtc, DateTime toUtc)
        => _appts.GetInRangeAsync(fromUtc, toUtc);

    public async Task<bool> ChangeStatusAsync(int appointmentId, int actorUserId, string actorRole, string newStatus)
    {
        var appt = await _appts.GetByIdAsync(appointmentId);
        if (appt is null) return false;

        // Authorization rules
        bool allowed = (actorRole, newStatus) switch
        {
            ("Admin",    _) => true,
            ("Provider", "Confirmed")  => actorUserId == appt.ProviderId && appt.Status == "Pending",
            ("Provider", "Completed")  => actorUserId == appt.ProviderId && appt.Status == "Confirmed",
            ("Provider", "Cancelled")  => actorUserId == appt.ProviderId && appt.Status is "Pending" or "Confirmed",
            ("Customer", "Cancelled")  => actorUserId == appt.CustomerId && appt.Status is "Pending" or "Confirmed",
            _ => false
        };
        if (!allowed) throw new UnauthorizedAccessException("Not allowed to change this appointment to that status.");

        await _appts.UpdateStatusAsync(appointmentId, newStatus);

        // Notify the other party
        var notifyUserId = actorUserId == appt.ProviderId ? appt.CustomerId : appt.ProviderId;
        await _notifications.CreateAsync(new Notification
        {
            UserId = notifyUserId,
            Title = $"Appointment {newStatus.ToLower()}",
            Body = $"Appointment #{appointmentId} ({appt.ServiceName}) is now {newStatus.ToLower()}.",
            RelatedAppointmentId = appointmentId
        });
        return true;
    }

    public Task<IEnumerable<ProviderUtilizationRow>> GetUtilizationAsync(DateTime fromUtc, DateTime toUtc)
        => _appts.GetUtilizationAsync(fromUtc, toUtc);

    public Task<IEnumerable<DailyCount>> GetDailyCountsAsync(DateTime fromUtc, DateTime toUtc)
        => _appts.GetDailyCountsAsync(fromUtc, toUtc);
}
