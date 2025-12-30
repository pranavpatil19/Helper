using DataAccessLayer.Configuration;
using DataAccessLayer.DependencyInjection.Common;
using DataAccessLayer.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;

namespace DataAccessLayer;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the full DAL stack using strongly typed <see cref="DatabaseOptions"/>.
    /// </summary>
    public static IServiceCollection AddDataAccessLayer(
        this IServiceCollection services,
        DatabaseOptions options,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureServices = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        DalCoreServiceRegistrar.Register(services, options, configureHelper, configureServices);
        return services;
    }

    /// <summary>
    /// Registers the DAL stack using raw configuration (for example appsettings.json).
    /// </summary>
    public static IServiceCollection AddDataAccessLayer(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureServices = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>()
            ?? throw new DalConfigurationException($"Missing '{DatabaseOptions.SectionName}' configuration section.");

        return services.AddDataAccessLayer(options, configureHelper, configureServices);
    }

    /// <summary>
    /// Registers the DAL stack using a provider profile that builds the connection string on demand.
    /// </summary>
    public static IServiceCollection AddDataAccessLayer(
        this IServiceCollection services,
        ActiveDataSourceOptions activeDataSource,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureServices = null)
    {
        ArgumentNullException.ThrowIfNull(activeDataSource);
        var options = activeDataSource.ToDatabaseOptions();
        return services.AddDataAccessLayer(options, configureHelper, configureServices);
    }
}
