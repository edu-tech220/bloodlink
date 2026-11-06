using BloodLink.Api.Domain;
using BloodLink.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Api.Data;

/// <summary>Seeds a minimal dataset so the app is usable immediately after startup.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(BloodLinkDbContext db, IPasswordHasher passwordHasher)
    {
        if (!await db.Hospitals.AnyAsync())
        {
            var hospital = new Hospital
            {
                Name = "Hospital General Zumpango",
                ContactEmail = "contacto@hgz.example.com",
                Address = "Av. Principal 100, Zumpango, Edo. Mex.",
            };
            db.Hospitals.Add(hospital);
            await db.SaveChangesAsync();

            var (adminHash, adminSalt) = passwordHasher.HashPassword("Admin123!");
            var (hospitalHash, hospitalSalt) = passwordHasher.HashPassword("Hospital123!");

            db.Users.AddRange(
                new User
                {
                    Username = "admin",
                    PasswordHash = adminHash,
                    PasswordSalt = adminSalt,
                    Role = UserRole.Admin,
                },
                new User
                {
                    Username = "hospital",
                    PasswordHash = hospitalHash,
                    PasswordSalt = hospitalSalt,
                    Role = UserRole.Hospital,
                    HospitalId = hospital.Id,
                });

            await db.SaveChangesAsync();
        }
    }
}
