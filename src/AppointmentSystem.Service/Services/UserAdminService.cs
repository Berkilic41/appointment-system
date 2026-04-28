using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;
using AppointmentSystem.Bll.Services.Interfaces;

namespace AppointmentSystem.Bll.Services;

public class UserAdminService : IUserAdminService
{
    private static readonly HashSet<string> ValidRoles = ["Admin", "Provider", "Customer"];
    private readonly IUserRepository _repo;
    public UserAdminService(IUserRepository repo) => _repo = repo;

    public Task<IEnumerable<User>> GetAllAsync() => _repo.GetAllAsync();

    public Task UpdateRoleAsync(int id, string role)
    {
        if (!ValidRoles.Contains(role))
            throw new InvalidOperationException($"Invalid role. Must be one of: {string.Join(", ", ValidRoles)}");
        return _repo.UpdateRoleAsync(id, role);
    }

    public Task SetActiveAsync(int id, bool active) => _repo.SetActiveAsync(id, active);
}
