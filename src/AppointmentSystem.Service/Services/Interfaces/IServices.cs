using AppointmentSystem.Data.Entities;
using AppointmentSystem.Bll.DTOs;
using Service = AppointmentSystem.Data.Entities.Service;

namespace AppointmentSystem.Bll.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password);
    Task<AuthResult> RegisterCustomerAsync(string username, string email, string password, string fullName, string? phone);
    Task<User?> GetByIdAsync(int id);
}

public interface IProviderService
{
    Task<IEnumerable<ProviderProfile>> GetAllAsync();
    Task<ProviderProfile?> GetByIdAsync(int providerId);
    Task UpsertProfileAsync(int providerId, string? bio, string? specialty);
}

public interface IServiceCatalogService
{
    Task<IEnumerable<Service>> GetAllAsync(bool includeInactive = false);
    Task<Service?> GetByIdAsync(int id);
    Task<int> CreateAsync(Service service);
    Task UpdateAsync(Service service);
    Task SetActiveAsync(int id, bool active);
    Task<IEnumerable<Service>> GetForProviderAsync(int providerId);
    Task SetProviderServicesAsync(int providerId, IEnumerable<int> serviceIds);
    Task<IEnumerable<int>> GetProviderServiceIdsAsync(int providerId);
}

public interface IScheduleService
{
    Task<IEnumerable<WorkingHour>> GetWorkingHoursAsync(int providerId);
    Task ReplaceWorkingHoursAsync(int providerId, IEnumerable<WorkingHour> hours);
    Task<IEnumerable<TimeOff>> GetTimeOffAsync(int providerId, DateTime fromUtc, DateTime toUtc);
    Task<int> CreateTimeOffAsync(TimeOff entry);
    Task DeleteTimeOffAsync(int id, int providerId);

    /// <summary>Compute available start times for a service on a given day.</summary>
    Task<IEnumerable<TimeSlot>> GetAvailableSlotsAsync(int providerId, int serviceId, DateTime localDate);
}

public interface IAppointmentService
{
    Task<BookingResult> BookAsync(int customerId, int providerId, int serviceId, DateTime startUtc, string? notes);
    Task<Appointment?> GetByIdAsync(int id);
    Task<IEnumerable<Appointment>> GetForProviderAsync(int providerId, DateTime fromUtc, DateTime toUtc);
    Task<IEnumerable<Appointment>> GetForCustomerAsync(int customerId);
    Task<IEnumerable<Appointment>> GetInRangeAsync(DateTime fromUtc, DateTime toUtc);
    Task<bool> ChangeStatusAsync(int appointmentId, int actorUserId, string actorRole, string newStatus);
    Task<IEnumerable<ProviderUtilizationRow>> GetUtilizationAsync(DateTime fromUtc, DateTime toUtc);
    Task<IEnumerable<DailyCount>> GetDailyCountsAsync(DateTime fromUtc, DateTime toUtc);
}

public interface INotificationService
{
    Task<IEnumerable<Notification>> GetForUserAsync(int userId);
    Task<int> UnreadCountAsync(int userId);
    Task MarkAllReadAsync(int userId);
}

public interface IRatingService
{
    Task<Rating?> GetByAppointmentAsync(int appointmentId);
    Task<IEnumerable<Rating>> GetForProviderAsync(int providerId);
    Task SubmitAsync(int appointmentId, int customerId, int stars, string? comment);
}

public interface IUserAdminService
{
    Task<IEnumerable<User>> GetAllAsync();
    Task UpdateRoleAsync(int id, string role);
    Task SetActiveAsync(int id, bool active);
}
