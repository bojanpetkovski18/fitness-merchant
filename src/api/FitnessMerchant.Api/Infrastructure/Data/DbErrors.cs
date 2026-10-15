using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FitnessMerchant.Api.Infrastructure.Data;

/// <summary>Recognizes the PostgreSQL errors the handlers turn into field errors or 409s.</summary>
internal static class DbErrors
{
    public static bool IsUniqueViolation(DbUpdateException exception, out string? constraint) =>
        Is(exception, PostgresErrorCodes.UniqueViolation, out constraint);

    public static bool IsForeignKeyViolation(DbUpdateException exception) =>
        Is(exception, PostgresErrorCodes.ForeignKeyViolation, out _);

    private static bool Is(DbUpdateException exception, string sqlState, out string? constraint)
    {
        if (exception.InnerException is PostgresException { } postgres && postgres.SqlState == sqlState)
        {
            constraint = postgres.ConstraintName;
            return true;
        }
        constraint = null;
        return false;
    }
}
