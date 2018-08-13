/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
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
            SetValue(key, value ? "true" : null); // Not L10N
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
            return string.Join("&", // Not L10N
                Enumerable.Range(0, _nameValueCollection.Count).Select(i =>
                    Uri.EscapeDataString(_nameValueCollection.Keys[i]) + "=" + // Not L10N
                    Uri.EscapeDataString(_nameValueCollection.Get(i))));
        }
    }
}
