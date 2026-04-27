using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using LandTitleRegistration.Services;

namespace LandTitleRegistration
{
    /// <summary>
    /// Configures services and dependencies for cloud-native deployment on Azure.
    /// Implements Azure App Configuration, Key Vault, Redis Cache, and Application Insights.
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
            var appConfigUri = Configuration["Azure:AppConfigurationUri"];
            if (!string.IsNullOrEmpty(appConfigUri))
            {
                var configBuilder = new ConfigurationBuilder()
                    .AddConfiguration(Configuration)
                    .AddAzureAppConfiguration(options =>
                    {
                        options.Connect(new Uri(appConfigUri), new DefaultAzureCredential())
                               .ConfigureKeyVault(kv =>
                               {
                                   kv.SetCredential(new DefaultAzureCredential());
                               });
                    });
                
                Configuration = configBuilder.Build();
            }

            // Configure Azure Key Vault client with Workload Identity
            var keyVaultUri = Configuration["Azure:KeyVaultUri"];
            if (!string.IsNullOrEmpty(keyVaultUri))
            {
                services.AddSingleton(sp =>
                {
                    return new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential());
                });
            }

            // Configure Entity Framework Core with Azure SQL connection resiliency
            var connectionString = Configuration.GetConnectionString("DefaultConnection");
            services.AddDbContext<TitleDbContext>(options =>
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                    sqlOptions.CommandTimeout(30);
                });
            });

            // Configure Azure Cache for Redis (distributed session and caching)
            var redisConnection = Configuration["Azure:RedisConnection"];
            if (!string.IsNullOrEmpty(redisConnection))
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnection;
                    options.InstanceName = "LandTitle:";
                });
            }

            // Configure HttpClient with Polly for resilient HTTP calls
            services.AddHttpClient<Controllers.TitleController>()
                .AddTransientHttpErrorPolicy(policy =>
                    policy.WaitAndRetryAsync(3, retryAttempt =>
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
                .AddTransientHttpErrorPolicy(policy =>
                    policy.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

            // Configure Application Insights for structured logging
            var appInsightsConnectionString = Configuration["Azure:ApplicationInsightsConnectionString"];
            if (!string.IsNullOrEmpty(appInsightsConnectionString))
            {
                services.AddApplicationInsightsTelemetry(options =>
                {
                    options.ConnectionString = appInsightsConnectionString;
                });
            }

            // Register application services
            services.AddScoped<TitleService>();
            services.AddScoped<Controllers.TitleController>();

            // Configure logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddApplicationInsights();
            });
        }
    }
}
