/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Results
{
    /// <summary>
    /// Unit test for deriving drift time filter values from observed data
    /// </summary>
    [TestClass]
    public class MeasuredDriftValuesTest : AbstractUnitTest
    {

        [TestMethod]
        public void TestMeasuredDriftValues()
        {

            var testFilesDir = new TestFilesDir(TestContext, @"Test\Results\BlibDriftTimeTest.zip"); // Re-used from BlibDriftTimeTest
            // Open document with some peptides but no results
            var docPath = testFilesDir.GetTestPath("BlibDriftTimeTest.sky");
            SrmDocument docOriginal = ResultsUtil.DeserializeDocument(docPath);
            var docContainer = new ResultsTestDocumentContainer(docOriginal, docPath);
            var doc = docContainer.Document;

            // Import an mz5 file that contains drift info
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
            document = document.ChangeSettings(document.Settings.ChangePeptidePrediction(prediction => new PeptidePrediction(null, DriftTimePredictor.EMPTY)));

            // Verify ability to extract predictions from raw data
            var newPred = document.Settings.PeptideSettings.Prediction.DriftTimePredictor.ChangeMeasuredDriftTimesFromResults(
                    document, docContainer.DocumentFilePath);
            var result = newPred.MeasuredDriftTimePeptides;
            Assert.AreEqual(TestSmallMolecules? 2: 1, result.Count);
            const double expectedDT = 4.0019;
            var expectedOffset = .4829;
            Assert.AreEqual(expectedDT, result.Values.First().DriftTimeMsec(false).Value, .001);
            Assert.AreEqual(expectedOffset, result.Values.First().HighEnergyDriftTimeOffsetMsec, .001);

            // Check ability to update, and to preserve unchanged
            var revised = new Dictionary<LibKey, DriftTimeInfo>();
            var libKey = result.Keys.First();
            revised.Add(libKey, new DriftTimeInfo(4, 0.234));
            var libKey2 = new LibKey("DEADEELS",2);
            revised.Add(libKey2, new DriftTimeInfo(5, 0.123));
            document =
                document.ChangeSettings(
                    document.Settings.ChangePeptidePrediction(prediction => new PeptidePrediction(null, new DriftTimePredictor("test", revised, null, null, 40))));
            newPred = document.Settings.PeptideSettings.Prediction.ChangeDriftTimePredictor(
                document.Settings.PeptideSettings.Prediction.DriftTimePredictor.ChangeMeasuredDriftTimesFromResults(
                    document, docContainer.DocumentFilePath)).DriftTimePredictor;
            result = newPred.MeasuredDriftTimePeptides;
            Assert.AreEqual(TestSmallMolecules ? 3 : 2, result.Count);
            Assert.AreEqual(expectedDT, result[libKey].DriftTimeMsec(false).Value, .001);
            Assert.AreEqual(expectedOffset, result[libKey].HighEnergyDriftTimeOffsetMsec, .001);
            Assert.AreEqual(5, result[libKey2].DriftTimeMsec(false).Value, .001);
            Assert.AreEqual(0.123, result[libKey2].HighEnergyDriftTimeOffsetMsec, .001);
				

            docContainer.Release();
        }
    }
}
