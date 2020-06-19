using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Serilog;
using System;
using TableauCri.Models.Configuration;
using TableauCri.Services;

namespace TableauCri.Tests
{
    public sealed class ServiceFactory
    {
        private static ServiceFactory _instance = null;
        private static readonly object _lock = new object();

        private static IHost _host = null;

        private ServiceFactory()
        {
            _host = Host.CreateDefaultBuilder(null)
                .ConfigureHostConfiguration(
                    config =>
                    {
                        config.SetBasePath(TestContext.CurrentContext.TestDirectory);
                        config.AddJsonFile("appsettings.json", true, true);
                        config.AddEnvironmentVariables();
                    }
                )
                .ConfigureAppConfiguration(
                    (hostContext, config) =>
                    {
                        hostContext.HostingEnvironment.EnvironmentName = "Development";
                        Console.WriteLine(TestContext.CurrentContext.TestDirectory);
                        config.SetBasePath(TestContext.CurrentContext.TestDirectory);
                        config.AddJsonFile("appsettings.json", true, true);
                        // reminder: environment defaults to Production unless
                        // otherwise specified, e.g. in launchsettings.json
                        config.AddJsonFile(
                            $"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", true, true
                        );
                        config.AddEnvironmentVariables();
                    }
                )
                .ConfigureServices(
                    (hostContext, services) =>
                    {
                        hostContext.HostingEnvironment.EnvironmentName = "Development";

                        var tableauCriConfig = hostContext.Configuration.GetSection(nameof(TableauCriSettings));
                        services.Configure<TableauCriSettings>(tableauCriConfig);
                        services.Configure<SmtpSettings>(tableauCriConfig.GetSection(nameof(SmtpSettings)));
                        services.Configure<TableauApiSettings>(tableauCriConfig.GetSection(nameof(TableauApiSettings)));

                        var tableauMigrationConfig = hostContext.Configuration.GetSection(
                            nameof(TableauMigrationSettings)
                        );
                        services.Configure<TableauMigrationSettings>(tableauMigrationConfig);
                        services.Configure<TableauApiSettingsSource>(
                            tableauMigrationConfig.GetSection(nameof(TableauApiSettingsSource))
                        );
                        services.Configure<TableauApiSettingsDestination>(
                            tableauMigrationConfig.GetSection(nameof(TableauApiSettingsDestination))
                        );
                        services.Configure<VizDatasourceSettings>(
                            tableauMigrationConfig.GetSection(nameof(VizDatasourceSettings))
                        );

                        services.AddOptions();
                        //services.AddMemoryCache();
                        //services.AddSingleton<ILogger>(Log.Logger);

                        services.AddSingleton<ISmtpService, SmtpService>();
                        services.AddSingleton<ITableauApiService, TableauApiService>();
                        services.AddTransient<ITableauProjectService, TableauProjectService>();
                        services.AddTransient<ITableauDatasourceService, TableauDatasourceService>();
                        services.AddTransient<ITableauGroupService, TableauGroupService>();
                        services.AddTransient<ITableauUserService, TableauUserService>();
                        services.AddSingleton<ITableauWorkbookService, TableauWorkbookService>();
                        services.AddSingleton<ITableauPermissionService, TableauPermissionService>();

                        services.AddSingleton<ITableauApiServiceSource, TableauApiServiceSource>();
                        services.AddSingleton<ITableauApiServiceDestination, TableauApiServiceDestination>();
                        services.AddSingleton<IVizDatasourceService, VizDatasourceService>();
                        services.AddSingleton<ITableauMigrationService, TableauMigrationService>();
                        //services.AddSingleton<ITableauCriService, ITableauCriService>();

                        //services.AddHostedService<AppService>();
                    }
                )
                .UseSerilog(
                    new LoggerConfiguration()
                        .MinimumLevel.Debug()
                        .WriteTo.Console()
                        .CreateLogger()
                )
                .UseConsoleLifetime()
                .Build();
        }

        /// <summary>
        /// Retrieve instance (singleton)
        /// </summary>
        public static ServiceFactory Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance = _instance ?? new ServiceFactory();
                    return _instance;
                }
            }
        }

        /// <summary>
        /// Get service specified in type parameter
        /// </summary>
        /// <returns></returns>
        public T GetService<T>()
        {
            //return _serviceProvider.GetService<T>();
            return _host.Services.GetRequiredService<T>();
        }

        public IOptionsMonitor<T> GetOptions<T>()
        {
            return ServiceFactory.Instance.GetService<IOptionsMonitor<T>>();
        }
    }
}
