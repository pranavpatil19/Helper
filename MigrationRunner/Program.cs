using DataAccessLayer.Database.ECM.DbContexts;
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

builder.Services.AddMigrationRunnerServices(runnerOptions);

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
