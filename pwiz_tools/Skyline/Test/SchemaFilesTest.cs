/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Serialization;
using pwiz.SkylineTestUtil;
using pwiz.SkylineTestUtil.Schemas;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Verifies existence of Skyline schema files
    /// </summary>
    [TestClass]
    public class SchemaDocumentsTest : AbstractUnitTest
    {
        /// <summary>
        /// This test enforces this proper relationship between DocumentFormat.CURRENT and the Skyline schema xsd files.
        /// 
        /// Our version number convention since 2019 is a double yy.n where yy is a two digit year, and n is the release count. So
        /// if the second release in 2021 involved a schema change, we'd expect to find a file Skyline_21_2.xsd, and a value
        /// declared in the DocumentFormat.cs as "public static readonly DocumentFormat VERSION_21_2 = new DocumentFormat(21.2);".
        ///
        /// Starting in 2021 any development changes to the schema are always in a file "current.xsd", and we copy that to a newly
        /// created "Skyline_yy_n.xsd" file when we make a release.
        /// 
        /// So if we find an xsd file whose name indicates that it represents the same version as DocumentFormat.CURRENT
        /// we expect that it has exactly the same contents as current.xsd. If not, that probably means someone forgot to
        /// declare a new document version, and to set DocumentFormat.CURRENT to be that new value.
        ///
        /// Also, if we find an older DocumentFormat.VERSION_yy_n without a corresponding Skyline_yy_n.xsd file in source
        /// control, that means that somebody neglected to copy current.xsd to a newly created Skyline_yy_n.xsd file.
        ///
        /// </summary>
        [TestMethod]
        public void TestDocumentFormatSchemaFiles()
        {
            string explicitCurrentResourceName = GetVersionSpecificXsdResourceName(DocumentFormat.CURRENT);
            var explicitCurrentXsdContents =
                GetResourceText(GetVersionSpecificXsdResourceName(DocumentFormat.CURRENT));
            if (IsOfficialBuild())
            {
                // When we are releasing a new Skyline or Skyline-Daily, we expect that there will exist
                // a "Skyline_X.Y.xsd" file with the current version.
                // That way, the next times someone wants to modify Skyline_Current.xsd, they will know that they
                // also have to increment the current version number.
                Assert.IsNotNull(explicitCurrentXsdContents,
                    "Resource {0} should exist for official Daily or Release build\r\n" +
                    "Copy pwiz_tools/Skyline/TestUtil/Schemas/Skyline_Current.xsd to {1} and mark it as an embedded resource.",
                    explicitCurrentResourceName, Path.GetFileName(explicitCurrentXsdContents));
            }

            var currentXsdContents =
                GetResourceText(SchemaDocuments.GetSkylineSchemaResourceName(DocumentFormat.CURRENT.ToString()));
            Assert.IsNotNull(currentXsdContents);
            if (explicitCurrentXsdContents != null)
            {
                Assert.AreEqual(currentXsdContents, explicitCurrentXsdContents);
            }

            // List all the historical values of VERSION_* (located in DocumentFormat.cs)
            var documentFormatPublicStaticFields = typeof(DocumentFormat).GetFields(BindingFlags.Static | BindingFlags.Public);
            Assert.AreNotEqual(0, documentFormatPublicStaticFields.Length);
            int versionsFound = 0;
            foreach (var field in documentFormatPublicStaticFields)
            {
                if (field.FieldType != typeof(DocumentFormat))
                {
                    continue;
                }

                versionsFound++;
                Assert.IsTrue(field.IsInitOnly, "{0} variable must be declared read only", field.Name);
                var documentFormat = (DocumentFormat) field.GetValue(null);

                // Recent versions must have an XSD file in the project, per our "current.xsd" process adopted in 2021
                // There are older ones than this, but we don't need to check them for interaction with current.xsd
                if (documentFormat.AsDouble() < 21)
                {
                    continue;
                }

                var versionedSchemaText =
                    GetResourceText(SchemaDocuments.GetSkylineSchemaResourceName(documentFormat.ToString()));
                Assert.IsNotNull(versionedSchemaText, "Missing XSD embedded resource for version {0}", documentFormat);
            }
            Assert.AreNotEqual(0, versionsFound);
        }

        private string GetResourceText(string resourceName)
        {
            using (var resourceStream = typeof(SchemaDocuments).Assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                {
                    return null;
                }

                return new StreamReader(resourceStream).ReadToEnd();
            }
        }
        /// <summary>
        /// Returns the location of the embedded reasource "Skyline_X.Y.xsd" file in TestUtil.dll.
        /// This is the same as what <see cref="SchemaDocuments.GetSkylineSchemaResourceName"/> returns,
        /// except that the other function returns the string "Skyline_Current.xsd" if you ask for
        /// the schema file for the current version.
        /// </summary>
        private string GetVersionSpecificXsdResourceName(DocumentFormat documentFormat)
        {
            string resourceName =
                typeof(SchemaDocuments).Namespace + @"." +
                string.Format(CultureInfo.InvariantCulture, @"Skyline_{0}.xsd", documentFormat);
            if (!Equals(documentFormat, DocumentFormat.CURRENT))
            {
                Assert.AreEqual(SchemaDocuments.GetSkylineSchemaResourceName(documentFormat.ToString()), resourceName);
            }

            return resourceName;
        }

        /// <summary>
        /// Returns true if Skyline was built with the "--official" command line flag.
        /// </summary>
        private static bool IsOfficialBuild()
        {
            // Get the version attribute from the Skyline.exe assembly.
            var versionAttribute = typeof(Skyline.Program).Assembly.GetCustomAttributes()
                .OfType<AssemblyInformationalVersionAttribute>().FirstOrDefault();
            if (versionAttribute == null)
            {
                return false;
            }

            if (versionAttribute.InformationalVersion.Contains("(developer build)") ||
                versionAttribute.InformationalVersion.Contains("(automated build)"))
            {
                return false;
            }

            return true;
        }
    }
}