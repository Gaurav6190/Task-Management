using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TaskManagement.Infrastructure.Persistence.Converters;

/// <summary>
/// SQL Server datetime2 does not store DateTimeKind, so values come back as Unspecified.
/// All times in this system are UTC; mark them as such on read so JSON serializes them with "Z".
/// </summary>
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
