using System.ComponentModel.DataAnnotations;
using BloodLink.Web.Models;
using BloodLink.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BloodLink.Web.Pages.Requests;

[Authorize]
public class IndexModel : PageModel
{
    private readonly BloodLinkApiClient _apiClient;

    public IndexModel(BloodLinkApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IReadOnlyList<BloodRequestResponse> Requests { get; set; } = Array.Empty<BloodRequestResponse>();
    public IReadOnlyList<HospitalResponse> Hospitals { get; set; } = Array.Empty<HospitalResponse>();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public bool IsAdmin => User.IsInRole("Admin");

    public class InputModel
    {
        [Required] public int HospitalId { get; set; }
        [Required] public BloodType BloodType { get; set; }
        [Required] public BloodComponent Component { get; set; }
        [Range(1, 50)] public int QuantityUnits { get; set; } = 1;
        public RequestPriority Priority { get; set; } = RequestPriority.Routine;
        public string? Notes { get; set; }
    }

    public async Task OnGetAsync()
    {
        var hospitalIdClaim = User.FindFirst("hospitalId")?.Value;
        if (int.TryParse(hospitalIdClaim, out var hospitalId))
        {
            Input.HospitalId = hospitalId;
        }

        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        try
        {
            await _apiClient.CreateRequestAsync(new CreateBloodRequestRequest(
                Input.HospitalId, Input.BloodType, Input.Component, Input.QuantityUnits, Input.Priority, Input.Notes));
            SuccessMessage = "Solicitud creada. Se intento una asignacion automatica de unidades.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAllocateAsync(int id)
    {
        try
        {
            await _apiClient.AllocateRequestAsync(id);
            SuccessMessage = "Se reintento la asignacion de unidades.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostIssueAsync(int id)
    {
        try
        {
            await _apiClient.IssueRequestAsync(id);
            SuccessMessage = "Unidades entregadas.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        try
        {
            await _apiClient.CancelRequestAsync(id);
            SuccessMessage = "Solicitud cancelada.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        try
        {
            var result = await _apiClient.GetRequestsAsync(pageSize: 50);
            Requests = result?.Items ?? Array.Empty<BloodRequestResponse>();
            Hospitals = await _apiClient.GetHospitalsAsync() ?? Array.Empty<HospitalResponse>();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
