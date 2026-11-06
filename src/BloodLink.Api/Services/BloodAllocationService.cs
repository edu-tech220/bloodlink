using BloodLink.Api.Data;
using BloodLink.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Api.Services;

public enum AllocationOutcome
{
    FullyAllocated,
    PartiallyAllocated,
    NoStockAvailable,
}

public record AllocationResult(AllocationOutcome Outcome, int UnitsAllocated, int UnitsStillNeeded);

public interface IBloodAllocationService
{
    Task<AllocationResult> TryAllocateAsync(int bloodRequestId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reserves compatible blood units for a request using First-Expired-First-Out
/// ordering. Two hospitals racing for the same scarce units will both read the
/// same "Available" rows; optimistic concurrency on BloodUnit.Version makes the
/// loser's SaveChanges fail instead of silently double-booking a unit, and the
/// loser simply re-reads the now-smaller pool and retries.
/// </summary>
public class BloodAllocationService : IBloodAllocationService
{
    private const int MaxAttempts = 5;

    private readonly BloodLinkDbContext _db;
    private readonly IBloodCompatibilityService _compatibility;

    public BloodAllocationService(BloodLinkDbContext db, IBloodCompatibilityService compatibility)
    {
        _db = db;
        _compatibility = compatibility;
    }

    public async Task<AllocationResult> TryAllocateAsync(int bloodRequestId, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await AttemptAllocationAsync(bloodRequestId, cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                foreach (var entry in _db.ChangeTracker.Entries().ToList())
                {
                    entry.State = EntityState.Detached;
                }

                if (attempt == MaxAttempts)
                {
                    throw;
                }
            }
        }

        throw new InvalidOperationException("No se pudo completar la asignacion tras varios intentos.");
    }

    private async Task<AllocationResult> AttemptAllocationAsync(int bloodRequestId, CancellationToken cancellationToken)
    {
        var request = await _db.BloodRequests.FirstAsync(r => r.Id == bloodRequestId, cancellationToken);

        if (request.Status is RequestStatus.Issued or RequestStatus.Cancelled or RequestStatus.Rejected)
        {
            return new AllocationResult(AllocationOutcome.NoStockAvailable, 0, request.QuantityUnits);
        }

        var alreadyAllocated = await _db.RequestAllocations.CountAsync(a => a.BloodRequestId == bloodRequestId, cancellationToken);
        var stillNeeded = request.QuantityUnits - alreadyAllocated;
        if (stillNeeded <= 0)
        {
            request.Status = RequestStatus.Reserved;
            await _db.SaveChangesAsync(cancellationToken);
            return new AllocationResult(AllocationOutcome.FullyAllocated, alreadyAllocated, 0);
        }

        var compatibleTypes = _compatibility.CompatibleDonorTypes(request.BloodType, request.Component);

        var candidates = await _db.BloodUnits
            .Where(u => u.Status == BloodUnitStatus.Available
                        && u.Component == request.Component
                        && compatibleTypes.Contains(u.BloodType)
                        && u.ExpiresAt > DateTime.UtcNow)
            .OrderBy(u => u.ExpiresAt)
            .Take(stillNeeded)
            .ToListAsync(cancellationToken);

        foreach (var unit in candidates)
        {
            unit.Status = BloodUnitStatus.Reserved;
            unit.Version++;
            _db.RequestAllocations.Add(new RequestAllocation
            {
                BloodRequestId = request.Id,
                BloodUnitId = unit.Id,
                AllocatedAt = DateTime.UtcNow,
            });
        }

        var totalAllocated = alreadyAllocated + candidates.Count;
        request.Status = totalAllocated >= request.QuantityUnits
            ? RequestStatus.Reserved
            : totalAllocated > 0
                ? RequestStatus.PartiallyReserved
                : RequestStatus.Pending;

        await _db.SaveChangesAsync(cancellationToken);

        var remaining = request.QuantityUnits - totalAllocated;
        var outcome = remaining <= 0
            ? AllocationOutcome.FullyAllocated
            : totalAllocated > 0
                ? AllocationOutcome.PartiallyAllocated
                : AllocationOutcome.NoStockAvailable;

        return new AllocationResult(outcome, totalAllocated, Math.Max(remaining, 0));
    }
}
