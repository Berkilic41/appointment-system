using AppointmentSystem.Bll.Services;
using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;
using Moq;
using Xunit;

namespace AppointmentSystem.Service.Tests.Services;

public class ScheduleServiceTests
{
    private readonly Mock<IScheduleRepository>    _scheduleMock;
    private readonly Mock<IAppointmentRepository> _apptMock;
    private readonly Mock<IServiceRepository>     _servicesMock;
    private readonly ScheduleService              _service;

    public ScheduleServiceTests()
    {
        _scheduleMock = new Mock<IScheduleRepository>();
        _apptMock     = new Mock<IAppointmentRepository>();
        _servicesMock = new Mock<IServiceRepository>();
        _service = new ScheduleService(_scheduleMock.Object, _apptMock.Object, _servicesMock.Object);
    }

    private static Service MakeService(int id, int durationMinutes) =>
        new() { Id = id, Name = "Test Service", DurationMinutes = durationMinutes, IsActive = true };

    private static WorkingHour MakeHours(int providerId, DayOfWeek day, int startMinutes, int endMinutes) =>
        new() { Id = 1, ProviderId = providerId, DayOfWeek = day, StartMinutes = startMinutes, EndMinutes = endMinutes };

    private static Appointment MakeAppt(DateTime start, DateTime end, string status = "Confirmed") =>
        new() { Id = 1, Status = status, StartUtc = start, EndUtc = end };

    private static TimeOff MakeTimeOff(DateTime start, DateTime end) =>
        new() { Id = 1, StartUtc = start, EndUtc = end };

    // ─── Service Not Found ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableSlotsAsync_ServiceNotFound_ThrowsInvalidOperation()
    {
        _servicesMock.Setup(s => s.GetByIdAsync(99)).ReturnsAsync((Service?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetAvailableSlotsAsync(1, 99, DateTime.UtcNow.Date));
    }

    // ─── No Working Hours ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableSlotsAsync_NoWorkingHours_ReturnsEmpty()
    {
        var date = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc); // Monday
        _servicesMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(MakeService(1, 30));
        _scheduleMock.Setup(s => s.GetWorkingHoursAsync(1)).ReturnsAsync([]);
        _apptMock.Setup(a => a.GetForProviderAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);
        _scheduleMock.Setup(s => s.GetTimeOffAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);

        var slots = await _service.GetAvailableSlotsAsync(1, 1, date);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_WorkingHoursForDifferentDay_ReturnsEmpty()
    {
        // Date is Monday (DayOfWeek = 1), working hours are for Friday (5)
        var date = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc); // Monday
        _servicesMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(MakeService(1, 30));
        _scheduleMock.Setup(s => s.GetWorkingHoursAsync(1))
            .ReturnsAsync(new[] { MakeHours(1, DayOfWeek.Friday, 540, 1020) });
        _apptMock.Setup(a => a.GetForProviderAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);
        _scheduleMock.Setup(s => s.GetTimeOffAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);

        var slots = await _service.GetAvailableSlotsAsync(1, 1, date);

        Assert.Empty(slots);
    }

