using BloodLink.Api.Data;
using BloodLink.Api.Domain;
using BloodLink.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Api.Tests;

/// <summary>
/// Exercises the optimistic-concurrency path of <see cref="BloodAllocationService"/> with two
/// real, independent DbContext/connection pairs racing for the same scarce unit (a SQLite
/// shared-cache in-memory database, so no external SQL Server is required). Whichever
/// interleaving actually happens, exactly one request must win the unit - never both.
/// </summary>
public class BloodAllocationConcurrencyTests
{
    private static DbContextOptions<BloodLinkDbContext> CreateOptions(string connectionString) =>
        new DbContextOptionsBuilder<BloodLinkDbContext>().UseSqlite(connectionString).Options;

    [Fact]
    public async Task TwoRequestsRacingForOneUnit_OnlyOneGetsReserved()
    {
        var connectionString = $"Data Source=file:concurrency_{Guid.NewGuid():N}?mode=memory&cache=shared";

        // A keep-alive connection: SQLite drops a shared in-memory database once every
        // connection to it closes, so this one stays open for the test's lifetime.
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();

        int unitId, requestAId, requestBId;
        await using (var setup = new BloodLinkDbContext(CreateOptions(connectionString)))
        {
            await setup.Database.EnsureCreatedAsync();

            var hospital = new Hospital { Name = "Hospital de Prueba", ContactEmail = "a@b.com", Address = "N/A" };
            setup.Hospitals.Add(hospital);
            await setup.SaveChangesAsync();

            var unit = new BloodUnit
            {
                Code = "RACE-UNIT",
                BloodType = BloodType.ONegative,
                Component = BloodComponent.RedCells,
                CollectedAt = DateTime.UtcNow.AddDays(-1),
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                Status = BloodUnitStatus.Available,
            };
            setup.BloodUnits.Add(unit);

            var requestA = new BloodRequest
            {
                HospitalId = hospital.Id,
                BloodType = BloodType.ONegative,
                Component = BloodComponent.RedCells,
                QuantityUnits = 1,
                Status = RequestStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            };
            var requestB = new BloodRequest
            {
                HospitalId = hospital.Id,
                BloodType = BloodType.ONegative,
                Component = BloodComponent.RedCells,
                QuantityUnits = 1,
                Status = RequestStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            };
            setup.BloodRequests.AddRange(requestA, requestB);
            await setup.SaveChangesAsync();

            unitId = unit.Id;
            requestAId = requestA.Id;
            requestBId = requestB.Id;
        }

        await using var dbA = new BloodLinkDbContext(CreateOptions(connectionString));
        await using var dbB = new BloodLinkDbContext(CreateOptions(connectionString));
        var serviceA = new BloodAllocationService(dbA, new BloodCompatibilityService());
        var serviceB = new BloodAllocationService(dbB, new BloodCompatibilityService());

        var results = await Task.WhenAll(
            serviceA.TryAllocateAsync(requestAId),
            serviceB.TryAllocateAsync(requestBId));

        var totalAllocated = results.Sum(r => r.UnitsAllocated);
        Assert.Equal(1, totalAllocated);

        await using var verify = new BloodLinkDbContext(CreateOptions(connectionString));
        var finalUnit = await verify.BloodUnits.SingleAsync(u => u.Id == unitId);
        Assert.Equal(BloodUnitStatus.Reserved, finalUnit.Status);

        var allocationCount = await verify.RequestAllocations.CountAsync(a => a.BloodUnitId == unitId);
        Assert.Equal(1, allocationCount);

        var statuses = await verify.BloodRequests
            .Where(r => r.Id == requestAId || r.Id == requestBId)
            .Select(r => r.Status)
            .ToListAsync();
        Assert.Contains(RequestStatus.Reserved, statuses);
        Assert.Contains(RequestStatus.Pending, statuses);
    }
}
