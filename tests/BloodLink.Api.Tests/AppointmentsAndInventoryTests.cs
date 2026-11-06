using System.Net;
using System.Net.Http.Json;
using BloodLink.Api.Domain;
using BloodLink.Api.Dtos;

namespace BloodLink.Api.Tests;

public class AppointmentsAndInventoryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AppointmentsAndInventoryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<int> CreateDonorAsync(HttpClient admin, DateOnly dateOfBirth, BloodType bloodType = BloodType.OPositive)
    {
        var response = await admin.PostAsJsonAsync("/api/donors",
            new CreateDonorRequest($"Donante {Guid.NewGuid():N}", bloodType, dateOfBirth, "5550000000"));
        var donor = await response.Content.ReadFromJsonAsync<DonorResponse>();
        return donor!.Id;
    }

    [Fact]
    public async Task ScheduleAppointment_ForUnderageDonor_IsRejected()
    {
        var admin = await _factory.CreateClient().AsAdminAsync();
        var donorId = await CreateDonorAsync(admin, DateOnly.FromDateTime(DateTime.Today.AddYears(-16)));

        var response = await admin.PostAsJsonAsync("/api/appointments",
            new ScheduleAppointmentRequest(donorId, DateTime.Today.AddDays(1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ScheduleAppointment_Twice_SecondAttemptConflicts()
    {
        var admin = await _factory.CreateClient().AsAdminAsync();
        var donorId = await CreateDonorAsync(admin, DateOnly.FromDateTime(DateTime.Today.AddYears(-25)));

        var first = await admin.PostAsJsonAsync("/api/appointments", new ScheduleAppointmentRequest(donorId, DateTime.Today.AddDays(1)));
        first.EnsureSuccessStatusCode();

        var second = await admin.PostAsJsonAsync("/api/appointments", new ScheduleAppointmentRequest(donorId, DateTime.Today.AddDays(2)));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CompleteAppointment_CreatesQuarantinedUnit_ThenReleaseMakesItAvailable()
    {
        var admin = await _factory.CreateClient().AsAdminAsync();
        var donorId = await CreateDonorAsync(admin, DateOnly.FromDateTime(DateTime.Today.AddYears(-25)), BloodType.BPositive);

        var scheduleResponse = await admin.PostAsJsonAsync("/api/appointments", new ScheduleAppointmentRequest(donorId, DateTime.Today.AddDays(1)));
        var appointment = await scheduleResponse.Content.ReadFromJsonAsync<AppointmentResponse>();

        var completeResponse = await admin.PostAsync($"/api/appointments/{appointment!.Id}/complete", null);
        completeResponse.EnsureSuccessStatusCode();
        var unit = await completeResponse.Content.ReadFromJsonAsync<BloodUnitResponse>();

        Assert.Equal(BloodUnitStatus.Quarantined, unit!.Status);
        Assert.Equal(BloodType.BPositive, unit.BloodType);
        Assert.Equal(BloodComponent.WholeBlood, unit.Component);

        var releaseResponse = await admin.PostAsync($"/api/inventory/units/{unit.Id}/release", null);
        releaseResponse.EnsureSuccessStatusCode();

        var unitsResponse = await admin.GetAsync("/api/inventory/units?status=Available&pageSize=100");
        var page = await unitsResponse.Content.ReadFromJsonAsync<PagedResult<BloodUnitResponse>>();
        Assert.Contains(page!.Items, u => u.Id == unit.Id);
    }

    [Fact]
    public async Task Discard_OnAvailableUnit_RemovesItFromAvailableInventory()
    {
        var admin = await _factory.CreateClient().AsAdminAsync();
        var donorId = await CreateDonorAsync(admin, DateOnly.FromDateTime(DateTime.Today.AddYears(-25)));

        var scheduleResponse = await admin.PostAsJsonAsync("/api/appointments", new ScheduleAppointmentRequest(donorId, DateTime.Today.AddDays(1)));
        var appointment = await scheduleResponse.Content.ReadFromJsonAsync<AppointmentResponse>();
        var completeResponse = await admin.PostAsync($"/api/appointments/{appointment!.Id}/complete", null);
        var unit = await completeResponse.Content.ReadFromJsonAsync<BloodUnitResponse>();

        var discardResponse = await admin.PostAsync($"/api/inventory/units/{unit!.Id}/discard", null);
        discardResponse.EnsureSuccessStatusCode();

        var unitsResponse = await admin.GetAsync($"/api/inventory/units?status=Discarded&pageSize=100");
        var page = await unitsResponse.Content.ReadFromJsonAsync<PagedResult<BloodUnitResponse>>();
        Assert.Contains(page!.Items, u => u.Id == unit.Id);

        var secondDiscard = await admin.PostAsync($"/api/inventory/units/{unit.Id}/discard", null);
        Assert.Equal(HttpStatusCode.BadRequest, secondDiscard.StatusCode);
    }

    [Fact]
    public async Task InventorySummary_IsAccessibleByHospitalRoleToo()
    {
        var hospitalClient = await _factory.CreateClient().AsHospitalAsync();
        var response = await hospitalClient.GetAsync("/api/inventory/summary");
        response.EnsureSuccessStatusCode();

        var summary = await response.Content.ReadFromJsonAsync<List<InventorySummaryItem>>();
        Assert.NotNull(summary);
        Assert.NotEmpty(summary!);
    }

    [Fact]
    public async Task InventoryUnitsList_IsForbiddenForHospitalRole()
    {
        var hospitalClient = await _factory.CreateClient().AsHospitalAsync();
        var response = await hospitalClient.GetAsync("/api/inventory/units");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
