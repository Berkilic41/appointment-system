using Xunit;
using Moq;
using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;
using AppointmentSystem.Bll.Services;

namespace AppointmentSystem.Service.Tests.Services;

public class AppointmentServiceTests
{
    private readonly Mock<IAppointmentRepository> _appointmentRepoMock;
    private readonly Mock<IServiceRepository> _serviceRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<INotificationRepository> _notificationRepoMock;
    private readonly AppointmentService _service;

    public AppointmentServiceTests()
    {
        _appointmentRepoMock = new Mock<IAppointmentRepository>();
        _serviceRepoMock = new Mock<IServiceRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _notificationRepoMock = new Mock<INotificationRepository>();

        _service = new AppointmentService(
            _appointmentRepoMock.Object,
            _serviceRepoMock.Object,
            _userRepoMock.Object,
            _notificationRepoMock.Object
        );
    }

    [Fact]
    public async Task BookAsync_WhenSlotAlreadyBooked_ThrowsBookingConflictException()
    {
        // Arrange
        var customerId = 1;
        var providerId = 2;
        var serviceId = 3;
        var startUtc = new DateTime(2026, 5, 15, 10, 0, 0, DateTimeKind.Utc);

        _serviceRepoMock.Setup(r => r.GetByIdAsync(serviceId))
            .ReturnsAsync(new Service { Id = serviceId, Name = "Haircut", DurationMinutes = 30, Price = 25m, IsActive = true });

        _userRepoMock.Setup(r => r.GetByIdAsync(providerId))
            .ReturnsAsync(new User { Id = providerId, Username = "provider", Email = "p@p.com", Role = "Provider", IsActive = true });

        _appointmentRepoMock.Setup(r => r.CreateWithConflictCheckAsync(It.IsAny<Appointment>()))
            .ThrowsAsync(new BookingConflictException("This time slot is no longer available."));

        // Act
        var result = await _service.BookAsync(customerId, providerId, serviceId, startUtc, null);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.AppointmentId);
        Assert.Equal("This time slot is no longer available.", result.ErrorMessage);
        _appointmentRepoMock.Verify(r => r.CreateWithConflictCheckAsync(It.IsAny<Appointment>()), Times.Once);
    }

    [Fact]
    public async Task BookAsync_WhenSlotAvailable_CreatesAppointmentAndNotifications()
    {
        // Arrange
        var customerId = 1;
        var providerId = 2;
        var serviceId = 3;
        var appointmentId = 100;
        var startUtc = new DateTime(2026, 5, 15, 10, 0, 0, DateTimeKind.Utc);
        var notes = "Please call 5 minutes before";

        _serviceRepoMock.Setup(r => r.GetByIdAsync(serviceId))
            .ReturnsAsync(new Service { Id = serviceId, Name = "Haircut", DurationMinutes = 30, Price = 25m, IsActive = true });

        _userRepoMock.Setup(r => r.GetByIdAsync(providerId))
            .ReturnsAsync(new User { Id = providerId, Username = "provider", Email = "p@p.com", Role = "Provider", IsActive = true });

        _appointmentRepoMock.Setup(r => r.CreateWithConflictCheckAsync(It.IsAny<Appointment>()))
            .ReturnsAsync(appointmentId);

        _notificationRepoMock.Setup(r => r.CreateAsync(It.IsAny<Notification>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.BookAsync(customerId, providerId, serviceId, startUtc, notes);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(appointmentId, result.AppointmentId);
        Assert.Null(result.ErrorMessage);

        _appointmentRepoMock.Verify(r => r.CreateWithConflictCheckAsync(It.Is<Appointment>(a =>
            a.ProviderId == providerId &&
            a.CustomerId == customerId &&
            a.ServiceId == serviceId &&
            a.StartUtc == startUtc &&
            a.EndUtc == startUtc.AddMinutes(30) &&
            a.Status == "Pending" &&
            a.Notes == notes
        )), Times.Once);

        _notificationRepoMock.Verify(r => r.CreateAsync(It.IsAny<Notification>()), Times.Exactly(2));
        _notificationRepoMock.Verify(r => r.CreateAsync(It.Is<Notification>(n =>
            n.UserId == providerId && n.Title == "New booking request" && n.RelatedAppointmentId == appointmentId
        )), Times.Once);
        _notificationRepoMock.Verify(r => r.CreateAsync(It.Is<Notification>(n =>
            n.UserId == customerId && n.Title == "Booking submitted" && n.RelatedAppointmentId == appointmentId
        )), Times.Once);
    }

    [Fact]
    public async Task BookAsync_WhenSlotInPast_ReturnsFailure()
    {
        var customerId = 1;
        var providerId = 2;
        var serviceId = 3;
        var startUtc = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc); // past

        _serviceRepoMock.Setup(r => r.GetByIdAsync(serviceId))
            .ReturnsAsync(new Service { Id = serviceId, Name = "Haircut", DurationMinutes = 30, Price = 25m, IsActive = true });

        _userRepoMock.Setup(r => r.GetByIdAsync(providerId))
            .ReturnsAsync(new User { Id = providerId, Role = "Provider", IsActive = true });

        var result = await _service.BookAsync(customerId, providerId, serviceId, startUtc, null);

        Assert.False(result.Success);
        Assert.Equal("Cannot book a slot in the past.", result.ErrorMessage);
        _appointmentRepoMock.Verify(r => r.CreateWithConflictCheckAsync(It.IsAny<Appointment>()), Times.Never);
    }

    [Fact]
    public async Task BookAsync_WhenServiceNotFound_ReturnsFailure()
    {
        _serviceRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Service?)null);

        var result = await _service.BookAsync(1, 2, 999, DateTime.UtcNow.AddDays(1), null);

        Assert.False(result.Success);
        Assert.Equal("Service not found.", result.ErrorMessage);
    }

    [Fact]
    public async Task BookAsync_WhenProviderNotFound_ReturnsFailure()
    {
        _serviceRepoMock.Setup(r => r.GetByIdAsync(3))
            .ReturnsAsync(new Service { Id = 3, DurationMinutes = 30, IsActive = true });
        _userRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((User?)null);

        var result = await _service.BookAsync(1, 999, 3, DateTime.UtcNow.AddDays(1), null);

        Assert.False(result.Success);
        Assert.Equal("Provider not found.", result.ErrorMessage);
    }

    [Fact]
    public async Task BookAsync_WhenUserIsNotProvider_ReturnsFailure()
    {
        _serviceRepoMock.Setup(r => r.GetByIdAsync(3))
            .ReturnsAsync(new Service { Id = 3, DurationMinutes = 30, IsActive = true });
        _userRepoMock.Setup(r => r.GetByIdAsync(2))
            .ReturnsAsync(new User { Id = 2, Role = "Customer", IsActive = true });

        var result = await _service.BookAsync(1, 2, 3, DateTime.UtcNow.AddDays(1), null);

        Assert.False(result.Success);
        Assert.Equal("Provider not found.", result.ErrorMessage);
    }
}

