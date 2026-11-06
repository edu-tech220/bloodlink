using System.ComponentModel.DataAnnotations;
using BloodLink.Api.Domain;

namespace BloodLink.Api.Dtos;

public record CreateBloodRequestRequest(
    [Required] int HospitalId,
    [Required] BloodType BloodType,
    [Required] BloodComponent Component,
    [Range(1, 50)] int QuantityUnits,
    RequestPriority Priority,
    string? Notes);

public record BloodRequestResponse(
    int Id,
    int HospitalId,
    string HospitalName,
    BloodType BloodType,
    BloodComponent Component,
    int QuantityUnits,
    int UnitsAllocated,
    RequestPriority Priority,
    RequestStatus Status,
    DateTime CreatedAt,
    string? Notes);
