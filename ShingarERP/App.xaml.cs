using System;
using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using ShingarERP.Core.Interfaces;
using ShingarERP.Data;
using ShingarERP.Data.Repositories;
using ShingarERP.Services;
using ShingarERP.UI.ViewModels;

namespace ShingarERP
{
    /// <summary>
    /// Application entry point with full Dependency Injection container setup.
    /// </summary>
    public partial class App : Application
    {
        private IServiceProvider _serviceProvider = null!;
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                ConfigureLogging();

                var config   = BuildConfiguration();
                var services = new ServiceCollection();

                RegisterServices(services, config);

                _serviceProvider = services.BuildServiceProvider();

                // Auto-migrate database on startup (dev convenience – switch to explicit for prod)
                MigrateDatabase(_serviceProvider);

                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                _log.Fatal(ex, "Application failed to start");
                MessageBox.Show($"Failed to start ShingarERP:\n\n{ex.Message}",
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown(1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LogManager.Shutdown();
            base.OnExit(e);
        }

        // ── Configuration ────────────────────────────────────────────

        private static IConfiguration BuildConfiguration()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            return new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json",
                    optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
        }

        // ── DI Registration ──────────────────────────────────────────

        private static void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            // Logging
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddNLog();
                builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
            });

            // Configuration
            services.AddSingleton(config);

            // EF Core – SQL Server
            services.AddDbContext<ShingarContext>(options =>
            {
                var connectionString = config.GetConnectionString("ShingarERP")
                    ?? "Server=(localdb)\\mssqllocaldb;Database=ShingarERP_Dev;Trusted_Connection=True;MultipleActiveResultSets=true";
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.CommandTimeout(60);
                    sqlOptions.EnableRetryOnFailure(3);
                });
                options.EnableSensitiveDataLogging(
                    Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development");
            });

            // Repository / Unit of Work
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<InventoryRepository>();
            services.AddScoped<CustomerRepository>();

            // HTTP clients for external APIs
            services.AddHttpClient<GoldRateService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.Add("User-Agent", "ShingarERP/1.0");
            });

            // Business services
            services.AddScoped<InventoryService>();
            services.AddScoped<CustomerService>();
            services.AddScoped<AccountingService>();

            // Utilities
            services.AddSingleton<Utilities.ReportGenerator>();

            // ViewModels (Transient – new instance per View)
            services.AddTransient<MetalInventoryViewModel>();
            services.AddTransient<FinishedGoodsViewModel>();
            services.AddTransient<CustomerMasterViewModel>();
            services.AddTransient<LedgerViewModel>();

            // Main Window
            services.AddTransient<MainWindow>();
        }

        // ── Database Migration ───────────────────────────────────────

        private static void MigrateDatabase(IServiceProvider provider)
        {
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ShingarContext>();
            try
            {
                context.Database.Migrate();
                _log.Info("Database migration completed.");
            }
            catch (Exception ex)
            {
                _log.Warn(ex, "Database migration failed – attempting EnsureCreated as fallback.");
                try
                {
                    context.Database.EnsureCreated();
                }
                catch (Exception ex2)
                {
                    _log.Error(ex2, "EnsureCreated also failed. Application may not have database access.");
                }
            }
        }

        // ── Logging setup ────────────────────────────────────────────

        private static void ConfigureLogging()
        {
            var nlogConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nlog.config");
            if (File.Exists(nlogConfigPath))
                LogManager.LoadConfiguration(nlogConfigPath);
            else
                LogManager.Setup().LoadConfiguration(builder =>
                {
                    builder.ForLogger().FilterMinLevel(NLog.LogLevel.Info)
                           .WriteToConsole(layout: "${longdate}|${level:uppercase=true}|${logger}|${message:withexception=true}");
                    builder.ForLogger().FilterMinLevel(NLog.LogLevel.Debug)
                           .WriteToFile(
                               fileName: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "ShingarERP_${shortdate}.log"),
                               layout: "${longdate}|${level:uppercase=true}|${logger}|${message:withexception=true}",
                               maxArchiveFiles: 30);
                });
        }

        /// <summary>Expose service provider for resolving services in code-behind (use sparingly).</summary>
        public static T GetService<T>() where T : notnull
            => ((App)Current)._serviceProvider.GetRequiredService<T>();
    }
}
