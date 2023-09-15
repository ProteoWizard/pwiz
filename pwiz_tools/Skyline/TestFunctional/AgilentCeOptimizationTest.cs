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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AgilentCeOptimizationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAgilentCeOptimization()
        {
            TestFilesZip = @"TestFunctional\\AgilentCeOptimizationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            { 
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("AgilentCeTest.sky"));
            });
            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResults=>{
                importResults.OptimizationName = ExportOptimize.CE;
                importResults.NamedPathSets = new[]
                {
                    new KeyValuePair<string, MsDataFileUri[]>("Step-B_MethodB_0002",
                        new[] { new MsDataFilePath(TestFilesDir.GetTestPath("Step-B_MethodB_0002.d")) })
                };
                importResults.OkDialog();
            });
            WaitForDocumentLoaded();
            var document = SkylineWindow.Document;
            var measuredResults = document.Settings.MeasuredResults;
            Assert.AreEqual(1, measuredResults.Chromatograms.Count);
            var chromatogramSet = measuredResults.Chromatograms[0];
            Assert.IsNotNull(measuredResults);
            float tolerance = (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            foreach (var peptideDocNode in document.Molecules)
            {
                foreach (var transitionGroup in peptideDocNode.TransitionGroups)
                {
                    Assert.IsTrue(document.Settings.MeasuredResults.TryLoadChromatogram(chromatogramSet, peptideDocNode, transitionGroup, tolerance, out var chromatogramGroupInfos));
                    Assert.AreEqual(1, chromatogramGroupInfos.Length);
                    foreach (var transition in transitionGroup.Transitions)
                    {
                        var optStepChromatograms = chromatogramGroupInfos[0].GetAllTransitionInfo(transition, tolerance,
                            chromatogramSet.OptimizationFunction, TransformChrom.raw);
                        int chromatogramCount = CountChromatograms(optStepChromatograms);
                        Assert.AreEqual(7, chromatogramCount, "Incorrect number of chromatograms for Q1:{0} Q3:{1}", transitionGroup.PrecursorMz, transition.Mz);
                    }
                }
            }
        }

        private int CountChromatograms(OptStepChromatograms optStepChromatograms)
        {
            int count = 0;
            for (int optStep = -optStepChromatograms.StepCount; optStep <= optStepChromatograms.StepCount; optStep++)
            {
                if (optStepChromatograms.GetChromatogramForStep(optStep) != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
