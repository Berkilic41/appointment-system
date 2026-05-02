using Xunit;
using Moq;
using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;
using AppointmentSystem.Bll.Services;

namespace AppointmentSystem.Service.Tests.Services;

public class RatingServiceTests
{
    private readonly Mock<IRatingRepository> _ratingRepoMock;
    private readonly Mock<IAppointmentRepository> _appointmentRepoMock;
    private readonly RatingService _service;

    public RatingServiceTests()
    {
        _ratingRepoMock = new Mock<IRatingRepository>();
        _appointmentRepoMock = new Mock<IAppointmentRepository>();
        _service = new RatingService(_ratingRepoMock.Object, _appointmentRepoMock.Object);
    }

    private Appointment MakeCompletedAppointment(int id, int customerId, int providerId) => new()
    {
        Id = id, ProviderId = providerId, CustomerId = customerId, ServiceId = 3,
        Status = "Completed",
        StartUtc = new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Utc),
        EndUtc = new DateTime(2026, 5, 10, 10, 30, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task SubmitAsync_WithValidDataAndCompletedAppointment_Succeeds()
    {
        var appt = MakeCompletedAppointment(100, 1, 2);
        _appointmentRepoMock.Setup(r => r.GetByIdAsync(100)).ReturnsAsync(appt);
        _ratingRepoMock.Setup(r => r.GetByAppointmentAsync(100)).ReturnsAsync((Rating?)null);
        _ratingRepoMock.Setup(r => r.CreateAsync(It.IsAny<Rating>())).Returns(Task.CompletedTask);

        await _service.SubmitAsync(100, 1, 5, "Excellent!");

        _ratingRepoMock.Verify(r => r.CreateAsync(It.Is<Rating>(rat =>
            rat.AppointmentId == 100 &&
            rat.CustomerId == 1 &&
            rat.ProviderId == 2 &&
            rat.Stars == 5 &&
            rat.Comment == "Excellent!"
        )), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SubmitAsync_WhenStarsTooLow_ThrowsInvalidOperationException(int stars)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SubmitAsync(100, 1, stars, null)
        );
        Assert.Equal("Rating must be between 1 and 5 stars.", ex.Message);
        _appointmentRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(10)]
    public async Task SubmitAsync_WhenStarsTooHigh_ThrowsInvalidOperationException(int stars)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SubmitAsync(100, 1, stars, null)
        );
        Assert.Equal("Rating must be between 1 and 5 stars.", ex.Message);
    }

    [Fact]
    public async Task SubmitAsync_WhenCustomerRatesOthersAppointment_ThrowsUnauthorizedException()
    {
        var appt = MakeCompletedAppointment(100, 99, 2); // customer=99
        _appointmentRepoMock.Setup(r => r.GetByIdAsync(100)).ReturnsAsync(appt);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.SubmitAsync(100, 1, 5, null) // user=1, not 99
        );
        Assert.Equal("You can only rate your own appointments.", ex.Message);
        _ratingRepoMock.Verify(r => r.CreateAsync(It.IsAny<Rating>()), Times.Never);
    }

    [Fact]
    public async Task SubmitAsync_WhenAppointmentNotCompleted_ThrowsInvalidOperationException()
    {
        var appt = MakeCompletedAppointment(100, 1, 2);
        appt.Status = "Pending";
        _appointmentRepoMock.Setup(r => r.GetByIdAsync(100)).ReturnsAsync(appt);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SubmitAsync(100, 1, 5, null)
        );
        Assert.Equal("You can only rate completed appointments.", ex.Message);
        _ratingRepoMock.Verify(r => r.CreateAsync(It.IsAny<Rating>()), Times.Never);
    }

    [Fact]
    public async Task SubmitAsync_WhenAlreadyRated_ThrowsInvalidOperationException()
    {
        var appt = MakeCompletedAppointment(100, 1, 2);
        _appointmentRepoMock.Setup(r => r.GetByIdAsync(100)).ReturnsAsync(appt);
        _ratingRepoMock.Setup(r => r.GetByAppointmentAsync(100))
            .ReturnsAsync(new Rating { Id = 50, AppointmentId = 100, CustomerId = 1, ProviderId = 2, Stars = 3 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SubmitAsync(100, 1, 5, null)
        );
        Assert.Equal("This appointment has already been rated.", ex.Message);
        _ratingRepoMock.Verify(r => r.CreateAsync(It.IsAny<Rating>()), Times.Never);
    }
}
