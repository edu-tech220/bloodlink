using BloodLink.Api.Domain;

namespace BloodLink.Api.Services;

/// <summary>
/// Shelf life in days per component, based on standard blood-bank storage guidelines
/// (simplified for demo purposes: no distinction between liquid and frozen plasma, etc).
/// </summary>
public static class ShelfLifePolicy
{
    private static readonly Dictionary<BloodComponent, int> ShelfLifeDays = new()
    {
        [BloodComponent.WholeBlood] = 35,
        [BloodComponent.RedCells] = 42,
        [BloodComponent.Platelets] = 5,
        [BloodComponent.Plasma] = 365,
        [BloodComponent.Cryoprecipitate] = 365,
    };

    public static DateTime ComputeExpiry(BloodComponent component, DateTime collectedAt)
        => collectedAt.AddDays(ShelfLifeDays[component]);
}
