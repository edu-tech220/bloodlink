namespace BloodLink.Api.Domain;

/// <summary>
/// Links a BloodRequest to the specific BloodUnit that was reserved for it,
/// so a request's history survives even after its units are issued.
/// </summary>
public class RequestAllocation
{
    public int Id { get; set; }
    public int BloodRequestId { get; set; }
    public BloodRequest? BloodRequest { get; set; }
    public int BloodUnitId { get; set; }
    public BloodUnit? BloodUnit { get; set; }
    public DateTime AllocatedAt { get; set; }
}
