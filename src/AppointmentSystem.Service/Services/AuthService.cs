using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;
using AppointmentSystem.Bll.DTOs;
using AppointmentSystem.Bll.Helpers;
using AppointmentSystem.Bll.Services.Interfaces;

namespace AppointmentSystem.Bll.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    public AuthService(IUserRepository users) => _users = users;

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var user = await _users.GetByEmailAsync(email);
        if (user is null) return AuthResult.Fail("Invalid email or password.");
        if (!user.IsActive) return AuthResult.Fail("This account is disabled.");
        if (!PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
            return AuthResult.Fail("Invalid email or password.");
        return AuthResult.Ok(user);
    }

    public async Task<AuthResult> RegisterCustomerAsync(string username, string email, string password, string fullName, string? phone)
    {
        if (await _users.ExistsByEmailAsync(email))
            return AuthResult.Fail("Email already in use.");
        if (await _users.ExistsByUsernameAsync(username))
            return AuthResult.Fail("Username already taken.");

        var (hash, salt) = PasswordHasher.Hash(password);
        var user = new User
        {
            Username = username, Email = email,
            PasswordHash = hash, PasswordSalt = salt,
            Role = "Customer", FullName = fullName, Phone = phone
        };
        user.Id = await _users.CreateAsync(user);
        return AuthResult.Ok(user);
    }

    public Task<User?> GetByIdAsync(int id) => _users.GetByIdAsync(id);
}