public class AppointmentServiceStatusChangeTests
{
    private readonly Mock<IAppointmentRepository> _appointmentRepoMock;
    private readonly Mock<IServiceRepository> _serviceRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<INotificationRepository> _notificationRepoMock;
    private readonly AppointmentService _service;

    public AppointmentServiceStatusChangeTests()
    {
        _appointmentRepoMock = new Mock<IAppointmentRepository>();
        _serviceRepoMock = new Mock<IServiceRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _notificationRepoMock = new Mock<INotificationRepository>();

        _service = new AppointmentService(
            _appointmentRepoMock.Object,
            _serviceRepoMock.Object,
            _userRepoMock.Object,
            _notificationRepoMock.Object
        );
    }

    private Appointment MakeAppointment(int id, int providerId, int customerId, string status) => new()
    {
        Id = id,
        ProviderId = providerId,
        CustomerId = customerId,
        ServiceId = 3,
        ServiceName = "Haircut",
        Status = status,
        StartUtc = new DateTime(2026, 5, 15, 10, 0, 0, DateTimeKind.Utc),
        EndUtc = new DateTime(2026, 5, 15, 10, 30, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task ChangeStatusAsync_WhenProviderConfirmsPending_Succeeds()
    {
        var appt = MakeAppointment(100, 2, 1, "Pending");
        _appointmentRepoMock.Setup(r => r.GetByIdAsync(100)).ReturnsAsync(appt);
        _appointmentRepoMock.Setup(r => r.UpdateStatusAsync(100, "Confirmed")).Returns(Task.CompletedTask);
        _notificationRepoMock.Setup(r => r.CreateAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);

        var result = await _service.ChangeStatusAsync(100, 2, "Provider", "Confirmed");

        Assert.True(result);
        _appointmentRepoMock.Verify(r => r.UpdateStatusAsync(100, "Confirmed"), Times.Once);
        _notificationRepoMock.Verify(r => r.CreateAsync(It.Is<Notification>(n =>
            n.UserId == 1 && n.Title == "Appointment confirmed"
        )), Times.Once);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenProviderConfirmsAlreadyConfirmed_Fails()
    {
        var appt = MakeAppointment(100, 2, 1, "Confirmed");
        _appointmentRepoMock.Setup(r => r.GetByIdAsync(100)).ReturnsAsync(appt);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.ChangeStatusAsync(100, 2, "Provider", "Confirmed")
        );

        _appointmentRepoMock.Verify(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenCustomerTriesToConfirm_Fails()
    {
        var appt = MakeAppointment(100, 2, 1, "Pending");
        _appointmentRepoMock.Setup(r => r.GetByIdAsync(100)).ReturnsAsync(appt);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.ChangeStatusAsync(100, 1, "Customer", "Confirmed")
        );
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenCustomerCancelsPending_Succeeds()
    {
        var appt = MakeAppointment(100, 2, 1, "Pending");
        _appointmentRepoMock.Setup(r => r.GetByIdAsync(100)).ReturnsAsync(appt);
        _appointmentRepoMock.Setup(r => r.UpdateStatusAsync(100, "Cancelled")).Returns(Task.CompletedTask);
        _notificationRepoMock.Setup(r => r.CreateAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);

        var result = await _service.ChangeStatusAsync(100, 1, "Customer", "Cancelled");

        Assert.True(result);
        _notificationRepoMock.Verify(r => r.CreateAsync(It.Is<Notification>(n =>
            n.UserId == 2 && n.Title == "Appointment cancelled"
        )), Times.Once);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenCustomerCancelsSomeoneElses_Fails()
    {
        var appt = MakeAppointment(100, 2, 5, "Pending");
        _appointmentRepoMock.Setup(r => r.GetByIdAsync(100)).ReturnsAsync(appt);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.ChangeStatusAsync(100, 1, "Customer", "Cancelled")
        );
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenAdminChangesStatus_Succeeds()
    {
        var appt = MakeAppointment(100, 2, 1, "Confirmed");
        _appointmentRepoMock.Setup(r => r.GetByIdAsync(100)).ReturnsAsync(appt);
        _appointmentRepoMock.Setup(r => r.UpdateStatusAsync(100, "Cancelled")).Returns(Task.CompletedTask);
        _notificationRepoMock.Setup(r => r.CreateAsync(It.IsAny<Notification>())).Returns(Task.CompletedTask);

        var result = await _service.ChangeStatusAsync(100, 999, "Admin", "Cancelled");

        Assert.True(result);
        _appointmentRepoMock.Verify(r => r.UpdateStatusAsync(100, "Cancelled"), Times.Once);
    }
}
