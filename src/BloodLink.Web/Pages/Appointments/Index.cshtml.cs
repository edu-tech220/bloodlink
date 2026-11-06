using System.ComponentModel.DataAnnotations;
using BloodLink.Web.Models;
using BloodLink.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BloodLink.Web.Pages.Appointments;

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

    [BindProperty(SupportsGet = true)]
    public int? DonorId { get; set; }

    public IReadOnlyList<AppointmentResponse> Appointments { get; set; } = Array.Empty<AppointmentResponse>();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public class InputModel
    {
        [Required] public int DonorId { get; set; }
        [Required] public DateTime ScheduledAt { get; set; } = DateTime.Today.AddDays(1).AddHours(9);
    }

    public async Task OnGetAsync()
    {
        if (DonorId is { } donorId)
        {
            Input.DonorId = donorId;
        }
        await LoadAppointmentsAsync();
    }

    public async Task<IActionResult> OnPostScheduleAsync()
    {
        try
        {
            await _apiClient.ScheduleAppointmentAsync(new ScheduleAppointmentRequest(Input.DonorId, Input.ScheduledAt));
            SuccessMessage = "Cita agendada correctamente.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAppointmentsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCompleteAsync(int id)
    {
        try
        {
            await _apiClient.CompleteAppointmentAsync(id);
            SuccessMessage = "Cita completada: se genero una unidad en cuarentena.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAppointmentsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        try
        {
            await _apiClient.CancelAppointmentAsync(id);
            SuccessMessage = "Cita cancelada.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAppointmentsAsync();
        return Page();
    }

    private async Task LoadAppointmentsAsync()
    {
        try
        {
            var result = await _apiClient.GetAppointmentsAsync(pageSize: 50);
            Appointments = result?.Items ?? Array.Empty<AppointmentResponse>();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
