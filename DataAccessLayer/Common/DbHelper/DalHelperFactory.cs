using System;
using DataAccessLayer.Configuration;
using DataAccessLayer.Execution;
using DataAccessLayer.Exceptions;
using DataAccessLayer.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;

namespace DataAccessLayer.Common.DbHelper;

/// <summary>
/// Convenience surface for creating <see cref="DalHelperScope"/> instances without duplicating DI setup.
/// </summary>
public static class DalHelperFactory
{
    #region Public API

    /// <summary>
    /// Creates a helper scope for the provided options.
    /// </summary>
    public static DalHelperScope Create(
        DatabaseOptions options,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureDalServices = null,
        Action<IServiceCollection>? configureServices = null) =>
        CreateScope(() => options, expectedProvider: null, configureHelper, configureDalServices, configureServices);

    /// <summary>
    /// Creates a helper scope while enforcing the expected provider.
    /// </summary>
    public static DalHelperScope Create(
        DatabaseOptions options,
        DatabaseProvider expectedProvider,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureDalServices = null,
        Action<IServiceCollection>? configureServices = null) =>
        CreateScope(() => options, expectedProvider, configureHelper, configureDalServices, configureServices);

    /// <summary>
    /// Creates a helper scope and returns the helper + transaction manager instances.
    /// </summary>
    public static DalHelperScope Create(
        DatabaseOptions options,
        out IDatabaseHelper databaseHelper,
        out ITransactionManager transactionManager,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureDalServices = null,
        Action<IServiceCollection>? configureServices = null) =>
        CreateScopeWithOutputs(() => options, null, out databaseHelper, out transactionManager, configureHelper, configureDalServices, configureServices);

    /// <summary>
    /// Creates a helper scope with provider validation and returns the helper references.
    /// </summary>
    public static DalHelperScope Create(
        DatabaseOptions options,
        DatabaseProvider expectedProvider,
        out IDatabaseHelper databaseHelper,
        out ITransactionManager transactionManager,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureDalServices = null,
        Action<IServiceCollection>? configureServices = null) =>
        CreateScopeWithOutputs(() => options, expectedProvider, out databaseHelper, out transactionManager, configureHelper, configureDalServices, configureServices);

    /// <summary>
    /// Creates a helper scope from an active data-source profile.
    /// </summary>
    public static DalHelperScope Create(
        ActiveDataSourceOptions activeSource,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureDalServices = null,
        Action<IServiceCollection>? configureServices = null)
    {
        ArgumentNullException.ThrowIfNull(activeSource);
        return CreateScope(activeSource.ToDatabaseOptions, null, configureHelper, configureDalServices, configureServices);
    }

    /// <summary>
    /// Creates a helper scope from an active data-source profile and returns helper references.
    /// </summary>
    public static DalHelperScope Create(
        ActiveDataSourceOptions activeSource,
        out IDatabaseHelper databaseHelper,
        out ITransactionManager transactionManager,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureDalServices = null,
        Action<IServiceCollection>? configureServices = null)
    {
        ArgumentNullException.ThrowIfNull(activeSource);
        return CreateScopeWithOutputs(activeSource.ToDatabaseOptions, null, out databaseHelper, out transactionManager, configureHelper, configureDalServices, configureServices);
    }

    /// <summary>
    /// Creates a helper scope from inline provider + connection details.
    /// </summary>
    public static DalHelperScope Create(
        DatabaseProvider provider,
        string connectionString,
        Action<DbHelperOptions>? configureHelper = null,
        Action<DalServiceRegistrationOptions>? configureDalServices = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var options = BuildInlineOptions(provider, connectionString);
        return CreateScope(() => options, provider, configureHelper, configureDalServices, configureServices);
    }

    /// <summary>
    /// Creates a helper scope from inline provider details and returns helper references.
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
        var options = BuildInlineOptions(provider, connectionString);
        return CreateScopeWithOutputs(() => options, provider, out databaseHelper, out transactionManager, configureHelper, configureDalServices, configureServices);
    }

    #endregion

    #region Internal Helpers

    private static DalHelperScope CreateScope(
        Func<DatabaseOptions> optionsFactory,
        DatabaseProvider? expectedProvider,
        Action<DbHelperOptions>? configureHelper,
        Action<DalServiceRegistrationOptions>? configureDalServices,
        Action<IServiceCollection>? configureServices)
    {
        var options = ValidateOptions(optionsFactory(), expectedProvider);
        var services = new ServiceCollection();
        services.AddLogging();
        configureServices?.Invoke(services);
        services.AddDataAccessLayer(options, configureHelper, configureDalServices);
        return BuildScope(services);
    }

    private static DalHelperScope CreateScopeWithOutputs(
        Func<DatabaseOptions> optionsFactory,
        DatabaseProvider? expectedProvider,
        out IDatabaseHelper databaseHelper,
        out ITransactionManager transactionManager,
        Action<DbHelperOptions>? configureHelper,
        Action<DalServiceRegistrationOptions>? configureDalServices,
        Action<IServiceCollection>? configureServices)
    {
        var scope = CreateScope(optionsFactory, expectedProvider, configureHelper, configureDalServices, configureServices);
        databaseHelper = scope.DatabaseHelper;
        transactionManager = scope.TransactionManager;
        return scope;
    }

    private static DatabaseOptions BuildInlineOptions(DatabaseProvider provider, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or whitespace.", nameof(connectionString));
        }

        return new DatabaseOptions
        {
            Provider = provider,
            ConnectionString = connectionString
        };
    }

    private static DatabaseOptions ValidateOptions(DatabaseOptions options, DatabaseProvider? expectedProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (expectedProvider is { } provider && options.Provider != provider)
        {
            throw new ProviderNotSupportedException(
                $"Provider '{options.Provider}' does not match expected provider '{provider}'.");
        }

        return options;
    }

    private static DalHelperScope BuildScope(ServiceCollection services)
    {
        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var scope = provider.CreateAsyncScope();
        return new DalHelperScope(provider, scope);
    }

    #endregion
}
