using BloodLink.Web.Models;
using BloodLink.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BloodLink.Web.Pages.Donors;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly BloodLinkApiClient _apiClient;

    public IndexModel(BloodLinkApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public IReadOnlyList<DonorResponse> Donors { get; set; } = Array.Empty<DonorResponse>();
    public Dictionary<int, DonorEligibilityResponse> Eligibility { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var result = await _apiClient.GetDonorsAsync(pageSize: 50);
            Donors = result?.Items ?? Array.Empty<DonorResponse>();

            foreach (var donor in Donors)
            {
                var eligibility = await _apiClient.CheckEligibilityAsync(donor.Id);
                if (eligibility is not null)
                {
                    Eligibility[donor.Id] = eligibility;
                }
            }
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
