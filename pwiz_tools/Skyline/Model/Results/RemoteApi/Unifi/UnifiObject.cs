using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results.RemoteApi.Unifi
{
    public abstract class UnifiObject : Immutable
    {
        public string Id { get; protected set; }
        public string Name { get; protected set; }
        public static string GetProperty(JObject jobject, string propertyName)
        {
            var property = jobject.Property(propertyName);
            if (property == null || property.Value == null)
            {
                return null;
            }
            return property.Value.ToString();
        }

        public static DateTime? GetDateProperty(JObject jobject, string propertyName)
        {
            string value = GetProperty(jobject, propertyName);
            if (value == null)
            {
                return null;
            }
            try
            {
                return DateTime.Parse(value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static int? GetIntegerProperty(JObject jobject, string propertyName)
        {
            string value = GetProperty(jobject, propertyName);
            if (value == null)
            {
                return null;
            }
            return int.Parse(value, CultureInfo.InvariantCulture);
        }
    }
}
