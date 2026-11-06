using System.ComponentModel.DataAnnotations;

namespace BloodLink.Api.Dtos;

public record CreateHospitalRequest(
    [Required] string Name,
    [Required, EmailAddress] string ContactEmail,
    [Required] string Address);

public record HospitalResponse(int Id, string Name, string ContactEmail, string Address);
