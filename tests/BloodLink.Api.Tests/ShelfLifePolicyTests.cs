using BloodLink.Api.Domain;
using BloodLink.Api.Services;

namespace BloodLink.Api.Tests;

public class ShelfLifePolicyTests
{
    [Theory]
    [InlineData(BloodComponent.WholeBlood, 35)]
    [InlineData(BloodComponent.RedCells, 42)]
    [InlineData(BloodComponent.Platelets, 5)]
    [InlineData(BloodComponent.Plasma, 365)]
    [InlineData(BloodComponent.Cryoprecipitate, 365)]
    public void ComputeExpiry_AddsCorrectShelfLife(BloodComponent component, int expectedDays)
    {
        var collectedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expiresAt = ShelfLifePolicy.ComputeExpiry(component, collectedAt);
        Assert.Equal(collectedAt.AddDays(expectedDays), expiresAt);
    }
}
