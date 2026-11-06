using System.Net;
using System.Net.Http.Json;
using BloodLink.Api.Domain;
using BloodLink.Api.Dtos;

namespace BloodLink.Api.Tests;

public class DonorsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DonorsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateDonor_AsAdmin_Succeeds()
    {
        var client = await _factory.CreateClient().AsAdminAsync();

        var request = new CreateDonorRequest("Ana Torres", BloodType.OPositive, new DateOnly(1995, 5, 20), "5551234567");
        var response = await client.PostAsJsonAsync("/api/donors", request);

        response.EnsureSuccessStatusCode();
        var donor = await response.Content.ReadFromJsonAsync<DonorResponse>();
        Assert.NotNull(donor);
        Assert.Equal("Ana Torres", donor!.FullName);
        Assert.True(donor.Id > 0);
    }

    [Fact]
    public async Task CreateDonor_AsHospitalRole_IsForbidden()
    {
        var client = await _factory.CreateClient().AsHospitalAsync();

        var request = new CreateDonorRequest("Luis Perez", BloodType.BNegative, new DateOnly(1990, 1, 1), "5559876543");
        var response = await client.PostAsJsonAsync("/api/donors", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDonors_ReturnsPagedResult()
    {
        var client = await _factory.CreateClient().AsAdminAsync();
        await client.PostAsJsonAsync("/api/donors", new CreateDonorRequest("Donante Pag", BloodType.ABPositive, new DateOnly(1988, 3, 3), "555"));

        var response = await client.GetAsync("/api/donors?page=1&pageSize=5");
        response.EnsureSuccessStatusCode();

        var page = await response.Content.ReadFromJsonAsync<PagedResult<DonorResponse>>();
        Assert.NotNull(page);
        Assert.Equal(1, page!.Page);
        Assert.True(page.Items.Count <= 5);
        Assert.True(page.TotalCount >= 1);
    }

    [Fact]
    public async Task CheckEligibility_NewlyCreatedAdultDonor_IsEligible()
    {
        var client = await _factory.CreateClient().AsAdminAsync();
        var createResponse = await client.PostAsJsonAsync("/api/donors",
            new CreateDonorRequest("Elegible Donante", BloodType.APositive, DateOnly.FromDateTime(DateTime.Today.AddYears(-25)), "555"));
        var donor = await createResponse.Content.ReadFromJsonAsync<DonorResponse>();

        var response = await client.GetAsync($"/api/donors/{donor!.Id}/eligibility");
        response.EnsureSuccessStatusCode();
        var eligibility = await response.Content.ReadFromJsonAsync<DonorEligibilityResponse>();

        Assert.True(eligibility!.IsEligible);
    }
}
