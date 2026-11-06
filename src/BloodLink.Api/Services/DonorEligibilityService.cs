using BloodLink.Api.Domain;

namespace BloodLink.Api.Services;

public interface IDonorEligibilityService
{
    (bool IsEligible, string? Reason) CheckEligibility(Donor donor, DateTime appointmentDate);
}

public class DonorEligibilityService : IDonorEligibilityService
{
    public const int MinimumAgeYears = 18;
    public const int MinimumDaysBetweenDonations = 56;

    public (bool IsEligible, string? Reason) CheckEligibility(Donor donor, DateTime appointmentDate)
    {
        var appointmentDateOnly = DateOnly.FromDateTime(appointmentDate);
        var age = CalculateAge(donor.DateOfBirth, appointmentDateOnly);
        if (age < MinimumAgeYears)
        {
            return (false, $"El donante debe tener al menos {MinimumAgeYears} anios cumplidos.");
        }

        if (donor.LastDonationDate is { } lastDonation)
        {
            var daysSinceLastDonation = appointmentDateOnly.DayNumber - lastDonation.DayNumber;
            if (daysSinceLastDonation < MinimumDaysBetweenDonations)
            {
                var daysRemaining = MinimumDaysBetweenDonations - daysSinceLastDonation;
                return (false, $"Deben pasar {MinimumDaysBetweenDonations} dias entre donaciones. Faltan {daysRemaining} dias.");
            }
        }

        return (true, null);
    }

    private static int CalculateAge(DateOnly birthDate, DateOnly onDate)
    {
        var age = onDate.Year - birthDate.Year;
        if (birthDate > onDate.AddYears(-age))
        {
            age--;
        }
        return age;
    }
}
