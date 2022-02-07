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
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Serialization;
using pwiz.SkylineTestUtil;
using pwiz.SkylineTestUtil.Schemas;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Verfies existence of Skyline schema files
    /// </summary>
    [TestClass]
    public class DocumentFormatTest : AbstractUnitTest
    {
        /// <summary>
        /// This test enforces this proper relationship between DocumentFormat.CURRENT and the Skyline schema xsd files.
        /// 
        /// Our versioning convention since 2019 is a double yy.n where yy is a two digit year, and n is the release count. So
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
            // List all the historical values of VERSION_* (located in DocumentFormat.cs)
            var fields = typeof(DocumentFormat).GetFields(BindingFlags.Static | BindingFlags.Public);
            Assert.AreNotEqual(0, fields.Length);
            int versionsFound = 0;
            foreach (var field in fields)
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

                var versionedSchemaText = GetXsdResourceText(SchemaDocuments.GetSkylineSchemaResourceName(documentFormat.ToString()));
                var isCurrent = Equals(documentFormat, DocumentFormat.CURRENT);

                if (!isCurrent) // Older version, insist on a proper xsd file
                {
                    Assert.IsNotNull(versionedSchemaText, "Missing XSD embedded resource for version {0}", documentFormat);
                }
                // TODO(nicksh) add a check for "is this a release", and insist on a skyline_yy_n.xsd

                var xsdVersionedFileName = typeof(SchemaDocuments).Namespace + @"." +
                                           string.Format(CultureInfo.InvariantCulture, @"Skyline_{0}.xsd",
                                               documentFormat);
                if (isCurrent)
                {
                    if (versionedSchemaText != null)
                    {
                        // We have created a schema file for the current version, any further changes to the idea of "current" should involve an updated version number
                        var xsdCurrentFileName = typeof(SchemaDocuments).Namespace + @".Skyline_Current.xsd";
                        var currentText = GetXsdResourceText(xsdCurrentFileName);
                        Assert.AreEqual(versionedSchemaText, currentText, 
                            string.Format("Contents of {0} and {1} are not identical - did you forget to declare a new DocumentFormat.VERSION_yy_n (and change DocumentFormat.CURRENT to match) before changing contents of {1}?",
                                xsdVersionedFileName, xsdCurrentFileName));
                    }
                }
                else 
                {
                    Assert.AreEqual(SchemaDocuments.GetSkylineSchemaResourceName(documentFormat.ToString()), xsdVersionedFileName);
                }
            }
            Assert.AreNotEqual(0, versionsFound);
        }

        private string GetXsdResourceText(string resourceName)
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

    }
}