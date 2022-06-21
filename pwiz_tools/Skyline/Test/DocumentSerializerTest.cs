/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class DocumentSerializerTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestDocumentFormatCurrent()
        {
            double expectedDocumentFormat = Install.MajorVersion + Install.MinorVersion * 0.1;
            if (expectedDocumentFormat == 21.2)
                expectedDocumentFormat = 22.1;  // Allow for the mistake made after 21.2 release
            double deltaAllowed = 0.099;
            Assert.AreEqual(expectedDocumentFormat, DocumentFormat.CURRENT.AsDouble(), deltaAllowed,
                string.Format("DocumentFormat.CURRENT {0} is expected to be less than 0.1 from the current Skyline version {1}",
                    DocumentFormat.CURRENT, expectedDocumentFormat));
        }

        [TestMethod]
        public void TestSerializePeptides()
        {
            var srmDocument = new SrmDocument(SrmSettingsList.GetDefault());
            string strProteinSequence = string.Concat(
                "MSLSSKLSVQDLDLKDKRVFIRVDFNVPLDGKKITSNQRIVAALPTIKYVLEHHPRYVVL",
                "ASHLGRPNGERNEKYSLAPVAKELQSLLGKDVTFLNDCVGPEVEAAVKASAPGSVILLEN",
                "LRYHIEEEGSRKVDGQKVKASKEDVQKFRHELSSLADVYINDAFGTAHRAHSSMVGFDLP",
                "QRAAGFLLEKELKYFGKALENPTRPFLAILGGAKVADKIQLIDNLLDKVDSIIIGGGMAF",
                "TFKKVLENTEIGDSIFDKAGAEIVPKLMEKAKAKGVEVVLPVDFIIADAFSADANTKTVT",
                "DKEGIPAGWQGLDNGPESRKLFAATVAKAKTIVWNGPPGVFEFEKFAAGTKALLDEVVKS",
                "SAAGNTVIIGGGDTATVAKKYGVTDKISHVSTGGGASLELLEGKELPGVAFLSEKK");
            var fastaSequence = new FastaSequence("YCR012W", "PGK1", null, strProteinSequence);
            var peptideGroup = new PeptideGroupDocNode(fastaSequence, fastaSequence.Name, fastaSequence.Description, new PeptideDocNode[0]);
            Assert.AreEqual(true, peptideGroup.AutoManageChildren);
            peptideGroup = peptideGroup.ChangeSettings(srmDocument.Settings, SrmSettingsDiff.ALL);
            srmDocument = (SrmDocument) srmDocument.ChangeChildren(new DocNode[] {peptideGroup});
            Assert.AreNotEqual(0, srmDocument.PeptideCount);
            Assert.AreNotEqual(0, srmDocument.MoleculeTransitionCount);
            Assert.IsFalse(CompactFormatOption.NEVER.UseCompactFormat(srmDocument));
            Assert.IsTrue(CompactFormatOption.ALWAYS.UseCompactFormat(srmDocument));
            VerifyRoundTrips(srmDocument);
            VerifyRoundTrips(AddSmallMolecules(srmDocument));
        }

        private SrmDocument AddSmallMolecules(SrmDocument document)
        {
            var newChildren = new List<PeptideGroupDocNode>(document.MoleculeGroups);
            newChildren.AddRange(new RefinementSettings().ConvertToSmallMolecules(document, TestContext.TestRunDirectory, RefinementSettings.ConvertToSmallMoleculesMode.masses_and_names).MoleculeGroups);
            newChildren.AddRange(new RefinementSettings().ConvertToSmallMolecules(document, TestContext.TestRunDirectory, RefinementSettings.ConvertToSmallMoleculesMode.masses_only).MoleculeGroups);
            newChildren.AddRange(new RefinementSettings().ConvertToSmallMolecules(document, TestContext.TestRunDirectory).MoleculeGroups); // Do this last for fullest library translation
            document = (SrmDocument)document.ChangeChildren(newChildren.ToArray());
            return document;
        }

        [TestMethod]
        public void TestSerializeDocument()
        {
            var serializer = new XmlSerializer(typeof(SrmDocument));
            var clazz = typeof(DocumentSerializerTest);
            SrmDocument document;
            using (var stream = clazz.Assembly.GetManifestResourceStream(clazz, "DocumentSerializerTest.sky"))
            {
                Assert.IsNotNull(stream);
                document = (SrmDocument) serializer.Deserialize(stream);
            }
            VerifyRoundTrips(document);
        }

        [TestMethod]
        public void TestSerializeImportTime()
        {
            const string ATTR_IMPORT_TIME = "import_time";
            var msDataFileUri = MsDataFileUri.Parse("Test");
            var importTime = DateTime.UtcNow;
            var chromatogramSet = new ChromatogramSet("Test", new[] {msDataFileUri});
            chromatogramSet = chromatogramSet.ChangeMSDataFileInfos(new[]
                {chromatogramSet.MSDataFileInfos[0].ChangeImportTime(importTime)});
            AssertEx.AreEqual(importTime, chromatogramSet.MSDataFileInfos[0].ImportTime);
            var measuredResults = new MeasuredResults(new[]
            {
                chromatogramSet
            });
            Assert.AreEqual(importTime, measuredResults.Chromatograms[0].MSDataFileInfos[0].ImportTime);
            var roundTripMeasuredResults = AssertEx.RoundTrip(measuredResults);
            Assert.AreEqual(measuredResults.Chromatograms[0].MSDataFileInfos[0].ImportTime,
                roundTripMeasuredResults.Chromatograms[0].MSDataFileInfos[0].ImportTime);

            // Save out in the current document format, and make sure that the Import Time round trips
            var document = new SrmDocument(SrmSettingsList.GetDefault());
            document = document.ChangeSettingsNoDiff(
                document.Settings.ChangeMeasuredResults(measuredResults));
            VerifyRoundTrips(document);
            AssertEx.Serializable(document);
            string xml = null;
            var docRoundTrip = AssertEx.RoundTrip(document, SkylineVersion.CURRENT, ref xml);
            AssertEx.IsTrue(xml.Contains(ATTR_IMPORT_TIME));
            AssertEx.AreEqual(measuredResults, docRoundTrip.MeasuredResults);
            AssertEx.AreEqual(measuredResults.Chromatograms[0].MSDataFileInfos[0].ImportTime,
                docRoundTrip.MeasuredResults.Chromatograms[0].MSDataFileInfos[0].ImportTime);

            // Save as an older format and make sure Import Time is not written out
            string xmlOldFormat = null;
            var docOldFormat = AssertEx.RoundTrip(document, SkylineVersion.V21_1, ref xmlOldFormat);
            AssertEx.IsFalse(xmlOldFormat.Contains(ATTR_IMPORT_TIME));
            AssertEx.IsNull(docOldFormat.MeasuredResults.Chromatograms[0].MSDataFileInfos[0].ImportTime);
            Assert.AreNotEqual(docRoundTrip.MeasuredResults, docOldFormat.MeasuredResults);
            Assert.AreEqual(docRoundTrip.MeasuredResults.ClearImportTimes(), docOldFormat.MeasuredResults);
        }

        private void VerifyRoundTrips(SrmDocument document)
        {
            var compactFormatOptionOld = Settings.Default.CompactFormatOption;
            var regexpTransitionData = new Regex(".*transition_data.*");
            try
            {
                foreach (var compactFormatOption in new[] {CompactFormatOption.NEVER, CompactFormatOption.ONLY_FOR_LARGE_FILES, CompactFormatOption.ALWAYS})
                {
                    Settings.Default.CompactFormatOption = compactFormatOption.Name;
                    var stringWriter = new StringWriter();
                    var xmlSerializer = new XmlSerializer(typeof(SrmDocument));
                    xmlSerializer.Serialize(stringWriter, document);
                    var documentText = stringWriter.ToString();
                    if (compactFormatOption.UseCompactFormat(document))
                    {
                        if (document.MoleculeTransitions.Any())
                        {
                            StringAssert.Matches(documentText, regexpTransitionData);
                        }
                    }
                    else
                    {
                        StringAssert.DoesNotMatch(documentText, regexpTransitionData);
                    }
                    var document2 = (SrmDocument)xmlSerializer.Deserialize(new StringReader(stringWriter.ToString()));
                    Assert.AreEqual(document, document2);
                }
            }
            finally
            {
                Settings.Default.CompactFormatOption = compactFormatOptionOld;
            }
        }
    }
}
