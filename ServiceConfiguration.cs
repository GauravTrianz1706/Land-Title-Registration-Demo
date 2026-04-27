using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using LandTitleRegistration.Data;
using LandTitleRegistration.Services;
using LandTitleRegistration.Controllers;

namespace LandTitleRegistration
{
    /// <summary>
    /// Service configuration for cloud-ready Land Title Registration application.
    /// Configures Azure services, distributed caching, and dependency injection.
    /// </summary>
    public static class ServiceConfiguration
    {
        public static IServiceCollection ConfigureServices(
            IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure Entity Framework Core with Azure SQL
            services.AddDbContext<LandTitleDbContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    // Enable connection resiliency for Azure SQL
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                });
            });

            // Configure distributed caching with Azure Cache for Redis
            var redisConnectionString = configuration["Redis:ConnectionString"];
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnectionString;
                    options.InstanceName = configuration["Redis:InstanceName"] ?? "LandTitleRegistration:";
                });
            }
            else
            {
                // Fallback to in-memory cache for local development
                services.AddDistributedMemoryCache();
            }

            // Configure HttpClient with proper timeout and retry policies
            services.AddHttpClient("DocumentService", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            services.AddHttpClient("NotificationService", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            services.AddHttpClient("LegacySearchApi", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Register application services
            services.AddScoped<TitleService>();
            services.AddScoped<TitleController>();

            // Configure logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
                builder.AddApplicationInsights();
            });

            // Configure Application Insights
            var appInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"];
            if (!string.IsNullOrEmpty(appInsightsConnectionString))
            {
                services.AddApplicationInsightsTelemetry(options =>
                {
                    options.ConnectionString = appInsightsConnectionString;
                });
            }

            return services;
        }

        public static IConfigurationBuilder AddAzureConfiguration(
            this IConfigurationBuilder builder,
            string environment)
        {
            var config = builder.Build();

            // Add Azure App Configuration
            var appConfigEndpoint = config["AzureAppConfiguration:Endpoint"];
            if (!string.IsNullOrEmpty(appConfigEndpoint))
            {
                builder.AddAzureAppConfiguration(options =>
                {
                    options.Connect(new Uri(appConfigEndpoint), new DefaultAzureCredential())
                           .ConfigureKeyVault(kv =>
                           {
                               // Use Workload Identity for Key Vault access
                               kv.SetCredential(new DefaultAzureCredential());
                           });
                });
            }

            // Add Azure Key Vault
            var keyVaultUri = config["KeyVault:VaultUri"];
            if (!string.IsNullOrEmpty(keyVaultUri))
            {
                builder.AddAzureKeyVault(
                    new Uri(keyVaultUri),
                    new DefaultAzureCredential());
            }

            return builder;
        }
    }
}
