/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Globalization;
using Newtonsoft.Json.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.CommonMsData.RemoteApi.WatersConnect
{
    public abstract class WatersConnectObject : Immutable
    {
        public string Id { get; protected set; }
        public string Name { get; protected set; }
        public static string GetProperty(JObject jobject, string propertyName)
        {
            var property = jobject.Property(propertyName);
            return property?.Value.ToString();
        }

        /// <summary>
        /// True if two waters_connect ids name the same item. The server writes the same GUID in more
        /// than one format (e.g. "3616abca-1c42-..." in a listing, "3616ABCA1C42..." in a sample set's
        /// properties), so GUIDs are compared by value; anything else is compared ignoring case.
        /// </summary>
        public static bool IdEquals(string id1, string id2)
        {
            if (id1 == null || id2 == null)
                return false;
            if (Guid.TryParse(id1, out var guid1) && Guid.TryParse(id2, out var guid2))
                return guid1 == guid2;
            return string.Equals(id1, id2, StringComparison.OrdinalIgnoreCase);
        }

        public static DateTime? GetDateProperty(JObject jobject, string propertyName)
        {
            string value = GetProperty(jobject, propertyName);
            if (value.IsNullOrEmpty())
            {
                return null;
            }
            try
            {
                if (DateTime.TryParseExact(value, @"dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
                    return result;
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
        public static bool? GetBooleanProperty(JObject jtoken, string propertyName)
        {
            string value = GetProperty(jtoken, propertyName);
            if (value == null)
            {
                return null;
            }
            return bool.Parse(value);
        }

        public virtual WatersConnectUrl ToUrl(WatersConnectUrl currentConnectUrl)
        {
            return currentConnectUrl;

        }

    }
}
