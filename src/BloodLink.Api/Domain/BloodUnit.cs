namespace BloodLink.Api.Domain;

public class BloodUnit
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public BloodType BloodType { get; set; }
    public BloodComponent Component { get; set; }
    public DateTime CollectedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public BloodUnitStatus Status { get; set; } = BloodUnitStatus.Quarantined;
    public int? DonorId { get; set; }
    public Donor? Donor { get; set; }

    /// <summary>
    /// Concurrency token incremented on every state change. Used instead of a
    /// provider-specific rowversion so the same optimistic-concurrency logic
    /// works against both SQL Server and the SQLite in-memory test database.
    /// </summary>
    public int Version { get; set; }
}
