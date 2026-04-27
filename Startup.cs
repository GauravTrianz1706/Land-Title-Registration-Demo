using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Azure.Identity;
using LandTitleRegistration.Services;
using LandTitleRegistration.Controllers;

namespace LandTitleRegistration
{
    /// <summary>
    /// Startup configuration for cloud-native dependency injection and service registration.
    /// This demonstrates how to configure the application for Azure deployment.
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
            // Add Azure App Configuration
            var appConfigConnection = Configuration["AppConfig:ConnectionString"];
            if (!string.IsNullOrEmpty(appConfigConnection))
            {
                services.AddAzureAppConfiguration();
            }

            // Add Application Insights telemetry
            services.AddApplicationInsightsTelemetry(options =>
            {
                options.ConnectionString = Configuration["ApplicationInsights:ConnectionString"];
            });

            // Configure Entity Framework Core with Azure SQL Database
            services.AddDbContext<TitleDbContext>(options =>
            {
                var connectionString = Configuration.GetConnectionString("DefaultConnection");
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

            // Configure distributed Redis cache for session state and caching
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = Configuration["Redis:ConnectionString"];
                options.InstanceName = Configuration["Redis:InstanceName"];
            });

            // Configure HttpClient with retry policies using Polly
            services.AddHttpClient("DocumentService", client =>
            {
                client.BaseAddress = new Uri(Configuration["ServiceUrls:DocumentService"]);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddTransientHttpErrorPolicy(policy => 
                policy.WaitAndRetryAsync(3, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

            services.AddHttpClient("NotificationService", client =>
            {
                client.BaseAddress = new Uri(Configuration["ServiceUrls:NotificationService"]);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddTransientHttpErrorPolicy(policy => 
                policy.WaitAndRetryAsync(3, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

            // Register application services
            services.AddScoped<TitleService>();
            services.AddScoped<TitleController>();

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddApplicationInsights();
            });
        }
    }
}
