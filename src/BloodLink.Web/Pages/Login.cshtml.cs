using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BloodLink.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BloodLink.Web.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly BloodLinkApiClient _apiClient;

    public LoginModel(BloodLinkApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required] public string Username { get; set; } = string.Empty;
        [Required] public string Password { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var result = await _apiClient.LoginAsync(Input.Username, Input.Password);
            if (result is null)
            {
                ErrorMessage = "No se pudo iniciar sesion.";
                return Page();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, result.Username),
                new(ClaimTypes.Role, result.Role.ToString()),
                new("access_token", result.Token),
            };
            if (result.HospitalId is { } hospitalId)
            {
                claims.Add(new Claim("hospitalId", hospitalId.ToString()));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { ExpiresUtc = result.ExpiresAt, IsPersistent = true });

            return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/Dashboard" : returnUrl);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.StatusCode == 401 ? "Usuario o contrasena invalidos." : ex.Message;
            return Page();
        }
    }
}
