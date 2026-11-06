using BloodLink.Api.Data;
using BloodLink.Api.Domain;
using BloodLink.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Api.Controllers;

[ApiController]
[Route("api/hospitals")]
[Authorize]
public class HospitalsController : ControllerBase
{
    private readonly BloodLinkDbContext _db;

    public HospitalsController(BloodLinkDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HospitalResponse>>> GetHospitals()
    {
        var hospitals = await _db.Hospitals
            .OrderBy(h => h.Name)
            .Select(h => new HospitalResponse(h.Id, h.Name, h.ContactEmail, h.Address))
            .ToListAsync();
        return Ok(hospitals);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<HospitalResponse>> CreateHospital(CreateHospitalRequest request)
    {
        var hospital = new Hospital
        {
            Name = request.Name,
            ContactEmail = request.ContactEmail,
            Address = request.Address,
        };
        _db.Hospitals.Add(hospital);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetHospitals), new { },
            new HospitalResponse(hospital.Id, hospital.Name, hospital.ContactEmail, hospital.Address));
    }
}
