using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using LandTitleRegistration.Services;

namespace LandTitleRegistration.Controllers
{
    /// <summary>
    /// Handles land title registration, search, and document retrieval.
    /// Cloud-ready controller with externalized configuration and distributed state management.
    /// </summary>
    public class TitleController
    {
        private readonly TitleService _service;
        private readonly IConfiguration _configuration;
        private readonly IDistributedCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TitleController> _logger;

        // Service URLs from Azure App Configuration
        private string DocumentServiceUrl => _configuration["ServiceUrls:DocumentService"];
        private string NotificationService => _configuration["ServiceUrls:NotificationService"];
        private string LegacySearchApi => _configuration["ServiceUrls:LegacySearchApi"];

        // Storage paths from configuration (Azure Blob Storage containers)
        private string ArchiveContainer => _configuration["StoragePaths:ArchiveContainer"];
        private string TempExportContainer => _configuration["StoragePaths:TempExportContainer"];
        private string LogContainer => _configuration["StoragePaths:LogContainer"];

        public TitleController(
            TitleService service,
            IConfiguration configuration,
            IDistributedCache cache,
            IHttpClientFactory httpClientFactory,
            ILogger<TitleController> logger)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Dictionary<string, object>> RegisterTitleAsync(
            string ownerName, string parcelId,
            string propertyAddress, string titleType,
            string sessionId)
        {
            // Replace HttpContext.Session with Azure Cache for Redis distributed session
            await _cache.SetStringAsync($"session:{sessionId}:CurrentOwner", ownerName);
            await _cache.SetStringAsync($"session:{sessionId}:ActiveParcel", parcelId);
            await _cache.SetStringAsync($"session:{sessionId}:RegistrationStep", "initiated");

            var result = await _service.CreateRegistrationAsync(ownerName, parcelId, propertyAddress, titleType);

            // Store in distributed cache instead of static collection
            var cacheKey = $"title:{parcelId}";
            var serializedResult = JsonConvert.SerializeObject(result);
            await _cache.SetStringAsync(cacheKey, serializedResult, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            });

            _logger.LogInformation("Title registered for parcel {ParcelId} by owner {OwnerName}", parcelId, ownerName);

            return result;
        }

        public async Task<Dictionary<string, object>> GetTitleStatusAsync(string parcelId, string sessionId)
        {
            // Retrieve session data from distributed cache
            var sessionOwner = await _cache.GetStringAsync($"session:{sessionId}:CurrentOwner");

            var details = await _service.GetTitleByParcelAsync(parcelId);

            // Use Azure Blob Storage path instead of local file system
            var archivePath = $"{ArchiveContainer}/{parcelId}.pdf";

            return new Dictionary<string, object>
            {
                ["parcelId"] = parcelId,
                ["sessionOwner"] = sessionOwner,
                ["details"] = details,
                ["archivePath"] = archivePath
            };
        }

        public async Task<string> FetchDocumentFromServiceAsync(string docId)
        {
            // Replace synchronous HttpClient with async pattern using IHttpClientFactory
            var client = _httpClientFactory.CreateClient("DocumentService");
            
            try
            {
                var response = await client.GetAsync($"{DocumentServiceUrl}?id={docId}");
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
            var archivePath = _configuration["StoragePaths:ArchiveContainer"];
            
            if (string.IsNullOrEmpty(archivePath))
            {
                _logger.LogWarning("Archive path not configured, using default");
                archivePath = "archives";
            }

            return archivePath;
        }

        public async Task<Dictionary<string, object>> ExportTitleReportAsync(string month, string year)
        {
            // Use Azure Blob Storage container instead of local file path
            var blobPath = $"{TempExportContainer}/report_{month}_{year}.xlsx";
            
            var reportData = await _service.GenerateMonthlyReportAsync(month, year);

            return new Dictionary<string, object>
            {
                ["exportPath"] = blobPath,
                ["logPath"] = LogContainer,
                ["result"] = reportData
            };
        }
    }
}
