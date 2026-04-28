using AppointmentSystem.Data.Entities;

namespace AppointmentSystem.Bll.DTOs;

public record AuthResult(bool Success, string? ErrorMessage = null, User? User = null)
{
    public static AuthResult Ok(User user) => new(true, null, user);
    public static AuthResult Fail(string message) => new(false, message);
}

/// <summary>A bookable time slot computed from working hours - existing appointments.</summary>
public record TimeSlot(DateTime StartUtc, DateTime EndUtc, bool Available);

public record BookingResult(bool Success, int? AppointmentId = null, string? ErrorMessage = null);
