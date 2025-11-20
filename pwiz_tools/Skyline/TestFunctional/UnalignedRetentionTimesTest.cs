/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class UnalignedRetentionTimesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestUnalignedRetentionTimes()
        {
            TestFilesZip = @"TestFunctional\UnalignedRetentionTimesTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("UnalignedRtTest.sky"));
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 48);
            });
            WaitForDocumentLoaded();
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                peptideSettingsUi.AlignmentTarget = AlignmentTargetSpec.Library.ChangeName("FirstHalf");
                peptideSettingsUi.OkDialog();
            });
            WaitForDocumentLoaded();
            Assert.IsTrue(SkylineWindow.Document.Settings.HasAlignedTimes());

            // The libraries "FirstHalf.blib" and "SecondHalf.blib" only have a single peptide in common.
            // For this reason, none of the runs in "SecondHalf.blib" could be aligned with "FirstHalf.blib".

            Assert.IsTrue(SkylineWindow.Document.Settings.HasUnalignedTimes());
            // Replace the library "FirstHalf.blib" with "FirstTwoReplicates.blib".
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                RunUI(() => peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Library);
                RunLongDlg<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUi.EditLibraryList,
                    libListDlg =>
                    {
                        RunDlg<EditLibraryDlg>(libListDlg.AddItem, editLibraryDlg =>
                        {
                            editLibraryDlg.LibraryName = "FirstTwoReplicates";
                            editLibraryDlg.LibraryPath = TestFilesDir.GetTestPath("FirstTwoReplicates.blib");
                            editLibraryDlg.OkDialog();
                        });
                        RunUI(() =>
                        {
                            libListDlg.MoveItemUp();
                            Assert.AreEqual("FirstTwoReplicates FirstHalf SecondHalf",
                                TextUtil.SpaceSeparate(libListDlg.GetAllEdited().Select(libSpec => libSpec.Name)));
                        });
                    }, libListDlg => libListDlg.OkDialog());
                RunUI(() =>
                {
                    peptideSettingsUi.PickedLibraries = new[] { "FirstTwoReplicates", "SecondHalf" };
                    peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                    peptideSettingsUi.AlignmentTarget = AlignmentTargetSpec.Library.ChangeName("FirstTwoReplicates");
                });
            }, peptideSettingsUi => peptideSettingsUi.OkDialog());
            WaitForDocumentLoaded();
            Assert.IsTrue(SkylineWindow.Document.Settings.HasAlignedTimes());
            // "FirstTwoReplicates.blib" has the complete set of peptides, so the runs in "SecondHalf.blib" could be aligned.
            Assert.IsFalse(SkylineWindow.Document.Settings.HasUnalignedTimes());
        }
    }
}
