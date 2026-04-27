using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights.Extensibility;
using Azure.Identity;
using LandTitleRegistration.Services;

namespace LandTitleRegistration
{
    /// <summary>
    /// Service configuration for cloud-ready Land Title Registration application.
    /// This class demonstrates how to configure all Azure services and dependencies.
    /// </summary>
    public static class ServiceConfiguration
    {
        public static IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Add configuration sources
            // In production, this would include Azure App Configuration and Key Vault
            
            // Add logging with Application Insights
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddApplicationInsights(
                    configureTelemetryConfiguration: (config) =>
                    {
                        config.ConnectionString = configuration["ApplicationInsights:ConnectionString"];
                    },
                    configureApplicationInsightsLoggerOptions: (options) => { }
                );
            });

            // Add Application Insights telemetry
            services.AddApplicationInsightsTelemetry(options =>
            {
                options.ConnectionString = configuration["ApplicationInsights:ConnectionString"];
            });

            // Add distributed caching with Azure Redis
            var redisConnectionString = configuration["Redis:ConnectionString"];
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnectionString;
                    options.InstanceName = "LandTitle:";
                });
            }
            else
            {
                // Fallback to in-memory cache for development
                services.AddDistributedMemoryCache();
            }

            // Add HTTP client factory for resilient HTTP calls
            services.AddHttpClient();

            // Register application services
            services.AddScoped<TitleService>();
            services.AddScoped<Controllers.TitleController>();

            return services;
        }

        /// <summary>
        /// Builds configuration with Azure App Configuration and Key Vault integration.
        /// This method shows how to integrate with Azure services using Workload Identity.
        /// </summary>
        public static IConfigurationBuilder AddAzureConfiguration(
            this IConfigurationBuilder builder,
            string environment)
        {
            // Build initial configuration to get Azure service endpoints
            var config = builder.Build();

            // Add Azure App Configuration if configured
            var appConfigEndpoint = config["AzureAppConfiguration:Endpoint"];
            if (!string.IsNullOrEmpty(appConfigEndpoint))
            {
                builder.AddAzureAppConfiguration(options =>
                {
                    options.Connect(new Uri(appConfigEndpoint), new DefaultAzureCredential())
                           .ConfigureKeyVault(kv =>
                           {
                               // Use Workload Identity (Managed Identity) for Key Vault access
                               kv.SetCredential(new DefaultAzureCredential());
                           })
                           .Select("LandTitle:*", environment)
                           .Select("Common:*", environment);
                });
            }

            // Add Azure Key Vault if configured
            var keyVaultEndpoint = config["KeyVault:Endpoint"];
            if (!string.IsNullOrEmpty(keyVaultEndpoint))
            {
                builder.AddAzureKeyVault(
                    new Uri(keyVaultEndpoint),
                    new DefaultAzureCredential());
            }

            return builder;
        }
    }
}
