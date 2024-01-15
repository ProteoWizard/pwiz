/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that the QC trace chromatograms are correctly read from the data file.
    /// </summary>
    [TestClass]
    public class QcTraceChromatogramTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestQcTraceChromatograms()
        {
            TestFilesZip = @"TestFunctional\QcTraceChromatogramTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var expectedQcTraceNames = new List<string> { "Pump Pressure 1", "Pump Pressure 2" };
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("QcTraceChromatogramTest.sky")));
            ImportResultsFile(TestFilesDir.GetTestPath("QcTraceChromatogramTest.mzML"));
            var measuredResults = SkylineWindow.Document.MeasuredResults;
            Assert.IsNotNull(measuredResults);
            Assert.AreEqual(1, measuredResults.Chromatograms.Count);
            Assert.IsTrue(measuredResults.TryLoadAllIonsChromatogram(measuredResults.Chromatograms[0], ChromExtractor.qc, true, out var qcTraceChromatogramGroups));
            var qcTraceNames = measuredResults.QcTraceNames.ToList();
            CollectionAssert.AreEquivalent(expectedQcTraceNames, qcTraceNames);
            using var msDataFile = new MsDataFileImpl(TestFilesDir.GetTestPath("QcTraceChromatogramTest.mzML"));
            var qcTraces = msDataFile.GetQcTraces().ToList();
            Assert.AreEqual(expectedQcTraceNames.Count, qcTraces.Count);
            foreach (var qcTrace in qcTraces)
            { 
                CollectionAssert.Contains(expectedQcTraceNames, qcTrace.Name);
                var chromatogramGroupInfo = qcTraceChromatogramGroups.Single(chromGroupInfo =>
                    chromGroupInfo.QcTraceName == qcTrace.Name);
                Assert.IsNotNull(chromatogramGroupInfo);
                Assert.AreEqual(1, chromatogramGroupInfo.TransitionPointSets.Count());
                var timeIntensities = chromatogramGroupInfo.GetTransitionInfo(0, TransformChrom.raw).TimeIntensities;
                CollectionAssert.AreEqual(MsDataFileImpl.ToFloatArray(qcTrace.Times), timeIntensities.Times.ToList());
                CollectionAssert.AreEqual(MsDataFileImpl.ToFloatArray(qcTrace.Intensities), timeIntensities.Intensities.ToList());
            }
        }
    }
}
