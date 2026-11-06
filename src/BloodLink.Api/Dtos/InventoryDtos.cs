using BloodLink.Api.Domain;

namespace BloodLink.Api.Dtos;

public record BloodUnitResponse(
    int Id,
    string Code,
    BloodType BloodType,
    BloodComponent Component,
    DateTime CollectedAt,
    DateTime ExpiresAt,
    BloodUnitStatus Status);

public record InventorySummaryItem(
    BloodType BloodType,
    BloodComponent Component,
    int AvailableUnits,
    bool LowStock);
