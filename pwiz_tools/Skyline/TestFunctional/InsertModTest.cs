/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for InsertTest
    /// </summary>
    [TestClass]
    public class InsertModTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestInsertMod()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Set up initial document quickly, without involving UI
            SrmDocument document = new SrmDocument(SrmSettingsList.GetDefault());
            var staticMods = new List<StaticMod>(document.Settings.PeptideSettings.Modifications.StaticModifications);
            staticMods.AddRange(new[]
                                    {
                                        new StaticMod("Phospho", "S,T,Y", null, true, "PO3H", LabelAtoms.None,
                                            RelativeRT.Matching, null, null, new[] {new FragmentLoss("PO4H3")}),
                                        new StaticMod("K(GlyGly)", "K", null, true, "N2H6C4O2", LabelAtoms.None,
                                            RelativeRT.Matching, null, null, null), 
                                    });
            document = document.ChangeSettings(document.Settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(staticMods).ChangeMaxNeutralLosses(2)));
            Assert.IsTrue(SkylineWindow.SetDocument(document, SkylineWindow.Document));

            PasteTransitionListSkipColumnSelect(TRANSITIONLIST_CSV_MODLOSS_CLIPBOARD_TEXT);

            var docPaste1 = WaitForDocumentChange(document);
            AssertEx.IsDocumentState(docPaste1, null, 3, 4, 12); // revision # is hard to predict with background loaders running
            Assert.AreEqual(4, GetVariableModCount(docPaste1));
            Assert.AreEqual(6, GetLossCount(docPaste1, 1));

            string insertListText = I18n(TRANSITIONS_MODLOSS_CLIPBOARD_TEXT);
            
            var showDialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            // Formerly SetExcelFileClipboardText(TestFilesDir.GetTestPath("MoleculeTransitionList.xlsx"),"sheet1",6,false); but TeamCity doesn't like that
            var windowDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => showDialog.TransitionListText = insertListText);

            RunUI(() => {
                windowDlg.SetSelectedColumnTypes(
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name);
            });
            OkDialog(windowDlg, windowDlg.OkDialog);

            WaitForProteinMetadataBackgroundLoaderCompletedUI();

            // Revert to the original empty document
            RunUI(SkylineWindow.Undo);
            RunUI(SkylineWindow.Undo);

            Assert.AreSame(document, SkylineWindow.Document);

            var showDialog1 = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            // Formerly SetExcelFileClipboardText(TestFilesDir.GetTestPath("MoleculeTransitionList.xlsx"),"sheet1",6,false); but TeamCity doesn't like that
            var windowDlg1 = ShowDialog<ImportTransitionListColumnSelectDlg>(() => showDialog1.TransitionListText = insertListText);

            RunUI(() => {
                windowDlg1.SetSelectedColumnTypes(
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name);
            });
            OkDialog(windowDlg1, windowDlg1.OkDialog);

            var docInsert1 = WaitForDocumentChange(document);
            AssertEx.IsDocumentState(docInsert1, null, 3, 4, 12); // revision # is hard to predict with background loaders running
            Assert.AreEqual(4, GetVariableModCount(docInsert1));
            Assert.AreEqual(6, GetLossCount(docInsert1, 1));

            string insertPart1 = I18n(TRANSITIONS_PREC_PART1_CLIPBOARD_TEXT);
            string insertPart2 = I18n(TRANSITIONS_PREC_PART2_CLIPBOARD_TEXT);
            string insertSep = I18n(TRANSITIONS_PREC_SEP_CLIPBOARD_TEXT);

            // Check error and grid cell selection for a bad product m/z
            VerifyTransitionListError(insertListText, 757.420279, 888.8888, 8, 2);
            // Non-numeric product m/z
            VerifyTransitionListError(insertListText, 908.447222, "x", 
                string.Format(Resources.MassListRowReader_CalcTransitionExplanations_Product_m_z_value__0__in_peptide__1__has_no_matching_product_ion, 0, @"ELKEQQDSPGNKDFLQSLK"),
                1, 2);
            // Check error and grid cell selection for a bad precursor m/z
            VerifyTransitionListError(insertListText, 648.352161, 777.7777, 6, 1);
            // Non-numeric precursor m/z
            VerifyTransitionListError(insertListText, 762.033412, "x",
                string.Format(Resources.MassListRowReader_CalcPrecursorExplanations_, 0, 28.5462, 28.5462, "ELKEQQDSPGNKDFLQSLK"),
                0, 1);
            // Empty peptide
            VerifyTransitionListError(insertListText, "TISQSSSLKSSSNSNK", "", string.Format(Resources.MassListRowReader_NextRow_Invalid_peptide_sequence__0__found, ""), 9, 0);
            // Bad peptide
            VerifyTransitionListError(insertListText, "TISQSSSLKSSSNSNK", "BBBbBBBR", string.Format(Resources.MassListRowReader_NextRow_Invalid_peptide_sequence__0__found, "BBBbBBBR"), 9, 0);
            // No mods explain all transitions
            VerifyTransitionListError(insertPart1 + insertPart2, null, null,
                String.Format(Resources.PeptideGroupBuilder_AppendTransition_Failed_to_explain_all_transitions_for_0__m_z__1__with_a_single_set_of_modifications, "CDSSPDSAEDVR", 709.2505),
                3, 0, 2);
            // Finally a working set of transitions
            SetClipboardText(insertPart1 + insertSep + insertPart2);
            var transitionDlg = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var windowDlg2 = ShowDialog<ImportTransitionListColumnSelectDlg>(() => transitionDlg.TransitionListText = insertPart1 + insertSep + insertPart2);
            RunUI(() => {
                windowDlg2.SetSelectedColumnTypes(
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name);
            });
            OkDialog(windowDlg2, windowDlg2.OkDialog);
            var docInsert2 = WaitForDocumentChange(docInsert1);
            AssertEx.IsDocumentState(docInsert2, null, 4, 7, 21); // revision # is hard to predict with background loaders running
            Assert.AreEqual(7, GetVariableModCount(docInsert2));
            Assert.AreEqual(11, GetLossCount(docInsert2, 1));
        }

        private static int GetVariableModCount(SrmDocument document)
        {
            return document.Peptides.Count(nodePep => nodePep.ExplicitMods != null && nodePep.ExplicitMods.IsVariableStaticMods);
        }

        private static int GetLossCount(SrmDocument document, int minLosses)
        {
            return document.PeptideTransitions.Count(nodeTran => nodeTran.HasLoss && nodeTran.Losses.Losses.Count >= minLosses);
        }

        private static string I18n(string text)
        {
            return text.Replace(".", LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        }

        private static void VerifyTransitionListError(string insertListText,
            object oldValue, object newValue, int row, int col)
        {
            VerifyTransitionListError(insertListText, oldValue, newValue, newValue, row, col);
        }

        private static void VerifyTransitionListError(string insertListText,
            object oldValue, object newValue, object containsValue, int row, int col, int replacements = 0)
        {
            string pasteText = oldValue != null && newValue != null ?
                insertListText.Replace(oldValue.ToString(), newValue.ToString()):
                insertListText;
            string allErrorText = "";
            var transitionDlg = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var windowDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => transitionDlg.TransitionListText = pasteText);
            RunUI(() => {
                windowDlg.SetSelectedColumnTypes(
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name);
            });
            var errDlg = ShowDialog<ImportTransitionListErrorDlg>(windowDlg.OkDialog);
            RunUI(() =>
            {
                foreach (var err in errDlg.ErrorList)
                {
                    allErrorText += err.ErrorMessage;
                }
                Assert.IsTrue(allErrorText.Contains(containsValue.ToString()),
                    string.Format("Unexpected value in paste dialog error window:\r\nexpected \"{0}\"\r\ngot \"{1}\"",
                        containsValue, errDlg.ErrorList));
                errDlg.Close();
            });
            RunUI(windowDlg.CancelDialog);
            WaitForClosedForm(transitionDlg);
            // TODO: Do we need to support these checks for columnSelectDlg?
            //Assert.AreEqual(row, insertTransDlg.SelectedGridRow);
            //Assert.AreEqual(col, insertTransDlg.SelectedGridColumn);
        }

        private const string TRANSITIONS_MODLOSS_CLIPBOARD_TEXT =
            @"ELKEQQDSPGNKDFLQSLK	762.033412	850.466895	sp|P08697|A2AP_HUMAN
