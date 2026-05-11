using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

// NOTE: System.Data.SqlClient has been replaced by Microsoft.Data.SqlClient for .NET 8.
// log4net has been replaced by Microsoft.Extensions.Logging for .NET 8 compatibility.
// Hardcoded credentials and connection strings MUST be moved to environment variables
// or a secrets manager (e.g., Azure Key Vault, AWS Secrets Manager).
// SQL injection vulnerabilities have been fixed using parameterized queries.
// SHA1 has been replaced with SHA256 (SHA1 is cryptographically broken).

namespace LandTitleRegistration.Services
{
    public class TitleService
    {
        // SECURITY: Hardcoded credentials removed.
        // TODO: Inject connection string via IConfiguration["ConnectionStrings:LandTitleDB"]
        // or environment variable LANDTITLE_DB_CONNECTIONSTRING.
        // Example: "Server=<host>;Database=LandTitleDB;User Id=<user>;Password=<password>;Encrypt=True;"
        private const string DbHost     = "sql-prod.landtitle.internal";   // TODO: move to config
        private const string DbName     = "LandTitleDB";                   // TODO: move to config
        private const string DbUser     = "lt_admin";                      // TODO: move to config
        // SECURITY: Password removed — use environment variable or secrets manager.
        // private const string DbPassword = "<removed — use secrets manager>";

        // SECURITY: API key removed — use environment variable or secrets manager.
        // private const string GovApiKey = "<removed — use secrets manager>";

        private readonly ILogger<TitleService> _logger;

        public TitleService(ILogger<TitleService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Builds the database connection string.
        /// TODO: Replace with IConfiguration["ConnectionStrings:LandTitleDB"] injected via constructor.
        /// NEVER hardcode credentials — use environment variables or a secrets manager.
        /// </summary>
        private string GetConnectionString()
        {
            // TODO: Replace with:
            //   return _configuration.GetConnectionString("LandTitleDB")
            //       ?? throw new InvalidOperationException("Connection string 'LandTitleDB' not configured.");
            var password = Environment.GetEnvironmentVariable("LANDTITLE_DB_PASSWORD")
                           ?? throw new InvalidOperationException(
                               "Database password not configured. Set LANDTITLE_DB_PASSWORD environment variable.");
            return $"Server={DbHost};Database={DbName};User Id={DbUser};Password={password};Encrypt=True;TrustServerCertificate=False;";
        }

        /// <summary>
        /// Creates a new title registration record.
        /// SQL injection fixed: uses parameterized queries.
        /// SHA1 replaced with SHA256.
        /// </summary>
        public Dictionary<string, object> CreateRegistration(
            string ownerName, string parcelId,
            string propertyAddress, string titleType)
        {
            var titleRef = "LT-" + DateTime.UtcNow.Ticks.ToString().Substring(10);

            using (var conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();

                // FIXED: Parameterized query prevents SQL injection.
                const string sql =
                    "INSERT INTO TitleRegistrations " +
                    "(TitleRef, OwnerName, ParcelId, PropertyAddress, TitleType, RegisteredDate) " +
                    "VALUES (@TitleRef, @OwnerName, @ParcelId, @PropertyAddress, @TitleType, GETDATE())";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@TitleRef",         titleRef);
                    cmd.Parameters.AddWithValue("@OwnerName",        ownerName);
                    cmd.Parameters.AddWithValue("@ParcelId",         parcelId);
                    cmd.Parameters.AddWithValue("@PropertyAddress",  propertyAddress);
                    cmd.Parameters.AddWithValue("@TitleType",        titleType);
                    cmd.ExecuteNonQuery();
                }
            }

            // FIXED: SHA1 replaced with SHA256 (SHA1 is cryptographically broken).
            string confirmCode = ComputeSha256Hash(titleRef + ownerName);

            var result = new Dictionary<string, object>
            {
                ["titleRef"]     = titleRef,
                ["ownerName"]    = ownerName,
                ["parcelId"]     = parcelId,
                ["address"]      = propertyAddress,
                ["type"]         = titleType,
                ["confirmation"] = confirmCode
                // SECURITY: DbHost removed from response to avoid information disclosure.
            };
            _logger.LogInformation("Registration created: {TitleRef}", titleRef);
            return result;
        }

