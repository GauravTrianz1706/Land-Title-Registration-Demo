using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LandTitleRegistration.Data;
using LandTitleRegistration.Models;

namespace LandTitleRegistration.Services
{
    public class TitleService
    {
        private readonly LandTitleDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TitleService> _logger;

        public TitleService(
            LandTitleDbContext dbContext,
            IConfiguration configuration,
            ILogger<TitleService> logger)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<Dictionary<string, object>> CreateRegistrationAsync(
            string ownerName, string parcelId,
            string propertyAddress, string titleType)
        {
            // Use UTC time for cloud consistency across regions
            var titleRef = "LT-" + DateTimeOffset.UtcNow.Ticks.ToString().Substring(10);

            var registration = new TitleRegistration
            {
                TitleRef = titleRef,
                OwnerName = ownerName,
                ParcelId = parcelId,
                PropertyAddress = propertyAddress,
                TitleType = titleType,
                RegisteredDate = DateTimeOffset.UtcNow
            };

            _dbContext.TitleRegistrations.Add(registration);
            await _dbContext.SaveChangesAsync();

            // Generate confirmation code
            string confirmCode = ComputeSha256Hash(titleRef + ownerName);

            var result = new Dictionary<string, object>
            {
                ["titleRef"] = titleRef,
                ["ownerName"] = ownerName,
                ["parcelId"] = parcelId,
                ["address"] = propertyAddress,
                ["type"] = titleType,
                ["confirmation"] = confirmCode,
                ["registeredDate"] = registration.RegisteredDate
            };

            _logger.LogInformation("Registration created: {TitleRef} for owner: {OwnerName}", titleRef, ownerName);
            return result;
        }

        public async Task<Dictionary<string, object>> GetTitleByParcelAsync(string parcelId)
        {
            var registration = await _dbContext.TitleRegistrations
                .Where(t => t.ParcelId == parcelId)
                .FirstOrDefaultAsync();

            var result = new Dictionary<string, object>();
            if (registration != null)
            {
                result["Id"] = registration.Id.ToString();
                result["TitleRef"] = registration.TitleRef;
                result["OwnerName"] = registration.OwnerName;
                result["ParcelId"] = registration.ParcelId;
                result["PropertyAddress"] = registration.PropertyAddress;
                result["TitleType"] = registration.TitleType;
                result["RegisteredDate"] = registration.RegisteredDate.ToString("o");
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

        public string GenerateMonthlyReport(string month, string year)
        {
            // Get API key from configuration (should be in Key Vault)
            var govApiKey = _configuration["ApiKeys:GovApiKey"];
            var govReportApi = _configuration["ServiceUrls:GovReportApi"];

            var url = $"{govReportApi}?month={month}&year={year}&apiKey={govApiKey}";
            return $"Report requested via: {url}";
        }

        public async Task<List<string>> SearchByOwnerAsync(string ownerName)
        {
            var refs = await _dbContext.TitleRegistrations
                .Where(t => t.OwnerName.Contains(ownerName))
                .Select(t => t.TitleRef)
                .ToListAsync();

            return refs;
        }

        private string ComputeSha256Hash(string input)
        {
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
