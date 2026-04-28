using System.ComponentModel.DataAnnotations;
using AppointmentSystem.Data.Entities;
using DomainService = AppointmentSystem.Data.Entities.Service;

namespace AppointmentSystem.Web.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required, MinLength(3), MaxLength(50)] public string Username { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(150)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(150)] public string FullName { get; set; } = string.Empty;
    [MaxLength(50)] public string? Phone { get; set; }
    [Required, MinLength(6), DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    [Required, Compare(nameof(Password)), DataType(DataType.Password)] public string ConfirmPassword { get; set; } = string.Empty;
}

public class ProviderListViewModel
{
    public IEnumerable<ProviderProfile> Providers { get; set; } = [];
}

public class BookingPageViewModel
{
    public ProviderProfile Provider { get; set; } = null!;
    public IEnumerable<DomainService> Services { get; set; } = [];
    public DateTime WeekStart { get; set; }
    public IEnumerable<Appointment> WeeklyAppointments { get; set; } = [];
    public int? PreselectedServiceId { get; set; }
}

public class CustomerHomeViewModel
{
    public IEnumerable<Appointment> Upcoming { get; set; } = [];
    public IEnumerable<Appointment> Past { get; set; } = [];
}

public class ProviderDashboardViewModel
{
    public DateTime WeekStart { get; set; }
    public IEnumerable<Appointment> WeeklyAppointments { get; set; } = [];
    public int Pending { get; set; }
    public int Confirmed { get; set; }
    public int CompletedThisMonth { get; set; }
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
    public IEnumerable<Rating> RecentRatings { get; set; } = [];
}

public class ProviderScheduleViewModel
{
    public IEnumerable<WorkingHour> WorkingHours { get; set; } = [];
    public IEnumerable<DomainService> AllServices { get; set; } = [];
    public IEnumerable<int> SelectedServiceIds { get; set; } = [];
    public IEnumerable<TimeOff> Upcoming { get; set; } = [];
    public string? Bio { get; set; }
    public string? Specialty { get; set; }
}

public class AdminDashboardViewModel
{
    public IEnumerable<DailyCount> DailyCounts { get; set; } = [];
    public IEnumerable<ProviderUtilizationRow> Utilization { get; set; } = [];
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalThisWeek { get; set; }
}
