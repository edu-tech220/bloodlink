using BloodLink.Api.Data;
using BloodLink.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BloodLink.Api.Tests;

/// <summary>
/// Boots the real API pipeline (controllers, auth, DI) against a single
/// in-process SQLite connection instead of SQL Server, so the whole test
/// suite runs with `dotnet test` and no external services.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public CustomWebApplicationFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<BloodLinkDbContext>>();
            services.AddDbContext<BloodLinkDbContext>(options => options.UseSqlite(_connection));

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BloodLinkDbContext>();
            // EnsureCreated (not Migrate): the migrations were authored against SQL Server's
            // type mappings (nvarchar(max), datetime2, ...), which SQLite can't parse. EnsureCreated
            // builds the schema straight from the current model using SQLite's own type mappings.
            db.Database.EnsureCreated();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            DbSeeder.SeedAsync(db, passwordHasher).GetAwaiter().GetResult();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
