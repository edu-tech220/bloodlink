using BloodLink.Api.Data;
using BloodLink.Api.Domain;
using BloodLink.Api.Dtos;
using BloodLink.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BloodLink.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly BloodLinkDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtOptions _jwtOptions;

    public AuthController(
        BloodLinkDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IOptions<JwtOptions> jwtOptions)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _jwtOptions = jwtOptions.Value;
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return Unauthorized(new ProblemDetails { Title = "Usuario o contrasena invalidos." });
        }

        var token = _jwtTokenService.GenerateToken(user);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
        return Ok(new LoginResponse(token, user.Username, user.Role, user.HospitalId, expiresAt));
    }

    [HttpPost("register")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult> Register(RegisterUserRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Username == request.Username))
        {
            return Conflict(new ProblemDetails { Title = "El nombre de usuario ya existe." });
        }

        if (request.Role == UserRole.Hospital && request.HospitalId is null)
        {
            return BadRequest(new ProblemDetails { Title = "Un usuario de hospital requiere HospitalId." });
        }

        var (hash, salt) = _passwordHasher.HashPassword(request.Password);
        _db.Users.Add(new User
        {
            Username = request.Username,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = request.Role,
            HospitalId = request.HospitalId,
        });
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
