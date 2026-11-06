using System.ComponentModel.DataAnnotations;
using BloodLink.Api.Domain;

namespace BloodLink.Api.Dtos;

public record ScheduleAppointmentRequest(
    [Required] int DonorId,
    [Required] DateTime ScheduledAt);

public record AppointmentResponse(
    int Id,
    int DonorId,
    string DonorName,
    DateTime ScheduledAt,
    AppointmentStatus Status,
    int? ResultingBloodUnitId);
