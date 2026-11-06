using System.ComponentModel.DataAnnotations;
using BloodLink.Web.Models;
using BloodLink.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BloodLink.Web.Pages.Hospitals;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly BloodLinkApiClient _apiClient;

    public IndexModel(BloodLinkApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IReadOnlyList<HospitalResponse> Hospitals { get; set; } = Array.Empty<HospitalResponse>();
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required] public string Name { get; set; } = string.Empty;
        [Required, EmailAddress] public string ContactEmail { get; set; } = string.Empty;
        [Required] public string Address { get; set; } = string.Empty;
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        try
        {
            await _apiClient.CreateHospitalAsync(new CreateHospitalRequest(Input.Name, Input.ContactEmail, Input.Address));
            return RedirectToPage();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
            await LoadAsync();
            return Page();
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            Hospitals = await _apiClient.GetHospitalsAsync() ?? Array.Empty<HospitalResponse>();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
