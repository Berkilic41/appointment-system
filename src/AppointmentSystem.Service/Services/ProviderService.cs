using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;
using AppointmentSystem.Bll.Services.Interfaces;

namespace AppointmentSystem.Bll.Services;

public class ProviderService : IProviderService
{
    private readonly IUserRepository _users;
    public ProviderService(IUserRepository users) => _users = users;

    public Task<IEnumerable<ProviderProfile>> GetAllAsync() => _users.GetAllProvidersAsync();
    public Task<ProviderProfile?> GetByIdAsync(int id) => _users.GetProviderProfileAsync(id);
    public Task UpsertProfileAsync(int id, string? bio, string? specialty)
        => _users.UpsertProviderProfileAsync(id, bio, specialty);
}
