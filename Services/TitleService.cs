using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LandTitleRegistration.Services
{
    /// <summary>
    /// Cloud-ready TitleService:
    /// - Connection strings read from Azure Key Vault via IConfiguration (Workload Identity)
    /// - Hardcoded secrets (API keys, DB credentials) removed from source code
    /// - log4net replaced with ASP.NET Core ILogger (Azure Monitor / Application Insights)
    /// - DateTime.Now replaced with DateTimeOffset.UtcNow for cloud-region consistency
    /// - Hard-coded service URL for gov report API moved to IConfiguration
    /// </summary>
    public class TitleService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TitleService> _logger;

        // All secrets and connection details are now sourced from Azure Key Vault
        // via IConfiguration (Workload Identity — no credentials stored in code).
        // (blocker-10: cr-dotnet-0009 Hard-coded Connection Strings)
        // (blocker-21: cr-dotnet-0123 Lack of Externalized Secrets — DbPassword)
        // (blocker-22: cr-dotnet-0123 Lack of Externalized Secrets — GovApiKey)
        private string ConnectionString =>
            _configuration["ConnectionStrings:LandTitleDb"]
            ?? Environment.GetEnvironmentVariable("LANDTITLE_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Database connection string is not configured. " +
                "Set 'ConnectionStrings:LandTitleDb' in Azure App Configuration / Key Vault.");

        // Government API key sourced from Azure Key Vault (blocker-22: cr-dotnet-0123)
        private string GovApiKey =>
            _configuration["Secrets:GovApiKey"]
            ?? Environment.GetEnvironmentVariable("GOV_API_KEY")
            ?? throw new InvalidOperationException(
                "Government API key is not configured. " +
                "Set 'Secrets:GovApiKey' in Azure Key Vault.");

        // Government report URL sourced from Azure App Configuration (blocker-4: cr-dotnet-0011)
        private string GovReportBaseUrl =>
            _configuration["Services:GovReportBaseUrl"]
            ?? Environment.GetEnvironmentVariable("GOV_REPORT_BASE_URL")
            ?? "http://gov-report-service/reports";

        // ILogger replaces log4net RollingFileAppender — integrates with Azure Monitor
        // Application Insights for cloud-native structured logging.
        // (blocker-11, blocker-12: cr-dotnet-0035 Custom Log4Net Appenders)
        public TitleService(IConfiguration configuration, ILogger<TitleService> logger)
        {
            _configuration = configuration;
            _logger        = logger;
        }

        public Dictionary<string, object> CreateRegistration(
            string ownerName, string parcelId,
            string propertyAddress, string titleType)
        {
            // DateTimeOffset.UtcNow replaces DateTime.Now for cloud-region consistency
            // (blocker-20: cr-dotnet-0121 Clock/Time Dependencies)
            var now      = DateTimeOffset.UtcNow;
            var titleRef = "LT-" + now.Ticks.ToString().Substring(10);

            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();

                // Parameterised query prevents SQL injection and removes hard-coded values
                const string sql =
                    "INSERT INTO TitleRegistrations " +
                    "(TitleRef, OwnerName, ParcelId, PropertyAddress, TitleType, RegisteredDate) " +
                    "VALUES (@TitleRef, @OwnerName, @ParcelId, @PropertyAddress, @TitleType, @RegisteredDate)";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@TitleRef",         SqlDbType.NVarChar).Value = titleRef;
                    cmd.Parameters.Add("@OwnerName",        SqlDbType.NVarChar).Value = ownerName;
                    cmd.Parameters.Add("@ParcelId",         SqlDbType.NVarChar).Value = parcelId;
                    cmd.Parameters.Add("@PropertyAddress",  SqlDbType.NVarChar).Value = propertyAddress;
                    cmd.Parameters.Add("@TitleType",        SqlDbType.NVarChar).Value = titleType;
                    cmd.Parameters.Add("@RegisteredDate",   SqlDbType.DateTimeOffset).Value = now;
                    cmd.ExecuteNonQuery();
                }
            }

            string confirmCode = ComputeSha256Hash(titleRef + ownerName);

            var result = new Dictionary<string, object>
            {
                ["titleRef"]     = titleRef,
                ["ownerName"]    = ownerName,
                ["parcelId"]     = parcelId,
                ["address"]      = propertyAddress,
                ["type"]         = titleType,
                ["confirmation"] = confirmCode
                // DbHost removed from response — no internal infrastructure details exposed
            };

            // Structured logging via ILogger → Azure Monitor Application Insights
            // (blocker-11, blocker-12: cr-dotnet-0035)
            _logger.LogInformation("Registration created: {TitleRef} for parcel {ParcelId}", titleRef, parcelId);
            return result;
        }

        public Dictionary<string, object> GetTitleByParcel(string parcelId)
        {
            // Parameterised query (SQL injection prevention)
            const string sql = "SELECT * FROM TitleRegistrations WHERE ParcelId = @ParcelId";
            var result = new Dictionary<string, object>();

            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@ParcelId", SqlDbType.NVarChar).Value = parcelId;
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                                result[reader.GetName(i)] = reader.GetValue(i)?.ToString();
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Calculates the registration fee based on title type, land value, owner category,
        /// region, and whether this is a first registration. All business logic preserved.
        /// </summary>
        public decimal CalculateRegistrationFee(string titleType, decimal landValue,
            string ownerCategory, string region, bool isFirstRegistration)
        {
            decimal baseFee = 0m;
            if      (titleType == "FREEHOLD")   baseFee = 500m;
            else if (titleType == "LEASEHOLD")  baseFee = 350m;
            else if (titleType == "COMMONHOLD") baseFee = 420m;
            else if (titleType == "ABSOLUTE")   baseFee = 600m;
            else                                baseFee = 300m;

            if      (landValue > 1000000m) baseFee += landValue * 0.004m;
            else if (landValue > 500000m)  baseFee += landValue * 0.003m;
            else if (landValue > 100000m)  baseFee += landValue * 0.002m;

            if      (ownerCategory == "COMPANY")    baseFee *= 1.25m;
            else if (ownerCategory == "CHARITY")    baseFee *= 0.75m;
            else if (ownerCategory == "GOVERNMENT") baseFee  = 0m;

            if      (region == "LONDON")   baseFee *= 1.15m;
            else if (region == "SCOTLAND") baseFee *= 0.90m;

            if (isFirstRegistration) baseFee *= 0.50m;

            return Math.Round(baseFee, 2);
        }

        public bool IsTitleTypeValid(string titleType)
        {
            return titleType == "FREEHOLD"   || titleType == "LEASEHOLD" ||
                   titleType == "COMMONHOLD" || titleType == "ABSOLUTE";
        }

        public string GenerateMonthlyReport(string month, string year)
        {
            // Hard-coded URL and embedded API key replaced with IConfiguration values
            // (blocker-4: cr-dotnet-0011 Hard-coded Service URLs)
            // (blocker-22: cr-dotnet-0123 Lack of Externalized Secrets — GovApiKey)
            var url = $"{GovReportBaseUrl}?month={Uri.EscapeDataString(month)}" +
                      $"&year={Uri.EscapeDataString(year)}&apiKey={GovApiKey}";
            _logger.LogInformation("Monthly report requested for {Month}/{Year}", month, year);
            return $"Report requested via: {url}";
        }

        public List<string> SearchByOwner(string ownerName)
        {
            // Parameterised query (SQL injection prevention)
            const string sql =
                "SELECT TitleRef FROM TitleRegistrations WHERE OwnerName LIKE @OwnerName";
            var refs = new List<string>();

            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@OwnerName", SqlDbType.NVarChar).Value = "%" + ownerName + "%";
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read()) refs.Add(reader.GetString(0));
                }
            }
            return refs;
        }

        /// <summary>
        /// Computes a SHA-256 hash (replaces deprecated SHA1CryptoServiceProvider).
        /// </summary>
        private static string ComputeSha256Hash(string input)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
