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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.IonMobility;
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

        [TestMethod]
        public void TestMeasuredDriftValuesNoRawData()
        {
            PerformTestMeasuredDriftValues(false, true);
        }

        private void PerformTestMeasuredDriftValues(bool asSmallMolecules, bool provokeError = false) 
        {
            if (asSmallMolecules)
            {
                if (SkipSmallMoleculeTestVersions())
                {
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
                var rawFilePath = testFilesDir.GetTestPath("ID12692_01_UCA168_3727_040714" + ExtensionTestContext.ExtMz5);
                var chromSets = new[]
                                {
                                    new ChromatogramSet(replicateName, new[]
                                        { new MsDataFilePath(rawFilePath),  }),
                                };
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
                Assert.IsTrue(docContainer.SetDocument(docResults, docOriginal, true));
                docContainer.AssertComplete();
                var document = docContainer.Document;

                // Clear out any current settings
                document = document.ChangeSettings(document.Settings.ChangeTransitionIonMobilityFiltering(s => TransitionIonMobilityFiltering.EMPTY));

                // Verify ability to extract predictions from raw data
                var libraryName0 = "test0";
                var dbPath0 = testFilesDir.GetTestPath(libraryName0 + IonMobilityDb.EXT);
                if (provokeError)
                {
                    File.Delete(rawFilePath);
                }

                TransitionIonMobilityFiltering newIMFiltering = null;
                try
                {
                    newIMFiltering = document.Settings.TransitionSettings.IonMobilityFiltering.ChangeLibrary(
                        IonMobilityLibrary.CreateFromResults(
                            document, docContainer.DocumentFilePath, true,
                            libraryName0, dbPath0));
                }
                catch (FileNotFoundException) // If we catch the missing file early, we get this unwrapped exception. If caught deeper in the process it's wrapped in a IOException
                {
                    if (provokeError)
                    {
                        return; // Expected
                    }
                }
                // ReSharper disable once PossibleNullReferenceException
                var result = newIMFiltering.IonMobilityLibrary.GetIonMobilityLibKeyMap().AsDictionary();
                Assert.AreEqual(1, result.Count);
                var expectedDT = 4.0019;
                var expectedOffset = .4829;
                Assert.AreEqual(expectedDT, result.Values.First().First().IonMobility.Mobility.Value, .001);
                Assert.AreEqual(expectedOffset, result.Values.First().First().HighEnergyIonMobilityValueOffset??0, .001);

                // Check ability to update, and to preserve unchanged
                var revised = new List<PrecursorIonMobilities>();
                var libKey = result.Keys.First();
                revised.Add(new PrecursorIonMobilities(libKey, IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(expectedDT=4, eIonMobilityUnits.drift_time_msec), null, expectedOffset=0.234)));  // N.B. CCS handling would require actual raw data in this test, it's covered in a perf test
                var pepSequence = "DEADEELS";
                var libKey2 = asSmallMolecules ?
                    new LibKey(SmallMoleculeLibraryAttributes.Create(pepSequence, "C12H5", null, null, null, null), Adduct.M_PLUS_2H) : 
                    new LibKey(pepSequence, Adduct.DOUBLY_PROTONATED);
                revised.Add(new PrecursorIonMobilities(libKey2, IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(5, eIonMobilityUnits.drift_time_msec), null, 0.123)));
                var libraryName = "test";
                var dbPath = testFilesDir.GetTestPath(libraryName+IonMobilityDb.EXT);
                var imsdb = IonMobilityDb.CreateIonMobilityDb(dbPath, libraryName, false, revised);
                var newLibIM = new IonMobilityLibrary(libraryName, dbPath, imsdb);
                var ionMobilityWindowWidthCalculator = new IonMobilityWindowWidthCalculator(
                    IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power, 40, 0, 0, 0);
                var calculator = ionMobilityWindowWidthCalculator;
                document = document.ChangeSettings(
                    document.Settings.ChangeTransitionIonMobilityFiltering(
                        imf => imf.ChangeFilterWindowWidthCalculator(calculator).
                            ChangeLibrary(newLibIM)));
                result = document.Settings.TransitionSettings.IonMobilityFiltering.IonMobilityLibrary.GetIonMobilityLibKeyMap().AsDictionary();
                Assert.AreEqual(2, result.Count);
                Assert.AreEqual(expectedDT, result[libKey].First().IonMobility.Mobility.Value, .001);
                Assert.AreEqual(expectedOffset, result[libKey].First().HighEnergyIonMobilityValueOffset??0, .001);
                Assert.AreEqual(5, result[libKey2].First().IonMobility.Mobility.Value, .001);
                Assert.AreEqual(0.123, result[libKey2].First().HighEnergyIonMobilityValueOffset??0, .001);

                ionMobilityWindowWidthCalculator = new IonMobilityWindowWidthCalculator(
                    IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.fixed_width, 40, 0, 0, 100);
                document = document.ChangeSettings(
                    document.Settings.ChangeTransitionIonMobilityFiltering(
                        imf => imf.ChangeFilterWindowWidthCalculator(ionMobilityWindowWidthCalculator).
                            ChangeLibrary(newLibIM)));
                result = document.Settings.TransitionSettings.IonMobilityFiltering.IonMobilityLibrary.GetIonMobilityLibKeyMap().AsDictionary();
                Assert.AreEqual(2, result.Count);
                Assert.AreEqual(expectedDT, result[libKey].First().IonMobility.Mobility.Value, .001);
                Assert.AreEqual(expectedOffset, result[libKey].First().HighEnergyIonMobilityValueOffset??0, .001);
                Assert.AreEqual(5, result[libKey2].First().IonMobility.Mobility.Value, .001);
                Assert.AreEqual(0.123, result[libKey2].First().HighEnergyIonMobilityValueOffset??0, .001);
            }
        }
    }
}
