using BloodLink.Api.Data;
using BloodLink.Api.Domain;
using BloodLink.Api.Dtos;
using BloodLink.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Api.Controllers;

[ApiController]
[Route("api/requests")]
[Authorize]
public class RequestsController : ControllerBase
{
    private readonly BloodLinkDbContext _db;
    private readonly IBloodAllocationService _allocationService;

    public RequestsController(BloodLinkDbContext db, IBloodAllocationService allocationService)
    {
        _db = db;
        _allocationService = allocationService;
    }

    private bool IsAdmin => User.IsInRole(nameof(UserRole.Admin));

    private int? CurrentHospitalId =>
        int.TryParse(User.FindFirst("hospitalId")?.Value, out var id) ? id : null;

    [HttpGet]
    public async Task<ActionResult<PagedResult<BloodRequestResponse>>> GetRequests(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] RequestStatus? status = null)
    {
        var query = new PageQuery(page, pageSize);
        var baseQuery = _db.BloodRequests.Include(r => r.Hospital).AsQueryable();

        if (!IsAdmin)
        {
            var hospitalId = CurrentHospitalId;
            baseQuery = baseQuery.Where(r => r.HospitalId == hospitalId);
        }

        if (status is not null)
        {
            baseQuery = baseQuery.Where(r => r.Status == status);
        }

        var totalCount = await baseQuery.CountAsync();
        var requests = await baseQuery
            .OrderByDescending(r => r.CreatedAt)
            .Skip((query.NormalizedPage - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .Select(r => new BloodRequestResponse(
                r.Id, r.HospitalId, r.Hospital!.Name, r.BloodType, r.Component, r.QuantityUnits,
                r.Allocations.Count, r.Priority, r.Status, r.CreatedAt, r.Notes))
            .ToListAsync();

        return Ok(new PagedResult<BloodRequestResponse>(requests, query.NormalizedPage, query.NormalizedPageSize, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BloodRequestResponse>> GetRequest(int id)
    {
        var request = await _db.BloodRequests.Include(r => r.Hospital).Include(r => r.Allocations)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request is null) return NotFound();
        if (!IsAdmin && request.HospitalId != CurrentHospitalId) return Forbid();

        return Ok(ToResponse(request));
    }

    [HttpPost]
    public async Task<ActionResult<BloodRequestResponse>> CreateRequest(CreateBloodRequestRequest request)
    {
        if (!IsAdmin && request.HospitalId != CurrentHospitalId)
        {
            return Forbid();
        }

        var hospital = await _db.Hospitals.FindAsync(request.HospitalId);
        if (hospital is null) return NotFound(new ProblemDetails { Title = "Hospital no encontrado." });

        var bloodRequest = new BloodRequest
        {
            HospitalId = hospital.Id,
            BloodType = request.BloodType,
            Component = request.Component,
            QuantityUnits = request.QuantityUnits,
            Priority = request.Priority,
            Status = RequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Notes = request.Notes,
        };
        _db.BloodRequests.Add(bloodRequest);
        await _db.SaveChangesAsync();

        await _allocationService.TryAllocateAsync(bloodRequest.Id);
        await _db.Entry(bloodRequest).ReloadAsync();
        await _db.Entry(bloodRequest).Collection(r => r.Allocations).LoadAsync();
        await _db.Entry(bloodRequest).Reference(r => r.Hospital).LoadAsync();

        return CreatedAtAction(nameof(GetRequest), new { id = bloodRequest.Id }, ToResponse(bloodRequest));
    }

    [HttpPost("{id:int}/allocate")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<BloodRequestResponse>> Allocate(int id)
    {
        var exists = await _db.BloodRequests.AnyAsync(r => r.Id == id);
        if (!exists) return NotFound();

        await _allocationService.TryAllocateAsync(id);

        var request = await _db.BloodRequests.Include(r => r.Hospital).Include(r => r.Allocations)
            .FirstAsync(r => r.Id == id);
        return Ok(ToResponse(request));
    }

    [HttpPost("{id:int}/issue")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<BloodRequestResponse>> Issue(int id)
    {
        var request = await _db.BloodRequests
            .Include(r => r.Hospital)
            .Include(r => r.Allocations).ThenInclude(a => a.BloodUnit)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request is null) return NotFound();
        if (request.Status != RequestStatus.Reserved)
        {
            return BadRequest(new ProblemDetails { Title = $"La solicitud debe estar Reserved, esta en {request.Status}." });
        }

        foreach (var allocation in request.Allocations)
        {
            allocation.BloodUnit!.Status = BloodUnitStatus.Issued;
            allocation.BloodUnit.Version++;
        }
        request.Status = RequestStatus.Issued;
        await _db.SaveChangesAsync();

        return Ok(ToResponse(request));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<BloodRequestResponse>> Cancel(int id)
    {
        var request = await _db.BloodRequests
            .Include(r => r.Hospital)
            .Include(r => r.Allocations).ThenInclude(a => a.BloodUnit)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request is null) return NotFound();
        if (!IsAdmin && request.HospitalId != CurrentHospitalId) return Forbid();
        if (request.Status is RequestStatus.Issued or RequestStatus.Cancelled or RequestStatus.Rejected)
        {
            return BadRequest(new ProblemDetails { Title = $"La solicitud ya esta en estado {request.Status}." });
        }

        foreach (var allocation in request.Allocations)
        {
            allocation.BloodUnit!.Status = BloodUnitStatus.Available;
            allocation.BloodUnit.Version++;
        }
        _db.RequestAllocations.RemoveRange(request.Allocations);
        request.Status = RequestStatus.Cancelled;
        await _db.SaveChangesAsync();

        return Ok(ToResponse(request));
    }

    private static BloodRequestResponse ToResponse(BloodRequest r) => new(
        r.Id, r.HospitalId, r.Hospital?.Name ?? string.Empty, r.BloodType, r.Component, r.QuantityUnits,
        r.Allocations.Count, r.Priority, r.Status, r.CreatedAt, r.Notes);
}
