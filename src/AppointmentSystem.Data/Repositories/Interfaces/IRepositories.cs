using AppointmentSystem.Data.Entities;

namespace AppointmentSystem.Data.Repositories.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(int id);
    Task<bool> ExistsByEmailAsync(string email);
    Task<bool> ExistsByUsernameAsync(string username);
    Task<int> CreateAsync(User user);
    Task<IEnumerable<User>> GetAllAsync();
    Task UpdateRoleAsync(int id, string role);
    Task SetActiveAsync(int id, bool active);
    Task<IEnumerable<ProviderProfile>> GetAllProvidersAsync();
    Task<ProviderProfile?> GetProviderProfileAsync(int providerId);
    Task UpsertProviderProfileAsync(int providerId, string? bio, string? specialty);
}

public interface IServiceRepository
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

public interface IScheduleRepository
{
    Task<IEnumerable<WorkingHour>> GetWorkingHoursAsync(int providerId);
    Task ReplaceWorkingHoursAsync(int providerId, IEnumerable<WorkingHour> hours);
    Task<IEnumerable<TimeOff>> GetTimeOffAsync(int providerId, DateTime fromUtc, DateTime toUtc);
    Task<int> CreateTimeOffAsync(TimeOff entry);
    Task DeleteTimeOffAsync(int id, int providerId);
}

public interface IAppointmentRepository
{
    /// <summary>Books a new appointment within a SQL transaction with overlap detection.</summary>
    /// <returns>New appointment id, or throws BookingConflictException on overlap.</returns>
    Task<int> CreateWithConflictCheckAsync(Appointment appointment);

    Task<Appointment?> GetByIdAsync(int id);
    Task<IEnumerable<Appointment>> GetForProviderAsync(int providerId, DateTime fromUtc, DateTime toUtc);
    Task<IEnumerable<Appointment>> GetForCustomerAsync(int customerId, bool includePast = true);
    Task<IEnumerable<Appointment>> GetInRangeAsync(DateTime fromUtc, DateTime toUtc);
    Task UpdateStatusAsync(int id, string status);
    Task<IEnumerable<ProviderUtilizationRow>> GetUtilizationAsync(DateTime fromUtc, DateTime toUtc);
    Task<IEnumerable<DailyCount>> GetDailyCountsAsync(DateTime fromUtc, DateTime toUtc);
}

public interface INotificationRepository
{
    Task<IEnumerable<Notification>> GetForUserAsync(int userId, int take = 50);
    Task<int> UnreadCountAsync(int userId);
    Task CreateAsync(Notification notification);
    Task MarkAllReadAsync(int userId);
}

public interface IRatingRepository
{
    Task<Rating?> GetByAppointmentAsync(int appointmentId);
    Task CreateAsync(Rating rating);
    Task<IEnumerable<Rating>> GetForProviderAsync(int providerId, int take = 20);
}

public class BookingConflictException : Exception
{
    public BookingConflictException(string message) : base(message) { }
}
