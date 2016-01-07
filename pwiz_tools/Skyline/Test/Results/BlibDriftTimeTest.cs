/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Results
{
    /// <summary>
    /// Unit test for drift time data in Bibliospec libraries,
    /// specifically for the ability to borrow from sublibraries
    /// when the drift time for a charged peptide isn't found in
    /// in the sublibrary associated with the original final_fragment.csv
    /// </summary>
    [TestClass]
    public class BlibDriftTimeTest : AbstractUnitTest
    {

        [TestMethod]
        public void TestBlibDriftTimes()
        {
            var testFilesDir = new TestFilesDir(TestContext, @"Test\Results\BlibDriftTimeTest.zip");
            // Open document with some peptides but no results
            var docPath = testFilesDir.GetTestPath("BlibDriftTimeTest.sky");
            SrmDocument docOriginal = ResultsUtil.DeserializeDocument(docPath);
            var docContainer = new ResultsTestDocumentContainer(docOriginal, docPath);
            var doc = docContainer.Document;

            // Use the bare drift times in the spectral library
            var librarySpec = new BiblioSpecLiteSpec("drift test",
                                                testFilesDir.GetTestPath("BlibDriftTimeTest.blib"));
            doc = doc.ChangeSettings(
                doc.Settings.ChangePeptideLibraries(lib => lib.ChangeLibrarySpecs(new[] { librarySpec })).
                ChangePeptidePrediction(p => p.ChangeLibraryDriftTimesResolvingPower(20)).
                ChangePeptidePrediction(p => p.ChangeUseLibraryDriftTimes(true))
                );

            // Import an mz5 file that needs drift info that's in the original data set, 
            // but preserved in the .blib file associated with a different raw source
            // Without the bugfix this won't get any drift time filtering.
            const string replicateName = "ID12692_01_UCA168_3727_040714";
            var chromSets = new[]
                                {
                                    new ChromatogramSet(replicateName, new[]
                                        { new MsDataFilePath(testFilesDir.GetTestPath("ID12692_01_UCA168_3727_040714.mz5")),  }),
                                };
            var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
            Assert.IsTrue(docContainer.SetDocument(docResults, docOriginal, true));
            docContainer.AssertComplete();
            var document = docContainer.Document;

            float tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            double maxHeight = 0;
            var results = document.Settings.MeasuredResults;
            Assert.AreEqual(2, document.PeptidePrecursorPairs.Count());
            var pair = document.PeptidePrecursorPairs.ToArray()[1];
            ChromatogramGroupInfo[] chromGroupInfo;
            Assert.IsTrue(results.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup,
                tolerance, true, out chromGroupInfo));
            Assert.AreEqual(1, chromGroupInfo.Length);
            var chromGroup = chromGroupInfo[0];
            Assert.AreEqual(2 , chromGroup.NumPeaks); // This will be higher if we don't filter on DT
            foreach (var tranInfo in chromGroup.TransitionPointSets)
            {
                maxHeight = Math.Max(maxHeight, tranInfo.MaxIntensity);
            }
            Assert.AreEqual(278 , maxHeight, 1);  // Without DT filtering, this will be much greater - about 996

            docContainer.Release();
        }
    }
}
