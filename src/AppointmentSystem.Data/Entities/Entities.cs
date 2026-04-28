namespace AppointmentSystem.Data.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string Role { get; set; } = "Customer";
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class ProviderProfile
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Bio { get; set; }
    public string? Specialty { get; set; }
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
}

public class Service
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}

public class WorkingHour
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public byte DayOfWeek { get; set; }
    public short StartMinutes { get; set; }
    public short EndMinutes { get; set; }
}

public class TimeOff
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string? Reason { get; set; }
}

public class Appointment
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public int CustomerId { get; set; }
    public int ServiceId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string? ProviderName { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? ServiceName { get; set; }
    public int? RatingStars { get; set; }
}

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int? RelatedAppointmentId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Rating
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public int CustomerId { get; set; }
    public int ProviderId { get; set; }
    public int Stars { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CustomerName { get; set; }
    public string? ServiceName { get; set; }
}

public class ProviderUtilizationRow
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public int MinutesBooked { get; set; }
    public int AppointmentCount { get; set; }
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
}

public class DailyCount
{
    public DateTime Day { get; set; }
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Confirmed { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
}
