using Comercium.Domain;

namespace Comercium.Infrastructure.Persistence;

internal static class StoreDates
{
    public static DateTime UtcStart(DateOnly date) => date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();

    public static DateTime UtcEnd(DateOnly date) => date == DateOnly.MaxValue
        ? DateTime.MaxValue : UtcStart(date.AddDays(1));

    public static (DateTime Start, DateTime End) Range(DateOnly from, DateOnly through)
    {
        if (from > through)
            throw new BusinessException("La fecha inicial debe ser anterior o igual a la fecha final.");
        return (UtcStart(from), UtcEnd(through));
    }
}
