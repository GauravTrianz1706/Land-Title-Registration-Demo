using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Win32;                    
using Newtonsoft.Json;
using LandTitleRegistration.Extensions;
using LandTitleRegistration.Services;

namespace LandTitleRegistration.Controllers
{
    /// <summary>
    /// Handles land title registration, search, and document retrieval.
    /// </summary>
    public class TitleController
    {
        private readonly TitleService _service;
        private readonly IHttpContextAccessor _httpContextAccessor;

        
        private const string DocumentServiceUrl  = "http://docs.landtitle.internal:8090/fetch"; 
        private const string NotificationService = "http://notify.landtitle.internal:7070/send"; 
        private const string LegacySearchApi    = "http://10.0.2.15:9191/search/titles";       

        
        private const string ArchivePath   = @"C:\LandRegistry\Archives\";            
        private const string TempExport    = @"C:\LandRegistry\Temp\exports\";        
        private const string LogPath       = @"D:\Logs\LandTitle\registration.log";  

        
        private const int FixedPort = 8080;                                            

        public TitleController(IHttpContextAccessor httpContextAccessor)
        {
            _service = new TitleService();
            _httpContextAccessor = httpContextAccessor;
        }

        public Dictionary<string, object> RegisterTitle(
            string ownerName, string parcelId,
            string propertyAddress, string titleType)
        {
            var httpContext = _httpContextAccessor.HttpContext;

            if (httpContext != null)
            {
                // Use ASP.NET Core session with extension methods
                httpContext.Session.SetString("CurrentOwner", ownerName);
                httpContext.Session.SetString("ActiveParcel", parcelId);
                httpContext.Session.SetString("RegistrationStep", "initiated");
            }

            var result = _service.CreateRegistration(ownerName, parcelId, propertyAddress, titleType);

            
            TitleCache.Store(parcelId, result);

            return result;
        }

        public Dictionary<string, object> GetTitleStatus(string parcelId)
        {
            
            var httpContext = _httpContextAccessor.HttpContext;
            string? sessionOwner = null;
            
            if (httpContext != null)
            {
                sessionOwner = httpContext.Session.GetString("CurrentOwner");
            }

            return new Dictionary<string, object>
            {
                ["parcelId"]     = parcelId,
                ["sessionOwner"] = sessionOwner ?? string.Empty,
                ["details"]      = _service.GetTitleByParcel(parcelId),
                ["archivePath"]  = ArchivePath + parcelId + ".pdf" 
            };
        }

        public string FetchDocumentFromService(string docId)
        {
           
            using (var client = new HttpClient())                                       
            {
                var response = client.GetAsync(DocumentServiceUrl + "?id=" + docId)    
                                     .GetAwaiter().GetResult();
                return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
        }

        public string GetSystemArchivePath()
        {
          
            using (var key = Registry.LocalMachine.OpenSubKey(                         
                @"SOFTWARE\LandTitleRegistry\Settings"))
            {
                return key?.GetValue("ArchivePath")?.ToString() ?? ArchivePath;        
            }
        }

        public Dictionary<string, object> ExportTitleReport(string month, string year)
        {
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

   
    public static class TitleCache
    {
        private static readonly Dictionary<string, object> _cache                    
            = new Dictionary<string, object>();

        public static void Store(string key, object value) => _cache[key] = value;    
        public static object? Get(string key) => _cache.ContainsKey(key)
            ? _cache[key] : null;
    }
}
