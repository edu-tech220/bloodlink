using BloodLink.Api.Data;
using BloodLink.Api.Domain;
using BloodLink.Api.Dtos;
using BloodLink.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Api.Controllers;

[ApiController]
[Route("api/appointments")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AppointmentsController : ControllerBase
{
    private readonly BloodLinkDbContext _db;
    private readonly IDonorEligibilityService _eligibilityService;

    public AppointmentsController(BloodLinkDbContext db, IDonorEligibilityService eligibilityService)
    {
        _db = db;
        _eligibilityService = eligibilityService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AppointmentResponse>>> GetAppointments(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] AppointmentStatus? status = null)
    {
        var query = new PageQuery(page, pageSize);
        var baseQuery = _db.DonationAppointments.Include(a => a.Donor).AsQueryable();
        if (status is not null)
        {
            baseQuery = baseQuery.Where(a => a.Status == status);
        }

        var totalCount = await baseQuery.CountAsync();
        var appointments = await baseQuery
            .OrderByDescending(a => a.ScheduledAt)
            .Skip((query.NormalizedPage - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .Select(a => new AppointmentResponse(a.Id, a.DonorId, a.Donor!.FullName, a.ScheduledAt, a.Status, a.ResultingBloodUnitId))
            .ToListAsync();

        return Ok(new PagedResult<AppointmentResponse>(appointments, query.NormalizedPage, query.NormalizedPageSize, totalCount));
    }

    [HttpPost]
    public async Task<ActionResult<AppointmentResponse>> ScheduleAppointment(ScheduleAppointmentRequest request)
    {
        var donor = await _db.Donors.FindAsync(request.DonorId);
        if (donor is null) return NotFound(new ProblemDetails { Title = "Donante no encontrado." });

        var (isEligible, reason) = _eligibilityService.CheckEligibility(donor, request.ScheduledAt);
        if (!isEligible)
        {
            return BadRequest(new ProblemDetails { Title = "Donante no elegible.", Detail = reason });
        }

        var hasPendingAppointment = await _db.DonationAppointments
            .AnyAsync(a => a.DonorId == donor.Id && a.Status == AppointmentStatus.Scheduled);
        if (hasPendingAppointment)
        {
            return Conflict(new ProblemDetails { Title = "El donante ya tiene una cita programada." });
        }

        var appointment = new DonationAppointment
        {
            DonorId = donor.Id,
            ScheduledAt = request.ScheduledAt,
            Status = AppointmentStatus.Scheduled,
        };
        _db.DonationAppointments.Add(appointment);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAppointments), new { },
            new AppointmentResponse(appointment.Id, donor.Id, donor.FullName, appointment.ScheduledAt, appointment.Status, null));
    }

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<BloodUnitResponse>> CompleteAppointment(int id)
    {
        var appointment = await _db.DonationAppointments.Include(a => a.Donor).FirstOrDefaultAsync(a => a.Id == id);
        if (appointment is null) return NotFound();
        if (appointment.Status != AppointmentStatus.Scheduled)
        {
            return BadRequest(new ProblemDetails { Title = $"La cita ya esta en estado {appointment.Status}." });
        }

        var collectedAt = DateTime.UtcNow;
        var unit = new BloodUnit
        {
            Code = $"BU-{collectedAt:yyyyMMddHHmmss}-{appointment.DonorId}",
            BloodType = appointment.Donor!.BloodType,
            Component = BloodComponent.WholeBlood,
            CollectedAt = collectedAt,
            ExpiresAt = ShelfLifePolicy.ComputeExpiry(BloodComponent.WholeBlood, collectedAt),
            Status = BloodUnitStatus.Quarantined,
            DonorId = appointment.DonorId,
        };
        _db.BloodUnits.Add(unit);

        appointment.Status = AppointmentStatus.Completed;
        appointment.ResultingBloodUnit = unit;
        appointment.Donor.LastDonationDate = DateOnly.FromDateTime(collectedAt);

        await _db.SaveChangesAsync();

        return Ok(new BloodUnitResponse(unit.Id, unit.Code, unit.BloodType, unit.Component, unit.CollectedAt, unit.ExpiresAt, unit.Status));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult> CancelAppointment(int id)
    {
        var appointment = await _db.DonationAppointments.FindAsync(id);
        if (appointment is null) return NotFound();
        if (appointment.Status != AppointmentStatus.Scheduled)
        {
            return BadRequest(new ProblemDetails { Title = $"La cita ya esta en estado {appointment.Status}." });
        }

        appointment.Status = AppointmentStatus.Cancelled;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
