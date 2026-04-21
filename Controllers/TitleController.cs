using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Web;
using System.Web.SessionState;
using Microsoft.Win32;                    // czr-csharp-win32: Windows Registry — not portable
using Newtonsoft.Json;

namespace LandTitleRegistration.Controllers
{
    /// <summary>
    /// Handles land title registration, search, and document retrieval.
    /// </summary>
    public class TitleController
    {
        private readonly TitleService _service;

        // VIOLATION cr-csharp-0021 [Cloud Compat / Mandatory]: Hardcoded infrastructure
        // hostnames. Cloud-hosted apps receive dynamic IPs on restart and must externalise
        // all endpoints to environment variables or Azure App Configuration / AWS Parameter Store.
        private const string DocumentServiceUrl  = "http://docs.landtitle.internal:8090/fetch"; // cr-csharp-0021, cr-csharp-0088
        private const string NotificationService = "http://notify.landtitle.internal:7070/send"; // cr-csharp-0021, cr-csharp-0088
        private const string LegacySearchApi    = "http://10.0.2.15:9191/search/titles";        // cr-csharp-0021, cr-csharp-0088

        // VIOLATION czr-csharp-001 [Software Portability / Mandatory]: Hardcoded absolute
        // Windows path. Azure App Service, AWS Elastic Beanstalk, and any Linux-based container
        // host will not have this path — the drive letter alone breaks containerisation.
        private const string ArchivePath   = @"C:\LandRegistry\Archives\";             // czr-csharp-001
        private const string TempExport    = @"C:\LandRegistry\Temp\exports\";         // czr-csharp-001
        private const string LogPath       = @"D:\Logs\LandTitle\registration.log";    // czr-csharp-001

        // VIOLATION czr-csharp-port [Software Portability / High]: Fixed port bound in code.
        // Cloud PaaS and container orchestrators (AKS, ECS) dynamically assign ports.
        private const int FixedPort = 8080;                                             // czr-csharp-port

        public TitleController()
        {
            _service = new TitleService();
        }

        public Dictionary<string, object> RegisterTitle(
            string ownerName, string parcelId,
            string propertyAddress, string titleType)
        {
            var httpContext = HttpContext.Current;

            // VIOLATION cr-csharp-0065 [Cloud Compat / Mandatory]: Registration state stored
            // in ASP.NET InProc Session. Cloud load balancers distribute requests across
            // multiple instances — session on server A is invisible to server B.
            // Azure ARR affinity or sticky sessions are workarounds, not solutions.
            httpContext.Session["CurrentOwner"]     = ownerName;    // cr-csharp-0065
            httpContext.Session["ActiveParcel"]     = parcelId;     // cr-csharp-0065
            httpContext.Session["RegistrationStep"] = "initiated";  // cr-csharp-0065

            var result = _service.CreateRegistration(ownerName, parcelId, propertyAddress, titleType);

            // VIOLATION cr-csharp-0067 [Cloud Compat / Potential]: In-memory cache stored as
            // static dictionary — instance-local, lost on restart, invisible to other instances.
            TitleCache.Store(parcelId, result);

            return result;
        }

        public Dictionary<string, object> GetTitleStatus(string parcelId)
        {
            // VIOLATION cr-csharp-0065 [Cloud Compat / Mandatory]: Reading workflow state
            // from session — returns null on any cloud instance other than the originating one.
            var sessionOwner = HttpContext.Current.Session["CurrentOwner"]?.ToString(); // cr-csharp-0065

            return new Dictionary<string, object>
            {
                ["parcelId"]     = parcelId,
                ["sessionOwner"] = sessionOwner,
                ["details"]      = _service.GetTitleByParcel(parcelId),
                ["archivePath"]  = ArchivePath + parcelId + ".pdf"  // czr-csharp-001
            };
        }

        public string FetchDocumentFromService(string docId)
        {
            // VIOLATION cr-csharp-0088 [Cloud Compat / Mandatory]: Plain HTTP call to
            // internal document service. Azure API Management, AWS API Gateway, and
            // cloud WAF all enforce HTTPS. This call will be intercepted or blocked.
            using (var client = new HttpClient())                                        // cr-csharp-0088
            {
                var response = client.GetAsync(DocumentServiceUrl + "?id=" + docId)    // cr-csharp-0088
                                     .GetAwaiter().GetResult();
                return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
        }

        public string GetSystemArchivePath()
        {
            // VIOLATION czr-csharp-win32 [Software Portability / Mandatory]: Windows Registry
            // access via Microsoft.Win32. Registry does not exist on Linux containers,
            // Azure App Service on Linux, AWS Lambda, or any non-Windows runtime.
            using (var key = Registry.LocalMachine.OpenSubKey(                          // czr-csharp-win32
                @"SOFTWARE\LandTitleRegistry\Settings"))
            {
                return key?.GetValue("ArchivePath")?.ToString() ?? ArchivePath;        // czr-csharp-001
            }
        }

        public Dictionary<string, object> ExportTitleReport(string month, string year)
        {
            string filePath = TempExport + $"report_{month}_{year}.xlsx";              // czr-csharp-001
            return new Dictionary<string, object>
            {
                ["exportPath"] = filePath,                                              // czr-csharp-001
                ["port"]       = FixedPort,                                             // czr-csharp-port
                ["logPath"]    = LogPath,                                               // czr-csharp-001
                ["result"]     = _service.GenerateMonthlyReport(month, year)
            };
        }
    }

    // VIOLATION cr-csharp-0067 [Cloud Compat / Potential]: Static in-memory cache — no TTL,
    // instance-local, grows unbounded. Causes OOM on cloud instances with constrained memory.
    public static class TitleCache
    {
        private static readonly Dictionary<string, object> _cache                      // cr-csharp-0067
            = new Dictionary<string, object>();

        public static void Store(string key, object value) => _cache[key] = value;    // cr-csharp-0067
        public static object Get(string key) => _cache.ContainsKey(key)
            ? _cache[key] : null;
    }
}
