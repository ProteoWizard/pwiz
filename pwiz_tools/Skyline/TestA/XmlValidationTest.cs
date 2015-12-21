/*
 * Original author: Vagisha Sharma <vsharma .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for XmlValidationTest
    /// </summary>
    [TestClass]
    public class XmlValidationTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestA\XmlValidationTest.zip";
        private int _errorCount;

        [TestMethod]
        public void TestCurrentXmlFormat()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            var doc08Path = testFilesDir.GetTestPath("Study7_for_xml_validation.sky");
            var docCurrentPath = testFilesDir.GetTestPath("Study7_for_xml_validation_current.sky");

            // Test schema validation.
            var assembly = Assembly.GetAssembly(typeof(AssertEx));
            var stream = assembly.GetManifestResourceStream(
               typeof(AssertEx).Namespace + String.Format(CultureInfo.InvariantCulture, ".Schemas.Skyline_{0}.xsd", SrmDocument.FORMAT_VERSION));   // Not L10N
            TestSchemaValidation(stream, doc08Path, docCurrentPath);

            // Check explicit and implicit modifications in the current format.
            TestPeptideModifications(docCurrentPath);

            // Import a results file and check instrument information written out to the xml document.
            var resultsPath = testFilesDir.GetTestPath("CE_Vantage_15mTorr_0001_REP1_01.mzML");
            TestInstrumentInfo(resultsPath, docCurrentPath);
        }

        private static void TestInstrumentInfo(string resultsPath, string docCurrentPath)
        {
            var docCurrent = ResultsUtil.DeserializeDocument(docCurrentPath);
            var docContainer = new ResultsTestDocumentContainer(docCurrent, docCurrentPath);
            string replicateName = Path.GetFileNameWithoutExtension(resultsPath);
            var lockMassParameters = new LockMassParameters(456.78, 567.89, 12.34);
            var listChromatograms = new List<ChromatogramSet> { new ChromatogramSet(replicateName, new[] { new MsDataFilePath(resultsPath, lockMassParameters) }) };

            // Lockmass values are Waters only so don't expect them to be saved to the doc which has no Waters results
            // So test the serialization here
            var stringBuilder = new StringBuilder();
            using (var xmlWriter = XmlWriter.Create(stringBuilder))
            {
                xmlWriter.WriteStartDocument();
                xmlWriter.WriteStartElement("TestDocument");
                xmlWriter.WriteElements(listChromatograms, new XmlElementHelper<ChromatogramSet>());
                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndDocument();
            }
            var xmlReader = XmlReader.Create(new StringReader(stringBuilder.ToString()));
            xmlReader.ReadStartElement();
            var deserializedObjects = new List<ChromatogramSet>();
            xmlReader.ReadElements(deserializedObjects);
            Assert.AreEqual(1, deserializedObjects.Count);
            var compare = deserializedObjects[0];
            Assert.AreEqual(456.78, compare.MSDataFileInfos[0].FilePath.GetLockMassParameters().LockmassPositive.Value);
            Assert.AreEqual(567.89, compare.MSDataFileInfos[0].FilePath.GetLockMassParameters().LockmassNegative.Value);
            Assert.AreEqual(12.34, compare.MSDataFileInfos[0].FilePath.GetLockMassParameters().LockmassTolerance.Value);
            Assert.AreEqual(listChromatograms[0], compare);
            
            // We don't expect any new peptides/precursors/transitions added due to the imported results.
            var docCurrentResults = docContainer.ChangeMeasuredResults(new MeasuredResults(listChromatograms), 0, 0, 0);

            WriteDocument(docCurrentResults, docCurrentPath);

            docCurrentResults = ResultsUtil.DeserializeDocument(docCurrentPath);
            Assert.IsNotNull(docCurrentResults);
            AssertEx.IsDocumentState(docCurrentResults, 0, 7, 11, 22, 66);
            Assert.AreEqual(1, docCurrentResults.Settings.MeasuredResults.Chromatograms.Count);

            XmlDocument xdoc = new XmlDocument();
            xdoc.Load(docCurrentPath);

            var replicateList = xdoc.SelectNodes("/srm_settings/settings_summary/measured_results/replicate");
            Assert.IsNotNull(replicateList);
            Assert.AreEqual(1, replicateList.Count);
            var replicate = replicateList.Item(0);
            Assert.IsNotNull(replicate);
            var attribs = replicate.Attributes;
            Assert.IsNotNull(attribs);
            Assert.AreEqual(replicateName, attribs.GetNamedItem(SrmDocument.ATTR.name).Value);


            var instrumentList = replicate.SelectNodes("sample_file/instrument_info_list/instrument_info");
            Assert.IsNotNull(instrumentList);
            Assert.AreEqual(1, instrumentList.Count);
            var instrumentInfo = instrumentList.Item(0);
            Assert.IsNotNull(instrumentInfo);
            Assert.IsNotNull(instrumentInfo.Attributes);

            // model
            var model = instrumentInfo.SelectSingleNode(ChromatogramSet.EL.model.ToString());
            Assert.IsNotNull(model);
            Assert.IsNotNull(model.FirstChild);
            Assert.IsTrue(model.FirstChild.NodeType == XmlNodeType.Text);
            Assert.AreEqual("TSQ Vantage", model.FirstChild.Value);

            // ion source
            var ionsource = instrumentInfo.SelectSingleNode(ChromatogramSet.EL.ionsource.ToString());
            Assert.IsNotNull(ionsource);
            Assert.IsNotNull(ionsource.FirstChild);
            Assert.AreEqual(ionsource.FirstChild.NodeType, XmlNodeType.Text);
            Assert.AreEqual("nanoelectrospray", ionsource.FirstChild.Value);

            // analyzer
            var analyzer = instrumentInfo.SelectSingleNode(ChromatogramSet.EL.analyzer.ToString());
            Assert.IsNotNull(analyzer);
            Assert.IsNotNull(analyzer.FirstChild);
            Assert.AreEqual(analyzer.FirstChild.NodeType, XmlNodeType.Text);
            Assert.AreEqual("quadrupole/quadrupole/quadrupole", analyzer.FirstChild.Value);

            // dectector
            var detector = instrumentInfo.SelectSingleNode(ChromatogramSet.EL.detector.ToString());
            Assert.IsNotNull(detector);
            Assert.IsNotNull(detector.FirstChild);
            Assert.AreEqual(detector.FirstChild.NodeType, XmlNodeType.Text);
            Assert.AreEqual("electron multiplier", detector.FirstChild.Value);
        }

        private void TestSchemaValidation(Stream schemaFile, string doc08Path, string docCurrentPath)
        {
            // get the schema
            XmlTextReader schemaReader = new XmlTextReader(schemaFile);
            XmlSchema schema = XmlSchema.Read(schemaReader, ValidationCallBack);

            // create the validation settings
            XmlReaderSettings readerSettings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema
            };
            readerSettings.Schemas.Add(schema);
            readerSettings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
            readerSettings.ValidationEventHandler += ValidationCallBack;

            // Read the Skyline version 0.8 document
            XmlReader reader = XmlReader.Create(doc08Path, readerSettings);

            // Read the file with a validating reader
            while (reader.Read())
            {
            }
            reader.Close();
            // The document should not validate.
            Assert.IsTrue(_errorCount > 0);


            var doc08 = ResultsUtil.DeserializeDocument(doc08Path);
            Assert.IsNotNull(doc08);
            AssertEx.IsDocumentState(doc08, 0, 7, 11, 22, 66);

           

            // Save the document.
            // IMPORTANT: update the default modifications first.
            doc08.Settings.UpdateDefaultModifications(true);
            WriteDocument(doc08, docCurrentPath);

            Assert.IsTrue(File.Exists(docCurrentPath));
            var docCurrent = ResultsUtil.DeserializeDocument(docCurrentPath);
            Assert.IsNotNull(docCurrent);
            AssertEx.IsDocumentState(docCurrent, 0, 7, 11, 22, 66);

            AssertEx.DocumentCloned(doc08, docCurrent); // Make sure that the documents are the same


            _errorCount = 0;
            reader = XmlReader.Create(docCurrentPath, readerSettings);
            // Read the file with a validating reader
            while (reader.Read())
            {
            }
            reader.Close();
            // The document should validate without any errors.
            Assert.AreEqual(0, _errorCount);
        }

        private static void TestPeptideModifications(string docPath)
        {
            XmlDocument xdoc = new XmlDocument();
            xdoc.Load(docPath);

            var peptideList = xdoc.SelectNodes("/srm_settings/peptide_list/peptide");
            Assert.IsNotNull(peptideList);
            Assert.AreEqual(11, peptideList.Count);

            // Peptide AGLCQTFVYGGCR
            var peptide = peptideList.Item(0);
            Assert.IsNotNull(peptide);
            var attribs = peptide.Attributes;
            Assert.IsNotNull(attribs);
            Assert.AreEqual("AGLCQTFVYGGCR", attribs.GetNamedItem(SrmDocument.ATTR.sequence).Value);
            Assert.IsNotNull(attribs.GetNamedItem(SrmDocument.ATTR.avg_measured_retention_time));

            // There should be an explicit heavy modification on this peptide
            var modList = peptide.SelectNodes(SrmDocument.EL.explicit_modifications + "/" +
                                              SrmDocument.EL.explicit_heavy_modifications + "/" +
                                              SrmDocument.EL.explicit_modification);
            Assert.IsNotNull(modList);
            Assert.AreEqual(1, modList.Count);
            CheckModAttributes(modList.Item(0), "Label:13C", "7", "+5");

            // There should be two implicit static modifications on this peptide
            modList = peptide.SelectNodes(SrmDocument.EL.implicit_modifications + "/" +
                                          SrmDocument.EL.implicit_static_modifications + "/" +
                                          SrmDocument.EL.implicit_modification);
            Assert.IsNotNull(modList);
            Assert.AreEqual(2, modList.Count);
            CheckModAttributes(modList.Item(0), "Carbamidomethyl Cysteine", "3", "+57");
            CheckModAttributes(modList.Item(1), "Carbamidomethyl Cysteine", "11", "+57");


            // Peptide GYSIFSYATK
            peptide = peptideList.Item(9);
            Assert.IsNotNull(peptide);
            attribs = peptide.Attributes;
            Assert.IsNotNull(attribs);
            Assert.AreEqual("GYSIFSYATK", attribs.GetNamedItem(SrmDocument.ATTR.sequence).Value);
            Assert.IsNotNull(attribs.GetNamedItem(SrmDocument.ATTR.avg_measured_retention_time));

            // There should not be any explicit modifications on this peptide
            modList = peptide.SelectNodes(SrmDocument.EL.explicit_modifications);
            Assert.IsNotNull(modList);
            Assert.AreEqual(0, modList.Count);
            // There should not be any implicit static modifications on this peptide
            modList = peptide.SelectNodes(SrmDocument.EL.implicit_modifications + "/" +
                                          SrmDocument.EL.implicit_static_modifications + "/" +
                                          SrmDocument.EL.implicit_modification);
            Assert.IsNotNull(modList);
            Assert.AreEqual(0, modList.Count);
            // There should be one implicit heavy modifications on this peptide
            modList = peptide.SelectNodes(SrmDocument.EL.implicit_modifications + "/" +
                                          SrmDocument.EL.implicit_heavy_modifications + "/" +
                                          SrmDocument.EL.implicit_modification);
            Assert.IsNotNull(modList);
            Assert.AreEqual(1, modList.Count);
            CheckModAttributes(modList.Item(0), "Label:13C(6)15N(2) (C-term K)", "9", "+8");

        }

        private static void CheckModAttributes(XmlNode mod, string name, string index, string massdiff)
        {
            Assert.IsNotNull(mod);
            var attribs = mod.Attributes;
            Assert.IsNotNull(attribs);
            Assert.AreEqual(name, attribs.GetNamedItem(SrmDocument.ATTR.modification_name).Value);
            Assert.AreEqual(index, attribs.GetNamedItem(SrmDocument.ATTR.index_aa).Value);
            Assert.AreEqual(massdiff, attribs.GetNamedItem(SrmDocument.ATTR.mass_diff).Value);
        }

        private static void WriteDocument(SrmDocument doc, string docPath)
        {
            using (var writer = new XmlTextWriter(docPath, Encoding.UTF8) { Formatting = Formatting.Indented })
            {
                XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
                ser.Serialize(writer, doc);

                writer.Flush();
                writer.Close();
            }
        }

        // Schema validation warnings or errors.
        private void ValidationCallBack(object sender, ValidationEventArgs args)
        {
            // Increment the error count
            _errorCount++;
        }

        [TestMethod]
        public void TestModificationsXml()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            var doc08Path = testFilesDir.GetTestPath("mods_test_08.sky");
            var docCurrentPath = testFilesDir.GetTestPath("mods_test_current.sky");

            var doc08 = ResultsUtil.DeserializeDocument(doc08Path);
            Assert.IsNotNull(doc08);
            AssertEx.IsDocumentState(doc08, 0, 1, 4, 12, 36);

            // Save the document
            // IMPORTANT: update the default modifications first
            doc08.Settings.UpdateDefaultModifications(true);
            WriteDocument(doc08, docCurrentPath);

            Assert.IsTrue(File.Exists(docCurrentPath));
            var docCurrent = ResultsUtil.DeserializeDocument(docCurrentPath);
            Assert.IsNotNull(docCurrent);
            AssertEx.IsDocumentState(docCurrent, 0, 1, 4, 12, 36);

            AssertEx.DocumentCloned(doc08, docCurrent); // Make sure that the documents are the same

            XmlDocument xmldoc08 = new XmlDocument();
            xmldoc08.Load(doc08Path);
            XmlNodeList peptides08 = xmldoc08.GetElementsByTagName(SrmDocument.EL.peptide);
            Assert.AreEqual(4, peptides08.Count);

            XmlDocument xmldocCurrent = new XmlDocument();
            xmldocCurrent.Load(docCurrentPath);
            XmlNodeList peptidesCurrent = xmldocCurrent.GetElementsByTagName(SrmDocument.EL.peptide);
            Assert.AreEqual(4, peptidesCurrent.Count);

            for (int i = 0; i < 4; i++)
            {
                var pep08 = peptides08.Item(i);
                var pepCurrent = peptidesCurrent.Item(i);

                // Compare any variable modifications
                CompareModifications(pep08, pepCurrent, SrmDocument.EL.variable_modifications + "/" +
                                                        SrmDocument.EL.variable_modification);

                // Compare any explicit static modifications
                CompareModifications(pep08, pepCurrent, SrmDocument.EL.explicit_modifications + "/" +
                                                        SrmDocument.EL.explicit_static_modifications + "/" +
                                                        SrmDocument.EL.explicit_modification);

                // Compare any explicit "heavy" modifications
                CompareModifications(pep08, pepCurrent, SrmDocument.EL.explicit_modifications + "/" +
                                                        SrmDocument.EL.explicit_heavy_modifications +
                                                        "[not(@" + SrmDocument.ATTR.isotope_label + ")]/" +
                                                        SrmDocument.EL.explicit_modification);

                // Compare any explicit "heavy2" modifications
                CompareModifications(pep08, pepCurrent, SrmDocument.EL.explicit_modifications + "/" +
                                                        SrmDocument.EL.explicit_heavy_modifications +
                                                        "[@" + SrmDocument.ATTR.isotope_label + " = 'heavy2']/" +
                                                        SrmDocument.EL.explicit_modification);
            }


            // Check the implicit modifications in the current format
            var docStaticMods = docCurrent.Settings.PeptideSettings.Modifications.StaticModifications;
            var modC = docStaticMods.FirstOrDefault(mod => mod.Name.Equals("Carbamidomethyl Cysteine"));
            var modStaticExpl = docStaticMods.FirstOrDefault(mod => mod.Name.Equals("test_explicit_mod"));

            Assert.IsNotNull(modC);
            Assert.IsNotNull(modStaticExpl);

            var docHeavyMods = docCurrent.Settings.PeptideSettings.Modifications.HeavyModifications;
            var modR1 = docHeavyMods.FirstOrDefault(mod => mod.Name.Equals("Label:13C(6) (C-term R)"));
            var modHeavyExpl = docHeavyMods.FirstOrDefault(mod => mod.Name.Equals("heavy_explicit_mod1"));

            Assert.IsNotNull(modR1);
            Assert.IsNotNull(modHeavyExpl);

            var docHeavy2Mods =
                docCurrent.Settings.PeptideSettings.Modifications.GetModificationsByName("heavy2").Modifications;
            var modR2 = docHeavy2Mods.FirstOrDefault(mod => mod.Name.Equals("Label:13C(6)15N(4) (C-term R)"));
            var modExpl2 = docHeavy2Mods.FirstOrDefault(mod => mod.Name.Equals("Label:15N(1) (G)"));

            Assert.IsNotNull(modR2);
            Assert.IsNotNull(modExpl2);

            // First peptide
            CheckPeptideImplicitMods(peptidesCurrent.Item(0), new[] {modC, modC}, new[] {modR1}, new[] {modR2});
            // Second peptide
            CheckPeptideImplicitMods(peptidesCurrent.Item(1), new[] { modC, modC }, new[] { modR1 }, new[] { modR2 });
            // Third peptide; explicit structural mod was added so all structural mods will be explicit
            CheckPeptideImplicitMods(peptidesCurrent.Item(2), new StaticMod[0], new[] {modR1}, new[] {modR2});
            // Fourth peptide; explicit "heavy" and "heavy2" mods were added so all heavy mods will be explicit
            CheckPeptideImplicitMods(peptidesCurrent.Item(3), new[] {modC, modC}, new StaticMod[0], new StaticMod[0]);

        }

        private static void CheckPeptideImplicitMods(XmlNode peptideNode, StaticMod[] staticMods, StaticMod[] heavyMods,
                                              StaticMod[] heavy2Mods)
        {

            var modListStatic = peptideNode.SelectNodes(SrmDocument.EL.implicit_modifications + "/" +
                                                        SrmDocument.EL.implicit_static_modifications + "/" +
                                                        SrmDocument.EL.implicit_modification);
            CheckImplicitMods(staticMods, modListStatic);

            var modListHeavy = peptideNode.SelectNodes(SrmDocument.EL.implicit_modifications + "/" +
                                                       SrmDocument.EL.implicit_heavy_modifications +
                                                       "[not(@" + SrmDocument.ATTR.isotope_label + ")]/" +
                                                       SrmDocument.EL.implicit_modification);
            CheckImplicitMods(heavyMods, modListHeavy);

            var modListHeavy2 = peptideNode.SelectNodes(SrmDocument.EL.implicit_modifications + "/" +
                                                        SrmDocument.EL.implicit_heavy_modifications +
                                                        "[@" + SrmDocument.ATTR.isotope_label + " = 'heavy2']/" +
                                                        SrmDocument.EL.implicit_modification);
            CheckImplicitMods(heavy2Mods, modListHeavy2);

        }

        private static void CheckImplicitMods(IList<StaticMod> mods, XmlNodeList modList)
        {
            Assert.IsNotNull(modList);
            Assert.AreEqual(mods.Count(), modList.Count);
            for (int i = 0; i < mods.Count(); i++)
            {
                var modNode = modList.Item(i);
                Assert.IsNotNull(modNode);
                var attribs = modNode.Attributes;
                Assert.IsNotNull(attribs);
                var modName = attribs.GetNamedItem(SrmDocument.ATTR.modification_name).Value;
                Assert.AreEqual(mods[i].Name, modName);
            }
        }

        private static void CompareModifications(XmlNode pep08, XmlNode pepCurrent, string modElementPath)
        {
            var modList08 = pep08.SelectNodes(modElementPath);
            var modListCurrent = pepCurrent.SelectNodes(modElementPath);
            Assert.IsNotNull(modList08);
            Assert.IsNotNull(modListCurrent);
            Assert.AreEqual(modList08.Count, modListCurrent.Count);

            for (int i = 0; i < modList08.Count; i++)
            {
                var mod08 = modList08.Item(i);
                var modCurrent = modListCurrent.Item(i);
                Assert.IsNotNull(mod08);
                Assert.IsNotNull(modCurrent);

                var attribs08 = mod08.Attributes;
                var attribsCurrent = modCurrent.Attributes;
                Assert.IsNotNull(attribs08);
                Assert.IsNotNull(attribsCurrent);
                CompareAttributes(attribs08, attribsCurrent, SrmDocument.ATTR.index_aa);
                CompareAttributes(attribs08, attribsCurrent, SrmDocument.ATTR.modification_name);

                // Modifications in the current format document must contain a mass_diff attribute
                Assert.IsNotNull(attribsCurrent.GetNamedItem(SrmDocument.ATTR.mass_diff));
            }
        }

        private static void CompareAttributes(XmlAttributeCollection attribs1, XmlAttributeCollection attribs2,
                                       string attribute)
        {
            var attr1 = attribs1.GetNamedItem(attribute);
            var attr2 = attribs2.GetNamedItem(attribute);
            Assert.IsFalse(attr1 == null && attr2 != null);
            Assert.IsFalse(attr1 != null && attr2 == null);
            if (attr1 != null)
                Assert.AreEqual(attr1.Value, attr2.Value);
        }
    }
}
