using BloodLink.Api.Domain;
using BloodLink.Api.Services;

namespace BloodLink.Api.Tests;

public class DonorEligibilityServiceTests
{
    private readonly DonorEligibilityService _service = new();

    [Fact]
    public void CheckEligibility_UnderageDonor_IsRejected()
    {
        var donor = new Donor { DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddYears(-17)) };
        var (isEligible, reason) = _service.CheckEligibility(donor, DateTime.Today);

        Assert.False(isEligible);
        Assert.Contains("18", reason);
    }

    [Fact]
    public void CheckEligibility_FirstTimeAdultDonor_IsEligible()
    {
        var donor = new Donor { DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddYears(-30)) };
        var (isEligible, reason) = _service.CheckEligibility(donor, DateTime.Today);

        Assert.True(isEligible);
        Assert.Null(reason);
    }

    [Fact]
    public void CheckEligibility_DonatedTooRecently_IsRejected()
    {
        var donor = new Donor
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddYears(-30)),
            LastDonationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-10)),
        };

        var (isEligible, reason) = _service.CheckEligibility(donor, DateTime.Today);

        Assert.False(isEligible);
        Assert.Contains("56", reason);
    }

    [Fact]
    public void CheckEligibility_ExactlyAtMinimumInterval_IsEligible()
    {
        var donor = new Donor
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddYears(-30)),
            LastDonationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-DonorEligibilityService.MinimumDaysBetweenDonations)),
        };

        var (isEligible, _) = _service.CheckEligibility(donor, DateTime.Today);

        Assert.True(isEligible);
    }
}
