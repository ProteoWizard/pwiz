/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Reporting
{
    [TestClass]
    public class DsvWriterTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestInvariantExport()
        {
            SkylineDataSchema skylineDataSchema =
                new SkylineDataSchema(CreateMemoryDocumentContainer(LoadTestDocument()), DataSchemaLocalizer.INVARIANT);
            SkylineViewContext viewContext = new DocumentGridViewContext(skylineDataSchema);

            string testFile = Path.Combine(TestContext.TestDir, "TestInvariantExport.csv");
            var dsvWriter = new DsvWriter(CultureInfo.InvariantCulture, ',');
            viewContext.ExportToFile(null, GetTestReport(skylineDataSchema), testFile, dsvWriter);
            string strExported = File.ReadAllText(testFile);
            Assert.AreEqual(ExpectedInvariantReport, strExported);
            // Assert that the file written out was UTF8 encoding without any byte order mark
            byte[] bytesExported = File.ReadAllBytes(testFile);
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(strExported), bytesExported);
        }

        [TestMethod]
        public void TestExportWithCurrentLanguage()
        {
            CultureInfo cultureInfo = CultureInfo.CurrentUICulture;
            SkylineDataSchema skylineDataSchema =
                new SkylineDataSchema(CreateMemoryDocumentContainer(LoadTestDocument()), SkylineDataSchema.GetLocalizedSchemaLocalizer());
            SkylineViewContext viewContext = new DocumentGridViewContext(skylineDataSchema);

            string testFile = Path.Combine(TestContext.TestDir, "TestExportWithCurrentLanguage.csv");
            char separator = TextUtil.GetCsvSeparator(cultureInfo);
            var dsvWriter = new DsvWriter(cultureInfo, separator);
            viewContext.ExportToFile(null, GetTestReport(skylineDataSchema), testFile, dsvWriter);
            string strExported = File.ReadAllText(testFile);
            var actualLines = strExported.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var expectedLines = ExpectedInvariantReport.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            var invariantHeaders = expectedLines[0].Split(',');
            var expectedHeaders =
                invariantHeaders.Select(header => ColumnCaptions.ResourceManager.GetString(header, cultureInfo) ?? header).ToArray();
            var actualHeaders = actualLines[0].Split(separator);
            CollectionAssert.AreEqual(expectedHeaders, actualHeaders);
            // If the language in English, then the exported report will be identical to the invariant report except for the headers
            if (cultureInfo.Name == "en-US")
            {
                CollectionAssert.AreEqual(expectedLines.Skip(1).ToArray(), actualLines.Skip(1).ToArray());
            }
        }

        private SrmDocument LoadTestDocument()
        {
            using (var stream = typeof (DsvWriterTest).Assembly
                .GetManifestResourceStream(typeof (DsvWriterTest), "DsvWriterTest.sky"))
            {
                Assert.IsNotNull(stream);
                var serializer = new XmlSerializer(typeof (SrmDocument));
                return (SrmDocument) serializer.Deserialize(stream);
            }
        }

        private ViewInfo GetTestReport(SkylineDataSchema dataSchema)
        {
            var serializer = new XmlSerializer(typeof(ViewSpecList));
            ViewSpec viewSpec = ((ViewSpecList) serializer.Deserialize(new StringReader(TestReport))).ViewSpecs.First();
            return new ViewInfo(dataSchema, typeof (Skyline.Model.Databinding.Entities.Transition), viewSpec);
        }

        private MemoryDocumentContainer CreateMemoryDocumentContainer(SrmDocument document)
        {
            MemoryDocumentContainer memoryDocumentContainer = new MemoryDocumentContainer();
            Assert.IsTrue(memoryDocumentContainer.SetDocument(document, memoryDocumentContainer.Document));
            return memoryDocumentContainer;
        }

        private const string ExpectedInvariantReport = @"ProteinName,Peptide,Precursor,Transition,Numeric Annotation
Protein,ELVIS,280.6681++,L - y4+,1.5
Protein,ELVIS,280.6681++,V - y3+,1.5
Molecules,Caffeine,194.0798+,Part of Caffeine,2.5
Custom Ion,Ion [100.000549/100.000549],100.0000+,Ion [80.000549/80.000549] 80.0000+,#N/A
";
        private const string TestReport = @"<views>
    <view name='TestReport' rowsource='pwiz.Skyline.Model.Databinding.Entities.Transition' sublist='Results!*'>
        <column name='Precursor.Peptide.Protein.Name' />
        <column name='Precursor.Peptide' />
        <column name='Precursor' />
        <column name='' />
        <column name='Precursor.Peptide.&quot;annotation_Numeric Annotation&quot;' />
    </view>
</views>";
    }
}
