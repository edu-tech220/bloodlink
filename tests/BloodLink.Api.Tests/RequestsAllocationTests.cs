using System.Net;
using System.Net.Http.Json;
using BloodLink.Api.Data;
using BloodLink.Api.Domain;
using BloodLink.Api.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace BloodLink.Api.Tests;

public class RequestsAllocationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RequestsAllocationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<int> SeedUnitAsync(BloodType bloodType, BloodComponent component, int daysUntilExpiry, BloodUnitStatus status = BloodUnitStatus.Available)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BloodLinkDbContext>();

        var unit = new BloodUnit
        {
            Code = $"TEST-{Guid.NewGuid():N}",
            BloodType = bloodType,
            Component = component,
            CollectedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(daysUntilExpiry),
            Status = status,
        };
        db.BloodUnits.Add(unit);
        await db.SaveChangesAsync();
        return unit.Id;
    }

    private async Task<BloodUnit> GetUnitAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BloodLinkDbContext>();
        return (await db.BloodUnits.FindAsync(id))!;
    }

    private async Task<int> GetSeededHospitalIdAsync(HttpClient adminClient)
    {
        var hospitals = await adminClient.GetFromJsonAsync<List<HospitalResponse>>("/api/hospitals");
        return hospitals!.First().Id;
    }

    [Fact]
    public async Task CreateRequest_AllocatesSoonestToExpireUnitsFirst()
    {
        var admin = await _factory.CreateClient().AsAdminAsync();
        var hospitalId = await GetSeededHospitalIdAsync(admin);

        var farUnit = await SeedUnitAsync(BloodType.OPositive, BloodComponent.WholeBlood, daysUntilExpiry: 20);
        var nearUnit = await SeedUnitAsync(BloodType.OPositive, BloodComponent.WholeBlood, daysUntilExpiry: 5);
        var midUnit = await SeedUnitAsync(BloodType.OPositive, BloodComponent.WholeBlood, daysUntilExpiry: 10);

        var response = await admin.PostAsJsonAsync("/api/requests",
            new CreateBloodRequestRequest(hospitalId, BloodType.OPositive, BloodComponent.WholeBlood, 2, RequestPriority.Routine, null));
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<BloodRequestResponse>();

        Assert.Equal(RequestStatus.Reserved, created!.Status);
        Assert.Equal(2, created.UnitsAllocated);

        Assert.Equal(BloodUnitStatus.Reserved, (await GetUnitAsync(nearUnit)).Status);
        Assert.Equal(BloodUnitStatus.Reserved, (await GetUnitAsync(midUnit)).Status);
        Assert.Equal(BloodUnitStatus.Available, (await GetUnitAsync(farUnit)).Status);
    }

    [Fact]
    public async Task CreateRequest_WithInsufficientStock_IsPartiallyReservedUntilReallocated()
    {
        var admin = await _factory.CreateClient().AsAdminAsync();
        var hospitalId = await GetSeededHospitalIdAsync(admin);

        await SeedUnitAsync(BloodType.BNegative, BloodComponent.Plasma, daysUntilExpiry: 30);

        var createResponse = await admin.PostAsJsonAsync("/api/requests",
            new CreateBloodRequestRequest(hospitalId, BloodType.BNegative, BloodComponent.Plasma, 3, RequestPriority.Urgent, null));
        var created = await createResponse.Content.ReadFromJsonAsync<BloodRequestResponse>();

        Assert.Equal(RequestStatus.PartiallyReserved, created!.Status);
        Assert.Equal(1, created.UnitsAllocated);

        await SeedUnitAsync(BloodType.BNegative, BloodComponent.Plasma, daysUntilExpiry: 30);
        await SeedUnitAsync(BloodType.BNegative, BloodComponent.Plasma, daysUntilExpiry: 30);

        var allocateResponse = await admin.PostAsync($"/api/requests/{created.Id}/allocate", null);
        allocateResponse.EnsureSuccessStatusCode();
        var reallocated = await allocateResponse.Content.ReadFromJsonAsync<BloodRequestResponse>();

        Assert.Equal(RequestStatus.Reserved, reallocated!.Status);
        Assert.Equal(3, reallocated.UnitsAllocated);
    }

    [Fact]
    public async Task CreateRequest_WithIncompatibleStockOnly_StaysPending()
    {
        var admin = await _factory.CreateClient().AsAdminAsync();
        var hospitalId = await GetSeededHospitalIdAsync(admin);

        // O+ cannot be transfused into an AB- recipient (Rh mismatch), so this unit must be ignored.
        await SeedUnitAsync(BloodType.OPositive, BloodComponent.RedCells, daysUntilExpiry: 30);

        var response = await admin.PostAsJsonAsync("/api/requests",
            new CreateBloodRequestRequest(hospitalId, BloodType.ABNegative, BloodComponent.RedCells, 1, RequestPriority.Emergency, null));
        var created = await response.Content.ReadFromJsonAsync<BloodRequestResponse>();

        Assert.Equal(RequestStatus.Pending, created!.Status);
        Assert.Equal(0, created.UnitsAllocated);
    }

    [Fact]
    public async Task Issue_WhenReserved_MarksUnitsAsIssued()
    {
        var admin = await _factory.CreateClient().AsAdminAsync();
        var hospitalId = await GetSeededHospitalIdAsync(admin);
        var unitId = await SeedUnitAsync(BloodType.ABNegative, BloodComponent.Platelets, daysUntilExpiry: 3);

        var createResponse = await admin.PostAsJsonAsync("/api/requests",
            new CreateBloodRequestRequest(hospitalId, BloodType.ABNegative, BloodComponent.Platelets, 1, RequestPriority.Routine, null));
        var created = await createResponse.Content.ReadFromJsonAsync<BloodRequestResponse>();
        Assert.Equal(RequestStatus.Reserved, created!.Status);

        var issueResponse = await admin.PostAsync($"/api/requests/{created.Id}/issue", null);
        issueResponse.EnsureSuccessStatusCode();
        var issued = await issueResponse.Content.ReadFromJsonAsync<BloodRequestResponse>();
        Assert.Equal(RequestStatus.Issued, issued!.Status);

        Assert.Equal(BloodUnitStatus.Issued, (await GetUnitAsync(unitId)).Status);

        var secondIssueAttempt = await admin.PostAsync($"/api/requests/{created.Id}/issue", null);
        Assert.Equal(HttpStatusCode.BadRequest, secondIssueAttempt.StatusCode);
    }

    [Fact]
    public async Task Cancel_WhenReserved_ReleasesUnitsBackToAvailable()
    {
        var admin = await _factory.CreateClient().AsAdminAsync();
        var hospitalId = await GetSeededHospitalIdAsync(admin);
        var unitId = await SeedUnitAsync(BloodType.ONegative, BloodComponent.RedCells, daysUntilExpiry: 15);

        var createResponse = await admin.PostAsJsonAsync("/api/requests",
            new CreateBloodRequestRequest(hospitalId, BloodType.ONegative, BloodComponent.RedCells, 1, RequestPriority.Routine, null));
        var created = await createResponse.Content.ReadFromJsonAsync<BloodRequestResponse>();
        Assert.Equal(RequestStatus.Reserved, created!.Status);

        var cancelResponse = await admin.PostAsync($"/api/requests/{created.Id}/cancel", null);
        cancelResponse.EnsureSuccessStatusCode();
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<BloodRequestResponse>();

        Assert.Equal(RequestStatus.Cancelled, cancelled!.Status);
        Assert.Equal(BloodUnitStatus.Available, (await GetUnitAsync(unitId)).Status);
    }

    [Fact]
    public async Task HospitalUser_CannotCreateRequestForAnotherHospital()
    {
        var hospitalClient = await _factory.CreateClient().AsHospitalAsync();

        var response = await hospitalClient.PostAsJsonAsync("/api/requests",
            new CreateBloodRequestRequest(999, BloodType.OPositive, BloodComponent.WholeBlood, 1, RequestPriority.Routine, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
