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
    }
}
