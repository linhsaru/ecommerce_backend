using System;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Persistence.ValueConverters;

/// <summary>
/// Chuyen C# enum sang string lowercase de khop voi PostgreSQL enum ('pending', 'confirmed', ...).
/// </summary>
public sealed class LowerCaseEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public LowerCaseEnumConverter() : base(
        v => v.ToString().ToLowerInvariant(),
        v => Enum.Parse<TEnum>(v, true))
    {
    }
}
