using LMERP.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LMERP.Infrastructure.Persistence;

public sealed class LateFeeBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<LateFeeBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IEmiRepaymentService>();
                var count = await service.ProcessDailyFeesAsync(stoppingToken);
                logger.LogInformation("Daily late fee processor created {FeeCount} fee records.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Daily late fee processing failed."); }

            var timeZoneId = configuration["LateFeeProcessing:BusinessTimeZoneId"] ?? "Asia/Kolkata";
            TimeZoneInfo zone;
            try { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
            catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId == "Asia/Kolkata" ? "India Standard Time" : timeZoneId); }
            var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
            var runAt = TimeOnly.TryParse(configuration["LateFeeProcessing:RunAtLocalTime"], out var configuredTime) ? configuredTime : new TimeOnly(0, 5);
            var nextRun = new DateTimeOffset(localNow.Year, localNow.Month, localNow.Day, runAt.Hour, runAt.Minute, 0, localNow.Offset);
            if (nextRun <= localNow) nextRun = nextRun.AddDays(1);
            var wait = nextRun - localNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, stoppingToken);
        }
    }
}
