using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO;

namespace LandTitleRegistration.Controllers
{
    /// <summary>
    /// Handles land title registration, search, and document retrieval.
    /// Cloud-ready implementation using Azure services.
    /// </summary>
    public class TitleController
    {
        private readonly TitleService _service;
        private readonly IConfiguration _configuration;
        private readonly IDistributedCache _cache;
        private readonly ILogger<TitleController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        // Externalized configuration - loaded from Azure App Configuration
        private string DocumentServiceUrl => _configuration["ServiceUrls:DocumentService"];
        private string NotificationService => _configuration["ServiceUrls:NotificationService"];
        private string LegacySearchApi => _configuration["ServiceUrls:LegacySearchApi"];

        // Cloud storage paths - using Azure Blob Storage containers
        private string ArchiveContainer => _configuration["StoragePaths:ArchiveContainer"] ?? "archives";
        private string TempExportContainer => _configuration["StoragePaths:TempExportContainer"] ?? "exports";
        private string LogContainer => _configuration["StoragePaths:LogContainer"] ?? "logs";

        public TitleController(
            TitleService service,
            IConfiguration configuration,
            IDistributedCache cache,
            ILogger<TitleController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task<Dictionary<string, object>> RegisterTitle(
            string ownerName, string parcelId,
            string propertyAddress, string titleType)
        {
            // Store session data in distributed Redis cache instead of in-process session
            var sessionData = new Dictionary<string, string>
            {
                ["CurrentOwner"] = ownerName,
                ["ActiveParcel"] = parcelId,
                ["RegistrationStep"] = "initiated"
            };

            var sessionKey = $"session:{parcelId}";
            await _cache.SetStringAsync(sessionKey, JsonConvert.SerializeObject(sessionData));

            var result = await _service.CreateRegistration(ownerName, parcelId, propertyAddress, titleType);

            // Store in distributed cache instead of static collection
            var cacheKey = $"title:{parcelId}";
            await _cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(result));

            _logger.LogInformation("Title registered for parcel {ParcelId}", parcelId);

            return result;
        }

        public async Task<Dictionary<string, object>> GetTitleStatus(string parcelId)
        {
            // Retrieve session data from distributed cache
            var sessionKey = $"session:{parcelId}";
            var sessionDataJson = await _cache.GetStringAsync(sessionKey);
            var sessionData = string.IsNullOrEmpty(sessionDataJson) 
                ? new Dictionary<string, string>() 
                : JsonConvert.DeserializeObject<Dictionary<string, string>>(sessionDataJson);

            var sessionOwner = sessionData.ContainsKey("CurrentOwner") ? sessionData["CurrentOwner"] : null;

            var details = await _service.GetTitleByParcel(parcelId);

            return new Dictionary<string, object>
            {
                ["parcelId"] = parcelId,
                ["sessionOwner"] = sessionOwner,
                ["details"] = details,
                ["archivePath"] = Path.Combine(ArchiveContainer, $"{parcelId}.pdf")
            };
        }

        public async Task<string> FetchDocumentFromService(string docId)
        {
            // Use async HttpClient with IHttpClientFactory for proper connection pooling
            var client = _httpClientFactory.CreateClient();
            
            try
            {
                var response = await client.GetAsync($"{DocumentServiceUrl}?id={docId}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch document {DocId}", docId);
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

        public async Task<Dictionary<string, object>> ExportTitleReport(string month, string year)
        {
            var fileName = $"report_{month}_{year}.xlsx";
            var filePath = Path.Combine(TempExportContainer, fileName);
            
            var report = await _service.GenerateMonthlyReport(month, year);

            return new Dictionary<string, object>
            {
                ["exportPath"] = filePath,
                ["result"] = report
            };
        }
    }
}
