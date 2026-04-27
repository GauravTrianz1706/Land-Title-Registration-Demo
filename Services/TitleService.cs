using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace LandTitleRegistration.Services
{
    public class TitleService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TitleService> _logger;

        public TitleService(IConfiguration configuration, ILogger<TitleService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private string GetConnectionString()
        {
            // Replace hard-coded connection string with Azure Key Vault reference
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("Database connection string not configured");
                throw new InvalidOperationException("Database connection string is not configured");
            }

            return connectionString;
        }

        public async Task<Dictionary<string, object>> CreateRegistrationAsync(
            string ownerName, string parcelId,
            string propertyAddress, string titleType)
        {
            // Use DateTimeOffset.UtcNow for timezone-independent timestamps
            var titleRef = "LT-" + DateTimeOffset.UtcNow.Ticks.ToString().Substring(10);

            using (var conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();

                // Use parameterized queries to prevent SQL injection (ANSI SQL compatible)
                var sql = "INSERT INTO TitleRegistrations " +
                    "(TitleRef, OwnerName, ParcelId, PropertyAddress, TitleType, RegisteredDate) " +
                    "VALUES (@TitleRef, @OwnerName, @ParcelId, @PropertyAddress, @TitleType, @RegisteredDate)";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@TitleRef", titleRef);
                    cmd.Parameters.AddWithValue("@OwnerName", ownerName);
                    cmd.Parameters.AddWithValue("@ParcelId", parcelId);
                    cmd.Parameters.AddWithValue("@PropertyAddress", propertyAddress);
                    cmd.Parameters.AddWithValue("@TitleType", titleType);
                    cmd.Parameters.AddWithValue("@RegisteredDate", DateTimeOffset.UtcNow);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Replace SHA1CryptoServiceProvider with SHA256 (SHA1 is deprecated)
            string confirmCode = ComputeSha256Hash(titleRef + ownerName);

            var result = new Dictionary<string, object>
            {
                ["titleRef"] = titleRef,
                ["ownerName"] = ownerName,
                ["parcelId"] = parcelId,
                ["address"] = propertyAddress,
                ["type"] = titleType,
                ["confirmation"] = confirmCode,
                ["registeredAt"] = DateTimeOffset.UtcNow.ToString("o")
            };

            _logger.LogInformation("Registration created: {TitleRef} for owner {OwnerName}", titleRef, ownerName);
            return result;
        }

        public async Task<Dictionary<string, object>> GetTitleByParcelAsync(string parcelId)
        {
            // Use parameterized queries to prevent SQL injection
            var sql = "SELECT * FROM TitleRegistrations WHERE ParcelId = @ParcelId";
            var result = new Dictionary<string, object>();

            using (var conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ParcelId", parcelId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                                result[reader.GetName(i)] = reader.GetValue(i)?.ToString();
                        }
                    }
                }
            }
            return result;
        }

        public decimal CalculateRegistrationFee(string titleType, decimal landValue,
            string ownerCategory, string region, bool isFirstRegistration)
        {
            decimal baseFee = 0m;
            if (titleType == "FREEHOLD") baseFee = 500m;
            else if (titleType == "LEASEHOLD") baseFee = 350m;
            else if (titleType == "COMMONHOLD") baseFee = 420m;
            else if (titleType == "ABSOLUTE") baseFee = 600m;
            else baseFee = 300m;

            if (landValue > 1000000m) baseFee += landValue * 0.004m;
            else if (landValue > 500000m) baseFee += landValue * 0.003m;
            else if (landValue > 100000m) baseFee += landValue * 0.002m;

            if (ownerCategory == "COMPANY") baseFee *= 1.25m;
            else if (ownerCategory == "CHARITY") baseFee *= 0.75m;
            else if (ownerCategory == "GOVERNMENT") baseFee = 0m;

            if (region == "LONDON") baseFee *= 1.15m;
            else if (region == "SCOTLAND") baseFee *= 0.90m;

            if (isFirstRegistration) baseFee *= 0.50m;

            return Math.Round(baseFee, 2);
        }

        public bool IsTitleTypeValid(string titleType)
        {
            return titleType == "FREEHOLD" || titleType == "LEASEHOLD" ||
                   titleType == "COMMONHOLD" || titleType == "ABSOLUTE";
        }

        public async Task<string> GenerateMonthlyReportAsync(string month, string year)
        {
            // Replace hard-coded URL and API key with configuration
            var govReportApiUrl = _configuration["ServiceUrls:GovReportApi"];
            var apiKey = _configuration["ApiKeys:GovApiKey"];

            if (string.IsNullOrEmpty(govReportApiUrl) || string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Government report API not configured");
                return "Report API not configured";
            }

            var url = $"{govReportApiUrl}?month={month}&year={year}&apiKey={apiKey}";
            
            _logger.LogInformation("Report requested for {Month}/{Year}", month, year);
            return $"Report requested via: {url}";
        }

        public async Task<List<string>> SearchByOwnerAsync(string ownerName)
        {
            // Use parameterized queries to prevent SQL injection
            var sql = "SELECT TitleRef FROM TitleRegistrations WHERE OwnerName LIKE @OwnerName";
            var refs = new List<string>();

            using (var conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@OwnerName", $"%{ownerName}%");

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            refs.Add(reader.GetString(0));
                        }
                    }
                }
            }
            return refs;
        }

        private string ComputeSha256Hash(string input)
        {
            // Replace SHA1CryptoServiceProvider with SHA256 (more secure)
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder();
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
