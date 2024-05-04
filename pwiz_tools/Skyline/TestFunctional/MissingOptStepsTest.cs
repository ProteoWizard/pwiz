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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that when a transition is missing the full set of optimization step chromatograms,
    /// the chromatograms get assigned to the correct step numbers by
    /// <see cref="ChromatogramDataProvider.SetOptStepsForGroup"/>.
    /// </summary>
    [TestClass]
    public class MissingOptStepsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMissingOptSteps()
        {
            TestFilesZip = @"TestFunctional\MissingOptStepsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MissingOptStepsTest.sky")));
            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg=>
            {
                importResultsDlg.OptimizationName = ExportOptimize.CE;
                importResultsDlg.RadioAddNewChecked = true;
                importResultsDlg.NamedPathSets = new[]
                {
                    new KeyValuePair<string, MsDataFileUri[]>("Chromatograms", new[]
                    {
                        new MsDataFilePath(TestFilesDir.GetTestPath("Step-B_MethodB_0001.mzML")),
                        new MsDataFilePath(TestFilesDir.GetTestPath("Step-B_MethodB_0002.mzML"))
                    })
                };
                importResultsDlg.OkDialog();
            });
            WaitForDocumentLoaded();

            // Verify that all the transitions have the expected number of optimization steps
            foreach (var peptideDocNode in SkylineWindow.Document.Molecules)
            {
                // We expect all peptides except for one to have the full set of optimization steps
                var expectedMinOptStep = -3;
                if (peptideDocNode.Peptide.Sequence == "ATEHLSTLSEK")
                {
                    expectedMinOptStep = -2;
                }
                var expectedMaxOptStep = 3;
                foreach (var precursor in peptideDocNode.TransitionGroups)
                {
                    foreach (var transition in precursor.Transitions)
                    {
                        var nonEmptyResults = transition.Results[0].Where(chromInfo => !chromInfo.IsEmpty).ToList();
                        var minOptStep = nonEmptyResults
                            .Min(transitionChromInfo => transitionChromInfo.OptimizationStep);
                        var maxOptStep = nonEmptyResults
                            .Max(transitionChromInfo => transitionChromInfo.OptimizationStep);
                        string message = string.Format("Peptide: {0} Precursor: {1} Transition: {2}", 
                            peptideDocNode, precursor, transition.Transition);
                        Assert.AreEqual(expectedMinOptStep, minOptStep, message);
                        Assert.AreEqual(expectedMaxOptStep, maxOptStep, message);
                    }
                }
            }
        }
    }
}
