# Land Title Registration - Cloud-Ready Application

## Overview
This application has been modernized for cloud deployment on Azure with full support for:
- Azure Kubernetes Service (AKS)
- Azure App Service
- Azure SQL Database
- Azure Cache for Redis
- Azure Key Vault
- Azure App Configuration
- Azure Application Insights

## Cloud Readiness Fixes Applied

### 1. Configuration Management
- **Fixed**: Hard-coded service URLs, connection strings, and API keys
- **Solution**: Externalized to `appsettings.json` with Azure App Configuration and Key Vault integration
- **Files**: All configuration now in `appsettings.json` with placeholders for Azure services

### 2. Database Access
- **Fixed**: Direct SqlConnection usage without connection pooling
- **Solution**: Migrated to Entity Framework Core with Azure SQL connection resiliency
- **Files**: `Services/TitleService.cs`, `Data/LandTitleDbContext.cs`, `Models/TitleRegistration.cs`

### 3. Session State Management
- **Fixed**: In-process session state (HttpSessionState)
- **Solution**: Replaced with Azure Cache for Redis using IDistributedCache
- **Files**: `Controllers/TitleController.cs`

### 4. Static Collections
- **Fixed**: Static Dictionary for caching (TitleCache)
- **Solution**: Replaced with distributed Redis cache
- **Files**: `Controllers/TitleController.cs`

### 5. File System Dependencies
- **Fixed**: Hard-coded Windows file paths (C:\, D:\)
- **Solution**: Configuration-driven paths using Path.Combine for cross-platform compatibility
- **Files**: `Controllers/TitleController.cs`, `appsettings.json`

### 6. Platform-Specific Dependencies
- **Fixed**: Windows Registry access (Microsoft.Win32.Registry)
- **Solution**: Replaced with Azure App Configuration
- **Files**: `Controllers/TitleController.cs`

### 7. Logging
- **Fixed**: log4net file appenders writing to local file system
- **Solution**: Replaced with ASP.NET Core ILogger integrated with Azure Application Insights
- **Files**: `Services/TitleService.cs`

### 8. Time/Clock Dependencies
- **Fixed**: DateTime.Now usage causing timezone inconsistencies
- **Solution**: Replaced with DateTimeOffset.UtcNow for UTC consistency
- **Files**: `Services/TitleService.cs`

### 9. Synchronous HTTP Calls
- **Fixed**: Blocking HttpClient operations (.Result, .Wait())
- **Solution**: Converted to async/await pattern with CancellationToken support
- **Files**: `Controllers/TitleController.cs`

### 10. Hard-coded Ports
- **Fixed**: Hard-coded port numbers
- **Solution**: Externalized to configuration
- **Files**: `Controllers/TitleController.cs`, `appsettings.json`

### 11. Security
- **Fixed**: Hard-coded secrets and API keys in code
- **Solution**: Externalized to Azure Key Vault with Workload Identity
- **Files**: `Services/TitleService.cs`, `appsettings.json`

### 12. Cryptography
- **Fixed**: SHA1CryptoServiceProvider (deprecated)
- **Solution**: Replaced with SHA256.Create()
- **Files**: `Services/TitleService.cs`

## Configuration Requirements

### Environment Variables / Azure App Configuration
The following configuration values must be set:

```bash
# Database
DbHost=<azure-sql-server>.database.windows.net
DbName=LandTitleDB
DbUser=<sql-admin-user>
DbPassword=<sql-admin-password>

# Redis Cache
RedisConnectionString=<redis-cache-name>.redis.cache.windows.net:6380,password=<redis-key>,ssl=True,abortConnect=False

# Application Insights
ApplicationInsightsConnectionString=InstrumentationKey=<key>;IngestionEndpoint=<endpoint>

# Azure App Configuration
AppConfigurationEndpoint=https://<app-config-name>.azconfig.io

# Azure Key Vault
KeyVaultUri=https://<keyvault-name>.vault.azure.net/

# API Keys (should be in Key Vault)
GovApiKey=<government-api-key>
```

### Azure Resources Required
1. **Azure SQL Database**: For persistent data storage
2. **Azure Cache for Redis**: For distributed session and cache storage
3. **Azure Key Vault**: For secrets management
4. **Azure App Configuration**: For centralized configuration
5. **Azure Application Insights**: For monitoring and logging
6. **Azure Storage Account**: For file storage (archives, exports)

## Deployment Options

### Option 1: Azure Kubernetes Service (AKS)
- Use Workload Identity for credential-free access to Azure services
- Configure pod-level access to Key Vault and App Configuration
- Enable horizontal pod autoscaling
- No session affinity required (stateless design)

### Option 2: Azure App Service
- Enable Managed Identity
- Configure App Settings to reference Key Vault secrets
- Enable Application Insights integration
- Configure connection strings

## Migration from Legacy Code

### Breaking Changes
1. **API Changes**: Methods are now async (suffix with `Async`)
2. **Constructor Injection**: Services require dependency injection
3. **Session Management**: Session ID must be passed explicitly
4. **Configuration**: No more hard-coded values

### Compatibility
- Target Framework: .NET 8.0
- Platform: Linux containers (cross-platform)
- Database: Azure SQL Database (compatible with SQL Server)

## Security Improvements
1. ✅ No hard-coded credentials
2. ✅ Secrets stored in Azure Key Vault
3. ✅ Workload Identity for authentication
4. ✅ Parameterized SQL queries (EF Core)
5. ✅ Updated cryptography (SHA256)
6. ✅ Secure package versions

## Monitoring and Observability
- Structured logging to Application Insights
- Distributed tracing support
- Performance metrics
- Exception tracking
- Custom telemetry

## Next Steps
1. Configure Azure resources (SQL, Redis, Key Vault, App Configuration)
2. Update `appsettings.json` placeholders with actual values
3. Run Entity Framework migrations to create database schema
4. Deploy to Azure (AKS or App Service)
5. Configure monitoring alerts in Application Insights

## Support
For issues or questions about cloud deployment, refer to Azure documentation:
- [Azure SQL Database](https://docs.microsoft.com/azure/azure-sql/)
- [Azure Cache for Redis](https://docs.microsoft.com/azure/azure-cache-for-redis/)
- [Azure Key Vault](https://docs.microsoft.com/azure/key-vault/)
- [Azure App Configuration](https://docs.microsoft.com/azure/azure-app-configuration/)
