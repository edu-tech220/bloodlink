namespace BloodLink.Api.Domain;

public class BloodRequest
{
    public int Id { get; set; }
    public int HospitalId { get; set; }
    public Hospital? Hospital { get; set; }
    public BloodType BloodType { get; set; }
    public BloodComponent Component { get; set; }
    public int QuantityUnits { get; set; }
    public RequestPriority Priority { get; set; } = RequestPriority.Routine;
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public string? Notes { get; set; }

    public List<RequestAllocation> Allocations { get; set; } = new();
}
