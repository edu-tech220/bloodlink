using System.ComponentModel.DataAnnotations;
using BloodLink.Api.Domain;

namespace BloodLink.Api.Dtos;

public record CreateDonorRequest(
    [Required] string FullName,
    [Required] BloodType BloodType,
    [Required] DateOnly DateOfBirth,
    [Required] string Phone);

public record DonorResponse(
    int Id,
    string FullName,
    BloodType BloodType,
    DateOnly DateOfBirth,
    string Phone,
    DateOnly? LastDonationDate);

public record DonorEligibilityResponse(bool IsEligible, string? Reason);
