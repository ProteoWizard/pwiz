/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Load a small agilent results file with ramped CE and check against curated results.
    /// Actually it's an mzML file of the first 100 scans in a larger Agilent file
    /// but it still tests the MS2+rampedCE code.
    /// 
    /// </summary>
    [TestClass]
    public class AgilentMseTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestData\Results\AgilentMse.zip";



        [TestMethod]
        public void AgilentMseChromatogramTest()
        {
            DoAgilentMseChromatogramTest(RefinementSettings.ConvertToSmallMoleculesMode.none);
        }

        [TestMethod]
        public void AgilentMseChromatogramTestAsSmallMolecules()
        {
            DoAgilentMseChromatogramTest(RefinementSettings.ConvertToSmallMoleculesMode.formulas);
        }

        public enum small_mol_mode
        {
            simple,
            invert_charges,
            invert_charges_and_data
        };

        [TestMethod]
        public void AgilentMseChromatogramTestAsNegativeSmallMolecules()
        {
            DoAgilentMseChromatogramTest(RefinementSettings.ConvertToSmallMoleculesMode.formulas, small_mol_mode.invert_charges, 
                Resources.ChromCacheBuilder_BuildCache_This_document_contains_only_negative_ion_mode_transitions__and_the_imported_file_contains_only_positive_ion_mode_data_so_nothing_can_be_loaded_);
            DoAgilentMseChromatogramTest(RefinementSettings.ConvertToSmallMoleculesMode.formulas, small_mol_mode.invert_charges_and_data);
        }

        [TestMethod]
        public void AgilentMseChromatogramTestAsSmallMoleculeMasses()
        {
            DoAgilentMseChromatogramTest(RefinementSettings.ConvertToSmallMoleculesMode.masses_only);
        }

        public void DoAgilentMseChromatogramTest(RefinementSettings.ConvertToSmallMoleculesMode asSmallMolecules, small_mol_mode smallMolMode = small_mol_mode.simple, string expectedError = null)
        {
            if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.none && smallMolMode == small_mol_mode.simple && SkipSmallMoleculeTestVersions())
            {
                return;
            }

            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string docPath;
            SrmDocument document = InitAgilentMseDocument(testFilesDir, out docPath);
            if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.none)
            {
                var refine = new RefinementSettings();
                document = refine.ConvertToSmallMolecules(document, testFilesDir.FullPath, asSmallMolecules, smallMolMode == small_mol_mode.simple ? RefinementSettings.ConvertToSmallMoleculesChargesMode.none : RefinementSettings.ConvertToSmallMoleculesChargesMode.invert);
            }
            using (var docContainer = new ResultsTestDocumentContainer(document, docPath))
            {
                var doc = docContainer.Document;
                var listChromatograms = new List<ChromatogramSet>();
                var path = MsDataFileUri.Parse(smallMolMode == small_mol_mode.invert_charges_and_data ? @"AgilentMse\BSA-AI-0-10-25-41_first_100_scans_neg.mzML" : @"AgilentMse\BSA-AI-0-10-25-41_first_100_scans.mzML");
                listChromatograms.Add(AssertResult.FindChromatogramSet(doc, path) ??
                        new ChromatogramSet(path.GetFileName().Replace('.', '_'), new[] { path }));
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
                Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
                if (expectedError != null)
                {
                    docContainer.AssertError(expectedError);
                }
                else
                {
                    docContainer.AssertComplete();
                    document = docContainer.Document;

                    float tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
                    var results = document.Settings.MeasuredResults;
                    foreach (var pair in document.MoleculePrecursorPairs)
                    {
                        ChromatogramGroupInfo[] chromGroupInfo;
                        Assert.IsTrue(results.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup,
                            tolerance, out chromGroupInfo));
                        Assert.AreEqual(1, chromGroupInfo.Length);
                        VerifyMs1Truncated(chromGroupInfo.First());
                    }

                    // now drill down for specific values
                    int nPeptides = 0;
                    foreach (var nodePep in document.Molecules.Where(nodePep => !nodePep.Results[0].IsEmpty))
                    {
                        // expecting just one peptide result in this small data set
                        if (nodePep.Results[0].Any(chromInfo => chromInfo.PeakCountRatio > 0))
                        {
                            Assert.AreEqual(0.25205, 
                                (double)nodePep.GetMeasuredRetentionTime(0), .0001, "averaged retention time differs in node " + nodePep.ModifiedTarget);
                            Assert.AreEqual(0.3333, (double)nodePep.GetPeakCountRatio(0), 0.0001);
                            nPeptides++;
                        }
                    }
                    Assert.AreEqual(smallMolMode == small_mol_mode.invert_charges ? 0 : 1, nPeptides); // If we switched document polarity, we'd expect no chromatograms extracted
                }
            }
            testFilesDir.Dispose();
        }

        /// <summary>
        /// Verifies that the MS1 chromatograms have been truncated down to just slightly larger than the MS2 chromatograms.
        /// </summary>
        /// <param name="chromatogramGroupInfo"></param>
        private void VerifyMs1Truncated(ChromatogramGroupInfo chromatogramGroupInfo)
        {
            var ms1Chromatograms = new List<ChromatogramInfo>();
            var fragmentChromatograms = new List<ChromatogramInfo>();
            for (int i = 0; i < chromatogramGroupInfo.NumTransitions; i++)
            {
                var chromatogramInfo = chromatogramGroupInfo.GetRawTransitionInfo(i);
                if (chromatogramInfo.Source == ChromSource.fragment)
                {
                    fragmentChromatograms.Add(chromatogramInfo);
                }
                else if (chromatogramInfo.Source == ChromSource.ms1)
                {
                    ms1Chromatograms.Add(chromatogramInfo);
                }
            }
            if (!fragmentChromatograms.Any())
            {
                return;
            }
            double minFragmentTime = fragmentChromatograms.Min(chrom => chrom.Times.First());
            double maxFragmentTime = fragmentChromatograms.Max(chrom => chrom.Times.Last());
            foreach (var chromatogram in ms1Chromatograms)
            {
                Assert.IsTrue(chromatogram.Times[1] > minFragmentTime);
                Assert.IsTrue(chromatogram.Times[chromatogram.Times.Count - 2] < maxFragmentTime);
            }
        }

        private static SrmDocument InitAgilentMseDocument(TestFilesDir testFilesDir, out string docPath)
        {
            return InitAgilentMseDocument(testFilesDir, "Agilent-allions-BSA_first_100_scans.sky", out docPath);
        }

        private static SrmDocument InitAgilentMseDocument(TestFilesDir testFilesDir, string fileName, out string docPath)
        {
            docPath = testFilesDir.GetTestPath(fileName);
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(doc, 0, 1, 14, 14, 45);  // int revision, int groups, int peptides, int tranGroups, int transitions
            return doc;
        }
    }
}