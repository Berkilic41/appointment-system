using AppointmentSystem.Bll.Common;
using AppointmentSystem.Bll.Helpers;
using AppointmentSystem.Bll.Services.Interfaces;
using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;

namespace AppointmentSystem.Bll.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    public AuthService(IUserRepository users) => _users = users;

    public async Task<Result<User>> LoginAsync(string email, string password)
    {
        var user = await _users.GetByEmailAsync(email);
        if (user is null) return Result<User>.Fail("Invalid email or password.");
        if (!user.IsActive) return Result<User>.Fail("This account is disabled.");
        if (!PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
            return Result<User>.Fail("Invalid email or password.");
        return Result<User>.Ok(user);
    }

    public async Task<Result<User>> RegisterCustomerAsync(string username, string email, string password, string fullName, string? phone)
    {
        if (await _users.ExistsByEmailAsync(email))
            return Result<User>.Fail("Email already in use.");
        if (await _users.ExistsByUsernameAsync(username))
            return Result<User>.Fail("Username already taken.");

        var (hash, salt) = PasswordHasher.Hash(password);
        var user = new User
        {
            Username = username, Email = email,
            PasswordHash = hash, PasswordSalt = salt,
            Role = "Customer", FullName = fullName, Phone = phone
        };
        user.Id = await _users.CreateAsync(user);
        return Result<User>.Ok(user);
    }

    public Task<User?> GetByIdAsync(int id) => _users.GetByIdAsync(id);
}
