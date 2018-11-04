﻿/*
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class DocumentSerializerTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestSerializePeptides()
        {
            var srmDocument = new SrmDocument(SrmSettingsList.GetDefault());
            string strProteinSequence = string.Join(string.Empty, 
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
                    if (!Settings.Default.TestSmallMolecules)
                    {
                        Assert.AreEqual(document, document2);
                    }
                }
            }
            finally
            {
                Settings.Default.CompactFormatOption = compactFormatOptionOld;
            }
        }
    }
}
