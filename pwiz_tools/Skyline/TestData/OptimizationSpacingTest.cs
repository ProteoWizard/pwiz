/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
{
    /// <summary>
    /// Verifies <see cref="ChromatogramInfo.IsOptimizationSpacing"/> for
    /// a particular .wiff file where the Q3 values had all been rounded off to 3
    /// decimal places.
    /// </summary>
    [TestClass]
    public class OptimizationSpacingTest : AbstractUnitTest
    {
        private const string TEST_FILES_ZIP_PATH = @"TestData\OptimizationSpacingTest.zip";
        private const string WIFF_FILE_NAME = "04152024_HWAP8_LAMP1_NOTCH2_HHL_schedul_21min_R1_1.wiff";
        /// <summary>
        /// The chromatograms in the test wiff file did not have any CE values which were less than 10.
        /// <see cref="AbiMassListExporter.WriteTransition"/>
        /// </summary>
        private const double MIN_SCIEX_CE = 10;

        [TestMethod]
        public void TestIsOptimizationSpacing()
        {
            AssertIsOptimizationSpacing(true, 732.367, 732.378);
            AssertIsOptimizationSpacing(false, 732.367, 732.3782);
        }

        private void AssertIsOptimizationSpacing(bool expected, double mz1, double mz2)
        {
            Assert.AreEqual(expected, ChromatogramInfo.IsOptimizationSpacing(mz1, mz2),
                "IsOptimizationSpacing({0}, {1}) should be {2}", mz1, mz2, expected);
        }

        /// <summary>
        /// Reads all the chromatograms in the .wiff file and ensures that
        /// the chromatograms with similar Q1 and Q3 values all belong to the same
        /// group of optimization chromatograms.
        /// </summary>
        [TestMethod]
        public void TestVerifyOptimizationSpacingInFile()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_FILES_ZIP_PATH);
            var msDataFileUri = new MsDataFilePath(TestFilesDir.GetTestPath(WIFF_FILE_NAME));
            foreach (var chromKeyGroup in ReadChromatogramKeys(msDataFileUri)
                         .GroupBy(key => Tuple.Create(Math.Round(key.Precursor), Math.Round(key.Product))))
            {
                var precursorMzs = chromKeyGroup.Select(key => key.Precursor).Distinct().ToList();
                Assert.AreEqual(1, precursorMzs.Count);
                var productMzs = chromKeyGroup.Select(key => key.Product.RawValue).OrderBy(mz => mz).ToList();
                for (int i = 1; i < productMzs.Count; i++)
                {
                    AssertIsOptimizationSpacing(true, productMzs[i - 1], productMzs[i]);
                }
            }
        }

        private IEnumerable<ChromKey> ReadChromatogramKeys(MsDataFileUri msDataFileUri)
        {
            using var msDataFile = msDataFileUri.OpenMsDataFile(false, false, false, false, false);
            for (int i = 0; i < msDataFile.ChromatogramCount; i++)
            {
                var id = msDataFile.GetChromatogramId(i, out _);
                if (ChromKey.IsKeyId(id))
                {
                    yield return ChromKey.FromId(msDataFile.GetChromatogramId(i, out _), false);
                }
            }
        }

        /// <summary>
        /// Import a .wiff file optimizing for collision energy, and verify that
        /// every transition has the expected number of optimization steps.
        /// </summary>
        [TestMethod]
        public void TestImportOptimizationChromatograms()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_FILES_ZIP_PATH);
            var docPath = TestFilesDir.GetTestPath("OptimizationSpacingTest.sky");
            using var docContainer = new ResultsTestDocumentContainer(ResultsUtil.DeserializeDocument(docPath), docPath);
            var resultsPath = TestFilesDir.GetTestPath(WIFF_FILE_NAME);
            var optRegression = docContainer.Document.Settings.TransitionSettings.Prediction.CollisionEnergy;
            var resultsUri = new MsDataFilePath(resultsPath);
            var chromSet = new ChromatogramSet("Optimize", new[] { resultsUri }, Annotations.EMPTY, optRegression);
            ImportChromatogramSet(docContainer, chromSet);
        }

        private void ImportChromatogramSet(ResultsTestDocumentContainer docContainer, ChromatogramSet chromatogramSet)
        {
            var measuredResults = new MeasuredResults(new[] { chromatogramSet });
            docContainer.ResetProgress();
            docContainer.SetDocument(docContainer.Document.ChangeMeasuredResults(measuredResults),
                docContainer.Document, true);
            docContainer.AssertComplete();
            docContainer.Document.SerializeToFile(docContainer.DocumentFilePath, docContainer.DocumentFilePath, SkylineVersion.V23_1, new SilentProgressMonitor());
            VerifyOptimizationStepCounts(docContainer.Document);
        }

        private void VerifyOptimizationStepCounts(SrmDocument document)
        {
            Assert.IsNotNull(document.MeasuredResults);
            int replicateCount = document.MeasuredResults.Chromatograms.Count;

            foreach (var nodePep in document.Molecules)
            {
                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    foreach (var nodeTran in nodeGroup.Transitions)
                    {
                        var message = string.Format("Peptide: {0} Precursor: {1} Transition: {2}", 
                            nodePep, nodeGroup, nodeTran.Transition);
                        Assert.IsNotNull(nodeTran.Results, message);
                        Assert.AreEqual(replicateCount, nodeTran.Results.Count, message);
                        var chromInfoList = nodeTran.Results[replicateCount - 1];
                        Assert.IsNotNull(chromInfoList);

                        int expectedStepCount = GetExpectedOptStepCount(document, nodePep, nodeGroup, nodeTran);
                        int actualStepCount = chromInfoList.AsEnumerable().Count(chromInfo=>!chromInfo.IsEmpty);
                        Assert.AreEqual(expectedStepCount, actualStepCount, message);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the number of optimization steps which would be expected for a particular transition.
        /// </summary>
        private int GetExpectedOptStepCount(SrmDocument document, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
        {
            int validStepCount = 0;
            var regression = document.Settings.TransitionSettings.Prediction.CollisionEnergy;
            for (int step = -regression.StepCount; step <= regression.StepCount; step++)
            {
                var collisionEnergy = document.GetCollisionEnergy(nodePep, nodeGroup, nodeTran, step);
                if (collisionEnergy >= MIN_SCIEX_CE)
                {
                    validStepCount++;
                }
            }

            return validStepCount;
        }
    }
}
