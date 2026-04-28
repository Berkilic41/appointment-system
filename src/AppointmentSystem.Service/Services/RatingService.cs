using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;
using AppointmentSystem.Bll.Services.Interfaces;

namespace AppointmentSystem.Bll.Services;

public class RatingService : IRatingService
{
    private readonly IRatingRepository _ratings;
    private readonly IAppointmentRepository _appts;

    public RatingService(IRatingRepository ratings, IAppointmentRepository appts)
    {
        _ratings = ratings;
        _appts = appts;
    }

    public Task<Rating?> GetByAppointmentAsync(int appointmentId) => _ratings.GetByAppointmentAsync(appointmentId);
    public Task<IEnumerable<Rating>> GetForProviderAsync(int providerId) => _ratings.GetForProviderAsync(providerId);

    public async Task SubmitAsync(int appointmentId, int customerId, int stars, string? comment)
    {
        if (stars < 1 || stars > 5)
            throw new InvalidOperationException("Rating must be between 1 and 5 stars.");

        var appt = await _appts.GetByIdAsync(appointmentId)
            ?? throw new InvalidOperationException("Appointment not found.");
        if (appt.CustomerId != customerId)
            throw new UnauthorizedAccessException("You can only rate your own appointments.");
        if (appt.Status != "Completed")
            throw new InvalidOperationException("You can only rate completed appointments.");

        if (await _ratings.GetByAppointmentAsync(appointmentId) is not null)
            throw new InvalidOperationException("This appointment has already been rated.");

        await _ratings.CreateAsync(new Rating
        {
            AppointmentId = appointmentId,
            CustomerId = customerId,
            ProviderId = appt.ProviderId,
            Stars = stars,
            Comment = comment
        });
    }
}
