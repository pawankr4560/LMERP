using LMERP.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace LMERP.Infrastructure.Persistence;

public sealed class BusinessClock(IConfiguration configuration) : IBusinessClock
{
    public TimeZoneInfo TimeZone
    {
        get
        {
            var id = configuration["LateFeeProcessing:BusinessTimeZoneId"] ?? "Asia/Kolkata";
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById(id == "Asia/Kolkata" ? "India Standard Time" : id); }
        }
    }
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZone).DateTime);
}
