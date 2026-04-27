using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;

namespace LandTitleRegistration.Services
{
    public class TitleService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TitleService> _logger;
        private readonly TitleDbContext _dbContext;
        private readonly SecretClient _secretClient;

        // Configuration values from Azure App Configuration
        private string DbHost => _configuration["ConnectionStrings:DbHost"];
        private string DbName => _configuration["ConnectionStrings:DbName"];
        private string DbUser => _configuration["ConnectionStrings:DbUser"];
        
        // Secrets from Azure Key Vault
        private string GovApiKeySecretName => _configuration["Secrets:GovApiKeySecretName"];
        private string GovReportApiUrl => _configuration["ServiceUrls:GovReportApi"];

        public TitleService(
            IConfiguration configuration,
            ILogger<TitleService> logger,
            TitleDbContext dbContext,
            SecretClient secretClient)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _secretClient = secretClient ?? throw new ArgumentNullException(nameof(secretClient));
        }

        public async Task<Dictionary<string, object>> CreateRegistration(
            string ownerName, string parcelId,
            string propertyAddress, string titleType)
        {
            // Use DateTimeOffset.UtcNow instead of DateTime.Now for timezone consistency
            var titleRef = "LT-" + DateTimeOffset.UtcNow.Ticks.ToString().Substring(10);

            // Use Entity Framework Core instead of direct SqlConnection
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
                ["confirmation"] = confirmCode,
                ["registeredDate"] = registration.RegisteredDate
            };

            // Use structured logging instead of log4net
            _logger.LogInformation("Registration created: {TitleRef} for owner {OwnerName}", titleRef, ownerName);
            
            return result;
        }

        public async Task<Dictionary<string, object>> GetTitleByParcel(string parcelId)
        {
            // Use Entity Framework Core with parameterized queries (prevents SQL injection)
            var registration = await _dbContext.TitleRegistrations
                .Where(t => t.ParcelId == parcelId)
                .FirstOrDefaultAsync();

            if (registration == null)
            {
                _logger.LogWarning("No title found for parcel {ParcelId}", parcelId);
                return new Dictionary<string, object>();
            }

            var result = new Dictionary<string, object>
            {
                ["TitleRef"] = registration.TitleRef,
                ["OwnerName"] = registration.OwnerName,
                ["ParcelId"] = registration.ParcelId,
                ["PropertyAddress"] = registration.PropertyAddress,
                ["TitleType"] = registration.TitleType,
                ["RegisteredDate"] = registration.RegisteredDate
            };

            return result;
        }

        // ANSI SQL compatible fee calculation (no SQL Server-specific features)
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

        public async Task<string> GenerateMonthlyReport(string month, string year)
        {
            // Retrieve API key from Azure Key Vault
            string apiKey = await GetSecretFromKeyVault(GovApiKeySecretName);

            var url = $"{GovReportApiUrl}?month={month}&year={year}&apiKey={apiKey}";
            
            _logger.LogInformation("Monthly report requested for {Month}/{Year}", month, year);
            
            return $"Report requested via: {url}";
        }

        public async Task<List<string>> SearchByOwner(string ownerName)
        {
            // Use Entity Framework Core with parameterized queries
            var refs = await _dbContext.TitleRegistrations
                .Where(t => t.OwnerName.Contains(ownerName))
                .Select(t => t.TitleRef)
                .ToListAsync();

            _logger.LogInformation("Found {Count} titles for owner search: {OwnerName}", refs.Count, ownerName);

            return refs;
        }

        // Helper method to retrieve secrets from Azure Key Vault
        private async Task<string> GetSecretFromKeyVault(string secretName)
        {
            try
            {
                KeyVaultSecret secret = await _secretClient.GetSecretAsync(secretName);
                return secret.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve secret {SecretName} from Key Vault", secretName);
                throw;
            }
        }

        // Use SHA256 instead of deprecated SHA1
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

    // Entity Framework Core DbContext
    public class TitleDbContext : DbContext
    {
        public TitleDbContext(DbContextOptions<TitleDbContext> options) : base(options)
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
