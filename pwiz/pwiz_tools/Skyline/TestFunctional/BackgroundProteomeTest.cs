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
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
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
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var buildBackgroundProteomeDlg = ShowDialog<BuildBackgroundProteomeDlg>(
                peptideSettingsUI.ShowBuildBackgroundProteomeDlg);
            RunUI(() =>
                {
                    buildBackgroundProteomeDlg.BuildNew = true;
                    buildBackgroundProteomeDlg.BackgroundProteomeName = _backgroundProteomeName;
                    buildBackgroundProteomeDlg.BackgroundProteomePath =
                        TestFilesDir.GetTestPath(_backgroundProteomeName + ".protdb");
                    buildBackgroundProteomeDlg.AddFastaFile(TestFilesDir.GetTestPath("celegans_mini.fasta"));
                });
            OkDialog(buildBackgroundProteomeDlg, buildBackgroundProteomeDlg.OkDialog);
            RunUI(() =>
                {
                    peptideSettingsUI.MissedCleavages = 3;
                });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            // Wait until proteome digestion is done
            for (int i = 0; i < 1000; i ++ )
            {
                var peptideSettings = Program.ActiveDocument.Settings.PeptideSettings;
                var backgroundProteome = peptideSettings.BackgroundProteome;
                if (backgroundProteome.GetDigestion(peptideSettings) != null)
                {
                    break;
                }
                Thread.Sleep(100);
            }
            // Make sure digestion was successful
            var peptideSettingsFinal = Program.ActiveDocument.Settings.PeptideSettings;
            Assert.IsNotNull(peptideSettingsFinal.BackgroundProteome.GetDigestion(peptideSettingsFinal));

            RunUI(() =>
                {
                    SequenceTree sequenceTree = SkylineWindow.SequenceTree;
                    sequenceTree.BeginEdit(false);
                    sequenceTree.StatementCompletionEditBox.TextBox.Text = "Y18D10A.20";
                    sequenceTree.CommitEditBox(false);
                    sequenceTree.BeginEdit(false);
                    sequenceTree.StatementCompletionEditBox.TextBox.Text = "TISEVIAQGK";
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
            var peptideGroups = new List<PeptideGroupDocNode>(Program.ActiveDocument.PeptideGroups);
            Assert.AreEqual(2, peptideGroups.Count);
            Assert.AreEqual("Y18D10A.20", peptideGroups[0].Name);
            Assert.IsTrue(peptideGroups[0].AutoManageChildren);
            Assert.AreEqual("C37A2.7", peptideGroups[1].Name);
            Assert.IsFalse(peptideGroups[1].AutoManageChildren);
        }
    }
}