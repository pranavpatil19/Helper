using System;

namespace DataAccessLayer.Execution;

/// <summary>
/// Utility methods that validate raw SQL fragments before they are appended to SQL strings.
/// </summary>
public static class SqlSafety
{
    private static readonly string[] ForbiddenTokens = ["--", "/*", "*/", ";"];

    /// <summary>
    /// Ensures the supplied identifier/fragment is not null/whitespace and does not contain comment/statement separators.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the fragment is empty or contains disallowed tokens.</exception>
    public static string EnsureClause(string? fragment, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(fragment))
        {
            throw new ArgumentException("SQL fragment cannot be null or whitespace.", argumentName);
        }

        var trimmed = fragment.Trim();
        foreach (var token in ForbiddenTokens)
        {
            if (trimmed.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new ArgumentException($"SQL fragment contains forbidden token '{token}'.", argumentName);
            }
        }

        return trimmed;
    }
}
