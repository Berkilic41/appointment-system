using AppointmentSystem.Data.Entities;

namespace AppointmentSystem.Bll.DTOs;

/// <summary>A bookable time slot computed from working hours minus existing appointments.</summary>
public record TimeSlot(DateTime StartUtc, DateTime EndUtc, bool Available);

// AuthResult and BookingResult removed — replaced by Result<User> and Result<int>
// from AppointmentSystem.Bll.Common.Result<T>