        /// <summary>
        /// Retrieves a title registration by parcel ID.
        /// SQL injection fixed: uses parameterized queries.
        /// </summary>
        public Dictionary<string, object> GetTitleByParcel(string parcelId)
        {
            // FIXED: Parameterized query prevents SQL injection.
            const string sql = "SELECT * FROM TitleRegistrations WHERE ParcelId = @ParcelId";
            var result = new Dictionary<string, object>();

            using (var conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ParcelId", parcelId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                                result[reader.GetName(i)] = reader.GetValue(i)?.ToString() ?? string.Empty;
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Calculates the registration fee based on title type, land value, and other factors.
        /// </summary>
        public decimal CalculateRegistrationFee(string titleType, decimal landValue,
            string ownerCategory, string region, bool isFirstRegistration)
        {
            decimal baseFee = titleType switch
            {
                "FREEHOLD"   => 500m,
                "LEASEHOLD"  => 350m,
                "COMMONHOLD" => 420m,
                "ABSOLUTE"   => 600m,
                _            => 300m
            };

            if (landValue > 1_000_000m)     baseFee += landValue * 0.004m;
            else if (landValue > 500_000m)  baseFee += landValue * 0.003m;
            else if (landValue > 100_000m)  baseFee += landValue * 0.002m;

            baseFee = ownerCategory switch
            {
                "COMPANY"    => baseFee * 1.25m,
                "CHARITY"    => baseFee * 0.75m,
                "GOVERNMENT" => 0m,
                _            => baseFee
            };

            if (region == "LONDON")        baseFee *= 1.15m;
            else if (region == "SCOTLAND") baseFee *= 0.90m;

            if (isFirstRegistration) baseFee *= 0.50m;

            return Math.Round(baseFee, 2);
        }

        /// <summary>
        /// Validates whether the given title type is recognised.
        /// </summary>
        public bool IsTitleTypeValid(string titleType)
        {
            return titleType is "FREEHOLD" or "LEASEHOLD" or "COMMONHOLD" or "ABSOLUTE";
        }

        /// <summary>
        /// Generates a monthly report URL.
        /// SECURITY: API key removed from URL — pass via Authorization header or secure config.
        /// TODO: Inject GovApiKey via IConfiguration["GovRegistry:ApiKey"] or environment variable.
        /// </summary>
        public string GenerateMonthlyReport(string month, string year)
        {
            // SECURITY: API key must NOT be embedded in URLs (visible in logs/proxies).
            // TODO: Use HttpClient with Authorization header:
            //   client.DefaultRequestHeaders.Authorization =
            //       new AuthenticationHeaderValue("Bearer", _configuration["GovRegistry:ApiKey"]);
            var govApiKey = Environment.GetEnvironmentVariable("GOV_LANDREGISTRY_API_KEY")
                            ?? throw new InvalidOperationException(
                                "Gov API key not configured. Set GOV_LANDREGISTRY_API_KEY environment variable.");
            var url = $"http://gov.landregistry.internal/reports?month={Uri.EscapeDataString(month)}" +
                      $"&year={Uri.EscapeDataString(year)}";
            return $"Report requested via: {url}";
        }

        /// <summary>
        /// Searches for title registrations by owner name.
        /// SQL injection fixed: uses parameterized queries with LIKE.
        /// </summary>
        public List<string> SearchByOwner(string ownerName)
        {
            // FIXED: Parameterized query prevents SQL injection.
            const string sql =
                "SELECT TitleRef FROM TitleRegistrations WHERE OwnerName LIKE @OwnerName";
            var refs = new List<string>();

            using (var conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@OwnerName", "%" + ownerName + "%");
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read()) refs.Add(reader.GetString(0));
                }
            }
            return refs;
        }

        /// <summary>
        /// Computes a SHA-256 hash of the input string.
        /// FIXED: SHA1CryptoServiceProvider (broken) replaced with SHA256 (secure).
        /// SHA256.HashData() is the recommended .NET 8 API.
        /// </summary>
        private static string ComputeSha256Hash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
