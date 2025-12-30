using System;
using DataAccessLayer;
using DataAccessLayer.Configuration;
using DataAccessLayer.Execution;
using DataAccessLayer.Exceptions;
using DataAccessLayer.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;

namespace DataAccessLayer.Common.DbHelper;

/// <summary>
/// Creates isolated DAL helper scopes so callers can instantiate provider-specific helpers without DI boilerplate.
/// This powers "Approach 1" (manual bootstrap) described in the docs.
/// </summary>
public static class DalHelperFactory
{
    /// <summary>
    /// Creates a helper scope using strongly typed <see cref="DatabaseOptions"/>.
    /// Pass optional delegates to tweak helper behavior per scope without touching global DI setup.
    /// </summary>
    public static DalHelperScope Create(
        DatabaseOptions options,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureDalServices = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var validatedOptions = ValidateOptions(options);
        return CreateInternal(validatedOptions, configureHelper, configureDalServices, configureServices);
    }

    /// <summary>
    /// Creates a helper scope while asserting the provided options match the expected provider.
    /// </summary>
    public static DalHelperScope Create(
        DatabaseOptions options,
        DatabaseProvider expectedProvider,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureDalServices = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var validatedOptions = ValidateOptions(options, expectedProvider);
        return CreateInternal(validatedOptions, configureHelper, configureDalServices, configureServices);
    }

    /// <summary>
    /// Creates a helper scope and immediately surfaces the helper + transaction manager references.
    /// </summary>
    public static DalHelperScope Create(
        DatabaseOptions options,
        out IDatabaseHelper databaseHelper,
        out ITransactionManager transactionManager,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureDalServices = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var scope = Create(options, configureHelper, configureDalServices, configureServices);
        databaseHelper = scope.DatabaseHelper;
        transactionManager = scope.TransactionManager;
        return scope;
    }

    /// <summary>
    /// Creates a helper scope (with provider validation) and surfaces helper references immediately.
    /// </summary>
    public static DalHelperScope Create(
        DatabaseOptions options,
        DatabaseProvider expectedProvider,
        out IDatabaseHelper databaseHelper,
        out ITransactionManager transactionManager,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureDalServices = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var scope = Create(options, expectedProvider, configureHelper, configureDalServices, configureServices);
        databaseHelper = scope.DatabaseHelper;
        transactionManager = scope.TransactionManager;
        return scope;
    }

    /// <summary>
    /// Creates a helper scope from an <see cref="ActiveDataSourceOptions"/> profile.
    /// </summary>
    public static DalHelperScope Create(
        ActiveDataSourceOptions activeDataSource,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureDalServices = null,
        Action<IServiceCollection>? configureServices = null)
    {
        ArgumentNullException.ThrowIfNull(activeDataSource);
        var options = activeDataSource.ToDatabaseOptions();
        return Create(options, configureHelper, configureDalServices, configureServices);
    }

    /// <summary>
    /// Creates a helper scope from an <see cref="ActiveDataSourceOptions"/> profile and surfaces helper references immediately.
    /// </summary>
    public static DalHelperScope Create(
        ActiveDataSourceOptions activeDataSource,
        out IDatabaseHelper databaseHelper,
        out ITransactionManager transactionManager,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureDalServices = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var scope = Create(activeDataSource, configureHelper, configureDalServices, configureServices);
        databaseHelper = scope.DatabaseHelper;
        transactionManager = scope.TransactionManager;
        return scope;
    }

    /// <summary>
    /// Creates a helper scope with an inline provider + connection string definition.
    /// </summary>
    public static DalHelperScope Create(
        DatabaseProvider provider,
        string connectionString,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureDalServices = null,
        Action<IServiceCollection>? configureServices = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or whitespace.", nameof(connectionString));
        }

        var options = new DatabaseOptions
        {
            Provider = provider,
            ConnectionString = connectionString
        };

        return Create(options, configureHelper, configureDalServices, configureServices);
    }

    /// <summary>
    /// Creates a helper scope with an inline provider definition and surfaces helper references immediately.
    /// </summary>
    public static DalHelperScope Create(
        DatabaseProvider provider,
        string connectionString,
        out IDatabaseHelper databaseHelper,
        out ITransactionManager transactionManager,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureDalServices = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var scope = Create(provider, connectionString, configureHelper, configureDalServices, configureServices);
        databaseHelper = scope.DatabaseHelper;
        transactionManager = scope.TransactionManager;
        return scope;
    }

    private static ServiceCollection BuildServiceCollection(Action<IServiceCollection>? configureServices)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configureServices?.Invoke(services);
        return services;
    }

    private static DalHelperScope BuildScope(ServiceCollection services)
    {
        var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        var scope = provider.CreateAsyncScope();
        return new DalHelperScope(provider, scope);
    }

    private static DalHelperScope CreateInternal(
        DatabaseOptions validatedOptions,
        Action<DbHelperOptions>? configureHelper,
        Action<DalServiceRegistrationOptions>? configureDalServices,
        Action<IServiceCollection>? configureServices)
    {
        var services = BuildServiceCollection(configureServices);
        services.AddDataAccessLayer(validatedOptions, configureHelper, configureDalServices);
        return BuildScope(services);
    }

    private static DatabaseOptions ValidateOptions(
        DatabaseOptions options,
        DatabaseProvider? expectedProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (expectedProvider.HasValue && options.Provider != expectedProvider.Value)
        {
            throw new ProviderNotSupportedException(
                $"Provider '{options.Provider}' does not match expected provider '{expectedProvider.Value}'.");
        }

        return options;
    }
}
