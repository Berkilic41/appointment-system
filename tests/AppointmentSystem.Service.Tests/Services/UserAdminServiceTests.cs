using Xunit;
using Moq;
using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;
using AppointmentSystem.Bll.Services;

namespace AppointmentSystem.Service.Tests.Services;

public class UserAdminServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly UserAdminService _service;

    public UserAdminServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _service = new UserAdminService(_userRepoMock.Object);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Provider")]
    [InlineData("Customer")]
    public async Task UpdateRoleAsync_WithValidRole_Succeeds(string validRole)
    {
        _userRepoMock.Setup(r => r.UpdateRoleAsync(1, validRole)).Returns(Task.CompletedTask);

        await _service.UpdateRoleAsync(1, validRole);

        _userRepoMock.Verify(r => r.UpdateRoleAsync(1, validRole), Times.Once);
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("InvalidRole")]
    [InlineData("admin")]
    [InlineData("")]
    [InlineData("Admin,Provider")]
    public async Task UpdateRoleAsync_WithInvalidRole_ThrowsInvalidOperationException(string invalidRole)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateRoleAsync(1, invalidRole)
        );
        Assert.StartsWith("Invalid role. Must be one of:", ex.Message);
        _userRepoMock.Verify(r => r.UpdateRoleAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetActiveAsync_WithValidInput_CallsRepository(bool active)
    {
        _userRepoMock.Setup(r => r.SetActiveAsync(1, active)).Returns(Task.CompletedTask);

        await _service.SetActiveAsync(1, active);

        _userRepoMock.Verify(r => r.SetActiveAsync(1, active), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllUsers()
    {
        var users = new[]
        {
            new User { Id = 1, Username = "admin", Email = "admin@e.com", Role = "Admin" },
            new User { Id = 2, Username = "prov", Email = "prov@e.com", Role = "Provider" },
            new User { Id = 3, Username = "cust", Email = "cust@e.com", Role = "Customer" }
        };
        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(users);

        var result = await _service.GetAllAsync();

        Assert.Equal(3, result.Count());
        _userRepoMock.Verify(r => r.GetAllAsync(), Times.Once);
    }
}
