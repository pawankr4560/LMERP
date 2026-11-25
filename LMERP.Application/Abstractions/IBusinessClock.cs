namespace LMERP.Application.Abstractions;

public interface IBusinessClock
{
    DateOnly Today { get; }
    TimeZoneInfo TimeZone { get; }
}
