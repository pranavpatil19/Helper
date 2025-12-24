using CoreBusiness.Validation;
using Microsoft.EntityFrameworkCore;
using MigrationRunner.Configuration;
using Shared.Configuration;

namespace MigrationRunner;

/// <summary>
/// Captures all settings needed to register DAL/Core/EF services for a migration endpoint.
/// </summary>
public sealed record EndpointRegistration
{
    public required DatabaseOptions Database { get; init; }
    public bool IncludeEntityFramework { get; init; } = true;
    public Action<ValidationOptions>? ConfigureValidation { get; init; }
    public Action<DbContextOptionsBuilder>? ConfigureDbContext { get; init; }

    public static EndpointRegistration FromOptions(
        DatabaseEndpointOptions endpoint,
        bool includeEntityFramework = true,
        Action<ValidationOptions>? configureValidation = null,
        Action<DbContextOptionsBuilder>? configureDbContext = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        return new EndpointRegistration
        {
            Database = endpoint.ToDatabaseOptions(),
            IncludeEntityFramework = includeEntityFramework,
            ConfigureValidation = configureValidation,
            ConfigureDbContext = configureDbContext
        };
    }
}