ELKEQQDSPGNKDFLQSLK	762.033412	908.447222	sp|P08697|A2AP_HUMAN
ELKEQQDSPGNKDFLQSLK	762.033412	623.843144	sp|P08697|A2AP_HUMAN
EQQDSPGNKDFLQSLK	638.626205	764.855796	sp|P08697|A2AP_HUMAN
EQQDSPGNKDFLQSLK	638.626205	715.867348	sp|P08697|A2AP_HUMAN
EQQDSPGNKDFLQSLK	638.626205	623.843144	sp|P08697|A2AP_HUMAN
ITLLSALVETR	648.352161	983.588407	sp|P01011|AACT_HUMAN
ITLLSALVETR	648.352161	870.504343	sp|P01011|AACT_HUMAN
ITLLSALVETR	648.352161	757.420279	sp|P01011|AACT_HUMAN
TISQSSSLKSSSNSNK	578.935039	817.375081	sp|Q9UHB7|AFF4_HUMAN
TISQSSSLKSSSNSNK	578.935039	768.386633	sp|Q9UHB7|AFF4_HUMAN
TISQSSSLKSSSNSNK	578.935039	653.287746	sp|Q9UHB7|AFF4_HUMAN";

        private const string TRANSITIONS_PREC_PART1_CLIPBOARD_TEXT =
            @"CDSSPDSAEDVR	709.250503	1044.459243	sp|P02765|FETUA_HUMAN
