using Xunit;
using Moq;
using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;
using AppointmentSystem.Bll.Services;
using AppointmentSystem.Bll.Helpers;

namespace AppointmentSystem.Service.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _service = new AuthService(_userRepoMock.Object);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsSuccessAndUser()
    {
        var email = "john@example.com";
        var password = "SecurePassword123!";
        var (hash, salt) = PasswordHasher.Hash(password);

        var user = new User { Id = 1, Username = "john", Email = email, PasswordHash = hash, PasswordSalt = salt, Role = "Customer", IsActive = true, FullName = "John Doe" };
        _userRepoMock.Setup(r => r.GetByEmailAsync(email)).ReturnsAsync(user);

        var result = await _service.LoginAsync(email, password);

        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsFail()
    {
        var email = "john@example.com";
        var (hash, salt) = PasswordHasher.Hash("CorrectPassword");
        var user = new User { Id = 1, Email = email, PasswordHash = hash, PasswordSalt = salt, Role = "Customer", IsActive = true };
        _userRepoMock.Setup(r => r.GetByEmailAsync(email)).ReturnsAsync(user);

        var result = await _service.LoginAsync(email, "WrongPassword");

        Assert.False(result.Success);
        Assert.Null(result.User);
        Assert.Equal("Invalid email or password.", result.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_WhenAccountDisabled_ReturnsFail()
    {
        var email = "john@example.com";
        var password = "SecurePassword123!";
        var (hash, salt) = PasswordHasher.Hash(password);
        var user = new User { Id = 1, Email = email, PasswordHash = hash, PasswordSalt = salt, Role = "Customer", IsActive = false };
        _userRepoMock.Setup(r => r.GetByEmailAsync(email)).ReturnsAsync(user);

        var result = await _service.LoginAsync(email, password);

        Assert.False(result.Success);
        Assert.Equal("This account is disabled.", result.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_WhenEmailNotFound_ReturnsFail()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync("notfound@example.com")).ReturnsAsync((User?)null);

        var result = await _service.LoginAsync("notfound@example.com", "AnyPassword");

        Assert.False(result.Success);
        Assert.Equal("Invalid email or password.", result.ErrorMessage);
    }

    [Fact]
    public async Task RegisterCustomerAsync_WithValidData_CreatesUserAndReturnsSuccess()
    {
        var username = "newuser";
        var email = "newuser@example.com";
        var password = "SecurePassword123!";
        var fullName = "New User";
        var userId = 42;

        _userRepoMock.Setup(r => r.ExistsByEmailAsync(email)).ReturnsAsync(false);
        _userRepoMock.Setup(r => r.ExistsByUsernameAsync(username)).ReturnsAsync(false);
        _userRepoMock.Setup(r => r.CreateAsync(It.IsAny<User>())).ReturnsAsync(userId);

        var result = await _service.RegisterCustomerAsync(username, email, password, fullName, null);

        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal(userId, result.User.Id);
        Assert.Equal("Customer", result.User.Role);

        _userRepoMock.Verify(r => r.CreateAsync(It.Is<User>(u =>
            u.Username == username &&
            u.Email == email &&
            u.Role == "Customer" &&
            !string.IsNullOrEmpty(u.PasswordHash) &&
            !string.IsNullOrEmpty(u.PasswordSalt) &&
            u.IsActive == true
        )), Times.Once);
    }

    [Fact]
    public async Task RegisterCustomerAsync_WhenEmailExists_ReturnsFail()
    {
        _userRepoMock.Setup(r => r.ExistsByEmailAsync("existing@example.com")).ReturnsAsync(true);

        var result = await _service.RegisterCustomerAsync("user", "existing@example.com", "Pass123!", "Name", null);

        Assert.False(result.Success);
        Assert.Equal("Email already in use.", result.ErrorMessage);
        _userRepoMock.Verify(r => r.CreateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task RegisterCustomerAsync_WhenUsernameExists_ReturnsFail()
    {
        _userRepoMock.Setup(r => r.ExistsByEmailAsync("new@example.com")).ReturnsAsync(false);
        _userRepoMock.Setup(r => r.ExistsByUsernameAsync("takenuser")).ReturnsAsync(true);

        var result = await _service.RegisterCustomerAsync("takenuser", "new@example.com", "Pass123!", "Name", null);

        Assert.False(result.Success);
        Assert.Equal("Username already taken.", result.ErrorMessage);
        _userRepoMock.Verify(r => r.CreateAsync(It.IsAny<User>()), Times.Never);
    }
}
