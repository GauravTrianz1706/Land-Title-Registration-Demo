using Microsoft.AspNetCore.Http;
using System.Text;

namespace LandTitleRegistration.Extensions
{
    /// <summary>
    /// Extension methods for ISession to support string storage
    /// </summary>
    public static class SessionExtensions
    {
        public static void SetString(this ISession session, string key, string value)
        {
            session.Set(key, Encoding.UTF8.GetBytes(value));
        }

        public static string? GetString(this ISession session, string key)
        {
            if (session.TryGetValue(key, out byte[]? value))
            {
                return Encoding.UTF8.GetString(value);
            }
            return null;
        }
    }
}
