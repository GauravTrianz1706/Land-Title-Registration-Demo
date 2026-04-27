using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Storage.Blobs;
using Azure.Identity;

namespace LandTitleRegistration.Controllers
{
    /// <summary>
    /// Handles land title registration, search, and document retrieval.
    /// Cloud-ready implementation with externalized configuration and distributed state management.
    /// </summary>
    public class TitleController
    {
        private readonly TitleService _service;
        private readonly IConfiguration _configuration;
        private readonly IDistributedCache _cache;
        private readonly ILogger<TitleController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly BlobServiceClient _blobServiceClient;

        // Service URLs from Azure App Configuration
        private string DocumentServiceUrl => _configuration["ServiceUrls:DocumentService"];
        private string NotificationService => _configuration["ServiceUrls:NotificationService"];
        private string LegacySearchApi => _configuration["ServiceUrls:LegacySearchApi"];

        // Azure Blob Storage containers (replacing hard-coded Windows paths)
        private string ArchiveContainer => _configuration["StoragePaths:ArchiveContainer"];
        private string TempExportContainer => _configuration["StoragePaths:TempExportContainer"];

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

            // Initialize Azure Blob Storage client with Managed Identity
            var blobEndpoint = _configuration["AzureStorage:BlobEndpoint"];
            if (!string.IsNullOrEmpty(blobEndpoint))
            {
                _blobServiceClient = new BlobServiceClient(
                    new Uri(blobEndpoint),
                    new DefaultAzureCredential());
            }
        }

        public async Task<Dictionary<string, object>> RegisterTitleAsync(
            string ownerName, string parcelId,
            string propertyAddress, string titleType,
            string sessionId)
        {
            // Replace HttpContext.Session with distributed Redis cache
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

            _logger.LogInformation("Title registered for parcel {ParcelId}", parcelId);

            return result;
        }

        public async Task<Dictionary<string, object>> GetTitleStatusAsync(string parcelId, string sessionId)
        {
            // Retrieve session data from distributed cache
            var sessionOwner = await _cache.GetStringAsync($"session:{sessionId}:CurrentOwner");

            var details = await _service.GetTitleByParcelAsync(parcelId);

            // Generate Azure Blob Storage URL instead of local file path
            string archiveBlobUrl = null;
            if (_blobServiceClient != null)
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(ArchiveContainer);
                var blobClient = containerClient.GetBlobClient($"{parcelId}.pdf");
                archiveBlobUrl = blobClient.Uri.ToString();
            }

            return new Dictionary<string, object>
            {
                ["parcelId"] = parcelId,
                ["sessionOwner"] = sessionOwner,
                ["details"] = details,
                ["archiveBlobUrl"] = archiveBlobUrl
            };
        }

        public async Task<string> FetchDocumentFromServiceAsync(string docId)
        {
            // Use async HttpClient with IHttpClientFactory (no blocking calls)
            var client = _httpClientFactory.CreateClient("DocumentService");
            
            try
            {
                var response = await client.GetAsync($"{DocumentServiceUrl}?id={docId}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to fetch document {DocumentId}", docId);
                throw;
            }
        }

        public async Task<string> GetSystemArchivePathAsync()
        {
            // Replace Windows Registry with Azure App Configuration
            var archivePath = _configuration["StoragePaths:ArchiveContainer"];
            
            if (string.IsNullOrEmpty(archivePath))
            {
                _logger.LogWarning("Archive container not configured, using default");
                archivePath = "archives";
            }

            return archivePath;
        }

        public async Task<Dictionary<string, object>> ExportTitleReportAsync(string month, string year)
        {
            // Generate blob path instead of local file path
            string blobPath = $"report_{month}_{year}.xlsx";
            string blobUrl = null;

            if (_blobServiceClient != null)
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(TempExportContainer);
                var blobClient = containerClient.GetBlobClient(blobPath);
                blobUrl = blobClient.Uri.ToString();
            }

            var reportData = await _service.GenerateMonthlyReportAsync(month, year);

            return new Dictionary<string, object>
            {
                ["exportBlobUrl"] = blobUrl,
                ["exportPath"] = blobPath,
                ["result"] = reportData
            };
        }
    }
}
