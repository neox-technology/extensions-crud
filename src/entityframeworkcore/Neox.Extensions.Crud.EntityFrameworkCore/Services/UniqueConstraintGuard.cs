using System.Reflection;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;

using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.EntityFrameworkCore.Services;

internal static class UniqueConstraintGuard
{
    public static async Task SaveChangesAsync(DbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (TryGetConstraintName(exception, out var constraintName))
        {
            await RestoreEntriesAsync(exception, cancellationToken).ConfigureAwait(false);

            throw new UniqueConstraintViolationException(constraintName, exception);
        }
    }

    private static string Clean(string name)
    {
        return name.Trim().Trim('\'', '"', '.');
    }

    private static bool HasErrorCode(Exception exception, string propertyName, params int[] codes)
    {
        var property = exception.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

        return property?.GetValue(exception) is int number && codes.Contains(number);
    }

    private static async Task RestoreEntriesAsync(DbUpdateException exception, CancellationToken cancellationToken)
    {
        foreach (var entry in exception.Entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.State = EntityState.Detached;
                    break;

                case EntityState.Modified:
                case EntityState.Deleted:
                    try
                    {
                        await entry.ReloadAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        entry.State = EntityState.Unchanged;
                    }

                    break;
            }
        }
    }

    private static bool TryGetConstraintName(Exception exception, out string constraintName)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (TryReadConstraint(current, out constraintName))
            {
                return true;
            }
        }

        constraintName = string.Empty;

        return false;
    }

    private static bool TryReadConstraint(Exception exception, out string constraintName)
    {
        var message = exception.Message;

        var sqlite = Regex.Match(
            message,
            @"UNIQUE constraint failed:\s*(?<name>[^\r\n]+)",
            RegexOptions.CultureInvariant);

        if (sqlite.Success)
        {
            constraintName = Clean(sqlite.Groups["name"].Value);

            return constraintName.Length > 0;
        }

        var index = Regex.Match(
            message,
            @"unique index '(?<name>[^']+)'",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        if (index.Success)
        {
            constraintName = index.Groups["name"].Value;

            return constraintName.Length > 0;
        }

        var key = Regex.Match(
            message,
            @"UNIQUE KEY constraint '(?<name>[^']+)'",
            RegexOptions.CultureInvariant);

        if (key.Success)
        {
            constraintName = key.Groups["name"].Value;

            return constraintName.Length > 0;
        }

        if (HasErrorCode(exception, "Number", 2601, 2627)
            || HasErrorCode(exception, "SqliteExtendedErrorCode", 2067, 1555))
        {
            constraintName = "unique";

            return true;
        }

        constraintName = string.Empty;

        return false;
    }
}
