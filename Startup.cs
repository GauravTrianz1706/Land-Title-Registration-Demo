using System;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LandTitleRegistration.Services;

namespace LandTitleRegistration
{
    /// <summary>
    /// Startup configuration for cloud-ready Land Title Registration application.
    /// Configures Azure services, distributed caching, and dependency injection.
    /// </summary>
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Configure Azure App Configuration
            var appConfigConnection = Configuration["AzureAppConfiguration:ConnectionString"];
            if (!string.IsNullOrEmpty(appConfigConnection))
            {
                var configBuilder = new ConfigurationBuilder()
                    .AddConfiguration(Configuration)
                    .AddAzureAppConfiguration(options =>
                    {
                        options.Connect(appConfigConnection)
                               .ConfigureKeyVault(kv =>
                               {
                                   // Use Workload Identity for AKS pods
                                   kv.SetCredential(new DefaultAzureCredential());
                               });
                    });

                Configuration = configBuilder.Build();
            }

            // Configure Azure SQL Database with Entity Framework Core
            var connectionString = Configuration.GetConnectionString("DefaultConnection");
            services.AddDbContext<LandTitleDbContext>(options =>
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    // Enable connection resiliency for transient fault handling
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                });
            });

            // Configure Azure Cache for Redis (distributed session and cache)
            var redisConnection = Configuration["Redis:ConnectionString"];
            if (!string.IsNullOrEmpty(redisConnection))
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnection;
                    options.InstanceName = "LandTitle:";
                });
            }
            else
            {
                // Fallback to in-memory cache for local development
                services.AddDistributedMemoryCache();
            }

            // Configure HttpClient with factory pattern for connection pooling
            services.AddHttpClient();

            // Configure Application Insights for Azure Monitor
            var appInsightsConnection = Configuration["ApplicationInsights:ConnectionString"];
            if (!string.IsNullOrEmpty(appInsightsConnection))
            {
                services.AddApplicationInsightsTelemetry(options =>
                {
                    options.ConnectionString = appInsightsConnection;
                });
            }

            // Configure structured logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddApplicationInsights();
            });

            // Register application services
            services.AddScoped<TitleService>();
            services.AddScoped<Controllers.TitleController>();
        }
    }
}
