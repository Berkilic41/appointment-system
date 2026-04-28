using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;
using AppointmentSystem.Bll.Services.Interfaces;

namespace AppointmentSystem.Bll.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repo;
    public NotificationService(INotificationRepository repo) => _repo = repo;

    public Task<IEnumerable<Notification>> GetForUserAsync(int userId) => _repo.GetForUserAsync(userId);
    public Task<int> UnreadCountAsync(int userId) => _repo.UnreadCountAsync(userId);
    public Task MarkAllReadAsync(int userId) => _repo.MarkAllReadAsync(userId);
}
