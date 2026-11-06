namespace BloodLink.Web.Models;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Username, UserRole Role, int? HospitalId, DateTime ExpiresAt);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public record DonorResponse(int Id, string FullName, BloodType BloodType, DateOnly DateOfBirth, string Phone, DateOnly? LastDonationDate);
public record CreateDonorRequest(string FullName, BloodType BloodType, DateOnly DateOfBirth, string Phone);
public record DonorEligibilityResponse(bool IsEligible, string? Reason);

public record AppointmentResponse(int Id, int DonorId, string DonorName, DateTime ScheduledAt, AppointmentStatus Status, int? ResultingBloodUnitId);
public record ScheduleAppointmentRequest(int DonorId, DateTime ScheduledAt);

public record BloodUnitResponse(int Id, string Code, BloodType BloodType, BloodComponent Component, DateTime CollectedAt, DateTime ExpiresAt, BloodUnitStatus Status);
public record InventorySummaryItem(BloodType BloodType, BloodComponent Component, int AvailableUnits, bool LowStock);

public record HospitalResponse(int Id, string Name, string ContactEmail, string Address);
public record CreateHospitalRequest(string Name, string ContactEmail, string Address);

public record CreateBloodRequestRequest(int HospitalId, BloodType BloodType, BloodComponent Component, int QuantityUnits, RequestPriority Priority, string? Notes);
public record BloodRequestResponse(
    int Id, int HospitalId, string HospitalName, BloodType BloodType, BloodComponent Component,
    int QuantityUnits, int UnitsAllocated, RequestPriority Priority, RequestStatus Status, DateTime CreatedAt, string? Notes);

public record ApiProblem(string? Title, string? Detail);
