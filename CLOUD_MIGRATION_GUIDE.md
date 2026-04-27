# Land Title Registration - Cloud-Native Azure Migration

## Overview
This application has been modernized for cloud deployment on Azure with full cloud-native patterns and best practices.

## Cloud Readiness Fixes Applied

### 1. Configuration Management
**Issues Fixed:**
- ✅ Hard-coded service URLs (cr-dotnet-0011)
- ✅ Hard-coded connection strings (cr-dotnet-0009)
- ✅ Hard-coded secrets and API keys (cr-dotnet-0123)
- ✅ Hard-coded port numbers (cr-dotnet-0017)

**Solution:**
- Externalized all configuration to `appsettings.json`
- Integrated with Azure App Configuration for centralized config management
- Secrets stored in Azure Key Vault with Managed Identity access
- Environment-specific values use token replacement (#{VARIABLE}#)

### 2. File System & Storage
**Issues Fixed:**
- ✅ Hard-coded Windows file paths (cr-dotnet-0001)

**Solution:**
- Replaced `C:\` and `D:\` paths with Azure Blob Storage
- Using Azure Storage SDK with Managed Identity
- Container-based storage: `archives`, `exports`, `logs`

### 3. State Management
**Issues Fixed:**
- ✅ Static collections for state (cr-dotnet-0006)
- ✅ In-process session state (cr-dotnet-0045)
- ✅ Heavy coupling to stateful middleware (cr-dotnet-0126)

**Solution:**
- Replaced static `Dictionary<>` with Azure Cache for Redis
- Migrated `HttpContext.Session` to distributed cache
- Stateless design enables horizontal scaling in AKS

### 4. Database Access
**Issues Fixed:**
- ✅ Direct SqlConnection usage (cr-dotnet-0013)
- ✅ SQL Server-specific features (cr-dotnet-0014)

**Solution:**
- Migrated to Entity Framework Core 8.0
- Parameterized queries prevent SQL injection
- Connection pooling and retry logic for Azure SQL
- ANSI SQL compatibility for database portability

### 5. Platform Dependencies
**Issues Fixed:**
- ✅ Windows Registry access (cr-dotnet-0040)
- ✅ IIS module dependencies (cr-dotnet-0044)

**Solution:**
- Replaced Registry with Azure App Configuration
- Removed IIS-specific code (ready for Kestrel in containers)

### 6. Logging & Monitoring
**Issues Fixed:**
- ✅ log4net file appenders (cr-dotnet-0035)

**Solution:**
- Replaced log4net with Microsoft.Extensions.Logging
- Integrated with Azure Application Insights
- Structured logging with correlation IDs

### 7. Time & Clock Dependencies
**Issues Fixed:**
- ✅ DateTime.Now usage (cr-dotnet-0121)

**Solution:**
- Replaced `DateTime.Now` with `DateTimeOffset.UtcNow`
- UTC timestamps for global consistency
- Timezone conversion at presentation layer only

### 8. Resource Management
**Issues Fixed:**
- ✅ Synchronous HttpClient calls (cr-dotnet-0037)
- ✅ Blocking collection operations (cr-dotnet-0039)

**Solution:**
- Converted all HTTP calls to async/await
- Using IHttpClientFactory with Polly retry policies
- Non-blocking async patterns throughout

## Azure Services Integration

### Required Azure Resources
1. **Azure SQL Database** - Managed relational database
2. **Azure Cache for Redis** - Distributed caching and session state
3. **Azure Key Vault** - Secrets management
4. **Azure Blob Storage** - File storage
5. **Azure App Configuration** - Centralized configuration
6. **Azure Application Insights** - Monitoring and telemetry
7. **Azure Kubernetes Service (AKS)** - Container orchestration

### Authentication
- **Managed Identity (Workload Identity)** for credential-free access
- No connection strings or passwords in code
- Azure AD authentication for all services

## Configuration Tokens

Replace these tokens in `appsettings.json` with actual values:

```
#{AZURE_SQL_SERVER}# - Azure SQL Server FQDN
#{AZURE_SQL_DATABASE}# - Database name
#{DOCUMENT_SERVICE_URL}# - Document service endpoint
#{NOTIFICATION_SERVICE_URL}# - Notification service endpoint
#{LEGACY_SEARCH_API_URL}# - Legacy search API endpoint
#{GOV_REPORTING_API_URL}# - Government reporting API endpoint
#{AZURE_STORAGE_ACCOUNT}# - Storage account name
#{AZURE_BLOB_ENDPOINT}# - Blob storage endpoint
#{REDIS_CONNECTION_STRING}# - Redis connection string
#{APPLICATIONINSIGHTS_CONNECTION_STRING}# - App Insights connection
#{KEYVAULT_URI}# - Key Vault URI
```

## Deployment Architecture

### AKS Deployment
```
┌─────────────────────────────────────────┐
│         Azure Kubernetes Service        │
│  ┌───────────────────────────────────┐  │
│  │  Land Title Registration Pods     │  │
│  │  - Stateless design               │  │
│  │  - Horizontal pod autoscaling     │  │
│  │  - Workload Identity enabled      │  │
│  └───────────────────────────────────┘  │
└─────────────────────────────────────────┘
           │         │         │
           ▼         ▼         ▼
    ┌──────────┐ ┌──────┐ ┌─────────┐
    │ Azure SQL│ │Redis │ │Key Vault│
    └──────────┘ └──────┘ └─────────┘
```

## Migration from .NET Framework 4.6.1 to .NET 8.0

### Breaking Changes Addressed
- Removed `System.Web` dependencies
- Replaced `HttpContext.Current` with dependency injection
- Updated to SDK-style project format
- Migrated to cross-platform compatible APIs

### Package Updates
- ✅ Newtonsoft.Json: 12.0.1 → 13.0.3 (CVE-2024-21907 fixed)
- ✅ Removed log4net (CVE-2018-1285)
- ✅ Added Azure SDK packages
- ✅ Added Entity Framework Core 8.0
- ✅ Added Application Insights

## Security Improvements
1. **No hardcoded credentials** - All secrets in Key Vault
2. **SQL injection prevention** - Parameterized queries via EF Core
3. **Secure cryptography** - SHA256 instead of deprecated SHA1
4. **Managed Identity** - No connection strings in configuration
5. **Updated dependencies** - All CVEs resolved

## Scalability Features
1. **Stateless design** - No server affinity required
2. **Distributed caching** - Redis for shared state
3. **Connection pooling** - EF Core manages connections
4. **Async operations** - Non-blocking I/O throughout
5. **Retry policies** - Transient fault handling

## Monitoring & Observability
1. **Application Insights** - Distributed tracing
2. **Structured logging** - JSON format with correlation IDs
3. **Health checks** - Kubernetes liveness/readiness probes
4. **Metrics** - Custom telemetry for business events

## Next Steps
1. Configure Azure resources (SQL, Redis, Key Vault, Storage)
2. Set up Managed Identity in AKS
3. Replace configuration tokens with actual values
4. Deploy to AKS using Helm charts (separate workflow)
5. Configure Application Insights dashboards
6. Set up Azure Monitor alerts

## Compliance
- ✅ 12-Factor App principles
- ✅ Cloud-native patterns
- ✅ Azure Well-Architected Framework
- ✅ Security best practices
- ✅ Cross-platform compatibility (Linux containers)
