using System.Net.Http.Json;
using BloodLink.Web.Models;

namespace BloodLink.Web.Services;

public class BloodLinkApiClient
{
    private readonly HttpClient _httpClient;

    public BloodLinkApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<LoginResponse?> LoginAsync(string username, string password) =>
        SendAsync<LoginResponse>(() => _httpClient.PostAsJsonAsync("api/auth/login", new LoginRequest(username, password)));

    public Task<PagedResult<DonorResponse>?> GetDonorsAsync(int page = 1, int pageSize = 20) =>
        SendAsync<PagedResult<DonorResponse>>(() => _httpClient.GetAsync($"api/donors?page={page}&pageSize={pageSize}"));

    public Task<DonorResponse?> CreateDonorAsync(CreateDonorRequest request) =>
        SendAsync<DonorResponse>(() => _httpClient.PostAsJsonAsync("api/donors", request));

    public Task<DonorEligibilityResponse?> CheckEligibilityAsync(int donorId) =>
        SendAsync<DonorEligibilityResponse>(() => _httpClient.GetAsync($"api/donors/{donorId}/eligibility"));

    public Task<PagedResult<AppointmentResponse>?> GetAppointmentsAsync(int page = 1, int pageSize = 20) =>
        SendAsync<PagedResult<AppointmentResponse>>(() => _httpClient.GetAsync($"api/appointments?page={page}&pageSize={pageSize}"));

    public Task<AppointmentResponse?> ScheduleAppointmentAsync(ScheduleAppointmentRequest request) =>
        SendAsync<AppointmentResponse>(() => _httpClient.PostAsJsonAsync("api/appointments", request));

    public Task CompleteAppointmentAsync(int id) =>
        SendAsync(() => _httpClient.PostAsync($"api/appointments/{id}/complete", null));

    public Task CancelAppointmentAsync(int id) =>
        SendAsync(() => _httpClient.PostAsync($"api/appointments/{id}/cancel", null));

    public Task<IReadOnlyList<InventorySummaryItem>?> GetInventorySummaryAsync() =>
        SendAsync<IReadOnlyList<InventorySummaryItem>>(() => _httpClient.GetAsync("api/inventory/summary"));

    public Task<PagedResult<BloodUnitResponse>?> GetBloodUnitsAsync(int page = 1, int pageSize = 20) =>
        SendAsync<PagedResult<BloodUnitResponse>>(() => _httpClient.GetAsync($"api/inventory/units?page={page}&pageSize={pageSize}"));

    public Task<IReadOnlyList<HospitalResponse>?> GetHospitalsAsync() =>
        SendAsync<IReadOnlyList<HospitalResponse>>(() => _httpClient.GetAsync("api/hospitals"));

    public Task<HospitalResponse?> CreateHospitalAsync(CreateHospitalRequest request) =>
        SendAsync<HospitalResponse>(() => _httpClient.PostAsJsonAsync("api/hospitals", request));

    public Task<PagedResult<BloodRequestResponse>?> GetRequestsAsync(int page = 1, int pageSize = 20) =>
        SendAsync<PagedResult<BloodRequestResponse>>(() => _httpClient.GetAsync($"api/requests?page={page}&pageSize={pageSize}"));

    public Task<BloodRequestResponse?> CreateRequestAsync(CreateBloodRequestRequest request) =>
        SendAsync<BloodRequestResponse>(() => _httpClient.PostAsJsonAsync("api/requests", request));

    public Task<BloodRequestResponse?> AllocateRequestAsync(int id) =>
        SendAsync<BloodRequestResponse>(() => _httpClient.PostAsync($"api/requests/{id}/allocate", null));

    public Task<BloodRequestResponse?> IssueRequestAsync(int id) =>
        SendAsync<BloodRequestResponse>(() => _httpClient.PostAsync($"api/requests/{id}/issue", null));

    public Task<BloodRequestResponse?> CancelRequestAsync(int id) =>
        SendAsync<BloodRequestResponse>(() => _httpClient.PostAsync($"api/requests/{id}/cancel", null));

    private static async Task<T?> SendAsync<T>(Func<Task<HttpResponseMessage>> sendRequest)
    {
        using var response = await sendRequest();
        if (!response.IsSuccessStatusCode)
        {
            throw await BuildExceptionAsync(response);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<T>();
    }

    private static async Task SendAsync(Func<Task<HttpResponseMessage>> sendRequest)
    {
        using var response = await sendRequest();
        if (!response.IsSuccessStatusCode)
        {
            throw await BuildExceptionAsync(response);
        }
    }

    private static async Task<ApiException> BuildExceptionAsync(HttpResponseMessage response)
    {
        string message = $"Error al llamar a la API ({(int)response.StatusCode}).";
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblem>();
            message = problem?.Detail ?? problem?.Title ?? message;
        }
        catch
        {
            // El cuerpo no era JSON (por ejemplo, un 401 sin ProblemDetails); se usa el mensaje por defecto.
        }

        return new ApiException((int)response.StatusCode, message);
    }
}
