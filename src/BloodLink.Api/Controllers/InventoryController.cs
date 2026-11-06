using BloodLink.Api.Data;
using BloodLink.Api.Domain;
using BloodLink.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private const int LowStockThreshold = 5;

    private readonly BloodLinkDbContext _db;

    public InventoryController(BloodLinkDbContext db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<IReadOnlyList<InventorySummaryItem>>> GetSummary()
    {
        var counts = await _db.BloodUnits
            .Where(u => u.Status == BloodUnitStatus.Available)
            .GroupBy(u => new { u.BloodType, u.Component })
            .Select(g => new { g.Key.BloodType, g.Key.Component, Count = g.Count() })
            .ToListAsync();

        var summary = new List<InventorySummaryItem>();
        foreach (var bloodType in Enum.GetValues<BloodType>())
        {
            foreach (var component in Enum.GetValues<BloodComponent>())
            {
                var count = counts.FirstOrDefault(c => c.BloodType == bloodType && c.Component == component)?.Count ?? 0;
                summary.Add(new InventorySummaryItem(bloodType, component, count, count < LowStockThreshold));
            }
        }

        return Ok(summary);
    }

    [HttpGet("units")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<PagedResult<BloodUnitResponse>>> GetUnits(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] BloodUnitStatus? status = null,
        [FromQuery] BloodType? bloodType = null)
    {
        var query = new PageQuery(page, pageSize);
        var baseQuery = _db.BloodUnits.AsQueryable();
        if (status is not null) baseQuery = baseQuery.Where(u => u.Status == status);
        if (bloodType is not null) baseQuery = baseQuery.Where(u => u.BloodType == bloodType);

        var totalCount = await baseQuery.CountAsync();
        var units = await baseQuery
            .OrderBy(u => u.ExpiresAt)
            .Skip((query.NormalizedPage - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .Select(u => new BloodUnitResponse(u.Id, u.Code, u.BloodType, u.Component, u.CollectedAt, u.ExpiresAt, u.Status))
            .ToListAsync();

        return Ok(new PagedResult<BloodUnitResponse>(units, query.NormalizedPage, query.NormalizedPageSize, totalCount));
    }

    [HttpPost("units/{id:int}/release")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult> ReleaseFromQuarantine(int id)
    {
        var unit = await _db.BloodUnits.FindAsync(id);
        if (unit is null) return NotFound();
        if (unit.Status != BloodUnitStatus.Quarantined)
        {
            return BadRequest(new ProblemDetails { Title = $"La unidad esta en estado {unit.Status}, no en cuarentena." });
        }

        unit.Status = BloodUnitStatus.Available;
        unit.Version++;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("units/{id:int}/discard")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult> Discard(int id)
    {
        var unit = await _db.BloodUnits.FindAsync(id);
        if (unit is null) return NotFound();
        if (unit.Status is BloodUnitStatus.Issued or BloodUnitStatus.Discarded)
        {
            return BadRequest(new ProblemDetails { Title = $"La unidad esta en estado {unit.Status} y no puede descartarse." });
        }

        unit.Status = BloodUnitStatus.Discarded;
        unit.Version++;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("sweep-expired")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<int>> SweepExpired()
    {
        var expired = await _db.BloodUnits
            .Where(u => (u.Status == BloodUnitStatus.Available || u.Status == BloodUnitStatus.Quarantined)
                        && u.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync();

        foreach (var unit in expired)
        {
            unit.Status = BloodUnitStatus.Expired;
            unit.Version++;
        }

        await _db.SaveChangesAsync();
        return Ok(expired.Count);
    }
}