    // ─── Slot Generation ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableSlotsAsync_ClearDay_GeneratesCorrectNumberOfSlots()
    {
        // Monday with 9:00-11:00 working window (120 min) and 30-min slots = 4 slots
        var date = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        _servicesMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(MakeService(1, 30));
        _scheduleMock.Setup(s => s.GetWorkingHoursAsync(1))
            .ReturnsAsync(new[] { MakeHours(1, DayOfWeek.Monday, 540, 660) }); // 9:00–11:00
        _apptMock.Setup(a => a.GetForProviderAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);
        _scheduleMock.Setup(s => s.GetTimeOffAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);

        var slots = (await _service.GetAvailableSlotsAsync(1, 1, date)).ToList();

        Assert.Equal(4, slots.Count); // 9:00, 9:30, 10:00, 10:30
        Assert.All(slots, s => Assert.Equal(TimeSpan.FromMinutes(30), s.EndUtc - s.StartUtc));
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_SlotDurationLargerThanWindow_GeneratesZeroSlots()
    {
        // Window 30 min, service duration 60 min — no slots fit
        var date = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        _servicesMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(MakeService(1, 60));
        _scheduleMock.Setup(s => s.GetWorkingHoursAsync(1))
            .ReturnsAsync(new[] { MakeHours(1, DayOfWeek.Monday, 540, 570) }); // 9:00–9:30
        _apptMock.Setup(a => a.GetForProviderAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);
        _scheduleMock.Setup(s => s.GetTimeOffAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);

        var slots = (await _service.GetAvailableSlotsAsync(1, 1, date)).ToList();

        Assert.Empty(slots);
    }

    // ─── Time-Off Blocking ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableSlotsAsync_SlotBlockedByTimeOff_MarkedUnavailable()
    {
        var date     = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var slot9am  = date.AddMinutes(540); // 9:00
        var slot930  = date.AddMinutes(570); // 9:30

        _servicesMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(MakeService(1, 30));
        _scheduleMock.Setup(s => s.GetWorkingHoursAsync(1))
            .ReturnsAsync(new[] { MakeHours(1, DayOfWeek.Monday, 540, 660) }); // 9:00–11:00
        _apptMock.Setup(a => a.GetForProviderAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);
        // Time-off covers 9:00–9:30 exactly
        _scheduleMock.Setup(s => s.GetTimeOffAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new[] { MakeTimeOff(slot9am, slot930) });

        var slots = (await _service.GetAvailableSlotsAsync(1, 1, date)).ToList();

        Assert.Equal(4, slots.Count);
        Assert.False(slots[0].Available);  // 9:00 blocked
        Assert.True(slots[1].Available);   // 9:30 free
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_TimeOffSpanningMultipleSlots_BlocksAll()
    {
        var date    = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var offStart = date.AddMinutes(540);  // 9:00
        var offEnd   = date.AddMinutes(630);  // 10:30 — blocks 9:00, 9:30, 10:00

        _servicesMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(MakeService(1, 30));
        _scheduleMock.Setup(s => s.GetWorkingHoursAsync(1))
            .ReturnsAsync(new[] { MakeHours(1, DayOfWeek.Monday, 540, 660) });
        _apptMock.Setup(a => a.GetForProviderAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);
        _scheduleMock.Setup(s => s.GetTimeOffAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new[] { MakeTimeOff(offStart, offEnd) });

        var slots = (await _service.GetAvailableSlotsAsync(1, 1, date)).ToList();

        Assert.False(slots[0].Available); // 9:00
        Assert.False(slots[1].Available); // 9:30
        Assert.False(slots[2].Available); // 10:00
        Assert.True(slots[3].Available);  // 10:30 — free
    }

    // ─── Appointment Blocking ────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableSlotsAsync_SlotTakenByConfirmedAppt_MarkedUnavailable()
    {
        var date = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var apptStart = date.AddMinutes(540); // 9:00
        var apptEnd   = date.AddMinutes(570); // 9:30

        _servicesMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(MakeService(1, 30));
        _scheduleMock.Setup(s => s.GetWorkingHoursAsync(1))
            .ReturnsAsync(new[] { MakeHours(1, DayOfWeek.Monday, 540, 660) });
        _apptMock.Setup(a => a.GetForProviderAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new[] { MakeAppt(apptStart, apptEnd, "Confirmed") });
        _scheduleMock.Setup(s => s.GetTimeOffAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);

        var slots = (await _service.GetAvailableSlotsAsync(1, 1, date)).ToList();

        Assert.False(slots[0].Available); // 9:00 taken
        Assert.True(slots[1].Available);  // 9:30 free
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_SlotTakenByPendingAppt_MarkedUnavailable()
    {
        var date      = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var apptStart = date.AddMinutes(540);
        var apptEnd   = date.AddMinutes(570);

        _servicesMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(MakeService(1, 30));
        _scheduleMock.Setup(s => s.GetWorkingHoursAsync(1))
            .ReturnsAsync(new[] { MakeHours(1, DayOfWeek.Monday, 540, 660) });
        _apptMock.Setup(a => a.GetForProviderAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new[] { MakeAppt(apptStart, apptEnd, "Pending") });
        _scheduleMock.Setup(s => s.GetTimeOffAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);

        var slots = (await _service.GetAvailableSlotsAsync(1, 1, date)).ToList();

        Assert.False(slots[0].Available);
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_CancelledAppt_SlotRemainsAvailable()
    {
        var date      = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var apptStart = date.AddMinutes(540);
        var apptEnd   = date.AddMinutes(570);

        _servicesMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(MakeService(1, 30));
        _scheduleMock.Setup(s => s.GetWorkingHoursAsync(1))
            .ReturnsAsync(new[] { MakeHours(1, DayOfWeek.Monday, 540, 660) });
        _apptMock.Setup(a => a.GetForProviderAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new[] { MakeAppt(apptStart, apptEnd, "Cancelled") }); // cancelled = free
        _scheduleMock.Setup(s => s.GetTimeOffAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);

        var slots = (await _service.GetAvailableSlotsAsync(1, 1, date)).ToList();

        Assert.True(slots[0].Available); // Cancelled doesn't block
    }

    // ─── Slot Timing ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableSlotsAsync_SlotsHaveCorrectStartTimes()
    {
        var date = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        _servicesMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(MakeService(1, 30));
        _scheduleMock.Setup(s => s.GetWorkingHoursAsync(1))
            .ReturnsAsync(new[] { MakeHours(1, DayOfWeek.Monday, 540, 660) }); // 9:00–11:00
        _apptMock.Setup(a => a.GetForProviderAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);
        _scheduleMock.Setup(s => s.GetTimeOffAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);

        var slots = (await _service.GetAvailableSlotsAsync(1, 1, date)).ToList();

        Assert.Equal(date.AddMinutes(540), slots[0].StartUtc); // 9:00
        Assert.Equal(date.AddMinutes(570), slots[1].StartUtc); // 9:30
        Assert.Equal(date.AddMinutes(600), slots[2].StartUtc); // 10:00
        Assert.Equal(date.AddMinutes(630), slots[3].StartUtc); // 10:30
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_SlotsEndTimeIsStartPlusDuration()
    {
        var date = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        _servicesMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(MakeService(1, 45));
        _scheduleMock.Setup(s => s.GetWorkingHoursAsync(1))
            .ReturnsAsync(new[] { MakeHours(1, DayOfWeek.Monday, 540, 720) }); // 9:00–12:00
        _apptMock.Setup(a => a.GetForProviderAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);
        _scheduleMock.Setup(s => s.GetTimeOffAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);

        var slots = (await _service.GetAvailableSlotsAsync(1, 1, date)).ToList();

        Assert.All(slots, s =>
            Assert.Equal(TimeSpan.FromMinutes(45), s.EndUtc - s.StartUtc));
    }

    // ─── Parallel Query Verification ─────────────────────────────────────────

    [Fact]
    public async Task GetAvailableSlotsAsync_BothApptAndTimeOffQueriesExecuted()
    {
        var date = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        _servicesMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(MakeService(1, 30));
        _scheduleMock.Setup(s => s.GetWorkingHoursAsync(1))
            .ReturnsAsync(new[] { MakeHours(1, DayOfWeek.Monday, 540, 660) });
        _apptMock.Setup(a => a.GetForProviderAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);
        _scheduleMock.Setup(s => s.GetTimeOffAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);

        await _service.GetAvailableSlotsAsync(1, 1, date);

        _apptMock.Verify(a => a.GetForProviderAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
        _scheduleMock.Verify(s => s.GetTimeOffAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
    }
}
