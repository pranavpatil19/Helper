using DataAccessLayer.Database.ECM.Interfaces;
using DataAccessLayer.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MigrationRunner;
using MigrationRunner.Configuration;
using MigrationRunner.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var runnerSection = builder.Configuration.GetSection(MigrationRunnerOptions.SectionName);
builder.Services.Configure<MigrationRunnerOptions>(runnerSection);

var runnerOptions = runnerSection.Get<MigrationRunnerOptions>()
    ?? throw new DalConfigurationException($"Missing '{MigrationRunnerOptions.SectionName}' configuration.");

var sourceRegistration = EndpointRegistration.FromOptions(runnerOptions.Source
    ?? throw new DalConfigurationException("Source database configuration is missing."));
var destinationRegistration = EndpointRegistration.FromOptions(runnerOptions.Destination
    ?? throw new DalConfigurationException("Destination database configuration is missing."));

builder.Services.AddMigrationEndpoints(sourceRegistration, destinationRegistration);
builder.Services
    .AddScoped<ISourceUserDataGateway>(sp =>
        new EndpointUserDataGateway(
            sp.GetRequiredService<ISourceDbContextFactory>(),
            sp.GetRequiredService<ILogger<EndpointUserDataGateway>>()))
    .AddScoped<IDestinationUserDataGateway>(sp =>
        new EndpointUserDataGateway(
            sp.GetRequiredService<IDestinationDbContextFactory>(),
            sp.GetRequiredService<ILogger<EndpointUserDataGateway>>()))
    .AddScoped<IUserSynchronizationService, UserSynchronizationService>();

builder.Services.AddHostedService<MigrationHostedService>();

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
