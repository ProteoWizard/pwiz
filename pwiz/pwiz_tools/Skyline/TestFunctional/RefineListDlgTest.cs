/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RefineListDlgTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRefineListDlg()
        {
            RunFunctionalTest();
        }

        private const string PEPTIDE_MODIFIED = "PESTICIDER";
        private const string PEPTIDE_UNMODIFIED = "NEVER";
        private const string PEPTIDE_EXTRA = "CHECK";

        protected override void DoTest()
        {
            var document = new SrmDocument(SrmSettingsList.GetDefault());
            var staticMods = document.Settings.PeptideSettings.Modifications.StaticModifications.ToList();
            staticMods.Add(UniMod.GetModification("Phospho (ST)", true).ChangeVariable(true));
            var heavyMods = new List<StaticMod> {UniMod.GetModification("Label:13C(6)15N(4) (C-term R)", false)};
            document = document.ChangeSettings(document.Settings
                .ChangePeptideModifications(mods => mods.ChangeStaticModifications(staticMods).ChangeHeavyModifications(heavyMods))
                .ChangeTransitionFilter(filt => filt.ChangePrecursorCharges(new [] {2, 3, 4, 5})));
            Assert.IsTrue(SkylineWindow.SetDocument(document, SkylineWindow.Document));

            RunUI(() => SkylineWindow.Paste(TextUtil.LineSeparate(PEPTIDE_MODIFIED, PEPTIDE_UNMODIFIED)));
            document = WaitForDocumentChangeLoaded(document);
            RunUI(() => SkylineWindow.Paste(PEPTIDE_EXTRA));
            var docPaste1 = WaitForDocumentChangeLoaded(document);
            AssertEx.IsDocumentState(docPaste1, null, 2, 6, 44, 129);

            TestErrorMessages();
            TestUse();
        }

        private static void TestErrorMessages()
        {
            var document = SkylineWindow.Document;
            var dlgRefineList = ShowDialog<RefineListDlg>(SkylineWindow.AcceptPeptides);
            // Test invalide lines
            RunUI(() => dlgRefineList.PeptidesText = "1234");
            RunDlg<MessageDlg>(dlgRefineList.OkDialog, dlg =>
            {
                AssertEx.AreComparableStrings(Resources.RefineListDlg_OkDialog_The_sequence__0__is_not_a_valid_peptide,
                    dlg.Message, 1);
                dlg.OkDialog();
            });
            RunUI(() => dlgRefineList.PeptidesText = TextUtil.LineSeparate("NEV[+8.0.5]", "PESTIC[+57"));
            RunDlg<MessageDlg>(dlgRefineList.OkDialog, dlg =>
            {
                Assert.IsTrue(
                    dlg.Message.StartsWith(Resources.RefineListDlg_OkDialog_The_following_sequences_are_not_valid_peptides));
                dlg.OkDialog();
            });
            // None of the peptides are in the document
            RunUI(() => dlgRefineList.PeptidesText = TextUtil.LineSeparate("PEPTIDER", "ONCELER"));
            RunDlg<MessageDlg>(dlgRefineList.OkDialog, dlg =>
            {
                Assert.AreEqual(Resources.RefineListDlg_OkDialog_None_of_the_specified_peptides_are_in_the_document, dlg.Message);
                dlg.OkDialog();
            });
            // One peptide not found
            RunUI(() => dlgRefineList.PeptidesText = TextUtil.LineSeparate(PEPTIDE_MODIFIED, "ONCELER"));
            RunDlg<MultiButtonMsgDlg>(dlgRefineList.OkDialog, dlg =>
            {
                AssertEx.AreComparableStrings(
                    Resources.RefineListDlg_OkDialog_The_peptide__0__is_not_in_the_document_Do_you_want_to_continue, dlg.Message);
                dlg.CancelDialog();
            });
            // Multiple peptides not found
            RunUI(() => dlgRefineList.PeptidesText = TextUtil.LineSeparate(PEPTIDE_MODIFIED, "ONCELER", "TICK"));
            RunDlg<MultiButtonMsgDlg>(dlgRefineList.OkDialog, dlg =>
            {
                Assert.IsTrue(
                    dlg.Message.StartsWith(Resources.RefineListDlg_OkDialog_The_following_peptides_are_not_in_the_document));
                Assert.IsTrue(dlg.Message.EndsWith(Resources.RefineListDlg_OkDialog_Do_you_want_to_continue));
                dlg.CancelDialog();
            });
            // Many peptides not found
            var listPeptides = new List<string> {PEPTIDE_MODIFIED};
            const string peptideRand = "ONLOOKER";
            const int notFoundCount = 20;
            for (int i = 0; i < notFoundCount; i++)
            {
                listPeptides.Add(string.Join(string.Empty, peptideRand.ToArray().RandomOrder().ToArray()));
            }
            listPeptides.Add(PEPTIDE_UNMODIFIED);
            listPeptides.Add(PEPTIDE_EXTRA);
            RunUI(() => dlgRefineList.PeptidesText = TextUtil.LineSeparate(listPeptides));
            RunDlg<MultiButtonMsgDlg>(dlgRefineList.OkDialog, dlg =>
            {
                Assert.AreEqual(
                    string.Format(
                        Resources
                            .RefineListDlg_OkDialog_Of_the_specified__0__peptides__1__are_not_in_the_document_Do_you_want_to_continue,
                        listPeptides.Count, notFoundCount), dlg.Message);
                dlg.Btn1Click();
            });
            WaitForClosedForm(dlgRefineList);
            // By the time we can get back on the UI thread, the form processing should have completed
            // without changing anything
            RunUI(() => Assert.AreSame(document, SkylineWindow.Document));
        }

        private static void TestUse()
        {
            // Accept unmodified
            TestRefineListDlg(dlg => dlg.PeptidesText = PEPTIDE_UNMODIFIED, 2, 1, 8, 22);
            // Accept all modified
            TestRefineListDlg(dlg => dlg.PeptidesText = PEPTIDE_MODIFIED, 2, 4, 32, 96);
            // Remove empty proteins
            TestRefineListDlg(dlg =>
            {
                dlg.PeptidesText = PEPTIDE_UNMODIFIED;
                dlg.RemoveEmptyProteins = true;
            },
            1, 1, 8, 22);
            // Remove all but specific charge states
            string peptideCharges = TextUtil.LineSeparate(
                PEPTIDE_MODIFIED + Transition.GetChargeIndicator(2),
                PEPTIDE_MODIFIED + Transition.GetChargeIndicator(3),
                PEPTIDE_UNMODIFIED + Transition.GetChargeIndicator(5));
            TestRefineListDlg(dlg => dlg.PeptidesText = peptideCharges,
                document =>
                {
                    var peptides = document.Peptides.ToArray();
                    for (int i = 0; i < 4; i++)
                    {
                        Assert.IsTrue(peptides[i].TransitionGroups.All(nodeGroup =>
                            nodeGroup.TransitionGroup.PrecursorCharge == 2 ||
                            nodeGroup.TransitionGroup.PrecursorCharge == 3));
                    }
                    Assert.IsTrue(peptides[4].TransitionGroups.All(nodeGroup =>
                        nodeGroup.TransitionGroup.PrecursorCharge == 5));
                },
                2, 5, 18, 54);
            // Modification specific acceptance
            string peptideMods = TextUtil.LineSeparate(
                PEPTIDE_MODIFIED.Insert(6, "[57]"),
                PEPTIDE_MODIFIED.Insert(6, "[+57]").Insert(4, "[+" + 80.01234567 + "]").Insert(3, string.Format("[+{0:F01}]", 80)),
                PEPTIDE_UNMODIFIED);
            TestRefineListDlg(dlg =>
            {
                dlg.PeptidesText = peptideMods;
                dlg.RemoveEmptyProteins = dlg.MatchModified = true;
            }, 1, 3, 24, 70);
        }

        private static void TestRefineListDlg(Action<RefineListDlg> setup, int prots, int peptides, int groups, int transitions)
        {
            TestRefineListDlg(setup, null, prots, peptides, groups, transitions);
        }

        private static void TestRefineListDlg(Action<RefineListDlg> setup, Action<SrmDocument> test, int prots, int peptides, int groups, int transitions)
        {
            TestRefineListDlg(setup, document =>
            {
                AssertEx.IsDocumentState(document, null, prots, peptides, groups, transitions);
                if (test != null)
                    test(document);
            });
        }

        private static void TestRefineListDlg(Action<RefineListDlg> setup, Action<SrmDocument> test)
        {
            var document = SkylineWindow.Document;
            RunDlg<RefineListDlg>(SkylineWindow.AcceptPeptides, dlg =>
            {
                setup(dlg);
                dlg.OkDialog();
            });
            document = WaitForDocumentChange(document);
            test(document);
            RunUI(SkylineWindow.Undo);
        }
    }
}