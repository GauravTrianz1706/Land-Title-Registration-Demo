using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LandTitleRegistration.Services
{
    public class TitleService
    {
        private readonly LandTitleDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TitleService> _logger;

        // Secrets from Azure Key Vault via configuration
        private string GovApiKey => _configuration["Secrets:GovApiKey"];
        private string GovReportApiUrl => _configuration["ServiceUrls:GovReportApi"];

        public TitleService(
            LandTitleDbContext dbContext,
            IConfiguration configuration,
            ILogger<TitleService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Dictionary<string, object>> CreateRegistrationAsync(
            string ownerName, string parcelId,
            string propertyAddress, string titleType)
        {
            // Use DateTimeOffset.UtcNow instead of DateTime.Now for timezone consistency
            var titleRef = "LT-" + DateTimeOffset.UtcNow.Ticks.ToString().Substring(10);

            // Use Entity Framework Core with parameterized queries instead of direct SqlConnection
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

            // Use SHA256 instead of deprecated SHA1
            string confirmCode = ComputeSha256Hash(titleRef + ownerName);

            var result = new Dictionary<string, object>
            {
                ["titleRef"] = titleRef,
                ["ownerName"] = ownerName,
                ["parcelId"] = parcelId,
                ["address"] = propertyAddress,
                ["type"] = titleType,
                ["confirmation"] = confirmCode
            };

            _logger.LogInformation("Registration created: {TitleRef} for owner {OwnerName}", titleRef, ownerName);
            return result;
        }

        public async Task<Dictionary<string, object>> GetTitleByParcelAsync(string parcelId)
        {
            // Use Entity Framework Core with parameterized queries (prevents SQL injection)
            var registration = await _dbContext.TitleRegistrations
                .Where(t => t.ParcelId == parcelId)
                .FirstOrDefaultAsync();

            var result = new Dictionary<string, object>();

            if (registration != null)
            {
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

        public async Task<string> GenerateMonthlyReportAsync(string month, string year)
        {
            // Use configuration-driven URL with Key Vault secret
            var url = $"{GovReportApiUrl}?month={month}&year={year}&apiKey={GovApiKey}";
            
            _logger.LogInformation("Generating monthly report for {Month}/{Year}", month, year);
            
            return $"Report requested via: {url}";
        }

        public async Task<List<string>> SearchByOwnerAsync(string ownerName)
        {
            // Use Entity Framework Core with parameterized queries (prevents SQL injection)
            var refs = await _dbContext.TitleRegistrations
                .Where(t => t.OwnerName.Contains(ownerName))
                .Select(t => t.TitleRef)
                .ToListAsync();

            return refs;
        }

        private string ComputeSha256Hash(string input)
        {
            // Use SHA256 instead of deprecated SHA1CryptoServiceProvider
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder();
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }

    // Entity Framework Core DbContext for Azure SQL Database
    public class LandTitleDbContext : DbContext
    {
        public LandTitleDbContext(DbContextOptions<LandTitleDbContext> options)
            : base(options)
        {
        }

        public DbSet<TitleRegistration> TitleRegistrations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TitleRegistration>(entity =>
            {
                entity.HasKey(e => e.TitleRef);
                entity.Property(e => e.TitleRef).HasMaxLength(50).IsRequired();
                entity.Property(e => e.OwnerName).HasMaxLength(200).IsRequired();
                entity.Property(e => e.ParcelId).HasMaxLength(50).IsRequired();
                entity.Property(e => e.PropertyAddress).HasMaxLength(500);
                entity.Property(e => e.TitleType).HasMaxLength(50);
                entity.HasIndex(e => e.ParcelId);
                entity.HasIndex(e => e.OwnerName);
            });
        }
    }

    // Entity model for Title Registration
    public class TitleRegistration
    {
        public string TitleRef { get; set; }
        public string OwnerName { get; set; }
        public string ParcelId { get; set; }
        public string PropertyAddress { get; set; }
        public string TitleType { get; set; }
        public DateTimeOffset RegisteredDate { get; set; }
    }
}
