using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Resume.Api.Data;

/// <summary>Maps <see cref="DateTimeOffset"/> to UTC ticks (bigint) for reliable SQLite comparisons and translations.</summary>
public static class DateTimeOffsetUtcTicksConverters
{
    public static readonly ValueConverter<DateTimeOffset, long> UtcTicks = new(
        v => v.UtcTicks,
        v => new DateTimeOffset(v, TimeSpan.Zero));

    public static readonly ValueConverter<DateTimeOffset?, long?> NullableUtcTicks = new(
        v => v.HasValue ? v.Value.UtcTicks : null,
        v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null);
}
