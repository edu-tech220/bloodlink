using BloodLink.Web.Models;
using BloodLink.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BloodLink.Web.Pages;

[Authorize]
public class DashboardModel : PageModel
{
    private readonly BloodLinkApiClient _apiClient;

    public DashboardModel(BloodLinkApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public IReadOnlyList<InventorySummaryItem> Summary { get; set; } = Array.Empty<InventorySummaryItem>();
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            Summary = await _apiClient.GetInventorySummaryAsync() ?? Array.Empty<InventorySummaryItem>();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
