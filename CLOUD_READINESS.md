# Land Title Registration - Cloud-Ready Application

## Overview
This application has been modernized for Azure cloud deployment with cloud-native patterns and best practices.

## Cloud Readiness Improvements

### 1. Configuration Management
- **Externalized Configuration**: All hard-coded URLs, connection strings, and secrets moved to `appsettings.json`
- **Azure App Configuration**: Ready for integration with Azure App Configuration service
- **Azure Key Vault**: Configured to use Key Vault references for sensitive data
- **Environment Variables**: All configuration supports environment variable overrides

### 2. State Management
- **Distributed Caching**: Replaced static collections and in-process session state with `IDistributedCache`
- **Azure Cache for Redis**: Ready for Redis-backed distributed caching
- **Stateless Design**: Application can scale horizontally without session affinity

### 3. Database Access
- **Connection Pooling**: Using modern `Microsoft.Data.SqlClient` with built-in pooling
- **Parameterized Queries**: All SQL queries use parameters to prevent SQL injection
- **Async Operations**: All database operations are asynchronous for better scalability
- **Azure SQL Support**: Compatible with Azure SQL Database with transient fault handling

### 4. Logging & Monitoring
- **Structured Logging**: Replaced log4net with `ILogger` for structured logging
- **Azure Application Insights**: Integrated for telemetry and monitoring
- **Cloud-Native Logging**: Logs emit to stdout/stderr for container environments

### 5. HTTP Communication
- **IHttpClientFactory**: Proper HTTP client management with connection pooling
- **Async/Await Pattern**: All HTTP calls are asynchronous
- **Resilience**: Ready for Polly retry policies and circuit breakers

### 6. Cross-Platform Compatibility
- **Path Handling**: Using `Path.Combine` for cross-platform file paths
- **No Windows Dependencies**: Removed Windows Registry and IIS dependencies
- **Linux Container Ready**: Compatible with Linux containers in AKS

### 7. Security
- **Secrets Management**: All secrets externalized to Azure Key Vault
- **Workload Identity**: Ready for AKS Workload Identity (credential-free access)
- **Updated Cryptography**: Replaced SHA1 with SHA256 for better security

### 8. Time & Timezone
- **UTC Timestamps**: Using `DateTimeOffset.UtcNow` for timezone-independent operations
- **Consistent Time Handling**: All timestamps stored in UTC

## Configuration Requirements

### Environment Variables / Azure App Configuration

```bash
# Database Configuration (Azure Key Vault Reference)
DB_HOST=your-azure-sql-server.database.windows.net
DB_NAME=LandTitleDB
DB_USER=your-db-user
DB_PASSWORD=your-db-password

# Service URLs
DOCUMENT_SERVICE_URL=https://docs.landtitle.azure.com/fetch
NOTIFICATION_SERVICE_URL=https://notify.landtitle.azure.com/send
LEGACY_SEARCH_API_URL=https://search.landtitle.azure.com/search/titles
GOV_REPORT_API_URL=https://gov.landregistry.azure.com/reports

# Storage Paths (Azure Blob Storage or Persistent Volumes)
ARCHIVE_BASE_PATH=/app/data/archives
TEMP_EXPORT_PATH=/app/data/temp/exports

# API Keys (Azure Key Vault Reference)
GOV_API_KEY=your-gov-api-key

# Azure Services
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=your-key;IngestionEndpoint=https://...
REDIS_CONNECTION_STRING=your-redis-cache.redis.cache.windows.net:6380,password=your-password,ssl=True
```

## Azure Services Integration

### Required Azure Services
1. **Azure Kubernetes Service (AKS)** - Container orchestration
2. **Azure SQL Database** - Managed database service
3. **Azure Cache for Redis** - Distributed caching and session state
4. **Azure Key Vault** - Secrets management
5. **Azure App Configuration** - Centralized configuration
6. **Azure Application Insights** - Monitoring and telemetry
7. **Azure Blob Storage** (optional) - For archive and export files

### Workload Identity Setup
The application is ready for AKS Workload Identity to access Azure services without storing credentials:
- Azure Key Vault access
- Azure App Configuration access
- Azure SQL Database access (with Managed Identity)
- Azure Cache for Redis access

## Deployment Notes

### Container Deployment
- Application runs on .NET 8.0 runtime
- No IIS dependencies - runs on Kestrel
- Linux container compatible
- Supports horizontal pod autoscaling in AKS

### Health Checks
Consider adding health check endpoints for:
- Database connectivity
- Redis cache connectivity
- External service availability

### Monitoring
- Application Insights automatically tracks:
  - Request telemetry
  - Dependency calls (SQL, HTTP, Redis)
  - Exceptions and errors
  - Custom metrics and events

## Migration from Legacy

### Removed Dependencies
- ❌ Windows Registry access
- ❌ IIS-specific modules
- ❌ In-process session state
- ❌ Static collections for state
- ❌ Hard-coded file paths
- ❌ Hard-coded connection strings
- ❌ log4net file appenders
- ❌ Synchronous HTTP calls
- ❌ Direct SqlConnection management

### Added Dependencies
- ✅ Microsoft.Extensions.Configuration
- ✅ Microsoft.Extensions.Logging
- ✅ Microsoft.Extensions.Caching.Distributed
- ✅ Azure.Identity (Workload Identity)
- ✅ Azure Key Vault integration
- ✅ Application Insights
- ✅ IHttpClientFactory
- ✅ Modern SqlClient with Azure SQL support

## Next Steps

1. **Configure Azure Resources**: Set up required Azure services
2. **Update Configuration**: Populate appsettings.json or Azure App Configuration
3. **Set Up Key Vault**: Store secrets in Azure Key Vault
4. **Configure Workload Identity**: Set up managed identity for AKS pods
5. **Deploy to AKS**: Use Helm charts or Kubernetes manifests
6. **Monitor**: Configure Application Insights dashboards and alerts

## Support

For issues or questions about cloud deployment, refer to Azure documentation:
- [Azure Kubernetes Service](https://docs.microsoft.com/azure/aks/)
- [Azure App Configuration](https://docs.microsoft.com/azure/azure-app-configuration/)
- [Azure Key Vault](https://docs.microsoft.com/azure/key-vault/)
- [Application Insights](https://docs.microsoft.com/azure/azure-monitor/app/app-insights-overview)
