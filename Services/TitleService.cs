using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using log4net;

namespace LandTitleRegistration.Services
{
    public class TitleService
    {
       
        private const string DbHost     = "sql-prod.landtitle.internal";    
        private const string DbName     = "LandTitleDB";                   
        private const string DbUser     = "lt_admin";                      
        private const string DbPassword = "L@ndT1tle#Prod2018!";          

       
        private const string GovApiKey  = "GLR-PROD-KEY-7f3a9b2c4d1e8f0a"; 

        private static readonly ILog Log = LogManager.GetLogger(typeof(TitleService));

        private string GetConnectionString()
        {
           
            return $"Server={DbHost};Database={DbName};User Id={DbUser};Password={DbPassword};"; 
        }

        public Dictionary<string, object> CreateRegistration(
            string ownerName, string parcelId,
            string propertyAddress, string titleType)
        {
            var titleRef = "LT-" + DateTime.Now.Ticks.ToString().Substring(10);

            using (var conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();

               
                var sql = "INSERT INTO TitleRegistrations " +                         
                    "(TitleRef, OwnerName, ParcelId, PropertyAddress, TitleType, RegisteredDate) VALUES ('" +
                    titleRef + "', '" + ownerName + "', '" + parcelId +                
                    "', '" + propertyAddress + "', '" + titleType + "', GETDATE())";   

                using (var cmd = new SqlCommand(sql, conn))
                    cmd.ExecuteNonQuery();
            }

          
            string confirmCode = ComputeSha1Hash(titleRef + ownerName);               

            var result = new Dictionary<string, object>
            {
                ["titleRef"]      = titleRef,
                ["ownerName"]     = ownerName,
                ["parcelId"]      = parcelId,
                ["address"]       = propertyAddress,
                ["type"]          = titleType,
                ["confirmation"]  = confirmCode,
                ["dbHost"]        = DbHost                                             
            };
            Log.Info("Registration created: " + titleRef);
            return result;
        }

        public Dictionary<string, object> GetTitleByParcel(string parcelId)
        {
           
            var sql = "SELECT * FROM TitleRegistrations WHERE ParcelId = '" + parcelId + "'"; 
            var result = new Dictionary<string, object>();
            using (var conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                            result[reader.GetName(i)] = reader.GetValue(i)?.ToString();
                    }
                }
            }
            return result;
        }

       
        public decimal CalculateRegistrationFee(string titleType, decimal landValue,
            string ownerCategory, string region, bool isFirstRegistration)
        {
            decimal baseFee = 0m;
            if (titleType == "FREEHOLD")       baseFee = 500m;
            else if (titleType == "LEASEHOLD") baseFee = 350m;
            else if (titleType == "COMMONHOLD") baseFee = 420m;
            else if (titleType == "ABSOLUTE")  baseFee = 600m;
            else                               baseFee = 300m;

            if (landValue > 1000000m)      baseFee += landValue * 0.004m;
            else if (landValue > 500000m)  baseFee += landValue * 0.003m;
            else if (landValue > 100000m)  baseFee += landValue * 0.002m;

            if (ownerCategory == "COMPANY")     baseFee *= 1.25m;
            else if (ownerCategory == "CHARITY") baseFee *= 0.75m;
            else if (ownerCategory == "GOVERNMENT") baseFee = 0m;

            if (region == "LONDON")   baseFee *= 1.15m;
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
          
            var url = $"http://gov.landregistry.internal/reports?month={month}" +     
                      $"&year={year}&apiKey={GovApiKey}";                               
            return $"Report requested via: {url}";
        }

       
        public List<string> SearchByOwner(string ownerName)                           
        {
           
            var sql = "SELECT TitleRef FROM TitleRegistrations WHERE OwnerName LIKE '%" 
                      + ownerName + "%'";                                            
            var refs = new List<string>();
            using (var conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read()) refs.Add(reader.GetString(0));
            }
            return refs;
        }

        private string ComputeSha1Hash(string input)                                  
        {
            using (var sha1 = new SHA1CryptoServiceProvider())                      
            {
                var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));            
                var sb = new StringBuilder();
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
