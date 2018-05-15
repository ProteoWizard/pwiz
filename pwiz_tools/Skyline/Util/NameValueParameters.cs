using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Web;

namespace pwiz.Skyline.Util
{
    public class NameValueParameters
    {
        private readonly NameValueCollection _nameValueCollection;

        public static NameValueParameters Parse(string str)
        {
            return new NameValueParameters(HttpUtility.ParseQueryString(str));
        }

        public NameValueParameters()
        {
            _nameValueCollection = new NameValueCollection();
        }

        private NameValueParameters(NameValueCollection nameValueCollection)
        {
            _nameValueCollection = nameValueCollection;
        }

        public void SetValue(string key, string value)
        {
            if (value == null)
            {
                _nameValueCollection.Remove(key);
            }
            else
            {
                _nameValueCollection[key] = value;
            }
        }

        public void SetDoubleValue(string key, double? value)
        {
            SetValue(key, value == null ? null : value.Value.ToString(CultureInfo.InvariantCulture));
        }

        public void SetBoolValue(string key, bool value)
        {
            SetValue(key, value ? "true" : null);
        }

        public void SetDateValue(string key, DateTime? value)
        {
            if (value == null)
            {
                _nameValueCollection.Remove(key);
            }
            else
            {
                _nameValueCollection[key] = value.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        public void SetLongValue(string key, long? value)
        {
            if (value == null)
            {
                _nameValueCollection.Remove(key);
            }
            else
            {
                _nameValueCollection[key] = value.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        public string GetValue(string key)
        {
            return _nameValueCollection[key];
        }

        public double? GetDoubleValue(string key)
        {
            string value = GetValue(key);
            if (value == null)
            {
                return null;
            }
            return double.Parse(value, CultureInfo.InvariantCulture);
        }

        public long? GetLongValue(string key)
        {
            string value = GetValue(key);
            if (value == null)
            {
                return null;
            }
            return long.Parse(value, CultureInfo.InvariantCulture);
        }

        public DateTime? GetDateValue(string key)
        {
            string value = GetValue(key);
            if (value == null)
            {
                return null;
            }
            return DateTime.Parse(value, CultureInfo.InvariantCulture);
        }

        public bool GetBoolValue(string key)
        {
            return null != GetValue(key);
        }

        public override string ToString()
        {
            return string.Join("&",
                Enumerable.Range(0, _nameValueCollection.Count).Select(i =>
                    Uri.EscapeDataString(_nameValueCollection.Keys[i]) + "=" +
                    Uri.EscapeDataString(_nameValueCollection.Get(i))));
        }
    }
}
