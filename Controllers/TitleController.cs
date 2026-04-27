using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace LandTitleRegistration.Controllers
{
    /// <summary>
    /// Handles land title registration, search, and document retrieval.
    /// Cloud-ready implementation with externalized configuration and distributed caching.
    /// </summary>
    public class TitleController
    {
        private readonly TitleService _service;
        private readonly IConfiguration _configuration;
        private readonly IDistributedCache _distributedCache;
        private readonly ILogger<TitleController> _logger;
        private readonly HttpClient _httpClient;

        // Service URLs from Azure App Configuration
        private string DocumentServiceUrl => _configuration["ServiceUrls:DocumentService"];
        private string NotificationService => _configuration["ServiceUrls:NotificationService"];
        private string LegacySearchApi => _configuration["ServiceUrls:LegacySearchApi"];

        // File paths from configuration - cross-platform compatible
        private string ArchivePath => _configuration["StoragePaths:ArchiveBasePath"];
        private string TempExport => _configuration["StoragePaths:TempExportPath"];
        private string LogPath => _configuration["StoragePaths:LogBasePath"];

        // Port from environment variable for dynamic binding
        private int ServicePort => int.TryParse(Environment.GetEnvironmentVariable("SERVICE_PORT"), out var port) ? port : 8080;

        public TitleController(
            TitleService service,
            IConfiguration configuration,
            IDistributedCache distributedCache,
            ILogger<TitleController> logger,
            HttpClient httpClient)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<Dictionary<string, object>> RegisterTitle(
            string ownerName, string parcelId,
            string propertyAddress, string titleType,
            string sessionId)
        {
            // Replace HttpContext.Session with distributed cache (Azure Redis)
            await _distributedCache.SetStringAsync($"session:{sessionId}:CurrentOwner", ownerName);
            await _distributedCache.SetStringAsync($"session:{sessionId}:ActiveParcel", parcelId);
            await _distributedCache.SetStringAsync($"session:{sessionId}:RegistrationStep", "initiated");

            var result = await _service.CreateRegistration(ownerName, parcelId, propertyAddress, titleType);

            // Store in distributed cache instead of static collection
            await StoreTitleInCache(parcelId, result);

            _logger.LogInformation("Title registered for parcel {ParcelId} by owner {OwnerName}", parcelId, ownerName);

            return result;
        }

        public async Task<Dictionary<string, object>> GetTitleStatus(string parcelId, string sessionId)
        {
            // Retrieve session data from distributed cache
            var sessionOwner = await _distributedCache.GetStringAsync($"session:{sessionId}:CurrentOwner");

            var details = await _service.GetTitleByParcel(parcelId);

            // Use cross-platform path combination
            var archivePath = Path.Combine(ArchivePath, $"{parcelId}.pdf");

            return new Dictionary<string, object>
            {
                ["parcelId"] = parcelId,
                ["sessionOwner"] = sessionOwner,
                ["details"] = details,
                ["archivePath"] = archivePath
            };
        }

        public async Task<string> FetchDocumentFromService(string docId)
        {
            // Replace synchronous HttpClient with async/await pattern
            try
            {
                var response = await _httpClient.GetAsync($"{DocumentServiceUrl}?id={docId}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to fetch document {DocId} from service", docId);
                throw;
            }
        }

        public string GetSystemArchivePath()
        {
            // Replace Windows Registry access with Azure App Configuration
            var archivePathFromConfig = _configuration["StoragePaths:ArchiveBasePath"];
            
            if (string.IsNullOrEmpty(archivePathFromConfig))
            {
                _logger.LogWarning("Archive path not configured, using default");
                return "/app/data/archives";
            }

            return archivePathFromConfig;
        }

        public async Task<Dictionary<string, object>> ExportTitleReport(string month, string year)
        {
            // Use cross-platform path combination
            string filePath = Path.Combine(TempExport, $"report_{month}_{year}.xlsx");
            
            var reportResult = await _service.GenerateMonthlyReport(month, year);

            return new Dictionary<string, object>
            {
                ["exportPath"] = filePath,
                ["port"] = ServicePort, // Dynamic port from environment
                ["logPath"] = LogPath,
                ["result"] = reportResult
            };
        }

        // Helper method to store title in distributed cache
        private async Task StoreTitleInCache(string key, Dictionary<string, object> value)
        {
            var serialized = JsonConvert.SerializeObject(value);
            await _distributedCache.SetStringAsync($"title:{key}", serialized, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            });
        }

        // Helper method to retrieve title from distributed cache
        private async Task<Dictionary<string, object>> GetTitleFromCache(string key)
        {
            var serialized = await _distributedCache.GetStringAsync($"title:{key}");
            if (string.IsNullOrEmpty(serialized))
                return null;

            return JsonConvert.DeserializeObject<Dictionary<string, object>>(serialized);
        }
    }
}
