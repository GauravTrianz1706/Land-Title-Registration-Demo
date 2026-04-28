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
    /// Service configuration for cloud-native deployment on Azure
    /// Configures Azure App Configuration, Key Vault, Redis, Application Insights, and Entity Framework Core
    /// </summary>
    public static class ServiceConfiguration
    {
        public static IServiceCollection ConfigureCloudServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure Entity Framework Core with Azure SQL Database
            // Uses Azure AD authentication with Managed Identity (Workload Identity in AKS)
            services.AddDbContext<LandTitleDbContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    // Enable connection resiliency for transient fault handling
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                    
                    // Command timeout for long-running queries
                    sqlOptions.CommandTimeout(60);
                });
            });

            // Configure distributed caching with Azure Cache for Redis
            // Replaces in-process session state and static collections
            var redisConnectionString = configuration["Redis:ConnectionString"];
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnectionString;
                    options.InstanceName = "LandTitle:";
                });
            }

            // Configure HttpClient with IHttpClientFactory for service-to-service communication
            // Replaces direct HttpClient instantiation
            services.AddHttpClient("DocumentService", client =>
            {
                var baseUrl = configuration["ServiceUrls:DocumentService"];
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    client.BaseAddress = new Uri(baseUrl);
                }
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddTransientHttpErrorPolicy(policy => 
                policy.WaitAndRetryAsync(3, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

            services.AddHttpClient("NotificationService", client =>
            {
                var baseUrl = configuration["ServiceUrls:NotificationService"];
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    client.BaseAddress = new Uri(baseUrl);
                }
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            services.AddHttpClient("LegacySearchApi", client =>
            {
                var baseUrl = configuration["ServiceUrls:LegacySearchApi"];
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    client.BaseAddress = new Uri(baseUrl);
                }
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Register application services
            services.AddScoped<TitleService>();

            // Configure Application Insights for Azure Monitor
            var appInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"];
            if (!string.IsNullOrEmpty(appInsightsConnectionString))
            {
                services.AddApplicationInsightsTelemetry(options =>
                {
                    options.ConnectionString = appInsightsConnectionString;
                });
            }

            // Configure health checks for Kubernetes liveness and readiness probes
            services.AddHealthChecks()
                .AddDbContextCheck<LandTitleDbContext>("database")
                .AddRedis(redisConnectionString ?? "localhost", "redis");

            // Configure logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddApplicationInsights();
            });

            return services;
        }

        /// <summary>
        /// Configures Azure App Configuration and Key Vault integration
        /// Uses Workload Identity for credential-free access from AKS pods
        /// </summary>
        public static IConfigurationBuilder AddAzureCloudConfiguration(
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
                        .Select("LandTitle:*", environment)
                        .ConfigureKeyVault(kv =>
                        {
                            // Use Workload Identity for Key Vault access
                            kv.SetCredential(new DefaultAzureCredential());
                        });
                });
            }

            // Add Azure Key Vault directly if configured
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
