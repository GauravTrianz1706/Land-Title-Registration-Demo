# Land Title Registration - Cloud-Ready Backend Service

## Overview
This backend service has been modernized for cloud-native deployment on Azure. All cloud readiness blockers have been resolved.

## Cloud Readiness Fixes Applied

### 1. Configuration Management
- **Hard-coded URLs** → Externalized to Azure App Configuration
- **Hard-coded connection strings** → Moved to Azure Key Vault
- **Hard-coded secrets** → Stored in Azure Key Vault with Workload Identity access
- **Configuration file**: `appsettings.json` with environment variable placeholders

### 2. File System & Storage
- **Hard-coded Windows paths** (C:\, D:\) → Cross-platform paths using `Path.Combine`
- **Archive paths** → Configurable via Azure App Configuration
- **Temp export paths** → Container-friendly paths (`/app/data/temp/exports`)

### 3. State Management
- **Static collections** → Replaced with Azure Cache for Redis (distributed caching)
- **In-process session state** → Migrated to Azure Cache for Redis
- **HttpContext.Session** → Distributed cache with session keys

### 4. Database & Persistence
- **Direct SqlConnection** → Migrated to Entity Framework Core
- **SQL injection vulnerabilities** → Parameterized queries via EF Core
- **Connection pooling** → Built-in EF Core connection pooling with retry logic
- **Azure SQL resiliency** → Transient fault handling enabled

### 5. Logging & Monitoring
- **log4net file appenders** → Replaced with ASP.NET Core ILogger
- **Local file logging** → Azure Application Insights integration
- **Structured logging** → JSON-formatted logs for cloud monitoring

### 6. Platform Dependencies
- **Windows Registry access** → Replaced with Azure App Configuration
- **IIS module dependencies** → Removed (ready for Kestrel in AKS)
- **Platform-specific code** → Cross-platform .NET 8.0

### 7. Networking & Communication
- **Hard-coded port numbers** → Dynamic port binding via environment variables
- **Synchronous HttpClient** → Async/await pattern with Polly retry policies
- **Blocking operations** → Async enumeration patterns

### 8. Time & Timezone
- **DateTime.Now** → Replaced with `DateTimeOffset.UtcNow` for timezone consistency
- **Server-local timezone** → UTC storage with timezone metadata support

### 9. Security
- **SHA1 hashing** → Upgraded to SHA256
- **Hardcoded API keys** → Retrieved from Azure Key Vault
- **Insecure packages** → Updated to latest secure versions

## Azure Services Integration

### Required Azure Resources
1. **Azure App Configuration** - Centralized configuration management
2. **Azure Key Vault** - Secrets and connection string storage
3. **Azure Cache for Redis** - Distributed session and caching
4. **Azure SQL Database** - Managed database with connection resiliency
5. **Azure Application Insights** - Monitoring and structured logging
6. **Azure Kubernetes Service (AKS)** - Container orchestration with Workload Identity

### Environment Variables
Configure these environment variables for the application:

```bash
# Service Configuration
SERVICE_PORT=8080

# Azure Resources (or use Workload Identity)
AZURE_KEYVAULT_URI=https://<keyvault-name>.vault.azure.net/
AZURE_APPCONFIG_URI=https://<appconfig-name>.azconfig.io
AZURE_REDIS_CONNECTION=<redis-host>:6380,password=<password>,ssl=True
AZURE_APPINSIGHTS_CONNECTION_STRING=InstrumentationKey=<key>

# Database (or retrieve from Key Vault)
DB_HOST=<sql-server>.database.windows.net
DB_NAME=LandTitleDB
DB_USER=<username>
DB_PASSWORD=<password>
```

### Workload Identity Setup (Recommended)
For AKS deployment, use Workload Identity instead of connection strings:

1. Create managed identity for the application
2. Grant identity access to Key Vault, App Configuration, and Redis
3. Configure service account in Kubernetes
4. Application automatically authenticates using `DefaultAzureCredential`

## Package Updates

### Security Fixes
- `Newtonsoft.Json`: 12.0.1 → 13.0.3 (CVE-2024-21907 fixed)
- `log4net`: Removed (replaced with ILogger + Application Insights)
- `NuGet.Frameworks`: Removed (not needed)
- `System.Text.Encodings.Web`: Removed (not needed)

### New Cloud-Native Packages
- `Azure.Identity` - Workload Identity support
- `Azure.Security.KeyVault.Secrets` - Key Vault integration
- `Microsoft.EntityFrameworkCore.SqlServer` - EF Core with Azure SQL
- `Microsoft.Extensions.Caching.StackExchangeRedis` - Redis caching
- `Microsoft.ApplicationInsights.AspNetCore` - Application Insights
- `Microsoft.Extensions.Http.Polly` - HTTP resilience

## Framework Migration
- **From**: .NET Framework 4.6.1 (EOL)
- **To**: .NET 8.0 (LTS, cloud-optimized)

## Deployment Readiness

### ✅ Cloud-Ready Features
- Stateless architecture (horizontal scaling ready)
- Externalized configuration (12-factor app compliant)
- Distributed caching (multi-instance support)
- Async/await patterns (high concurrency)
- Cross-platform compatibility (Linux containers)
- Structured logging (cloud monitoring)
- Connection resiliency (transient fault handling)
- Secrets management (Azure Key Vault)

### 🚀 Next Steps
1. **Containerization**: Create Dockerfile (handled separately)
2. **Kubernetes Deployment**: Create manifests (handled separately)
3. **CI/CD Pipeline**: Configure deployment pipeline (handled separately)
4. **Infrastructure**: Provision Azure resources with Terraform/Bicep (handled separately)

## Testing Locally

### Prerequisites
- .NET 8.0 SDK
- Azure CLI (for local authentication)
- Docker (for Redis locally)

### Local Development Setup
```bash
# Start Redis locally
docker run -d -p 6379:6379 redis:latest

# Set environment variables
export SERVICE_PORT=8080
export AZURE_KEYVAULT_URI=https://your-keyvault.vault.azure.net/
export AZURE_APPCONFIG_URI=https://your-appconfig.azconfig.io

# Restore packages
dotnet restore

# Run tests
dotnet test

# Build
dotnet build
```

## Architecture Compliance

### 12-Factor App Principles
- ✅ **I. Codebase**: Single codebase tracked in version control
- ✅ **II. Dependencies**: Explicitly declared via NuGet packages
- ✅ **III. Config**: Externalized to environment and Azure services
- ✅ **IV. Backing Services**: Attached resources (SQL, Redis, Key Vault)
- ✅ **V. Build, Release, Run**: Strict separation of stages
- ✅ **VI. Processes**: Stateless, share-nothing architecture
- ✅ **VII. Port Binding**: Dynamic port binding via environment
- ✅ **VIII. Concurrency**: Horizontal scaling ready
- ✅ **IX. Disposability**: Fast startup and graceful shutdown
- ✅ **X. Dev/Prod Parity**: Same backing services across environments
- ✅ **XI. Logs**: Structured logs to stdout (Application Insights)
- ✅ **XII. Admin Processes**: One-off tasks as separate processes

## Support
For issues or questions about cloud deployment, refer to Azure documentation or contact the cloud platform team.
