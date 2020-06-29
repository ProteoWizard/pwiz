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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
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
            PerformTestMeasuredDriftValues(false);
        }

        [TestMethod]
        public void TestMeasuredDriftValuesAsSmallMolecules()
        {
            PerformTestMeasuredDriftValues(true);
        }

        private void PerformTestMeasuredDriftValues(bool asSmallMolecules) 
        {
            if (asSmallMolecules)
            {
                if (!RunSmallMoleculeTestVersions)
                {
                    Console.Write(MSG_SKIPPING_SMALLMOLECULE_TEST_VERSION);
                    return;
                }
            }
            var testFilesDir = new TestFilesDir(TestContext, @"TestData\Results\BlibDriftTimeTest.zip"); // Re-used from BlibDriftTimeTest
            // Open document with some peptides but no results
            var docPath = testFilesDir.GetTestPath("BlibDriftTimeTest.sky");
            // This was a malformed document, which caused problems after a fix to not recalculate
            // document library settings on open. To avoid rewriting this test for the document
            // which now contains 2 precursors, the first precursor is removed immediately.
            SrmDocument docOriginal = ResultsUtil.DeserializeDocument(docPath);
            var pathFirstPeptide = docOriginal.GetPathTo((int) SrmDocument.Level.Molecules, 0);
            var nodeFirstPeptide = (DocNodeParent) docOriginal.FindNode(pathFirstPeptide);
            docOriginal = (SrmDocument) docOriginal.RemoveChild(pathFirstPeptide, nodeFirstPeptide.Children[0]);
            if (asSmallMolecules)
            {
                var refine = new RefinementSettings();
                docOriginal = refine.ConvertToSmallMolecules(docOriginal, testFilesDir.FullPath);
            }
            using (var docContainer = new ResultsTestDocumentContainer(docOriginal, docPath))
            {
                var doc = docContainer.Document;

                // Import an mz5 file that contains drift info
                const string replicateName = "ID12692_01_UCA168_3727_040714";
                var chromSets = new[]
                                {
                                    new ChromatogramSet(replicateName, new[]
                                        { new MsDataFilePath(testFilesDir.GetTestPath("ID12692_01_UCA168_3727_040714" + ExtensionTestContext.ExtMz5)),  }),
                                };
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
                Assert.IsTrue(docContainer.SetDocument(docResults, docOriginal, true));
                docContainer.AssertComplete();
                var document = docContainer.Document;
                document = document.ChangeSettings(document.Settings.ChangePeptidePrediction(prediction => new PeptidePrediction(null, IonMobilityPredictor.EMPTY)));

                // Verify ability to extract predictions from raw data
                var newPred = document.Settings.PeptideSettings.Prediction.IonMobilityPredictor.ChangeMeasuredIonMobilityValuesFromResults(
                        document, docContainer.DocumentFilePath, true);
                var result = newPred.MeasuredMobilityIons;
                Assert.AreEqual(1, result.Count);
                const double expectedDT = 4.0019;
                var expectedOffset = .4829;
                Assert.AreEqual(expectedDT, result.Values.First().IonMobility.Mobility.Value, .001);
                Assert.AreEqual(expectedOffset, result.Values.First().HighEnergyIonMobilityValueOffset, .001);

                // Check ability to update, and to preserve unchanged
                var revised = new Dictionary<LibKey, IonMobilityAndCCS>();
                var libKey = result.Keys.First();
                revised.Add(libKey, IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(4, eIonMobilityUnits.drift_time_msec), null, 0.234));  // N.B. CCS handling would require actual raw data in this test, it's covered in a perf test
                var libKey2 = new LibKey("DEADEELS", asSmallMolecules ? Adduct.NonProteomicProtonatedFromCharge(2) : Adduct.DOUBLY_PROTONATED);
                revised.Add(libKey2, IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(5, eIonMobilityUnits.drift_time_msec), null, 0.123));
                document =
                    document.ChangeSettings(
                        document.Settings.ChangePeptidePrediction(prediction => new PeptidePrediction(null, new IonMobilityPredictor("test", revised, null, null, IonMobilityWindowWidthCalculator.IonMobilityPeakWidthType.resolving_power, 40, 0, 0))));
                newPred = document.Settings.PeptideSettings.Prediction.ChangeDriftTimePredictor(
                    document.Settings.PeptideSettings.Prediction.IonMobilityPredictor.ChangeMeasuredIonMobilityValuesFromResults(
                        document, docContainer.DocumentFilePath, true)).IonMobilityPredictor;
                result = newPred.MeasuredMobilityIons;
                Assert.AreEqual(2, result.Count);
                Assert.AreEqual(expectedDT, result[libKey].IonMobility.Mobility.Value, .001);
                Assert.AreEqual(expectedOffset, result[libKey].HighEnergyIonMobilityValueOffset, .001);
                Assert.AreEqual(5, result[libKey2].IonMobility.Mobility.Value, .001);
                Assert.AreEqual(0.123, result[libKey2].HighEnergyIonMobilityValueOffset, .001);
            }
        }
    }
}
