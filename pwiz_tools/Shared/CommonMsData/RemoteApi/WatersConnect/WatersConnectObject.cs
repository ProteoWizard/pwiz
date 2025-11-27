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
