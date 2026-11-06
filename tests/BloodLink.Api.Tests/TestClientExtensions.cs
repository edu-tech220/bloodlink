using System.Net.Http.Headers;
using System.Net.Http.Json;
using BloodLink.Api.Dtos;

namespace BloodLink.Api.Tests;

public static class TestClientExtensions
{
    public static async Task<HttpClient> AsAdminAsync(this HttpClient client)
    {
        await client.AuthenticateAsync("admin", "Admin123!");
        return client;
    }

    public static async Task<HttpClient> AsHospitalAsync(this HttpClient client)
    {
        await client.AuthenticateAsync("hospital", "Hospital123!");
        return client;
    }

    private static async Task AuthenticateAsync(this HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
    }
}
