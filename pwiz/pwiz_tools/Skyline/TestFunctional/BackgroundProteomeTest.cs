/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional tests for the Background Proteome.
    /// </summary>
    [TestClass]
    public class BackgroundProteomeTest : AbstractFunctionalTest
    {
        private readonly String _backgroundProteomeName;
        
        public BackgroundProteomeTest()
        {
            _backgroundProteomeName = "celegans_mini";
        }

        [TestMethod]
        public void TestBackgroundProteome()
        {
            TestFilesZip = @"TestFunctional\BackgroundProteomeTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var protdbPath = TestFilesDir.GetTestPath(_backgroundProteomeName + ProteomeDb.EXT_PROTDB);
            CreateBackgroundProteome(protdbPath, _backgroundProteomeName, TestFilesDir.GetTestPath("celegans_mini.fasta"));
            WaitForProteinMetadataBackgroundLoaderCompleted();

            RunUI(() =>
                {
                    SequenceTree sequenceTree = SkylineWindow.SequenceTree;
                    sequenceTree.BeginEdit(false);
// ReSharper disable LocalizableElement
                    sequenceTree.StatementCompletionEditBox.TextBox.Text = "Y18D10A.20";    // Not L10N
// ReSharper restore LocalizableElement
                    sequenceTree.CommitEditBox(false);
                });
            WaitForProteinMetadataBackgroundLoaderCompleted();
            RunUI(() =>
                {
                    SequenceTree sequenceTree = SkylineWindow.SequenceTree;
                    sequenceTree.BeginEdit(false);
// ReSharper disable LocalizableElement
                    sequenceTree.StatementCompletionEditBox.TextBox.Text = "TISEVIAQGK";    // Not L10N
// ReSharper restore LocalizableElement
                });
            var statementCompletionForm = WaitForOpenForm<StatementCompletionForm>();
            Assert.IsNotNull(statementCompletionForm);

            RunUI(() =>
                {
                    SequenceTree sequenceTree = SkylineWindow.SequenceTree;
                    Assert.IsNotNull(sequenceTree.StatementCompletionEditBox);
                    sequenceTree.StatementCompletionEditBox.OnSelectionMade(
                        (StatementCompletionItem) statementCompletionForm.ListView.Items[0].Tag);
                });
            WaitForProteinMetadataBackgroundLoaderCompleted();
            var peptideGroups = new List<PeptideGroupDocNode>(Program.ActiveDocument.PeptideGroups);
            Assert.AreEqual(2, peptideGroups.Count);
            Assert.AreEqual("Y18D10A.20", peptideGroups[0].Name);
            Assert.IsTrue(peptideGroups[0].AutoManageChildren);
            Assert.AreEqual("C37A2.7", peptideGroups[1].Name);
            Assert.IsFalse(peptideGroups[1].AutoManageChildren);

            // Save and re-open with prot db moved to see MissingFileDlg
            int pepCount = SkylineWindow.Document.PeptideCount;
            string documentPath = TestFilesDir.GetTestPath("BackgroundProtDoc.sky");
            RunUI(() =>
            {
                SkylineWindow.SaveDocument(documentPath);
                SkylineWindow.SwitchDocument(new SrmDocument(SrmSettingsList.GetDefault()), null);
            });
            Assert.AreEqual(0, SkylineWindow.Document.PeptideCount);
            File.Move(protdbPath, TestFilesDir.GetTestPath(_backgroundProteomeName + "-copy" + ProteomeDb.EXT_PROTDB));
            RunDlg<MissingFileDlg>(() => SkylineWindow.OpenFile(documentPath),
                dlg => dlg.OkDialog());
            Assert.AreEqual(pepCount, SkylineWindow.Document.PeptideCount);
            RunUI(() => SkylineWindow.NewDocument());
            RunDlg<MissingFileDlg>(() => SkylineWindow.OpenFile(documentPath),
                dlg => dlg.CancelDialog());
            Assert.AreEqual(0, SkylineWindow.Document.PeptideCount);
        }

        public static void CreateBackgroundProteome(string protdbPath, string basename, string fastaFilePath)
        {
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var buildBackgroundProteomeDlg = ShowDialog<BuildBackgroundProteomeDlg>(
                peptideSettingsUI.ShowBuildBackgroundProteomeDlg);
            RunUI(() =>
            {
                buildBackgroundProteomeDlg.BackgroundProteomeName = basename;
                buildBackgroundProteomeDlg.BackgroundProteomePath = protdbPath;
                buildBackgroundProteomeDlg.AddFastaFile(fastaFilePath);
            });
            OkDialog(buildBackgroundProteomeDlg, buildBackgroundProteomeDlg.OkDialog);
            RunUI(() => { peptideSettingsUI.MissedCleavages = 3; });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            // Wait until proteome digestion is done
            WaitForCondition(100*1000, () =>
            {
                var peptideSettings = Program.ActiveDocument.Settings.PeptideSettings;
                var backgroundProteome = peptideSettings.BackgroundProteome;
                return backgroundProteome.HasDigestion(peptideSettings) &&
                    !backgroundProteome.NeedsProteinMetadataSearch;
            });
        }
    }
}