CDSSPDSAEDVR	709.250503	274.187366	sp|P02765|FETUA_HUMAN
CDSSPDSAEDVR	709.250503	432.118359	sp|P02765|FETUA_HUMAN
";

        private const string TRANSITIONS_PREC_SEP_CLIPBOARD_TEXT =
            @"CDSSPDSAEDVRK	773.297985	1096.467045	sp|P02765|FETUA_HUMAN
CDSSPDSAEDVRK	773.297985	998.490149	sp|P02765|FETUA_HUMAN
CDSSPDSAEDVRK	773.297985	786.410442	sp|P02765|FETUA_HUMAN
";

        private const string TRANSITIONS_PREC_PART2_CLIPBOARD_TEXT =
    @"CDSSPDSAEDVR	709.250503	968.372082	sp|P02765|FETUA_HUMAN
CDSSPDSAEDVR	709.250503	870.395186	sp|P02765|FETUA_HUMAN
CDSSPDSAEDVR	709.250503	450.128924	sp|P02765|FETUA_HUMAN";

        private const string TRANSITIONLIST_CSV_MODLOSS_CLIPBOARD_TEXT =
            @"762.033412,850.466895,20,sp|P08697|A2AP_HUMAN.ELKEQQDSPGNKDFLQSLK.1y7.light,86.7,39.6
762.033412,908.447222,20,sp|P08697|A2AP_HUMAN.ELKEQQDSPGNKDFLQSLK.3y16.light,86.7,39.6
762.033412,623.843144,20,sp|P08697|A2AP_HUMAN.ELKEQQDSPGNKDFLQSLK.2y11.light,86.7,39.6
638.626205,764.855796,20,sp|P08697|A2AP_HUMAN.EQQDSPGNKDFLQSLK.y13.light,77.7,31.9
638.626205,715.867348,20,sp|P08697|A2AP_HUMAN.EQQDSPGNKDFLQSLK.y13.light,77.7,31.9
638.626205,623.843144,20,sp|P08697|A2AP_HUMAN.EQQDSPGNKDFLQSLK.y11.light,77.7,31.9
648.352161,983.588407,20,sp|P01011|AACT_HUMAN.ITLLSALVETR.2y9.light,78.4,37.5
648.352161,870.504343,20,sp|P01011|AACT_HUMAN.ITLLSALVETR.3y8.light,78.4,37.5
648.352161,757.420279,20,sp|P01011|AACT_HUMAN.ITLLSALVETR.1y7.light,78.4,37.5
578.935039,817.375081,20,sp|Q9UHB7|AFF4_HUMAN.TISQSSSLKSSSNSNK.5y15.light,73.3,28.2
578.935039,768.386633,20,sp|Q9UHB7|AFF4_HUMAN.TISQSSSLKSSSNSNK.6y15.light,73.3,28.2
578.935039,653.287746,20,sp|Q9UHB7|AFF4_HUMAN.TISQSSSLKSSSNSNK.4y12.light,73.3,28.2";
    }
}