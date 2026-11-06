using BloodLink.Api.Data;
using BloodLink.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Api.Services;

/// <summary>
/// Periodically marks blood units past their expiry date as Expired so they
/// stop showing up as available stock. Runs inside the API host; in a larger
/// deployment this would be a separate worker, but a BackgroundService keeps
/// the demo self-contained.
/// </summary>
public class ExpirationSweepService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval;
    private readonly ILogger<ExpirationSweepService> _logger;

    public ExpirationSweepService(IServiceScopeFactory scopeFactory, ILogger<ExpirationSweepService> logger, TimeSpan? interval = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = interval ?? TimeSpan.FromMinutes(30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BloodLinkDbContext>();

                var expired = await db.BloodUnits
                    .Where(u => (u.Status == BloodUnitStatus.Available || u.Status == BloodUnitStatus.Quarantined)
                                && u.ExpiresAt <= DateTime.UtcNow)
                    .ToListAsync(stoppingToken);

                foreach (var unit in expired)
                {
                    unit.Status = BloodUnitStatus.Expired;
                    unit.Version++;
                }

                if (expired.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Se marcaron {Count} unidades como expiradas.", expired.Count);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Fallo el barrido de unidades expiradas.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
