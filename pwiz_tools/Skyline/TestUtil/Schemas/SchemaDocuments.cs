/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Globalization;
using pwiz.Skyline.Model.Serialization;

namespace pwiz.SkylineTestUtil.Schemas
{
    public static class SchemaDocuments
    {
        /// <summary>
        /// Returns the path to the embedded resource for the XSD document for the particular document format version.
        /// If the version is equal to DocumentFormat.Current, then the filename is always "Skyline_Current.xsd".
        /// Otherwise, it is Skyline_###.xsd 
        /// </summary>
        public static string GetSkylineSchemaResourceName(string formatVersion)
        {
            string schemaFileName;
            if (DocumentFormat.CURRENT.ToString().Equals(formatVersion))
            {
                schemaFileName = "Skyline_Current.xsd";
            }
            else
            {
                schemaFileName = string.Format(CultureInfo.InvariantCulture, @"Skyline_{0}.xsd", formatVersion);
            }

            return typeof(SchemaDocuments).Namespace + @"." + schemaFileName;
        }

        public static string GetAuditLogSchemaResourceName(string version)
        {
            return typeof(SchemaDocuments).Namespace + @".AuditLog.Skyl_" + version + @".xsd";
        }
    }
}
