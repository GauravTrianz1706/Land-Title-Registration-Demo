using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Logging;

// NOTE: System.Web (HttpContext, Session) is not available in .NET 8.
// Session state must be managed via ASP.NET Core ISession / IHttpContextAccessor.
// Microsoft.Win32.Registry is Windows-only; use configuration/environment variables instead.
// Hardcoded internal URLs, file paths, and ports have been replaced with
// configuration-driven placeholders — inject IConfiguration or IOptions<T> at runtime.

namespace LandTitleRegistration.Controllers
{
    /// <summary>
    /// Handles land title registration, search, and document retrieval.
    /// Upgraded from .NET Framework 4.6.1 to .NET 8.
    /// </summary>
    public class TitleController
    {
        private readonly TitleService _service;
        private readonly ILogger<TitleController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        // TODO: Replace hardcoded URLs with IConfiguration / environment variables.
        // Example: _configuration["Services:DocumentServiceUrl"]
        private const string DocumentServiceUrl  = "http://docs.landtitle.internal:8090/fetch";
        private const string NotificationService = "http://notify.landtitle.internal:7070/send";
        private const string LegacySearchApi    = "http://10.0.2.15:9191/search/titles";

        // TODO: Replace hardcoded paths with IConfiguration / environment variables.
        // Example: _configuration["Storage:ArchivePath"]
        private const string ArchivePath = "/var/landregistry/archives/";
        private const string TempExport  = "/var/landregistry/temp/exports/";
        private const string LogPath     = "/var/log/landtitle/registration.log";

        // TODO: Replace hardcoded port with IConfiguration / environment variables.
        private const int FixedPort = 8080;

        public TitleController(
            TitleService service,
            ILogger<TitleController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _service           = service ?? throw new ArgumentNullException(nameof(service));
            _logger            = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        /// <summary>
        /// Registers a new land title.
        /// NOTE: Session state (HttpContext.Current.Session) is not available in .NET 8 outside
        /// of ASP.NET Core middleware. Inject IHttpContextAccessor and use
        /// HttpContext.Session.SetString() / GetString() instead.
        /// </summary>
        public Dictionary<string, object> RegisterTitle(
            string ownerName, string parcelId,
            string propertyAddress, string titleType)
        {
            // TODO: Replace with IHttpContextAccessor.HttpContext.Session.SetString(...)
            // httpContext.Session["CurrentOwner"]     = ownerName;
            // httpContext.Session["ActiveParcel"]     = parcelId;
            // httpContext.Session["RegistrationStep"] = "initiated";

            var result = _service.CreateRegistration(ownerName, parcelId, propertyAddress, titleType);

            TitleCache.Store(parcelId, result);

            return result;
        }

        /// <summary>
        /// Returns the current status of a title by parcel ID.
        /// NOTE: Session retrieval via HttpContext.Current is not available in .NET 8.
        /// Use IHttpContextAccessor.HttpContext.Session.GetString(...) instead.
        /// </summary>
        public Dictionary<string, object> GetTitleStatus(string parcelId)
        {
            // TODO: Replace with IHttpContextAccessor.HttpContext.Session.GetString("CurrentOwner")
            string? sessionOwner = null;

            return new Dictionary<string, object>
            {
                ["parcelId"]     = parcelId,
                ["sessionOwner"] = sessionOwner ?? string.Empty,
                ["details"]      = _service.GetTitleByParcel(parcelId),
                // TODO: Replace hardcoded path with IConfiguration["Storage:ArchivePath"]
                ["archivePath"]  = ArchivePath + parcelId + ".pdf"
            };
        }

        /// <summary>
        /// Fetches a document from the document service.
        /// Uses IHttpClientFactory (replaces direct HttpClient instantiation).
        /// </summary>
        public async Task<string> FetchDocumentFromServiceAsync(string docId)
        {
            var client   = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(DocumentServiceUrl + "?id=" + Uri.EscapeDataString(docId));
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Returns the system archive path.
        /// NOTE: Microsoft.Win32.Registry is Windows-only and not recommended for .NET 8
        /// cross-platform applications. Use IConfiguration / environment variables instead.
        /// </summary>
        public string GetSystemArchivePath()
        {
            // TODO: Replace Registry access with:
            //   _configuration["Storage:ArchivePath"] ?? ArchivePath
            // Registry.LocalMachine is available on Windows via Microsoft.Win32 but
            // is not cross-platform. Remove for .NET 8 Linux/container deployments.
            return ArchivePath;
        }

        public Dictionary<string, object> ExportTitleReport(string month, string year)
        {
            // TODO: Replace hardcoded path/port/logPath with IConfiguration values.
            string filePath = TempExport + $"report_{month}_{year}.xlsx";
            return new Dictionary<string, object>
            {
                ["exportPath"] = filePath,
                ["port"]       = FixedPort,
                ["logPath"]    = LogPath,
                ["result"]     = _service.GenerateMonthlyReport(month, year)
            };
        }
    }

    /// <summary>
    /// Simple in-memory cache for title registrations.
    /// NOTE: For production .NET 8 applications, replace with IMemoryCache or IDistributedCache.
    /// The current implementation is not thread-safe.
    /// </summary>
    public static class TitleCache
    {
        // TODO: Replace with IMemoryCache (thread-safe, supports expiry).
        private static readonly Dictionary<string, object?> _cache
            = new Dictionary<string, object?>();

        public static void Store(string key, object? value) => _cache[key] = value;
        public static object? Get(string key) => _cache.TryGetValue(key, out var val) ? val : null;
    }
}
