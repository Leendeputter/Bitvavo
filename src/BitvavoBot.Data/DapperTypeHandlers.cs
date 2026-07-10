using System.Data;
using System.Globalization;
using Dapper;

namespace BitvavoBot.Data;

/// <summary>
/// SQLite has no native decimal type; all monetary/quantity columns are stored as TEXT using
/// invariant-culture round-trip formatting so no precision is lost (functional spec 4.3: at least
/// 8 decimals of internal precision). These handlers make Dapper read/write decimal and
/// DateTimeOffset transparently.
/// </summary>
public static class DapperTypeHandlers
{
    private static bool _registered;

    public static void RegisterAll()
    {
        if (_registered) return;
        _registered = true;

        SqlMapper.AddTypeHandler(new DecimalTypeHandler());
        SqlMapper.AddTypeHandler(new NullableDecimalTypeHandler());
        SqlMapper.AddTypeHandler(new DateTimeOffsetTypeHandler());
        SqlMapper.AddTypeHandler(new NullableDateTimeOffsetTypeHandler());
    }

    private sealed class DecimalTypeHandler : SqlMapper.TypeHandler<decimal>
    {
        public override void SetValue(IDbDataParameter parameter, decimal value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString("G", CultureInfo.InvariantCulture);
        }

        public override decimal Parse(object value) => value switch
        {
            string s => decimal.Parse(s, CultureInfo.InvariantCulture),
            decimal d => d,
            double d => (decimal)d,
            long l => l,
            _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
        };
    }

    private sealed class NullableDecimalTypeHandler : SqlMapper.TypeHandler<decimal?>
    {
        public override void SetValue(IDbDataParameter parameter, decimal? value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.HasValue
                ? value.Value.ToString("G", CultureInfo.InvariantCulture)
                : DBNull.Value;
        }

        public override decimal? Parse(object value) => value switch
        {
            null or DBNull => null,
            string s => decimal.Parse(s, CultureInfo.InvariantCulture),
            decimal d => d,
            double d => (decimal)d,
            long l => l,
            _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
        };
    }

    private sealed class DateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString("O", CultureInfo.InvariantCulture);
        }

        public override DateTimeOffset Parse(object value) => value switch
        {
            string s => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            _ => throw new InvalidCastException($"Cannot convert {value.GetType()} to DateTimeOffset")
        };
    }

    private sealed class NullableDateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset?>
    {
        public override void SetValue(IDbDataParameter parameter, DateTimeOffset? value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.HasValue
                ? value.Value.ToString("O", CultureInfo.InvariantCulture)
                : DBNull.Value;
        }

        public override DateTimeOffset? Parse(object value) => value switch
        {
            null or DBNull => null,
            string s => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            _ => throw new InvalidCastException($"Cannot convert {value.GetType()} to DateTimeOffset")
        };
    }
}
