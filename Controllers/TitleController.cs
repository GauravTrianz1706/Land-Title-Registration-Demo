using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LandTitleRegistration.Controllers
{
    /// <summary>
    /// Handles land title registration, search, and document retrieval.
    /// Cloud-ready: uses Azure App Configuration for URLs/paths/ports,
    /// IDistributedCache (Azure Cache for Redis) for session and static state,
    /// ASP.NET Core ILogger for structured logging, and async HttpClient.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TitleController : ControllerBase
    {
        private readonly TitleService _service;
        private readonly IConfiguration _configuration;
        private readonly IDistributedCache _cache;
        private readonly ILogger<TitleController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        // URLs, paths, and port are now read from Azure App Configuration / environment variables
        // (blocker-1, blocker-2, blocker-3: Hard-coded Service URLs → Azure App Configuration)
        // (blocker-5: Hard-coded Port Numbers → environment variable via IConfiguration)
        // (blocker-6, blocker-7, blocker-8: Hard-coded File Paths → IConfiguration + Path.Combine)
        private string DocumentServiceUrl  => _configuration["Services:DocumentServiceUrl"]
                                              ?? Environment.GetEnvironmentVariable("DOCUMENT_SERVICE_URL")
                                              ?? "http://document-service/fetch";
        private string NotificationService => _configuration["Services:NotificationServiceUrl"]
                                              ?? Environment.GetEnvironmentVariable("NOTIFICATION_SERVICE_URL")
                                              ?? "http://notification-service/send";
        private string LegacySearchApi    => _configuration["Services:LegacySearchApiUrl"]
                                              ?? Environment.GetEnvironmentVariable("LEGACY_SEARCH_API_URL")
                                              ?? "http://legacy-search-service/search/titles";

        // Hard-coded Windows paths replaced with cross-platform Path.Combine + configuration
        // (blocker-6, blocker-7, blocker-8: cr-dotnet-0001)
        private string ArchivePath  => Path.Combine(
            _configuration["Storage:ArchiveBasePath"]
            ?? Environment.GetEnvironmentVariable("ARCHIVE_BASE_PATH")
            ?? Path.GetTempPath(), "Archives");

        private string TempExport   => Path.Combine(
            _configuration["Storage:TempExportPath"]
            ?? Environment.GetEnvironmentVariable("TEMP_EXPORT_PATH")
            ?? Path.GetTempPath(), "exports");

        private string LogPath      => Path.Combine(
            _configuration["Storage:LogBasePath"]
            ?? Environment.GetEnvironmentVariable("LOG_BASE_PATH")
            ?? Path.GetTempPath(), "LandTitle", "registration.log");

        // Port read from configuration instead of hard-coded value (blocker-5: cr-dotnet-0017)
        private int ServicePort => int.TryParse(
            _configuration["Services:Port"]
            ?? Environment.GetEnvironmentVariable("SERVICE_PORT"), out var p) ? p : 8080;

        public TitleController(
            TitleService service,
            IConfiguration configuration,
            IDistributedCache cache,
            ILogger<TitleController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _service           = service;
            _configuration     = configuration;
            _cache             = cache;
            _logger            = logger;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("register")]
        public async Task<Dictionary<string, object>> RegisterTitle(
            string ownerName, string parcelId,
            string propertyAddress, string titleType)
        {
            // Replace HttpSessionState (InProc) with Azure Cache for Redis via IDistributedCache
            // (blocker-16, blocker-17, blocker-18: cr-dotnet-0045 Session State Provider)
            // (blocker-23, blocker-24, blocker-25: cr-dotnet-0126 Heavy Coupling to Stateful Middleware)
            var sessionOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };
            await _cache.SetStringAsync("session:CurrentOwner:"     + parcelId, ownerName,          sessionOptions);
            await _cache.SetStringAsync("session:ActiveParcel:"     + parcelId, parcelId,            sessionOptions);
            await _cache.SetStringAsync("session:RegistrationStep:" + parcelId, "initiated",         sessionOptions);

            var result = _service.CreateRegistration(ownerName, parcelId, propertyAddress, titleType);

            // Replace static Dictionary (TitleCache) with Azure Cache for Redis
            // (blocker-9: cr-dotnet-0006 Static Collections for State)
            var serialized = JsonSerializer.Serialize(result);
            await _cache.SetStringAsync("titlecache:" + parcelId, serialized, sessionOptions);

            _logger.LogInformation("Title registered for parcel {ParcelId} by owner {OwnerName}", parcelId, ownerName);
            return result;
        }

        [HttpGet("status/{parcelId}")]
        public async Task<Dictionary<string, object>> GetTitleStatus(string parcelId)
        {
            // Replace HttpSessionState with IDistributedCache (blocker-19: cr-dotnet-0045)
            var sessionOwner = await _cache.GetStringAsync("session:CurrentOwner:" + parcelId);

            return new Dictionary<string, object>
            {
                ["parcelId"]     = parcelId,
                ["sessionOwner"] = sessionOwner ?? string.Empty,
                ["details"]      = _service.GetTitleByParcel(parcelId),
                // Path.Combine used for cross-platform compatibility (blocker-6: cr-dotnet-0001)
                ["archivePath"]  = Path.Combine(ArchivePath, parcelId + ".pdf")
            };
        }

        [HttpGet("document/{docId}")]
        public async Task<string> FetchDocumentFromService(string docId)
        {
            // Replace synchronous .GetAwaiter().GetResult() with proper async/await
            // (blocker-26: cr-dotnet-0037 Synchronous HttpClient)
            // Use IHttpClientFactory for proper HttpClient lifecycle management
            var client   = _httpClientFactory.CreateClient("DocumentService");
            var response = await client.GetAsync(DocumentServiceUrl + "?id=" + Uri.EscapeDataString(docId));
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        [HttpGet("archive-path")]
        public string GetSystemArchivePath()
        {
            // Replace Windows Registry access with Azure App Configuration
            // (blocker-13: cr-dotnet-0040 Registry Access)
            var configuredPath = _configuration["Storage:ArchiveBasePath"]
                                 ?? Environment.GetEnvironmentVariable("ARCHIVE_BASE_PATH");
            _logger.LogInformation("Archive path resolved from Azure App Configuration: {Path}", configuredPath);
            return configuredPath ?? ArchivePath;
        }

        [HttpGet("export")]
        public Dictionary<string, object> ExportTitleReport(string month, string year)
        {
            // Paths and port now come from IConfiguration (blocker-6/7/8, blocker-5)
            string filePath = Path.Combine(TempExport, $"report_{month}_{year}.xlsx");
            return new Dictionary<string, object>
            {
                ["exportPath"] = filePath,
                ["port"]       = ServicePort,
                ["logPath"]    = LogPath,
                ["result"]     = _service.GenerateMonthlyReport(month, year)
            };
        }

        [HttpPost("add-nominee")]
        public async Task<Dictionary<string, object>> AddNominee(
            string titleRef, string nomineeName, string relationship)
        {
            // Retrieve caller identity from session cache
            // (follows existing pattern of session state stored in IDistributedCache)
            var callerOwnerName = await _cache.GetStringAsync("session:CurrentOwner:" + titleRef);

            if (string.IsNullOrEmpty(callerOwnerName))
            {
                _logger.LogWarning(
                    "Add nominee failed: No session owner found for TitleRef {TitleRef}", titleRef);
                return new Dictionary<string, object>
                {
                    ["success"] = false,
                    ["message"] = "Session expired or invalid. Please authenticate as the registered owner."
                };
            }

            return _service.AddNominee(titleRef, nomineeName, relationship, callerOwnerName);
        }
    }

    // TitleCache static class removed — replaced by Azure Cache for Redis (IDistributedCache)
    // (blocker-9: cr-dotnet-0006 Static Collections for State)
    // All cache operations are now performed via IDistributedCache injected into TitleController.
}
