/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests importing results from multi-injection SRM replicates in directories when
    /// some but not all of the precursors have matching chromatograms in more than one result
    /// file per replicate.
    /// </summary>
    [TestClass]
    public class MultiInjectionReplicatesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMultiInjectionReplicates()
        {
            TestFilesZip = @"TestFunctional\MultiInjectionReplicatesTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MultiInjectionReplicatesTest.sky")));
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            var namedPathSets = DataSourceUtil.GetDataSourcesInSubdirs(TestFilesDir.GetTestPath("RawFiles")).ToArray();
            var expectedReplicateNames = new[] {"Std_6", "Std_10"};
            Assert.AreEqual(expectedReplicateNames.Length, namedPathSets.Length);
            foreach (var replicateName in expectedReplicateNames)
            {
                var entry = namedPathSets.FirstOrDefault(kvp => kvp.Key == replicateName);
                Assert.IsNotNull(entry);
                Assert.AreEqual(2, entry.Value.Length);
            }
            RunUI(() =>
            {
                importResultsDlg.RadioCreateMultipleMultiChecked = true;
                importResultsDlg.NamedPathSets = namedPathSets;
            });
            RunDlg<ImportResultsNameDlg>(importResultsDlg.OkDialog,
                importResultsNameDlg => importResultsNameDlg.NoDialog());
            WaitForConditionUI(() => SkylineWindow.Document.Settings.HasResults);
            WaitForDocumentLoaded();

            // Verify that the two molecules have the expected number of chromatograms
            var firstMolecule = SkylineWindow.Document.Molecules.First();
            foreach (var transitionGroup in firstMolecule.TransitionGroups)
            {
                Assert.AreEqual(expectedReplicateNames.Length, transitionGroup.Results.Count);
                foreach (var replicateResults in transitionGroup.Results)
                {
                    // For the first molecule, we expect that each transition group can only be found in one result file per replicate
                    Assert.AreEqual(1, replicateResults.Count);
                }
            }

            var secondMolecule = SkylineWindow.Document.Molecules.Skip(1).First();
            for (int iTransitionGroup = 0; iTransitionGroup < secondMolecule.Children.Count; iTransitionGroup++)
            {
                var transitionGroup = (TransitionGroupDocNode) secondMolecule.Children[iTransitionGroup];
                Assert.AreEqual(expectedReplicateNames.Length, transitionGroup.Results.Count);
                foreach (var replicateResults in transitionGroup.Results)
                {
                    // For the second molecule, we expect that the first two transition groups can be found in one result file,
                    // and the rest of the transition groups have chromatograms from two result files.
                    if (iTransitionGroup < 2)
                    {
                        Assert.AreEqual(1, replicateResults.Count);
                    }
                    else
                    {
                        Assert.AreEqual(2, replicateResults.Count);
                    }
                }
            }
        }
    }
}
