/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Summary description for SmallWiffTest
    /// </summary>
    [TestClass]
    public class SmallWiffTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestData\Results\SmallWiff.zip";

        // TODO: Next time SmallWiff.zip is updated, remove the suffix shenanigans below and rename the mzML files in the zip
        [TestMethod]
        public void FileTypeTest()
        {
            if (SkipWiff2TestInTestExplorer(nameof(FileTypeTest)))
                return;
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            // wiff1
            {
                string extWiff = ExtensionTestContext.ExtAbWiff;
                string suffix = ExtensionTestContext.CanImportAbWiff ? "" : "-test";

                // Do file type checks
                using (var msData = new MsDataFileImpl(testFilesDir.GetTestPath("051309_digestion" + suffix + extWiff)))
                {
                    Assert.IsTrue(msData.IsABFile);
                }

                using (var msData = new MsDataFileImpl(testFilesDir.GetTestPath("051309_digestion-s3.mzXML")))
                {
                    Assert.IsTrue(msData.IsABFile);
                    Assert.IsTrue(msData.IsMzWiffXml);
                }
            }

            // wiff2
            {
                string extWiff2 = ExtensionTestContext.ExtAbWiff2;
                string suffix = ExtensionTestContext.CanImportAbWiff2 ? "" : "-sample-centroid";

                // Do file type checks
                using (var msData = new MsDataFileImpl(TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.ABI, "swath.api" + suffix + extWiff2)))
                {
                    Assert.IsTrue(msData.IsABFile);
                }
            }
        }

        [TestMethod]
        public void Wiff2ResultsTest()
        {
            if (SkipWiff2TestInTestExplorer(nameof(Wiff2ResultsTest)))
                return;
            TestFilesDir testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string docPath = testFilesDir.GetTestPath("OnyxTOFMS.sky");
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            //AssertEx.IsDocumentState(doc, 0, 1, 1, 4);

            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                const string replicateName = "Wiff2Test";
                string extRaw = ExtensionTestContext.ExtAbWiff2;
                string suffix = ExtensionTestContext.CanImportAbWiff2 ? "" : "-sample-centroid";
                var chromSets = new[]
                {
                    new ChromatogramSet(replicateName, new[]
                        { new MsDataFilePath(TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.ABI, "swath.api" + suffix + extRaw)),  }),
                };
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
                Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
                docContainer.AssertComplete();
                docResults = docContainer.Document;
                //AssertResult.IsDocumentResultsState(docResults, replicateName,
                //    doc.MoleculeCount, doc.MoleculeTransitionGroupCount, 0, doc.MoleculeTransitionCount, 0);
            }

            testFilesDir.Dispose();
        }

        [TestMethod]
        public void MsDataFileUriEncodingTest()
        {
            var fname = "test.mzML";
            var pathSample = SampleHelp.LegacyEncodePath(fname, null, -1, null, true, false, false);
            var lockmassParametersA = new LockMassParameters(1.23, 3.45, 4.56);
            var lockmassParametersB = new LockMassParameters(1.23, null, 4.56);

            Assert.IsTrue(lockmassParametersA.CompareTo(LockMassParameters.EMPTY) > 0);
            Assert.IsTrue(lockmassParametersA.CompareTo(null) < 0);
            Assert.IsTrue(lockmassParametersB.CompareTo(lockmassParametersA) < 0);
            Assert.IsTrue(lockmassParametersA.CompareTo(new LockMassParameters(1.23, 3.45, 4.56)) == 0);
            

            var c = new ChromatogramSet("test", new[] { MsDataFileUri.Parse(pathSample) });
            Assert.AreEqual(fname, c.MSDataFilePaths.First().GetFilePath());
            Assert.IsTrue(c.MSDataFilePaths.First().LegacyGetCentroidMs1());
            Assert.IsFalse(c.MSDataFilePaths.First().LegacyGetCentroidMs2());
            Assert.IsFalse(c.MSDataFilePaths.Cast<MsDataFilePath>().First().LegacyCombineIonMobilitySpectra);

            pathSample = SampleHelp.LegacyEncodePath(fname, null, -1, lockmassParametersA, false, true, false);
            c = new ChromatogramSet("test", new[] { MsDataFileUri.Parse(pathSample) });
            Assert.AreEqual(lockmassParametersA, c.MSDataFilePaths.First().GetLockMassParameters());
            Assert.IsTrue(c.MSDataFilePaths.First().LegacyGetCentroidMs2());
            Assert.IsFalse(c.MSDataFilePaths.First().LegacyGetCentroidMs1());
            Assert.IsFalse(c.MSDataFilePaths.Cast<MsDataFilePath>().First().LegacyCombineIonMobilitySpectra);

            pathSample = SampleHelp.LegacyEncodePath(fname, "test_0", 1, lockmassParametersB,false,false, true);
            c = new ChromatogramSet("test", new[] { MsDataFileUri.Parse(pathSample) });
            Assert.AreEqual(lockmassParametersB, c.MSDataFilePaths.First().GetLockMassParameters());
            Assert.AreEqual("test_0", c.MSDataFilePaths.First().GetSampleName());
            Assert.AreEqual(1, c.MSDataFilePaths.First().GetSampleIndex());
            Assert.IsFalse(c.MSDataFilePaths.First().LegacyGetCentroidMs1());
            Assert.IsFalse(c.MSDataFilePaths.First().LegacyGetCentroidMs2());
            Assert.IsTrue(c.MSDataFilePaths.Cast<MsDataFilePath>().First().LegacyCombineIonMobilitySpectra);
        }

        [TestMethod]
        public void WiffResultsTest()
        {
            TestFilesDir testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            SrmDocument doc = InitWiffDocument(testFilesDir);
            using (var docContainer = new ResultsTestDocumentContainer(doc,
                testFilesDir.GetTestPath("SimpleWiffTest.sky")))
            {
                FileEx.SafeDelete(ChromatogramCache.FinalPathForName(docContainer.DocumentFilePath, null));

                var listChromatograms = new List<ChromatogramSet>();

                if (ExtensionTestContext.CanImportAbWiff)
                {
                    string pathWiff = testFilesDir.GetTestPath("051309_digestion.wiff");
                    string[] dataIds = MsDataFileImpl.ReadIds(pathWiff);

                    for (int i = 0; i < dataIds.Length; i++)
                    {
                        string nameSample = dataIds[i];
                        if (!Equals(nameSample, "test") && listChromatograms.Count == 0)
                            continue;
                        string pathSample = SampleHelp.EncodePath(pathWiff, nameSample, i, LockMassParameters.EMPTY);
                        listChromatograms.Add(new ChromatogramSet(nameSample, new[] { MsDataFileUri.Parse(pathSample) }));
                    }
                }
                else
                {
                    listChromatograms.Add(new ChromatogramSet("test",
                        new[] { MsDataFileUri.Parse(testFilesDir.GetTestPath("051309_digestion-test.mzML")) }));
                    listChromatograms.Add(new ChromatogramSet("rfp9,before,h,1",
                        new[] { MsDataFileUri.Parse(testFilesDir.GetTestPath("051309_digestion-rfp9,before,h,1.mzML")) }));
                }

                // Should have added test and one after
                Assert.AreEqual(2, listChromatograms.Count);

                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
                Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
                docContainer.AssertComplete();

                docResults = docContainer.Document;

                AssertEx.IsDocumentState(docResults, 5, 9, 9, 18, 54);
                Assert.IsTrue(docResults.Settings.MeasuredResults.IsLoaded);

                foreach (var nodeTran in docResults.PeptideTransitions)
                {
                    Assert.IsTrue(nodeTran.HasResults);
                    Assert.AreEqual(2, nodeTran.Results.Count);
                }

                // Remove the last chromatogram
                listChromatograms.RemoveAt(1);

                var docResultsSingle = docResults.ChangeMeasuredResults(new MeasuredResults(listChromatograms));

                AssertResult.IsDocumentResultsState(docResultsSingle, "test", 9, 2, 9, 10, 27);

                // Add mzXML version of test sample
                listChromatograms.Add(new ChromatogramSet("test-mzXML", new[] { MsDataFileUri.Parse(testFilesDir.GetTestPath("051309_digestion-s3.mzXML")) }));

                var docMzxml = docResults.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
                Assert.IsTrue(docContainer.SetDocument(docMzxml, docResults, true));
                docContainer.AssertComplete();
                docMzxml = docContainer.Document;
                // Verify mzXML and native contained same results
                // Unfortunately mzWiff produces chromatograms with now zeros, which
                // need to be interpolated into place.  This means a .wiff file and
                // its mzWiff mzXML file will never be the same.
                AssertResult.MatchChromatograms(docMzxml, 0, 1, -1, 0);
            }

            // TODO: Switch to a using clause when PWiz is fixed, and this assertion fails
//            AssertEx.ThrowsException<IOException>(() => testFilesDir.Dispose());
        }

        private static SrmDocument InitWiffDocument(TestFilesDir testFilesDir)
        {
            const LabelAtoms labelAtoms = LabelAtoms.C13 | LabelAtoms.N15;
            List<StaticMod> heavyMods = new List<StaticMod>
                {
                    new StaticMod("Heavy K", "K", ModTerminus.C, null, labelAtoms, null, null),
                    new StaticMod("Heavy R", "R", ModTerminus.C, null, labelAtoms, null, null),
                };
            SrmSettings settings = SrmSettingsList.GetDefault();
            settings = settings.ChangePeptideModifications(mods => mods.ChangeModifications(IsotopeLabelType.heavy, heavyMods));
            SrmDocument doc = new SrmDocument(settings);

            IdentityPath selectPath;
            string path = testFilesDir.GetTestPath("051309_transition list.csv");
            // Product m/z out of range
            var docError = doc;
            List<MeasuredRetentionTime> irtPeptides;
            List<SpectrumMzInfo> librarySpectra;
            List<TransitionImportErrorInfo> errorList;
            var inputs = new MassListInputs(path)
            {
                FormatProvider = CultureInfo.InvariantCulture,
                Separator = TextUtil.SEPARATOR_CSV
            };
            docError.ImportMassList(inputs, null, out selectPath, out irtPeptides, out librarySpectra, out errorList);
            Assert.AreEqual(errorList.Count, 1);
            AssertEx.AreComparableStrings(TextUtil.SpaceSeparate(Resources.MassListRowReader_CalcTransitionExplanations_The_product_m_z__0__is_out_of_range_for_the_instrument_settings__in_the_peptide_sequence__1_,
                                                Resources.MassListRowReader_CalcPrecursorExplanations_Check_the_Instrument_tab_in_the_Transition_Settings),
                                            errorList[0].ErrorMessage,
                                            2);
            Assert.AreEqual(errorList[0].Column, 2);
            Assert.AreEqual(errorList[0].LineNum, 19);

            doc = doc.ChangeSettings(settings.ChangeTransitionInstrument(inst => inst.ChangeMaxMz(1800)));
            inputs = new MassListInputs(path)
            {
                FormatProvider = CultureInfo.InvariantCulture,
                Separator = TextUtil.SEPARATOR_CSV
            };
            doc = doc.ImportMassList(inputs, null, null, out selectPath);

            AssertEx.IsDocumentState(doc, 2, 9, 9, 18, 54);
            return doc;
        }
    }
}