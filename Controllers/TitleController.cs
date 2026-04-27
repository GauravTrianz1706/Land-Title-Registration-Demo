using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System.Text;

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
        private readonly ILogger<TitleController> _logger;
        private readonly IDistributedCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;

        // Configuration-driven service URLs (replaced hard-coded URLs)
        private string DocumentServiceUrl => _configuration["ServiceUrls:DocumentService"];
        private string NotificationService => _configuration["ServiceUrls:NotificationService"];
        private string LegacySearchApi => _configuration["ServiceUrls:LegacySearchApi"];

        // Configuration-driven paths (replaced hard-coded Windows paths)
        private string ArchivePath => _configuration["StoragePaths:ArchiveBasePath"];
        private string TempExport => _configuration["StoragePaths:TempExportPath"];

        public TitleController(
            TitleService service,
            IConfiguration configuration,
            ILogger<TitleController> logger,
            IDistributedCache cache,
            IHttpClientFactory httpClientFactory)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task<Dictionary<string, object>> RegisterTitleAsync(
            string ownerName, string parcelId,
            string propertyAddress, string titleType,
            string sessionId)
        {
            // Replace HttpContext.Session with distributed cache (Azure Redis)
            await _cache.SetStringAsync($"session:{sessionId}:CurrentOwner", ownerName,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
            await _cache.SetStringAsync($"session:{sessionId}:ActiveParcel", parcelId,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
            await _cache.SetStringAsync($"session:{sessionId}:RegistrationStep", "initiated",
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });

            var result = await _service.CreateRegistrationAsync(ownerName, parcelId, propertyAddress, titleType);

            // Replace static collection with distributed cache
            var cacheKey = $"title:{parcelId}";
            var serializedResult = JsonConvert.SerializeObject(result);
            await _cache.SetStringAsync(cacheKey, serializedResult,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) });

            _logger.LogInformation("Title registered for parcel {ParcelId} by owner {OwnerName}", parcelId, ownerName);

            return result;
        }

        public async Task<Dictionary<string, object>> GetTitleStatusAsync(string parcelId, string sessionId)
        {
            // Retrieve session data from distributed cache
            var sessionOwner = await _cache.GetStringAsync($"session:{sessionId}:CurrentOwner");

            var details = await _service.GetTitleByParcelAsync(parcelId);

            // Use Path.Combine for cross-platform compatibility
            var archivePath = System.IO.Path.Combine(ArchivePath, $"{parcelId}.pdf");

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
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"{DocumentServiceUrl}?id={docId}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public string GetSystemArchivePath()
        {
            // Replace Windows Registry access with Azure App Configuration
            var archivePath = _configuration["StoragePaths:ArchiveBasePath"];
            
            if (string.IsNullOrEmpty(archivePath))
            {
                _logger.LogWarning("Archive path not configured, using default");
                archivePath = "/app/data/archives"; // Default for Linux containers
            }

            return archivePath;
        }

        public async Task<Dictionary<string, object>> ExportTitleReportAsync(string month, string year)
        {
            // Use cross-platform path construction
            var filePath = System.IO.Path.Combine(TempExport, $"report_{month}_{year}.xlsx");
            
            var result = await _service.GenerateMonthlyReportAsync(month, year);

            return new Dictionary<string, object>
            {
                ["exportPath"] = filePath,
                ["result"] = result
            };
        }
    }
}
