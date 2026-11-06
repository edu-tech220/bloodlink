using System.ComponentModel.DataAnnotations;
using BloodLink.Api.Domain;

namespace BloodLink.Api.Dtos;

public record LoginRequest(
    [Required] string Username,
    [Required] string Password);

public record LoginResponse(string Token, string Username, UserRole Role, int? HospitalId, DateTime ExpiresAt);

public record RegisterUserRequest(
    [Required] string Username,
    [Required, MinLength(8)] string Password,
    [Required] UserRole Role,
    int? HospitalId);
