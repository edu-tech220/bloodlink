using BloodLink.Api.Domain;
using BloodLink.Api.Services;

namespace BloodLink.Api.Tests;

public class BloodCompatibilityServiceTests
{
    private readonly BloodCompatibilityService _service = new();

    [Theory]
    [InlineData(BloodType.ONegative, BloodType.OPositive, true)]
    [InlineData(BloodType.ONegative, BloodType.ABPositive, true)]
    [InlineData(BloodType.OPositive, BloodType.ONegative, false)]
    [InlineData(BloodType.APositive, BloodType.APositive, true)]
    [InlineData(BloodType.APositive, BloodType.ANegative, false)]
    [InlineData(BloodType.ABPositive, BloodType.ABPositive, true)]
    [InlineData(BloodType.ABPositive, BloodType.APositive, false)]
    [InlineData(BloodType.BNegative, BloodType.ABPositive, true)]
    public void CanFulfill_RedCells_FollowsAboRhRules(BloodType donor, BloodType recipient, bool expected)
    {
        Assert.Equal(expected, _service.CanFulfill(donor, recipient, BloodComponent.RedCells));
    }

    [Theory]
    [InlineData(BloodType.ABPositive, BloodType.OPositive, true)]
    [InlineData(BloodType.ABNegative, BloodType.OPositive, true)]
    [InlineData(BloodType.ONegative, BloodType.ABPositive, false)]
    [InlineData(BloodType.ONegative, BloodType.OPositive, true)]
    [InlineData(BloodType.APositive, BloodType.BPositive, false)]
    [InlineData(BloodType.APositive, BloodType.APositive, true)]
    public void CanFulfill_Plasma_IsMirroredFromRedCells(BloodType donor, BloodType recipient, bool expected)
    {
        Assert.Equal(expected, _service.CanFulfill(donor, recipient, BloodComponent.Plasma));
    }

    [Fact]
    public void CompatibleDonorTypes_ForAbPositiveRecipient_IncludesEveryType()
    {
        var donors = _service.CompatibleDonorTypes(BloodType.ABPositive, BloodComponent.RedCells);
        Assert.Equal(Enum.GetValues<BloodType>().Length, donors.Count);
    }

    [Fact]
    public void CompatibleDonorTypes_ForOPositiveRecipient_AcceptsPlasmaFromEveryType()
    {
        // O recipients carry no A/B antigens, so no donor plasma's antibodies can attack them:
        // O is the "universal plasma recipient", the mirror image of O being the universal RBC donor.
        var donors = _service.CompatibleDonorTypes(BloodType.OPositive, BloodComponent.Plasma);
        Assert.Equal(Enum.GetValues<BloodType>().Length, donors.Count);
    }

    [Fact]
    public void CompatibleDonorTypes_ForAbPositiveRecipient_PlasmaOnlyAllowsAbDonors()
    {
        // AB recipients carry both antigens, so only antibody-free AB plasma is safe for them.
        var donors = _service.CompatibleDonorTypes(BloodType.ABPositive, BloodComponent.Plasma);
        Assert.Equal(2, donors.Count);
        Assert.Contains(BloodType.ABPositive, donors);
        Assert.Contains(BloodType.ABNegative, donors);
    }
}
