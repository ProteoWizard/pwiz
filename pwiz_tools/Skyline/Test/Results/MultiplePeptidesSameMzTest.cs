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
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Results
{
    /// <summary>
    /// test for fix of Issue 263: "Too strict about choosing only one precursor for every MS/MS scan in Targeted MS/MS"
    /// the three peptides in this data set have the same pass, formerly only one of them got assigned a precursor. 
    /// </summary>
    [TestClass]
    public class MultiplePeptidesSameMzTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"Test\Results\MultiplePeptidesSameMz.zip";

        [TestMethod]
        public void MultiplePeptidesSameMz()
        {
            RunMultiplePeptidesSameMz(RefinementSettings.ConvertToSmallMoleculesMode.none);
            RunMultiplePeptidesSameMz(RefinementSettings.ConvertToSmallMoleculesMode.formulas);
            RunMultiplePeptidesSameMz(RefinementSettings.ConvertToSmallMoleculesMode.masses_and_names);
            RunMultiplePeptidesSameMz(RefinementSettings.ConvertToSmallMoleculesMode.masses_only);
        }

        private void RunMultiplePeptidesSameMz(RefinementSettings.ConvertToSmallMoleculesMode asSmallMolecules)
        {
            if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.none)
                TestDirectoryName = asSmallMolecules.ToString();

            TestSmallMolecules = false;  // Don't need the magic test node, we have an explicit test

            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string docPath;
            SrmDocument document = InitMultiplePeptidesSameMzDocument(testFilesDir, out docPath);
            document = (new RefinementSettings()).ConvertToSmallMolecules(document, asSmallMolecules);
            var docContainer = new ResultsTestDocumentContainer(document, docPath);

            var doc = docContainer.Document;
            var listChromatograms = new List<ChromatogramSet>();
            var path = MsDataFileUri.Parse(@"AMultiplePeptidesSameMz\ljz_20131201k_Newvariant_standards_braf.mzML");
            listChromatograms.Add(AssertResult.FindChromatogramSet(doc, path) ??
                    new ChromatogramSet(path.GetFileName().Replace('.', '_'), new[] { path }));
            var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
            Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
            docContainer.AssertComplete();
            document = docContainer.Document;

            float tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            var results = document.Settings.MeasuredResults;
            foreach (var pair in document.MoleculePrecursorPairs)
            {
                ChromatogramGroupInfo[] chromGroupInfo;
                Assert.IsTrue(results.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup,
                    tolerance, true, out chromGroupInfo));
                Assert.AreEqual(1, chromGroupInfo.Length);  // without the fix, only the first pair will have a chromatogram
            }
            // now drill down for specific values
            int nPeptides = 0;
            foreach (var nodePep in document.Molecules.Where(nodePep => nodePep.Results[0] != null))
            {
                // expecting three peptide result in this small data set
                if (nodePep.Results[0].Sum(chromInfo => chromInfo.PeakCountRatio > 0 ? 1 : 0) > 0)
                {
                    Assert.AreEqual(34.2441024780273,(double)nodePep.GetMeasuredRetentionTime(0), .0001);
                    nPeptides++;
                }
            }
            Assert.AreEqual(3, nPeptides); // without the fix this will give just one result
            // Release file handles
            docContainer.Release();
            testFilesDir.Dispose();
        }

        private static SrmDocument InitMultiplePeptidesSameMzDocument(TestFilesDir testFilesDir, out string docPath)
        {
            return InitMultiplePeptidesSameMzDocument(testFilesDir, "Mutant Peptides  with Braf AG A00Y - Cut Down.sky", out docPath);
        }

        private static SrmDocument InitMultiplePeptidesSameMzDocument(TestFilesDir testFilesDir, string fileName, out string docPath)
        {
            docPath = testFilesDir.GetTestPath(fileName);
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(doc, 0, 3, 3, 6, 58);  // int revision, int groups, int peptides, int tranGroups, int transitions
            return doc;
        }
    }
}