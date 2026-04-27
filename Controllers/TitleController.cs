using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace LandTitleRegistration.Controllers
{
    /// <summary>
    /// Handles land title registration, search, and document retrieval.
    /// Cloud-ready controller with externalized configuration and distributed caching.
    /// </summary>
    public class TitleController
    {
        private readonly TitleService _service;
        private readonly IConfiguration _configuration;
        private readonly IDistributedCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TitleController> _logger;

        public TitleController(
            TitleService service,
            IConfiguration configuration,
            IDistributedCache cache,
            IHttpClientFactory httpClientFactory,
            ILogger<TitleController> logger)
        {
            _service = service;
            _configuration = configuration;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<Dictionary<string, object>> RegisterTitleAsync(
            string ownerName, string parcelId,
            string propertyAddress, string titleType,
            string sessionId)
        {
            // Store session data in distributed cache (Azure Cache for Redis)
            var sessionKey = $"session:{sessionId}";
            var sessionData = new Dictionary<string, string>
            {
                ["CurrentOwner"] = ownerName,
                ["ActiveParcel"] = parcelId,
                ["RegistrationStep"] = "initiated"
            };

            var serializedSession = JsonConvert.SerializeObject(sessionData);
            await _cache.SetStringAsync(sessionKey, serializedSession, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            });

            var result = await _service.CreateRegistrationAsync(ownerName, parcelId, propertyAddress, titleType);

            // Store in distributed cache instead of static collection
            var cacheKey = $"title:{parcelId}";
            var serializedResult = JsonConvert.SerializeObject(result);
            await _cache.SetStringAsync(cacheKey, serializedResult, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            });

            return result;
        }

        public async Task<Dictionary<string, object>> GetTitleStatusAsync(string parcelId, string sessionId)
        {
            // Retrieve session data from distributed cache
            var sessionKey = $"session:{sessionId}";
            var sessionDataJson = await _cache.GetStringAsync(sessionKey);
            string sessionOwner = null;

            if (!string.IsNullOrEmpty(sessionDataJson))
            {
                var sessionData = JsonConvert.DeserializeObject<Dictionary<string, string>>(sessionDataJson);
                sessionOwner = sessionData.ContainsKey("CurrentOwner") ? sessionData["CurrentOwner"] : null;
            }

            // Get archive path from configuration
            var archiveBasePath = _configuration["StoragePaths:ArchiveBasePath"];
            var archivePath = Path.Combine(archiveBasePath, $"{parcelId}.pdf");

            return new Dictionary<string, object>
            {
                ["parcelId"] = parcelId,
                ["sessionOwner"] = sessionOwner,
                ["details"] = await _service.GetTitleByParcelAsync(parcelId),
                ["archivePath"] = archivePath
            };
        }

        public async Task<string> FetchDocumentFromServiceAsync(string docId, CancellationToken cancellationToken = default)
        {
            // Use async HttpClient with proper cancellation token support
            var documentServiceUrl = _configuration["ServiceUrls:DocumentService"];
            var client = _httpClientFactory.CreateClient("DocumentService");

            var response = await client.GetAsync($"{documentServiceUrl}?id={docId}", cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public string GetSystemArchivePath()
        {
            // Replace Windows Registry with Azure App Configuration
            var archivePath = _configuration["StoragePaths:ArchiveBasePath"];
            return archivePath ?? "/app/data/archives";
        }

        public Dictionary<string, object> ExportTitleReport(string month, string year)
        {
            // Get paths from configuration instead of hard-coded Windows paths
            var tempExportPath = _configuration["StoragePaths:TempExportPath"];
            var logBasePath = _configuration["StoragePaths:LogBasePath"];
            var port = _configuration["ApplicationSettings:Port"];

            var filePath = Path.Combine(tempExportPath, $"report_{month}_{year}.xlsx");

            return new Dictionary<string, object>
            {
                ["exportPath"] = filePath,
                ["port"] = port,
                ["logPath"] = logBasePath,
                ["result"] = _service.GenerateMonthlyReport(month, year)
            };
        }
    }
}
