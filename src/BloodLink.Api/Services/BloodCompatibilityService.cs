using BloodLink.Api.Domain;

namespace BloodLink.Api.Services;

/// <summary>
/// Models ABO/Rh compatibility from first principles (antigen presence) instead of a
/// hard-coded lookup table, so the rule is auditable and easy to extend.
/// </summary>
public interface IBloodCompatibilityService
{
    bool CanFulfill(BloodType donorType, BloodType recipientType, BloodComponent component);
    IReadOnlyList<BloodType> CompatibleDonorTypes(BloodType recipientType, BloodComponent component);
}

public class BloodCompatibilityService : IBloodCompatibilityService
{
    private readonly record struct Antigens(bool HasA, bool HasB, bool RhPositive);

    private static readonly Dictionary<BloodType, Antigens> AntigenMap = new()
    {
        [BloodType.ONegative] = new Antigens(false, false, false),
        [BloodType.OPositive] = new Antigens(false, false, true),
        [BloodType.ANegative] = new Antigens(true, false, false),
        [BloodType.APositive] = new Antigens(true, false, true),
        [BloodType.BNegative] = new Antigens(false, true, false),
        [BloodType.BPositive] = new Antigens(false, true, true),
        [BloodType.ABNegative] = new Antigens(true, true, false),
        [BloodType.ABPositive] = new Antigens(true, true, true),
    };

    public bool CanFulfill(BloodType donorType, BloodType recipientType, BloodComponent component)
    {
        var donor = AntigenMap[donorType];
        var recipient = AntigenMap[recipientType];

        // Plasma carries antibodies against the antigens the donor LACKS, so the
        // compatibility direction is the mirror image of cellular components.
        if (component == BloodComponent.Plasma)
        {
            if (!donor.HasA && recipient.HasA) return false;
            if (!donor.HasB && recipient.HasB) return false;
            return true;
        }

        // Cellular components (whole blood, red cells, platelets, cryoprecipitate):
        // the recipient must already carry every antigen present in the donated unit,
        // and an Rh-positive unit can only go to an Rh-positive recipient.
        if (donor.HasA && !recipient.HasA) return false;
        if (donor.HasB && !recipient.HasB) return false;
        if (donor.RhPositive && !recipient.RhPositive) return false;
        return true;
    }

    public IReadOnlyList<BloodType> CompatibleDonorTypes(BloodType recipientType, BloodComponent component)
    {
        return Enum.GetValues<BloodType>()
            .Where(donorType => CanFulfill(donorType, recipientType, component))
            .ToList();
    }
}
