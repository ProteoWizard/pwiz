/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Test for Nick's fix to a peak sorting problem where we would throw 
    /// "Unable to sort because the IComparer.Compare() method returns inconsistent results. Either a value does not compare 
    /// equal to itself, or one value repeatedly compared to another value yields different results. IComparer: ''
    /// 
    /// This test is quick to run, but it involves a large data file so it's here with the perf tests to avoid code repository bloat.
    /// </summary>
    [TestClass]
    public class TestPeakSorting : AbstractFunctionalTest
    {

        [TestMethod]
        public void PeakSortingTest()
        {
            TestFilesZip = GetPerfTestDataURL(@"PeakSortingTest.zip");
            TestFilesPersistent = new[] {"PeakSortingIssue.raw"};
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var resultsPath = TestFilesDir.GetTestPath("PeakSortingIssue.raw");
            var docPath = TestFilesDir.GetTestPath("PeakSortingIssue.sky");
            var doc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(doc, 0, 1, 3, 6, 90);
            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                var replicateName = Path.GetFileNameWithoutExtension(resultsPath);
                var listChromatograms =
                    new List<ChromatogramSet> { new ChromatogramSet(replicateName, new[] { MsDataFileUri.Parse(resultsPath) }) };
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
                Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
                docContainer.AssertComplete();
                docResults = docContainer.Document;
                AssertResult.IsDocumentResultsState(docResults, replicateName, 0, 0, 0, 0, 3);
            }
        }
    }
}