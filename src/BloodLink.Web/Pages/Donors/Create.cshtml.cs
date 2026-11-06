using System.ComponentModel.DataAnnotations;
using BloodLink.Web.Models;
using BloodLink.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BloodLink.Web.Pages.Donors;

[Authorize(Roles = "Admin")]
public class CreateModel : PageModel
{
    private readonly BloodLinkApiClient _apiClient;

    public CreateModel(BloodLinkApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required] public string FullName { get; set; } = string.Empty;
        [Required] public BloodType BloodType { get; set; }
        [Required] public DateOnly DateOfBirth { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(-25));
        [Required] public string Phone { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _apiClient.CreateDonorAsync(new CreateDonorRequest(Input.FullName, Input.BloodType, Input.DateOfBirth, Input.Phone));
            return RedirectToPage("/Donors/Index");
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }
}
