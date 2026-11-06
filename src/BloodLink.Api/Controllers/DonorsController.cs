using BloodLink.Api.Data;
using BloodLink.Api.Domain;
using BloodLink.Api.Dtos;
using BloodLink.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Api.Controllers;

[ApiController]
[Route("api/donors")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class DonorsController : ControllerBase
{
    private readonly BloodLinkDbContext _db;
    private readonly IDonorEligibilityService _eligibilityService;

    public DonorsController(BloodLinkDbContext db, IDonorEligibilityService eligibilityService)
    {
        _db = db;
        _eligibilityService = eligibilityService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<DonorResponse>>> GetDonors([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = new PageQuery(page, pageSize);
        var totalCount = await _db.Donors.CountAsync();
        var donors = await _db.Donors
            .OrderBy(d => d.FullName)
            .Skip((query.NormalizedPage - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .Select(d => new DonorResponse(d.Id, d.FullName, d.BloodType, d.DateOfBirth, d.Phone, d.LastDonationDate))
            .ToListAsync();

        return Ok(new PagedResult<DonorResponse>(donors, query.NormalizedPage, query.NormalizedPageSize, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DonorResponse>> GetDonor(int id)
    {
        var donor = await _db.Donors.FindAsync(id);
        if (donor is null) return NotFound();
        return Ok(new DonorResponse(donor.Id, donor.FullName, donor.BloodType, donor.DateOfBirth, donor.Phone, donor.LastDonationDate));
    }

    [HttpPost]
    public async Task<ActionResult<DonorResponse>> CreateDonor(CreateDonorRequest request)
    {
        var donor = new Donor
        {
            FullName = request.FullName,
            BloodType = request.BloodType,
            DateOfBirth = request.DateOfBirth,
            Phone = request.Phone,
        };
        _db.Donors.Add(donor);
        await _db.SaveChangesAsync();

        var response = new DonorResponse(donor.Id, donor.FullName, donor.BloodType, donor.DateOfBirth, donor.Phone, donor.LastDonationDate);
        return CreatedAtAction(nameof(GetDonor), new { id = donor.Id }, response);
    }

    [HttpGet("{id:int}/eligibility")]
    public async Task<ActionResult<DonorEligibilityResponse>> CheckEligibility(int id, [FromQuery] DateTime? on = null)
    {
        var donor = await _db.Donors.FindAsync(id);
        if (donor is null) return NotFound();

        var (isEligible, reason) = _eligibilityService.CheckEligibility(donor, on ?? DateTime.UtcNow);
        return Ok(new DonorEligibilityResponse(isEligible, reason));
    }
}
