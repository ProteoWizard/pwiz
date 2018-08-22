/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
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

using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ReporterIonTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestCustomIon()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // These are some tiny molecules, drop the low end of the instrument mz filter
            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                transitionSettingsUI.MinMz = 10;
            });
            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);

            TestPreloadedMeasuredIons();
            TestCustomIonMz();
            TestSettingIonsUI();
        }

        private void TestPreloadedMeasuredIons()
        {
            var ions = new[]
            {
                new MeasuredIon("Water", "H2O", null, null, Adduct.M_PLUS),
                new MeasuredIon("Carbon", "CO2", null, null, Adduct.M_PLUS, true)
            };
            var ionList = new MeasuredIonList();
            ionList.AddRange(ions);
            Settings.Default.MeasuredIonList = ionList;

            RunUI(() => SkylineWindow.ModifyDocument("Change measured ions", document => document.ChangeSettings(
                document.Settings.ChangeTransitionFilter(filter => filter.ChangeMeasuredIons(ions)))));

            TransitionSettingsUI tranSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                tranSettings.SelectedTab = TransitionSettingsUI.TABS.Filter;
                Assert.IsTrue(tranSettings.ValidateIonCheckBoxes(new[] { CheckState.Checked, CheckState.Indeterminate }));
            });
            OkDialog(tranSettings, tranSettings.OkDialog);
        }

        private void TestCustomIonMz()
        {
            var tranSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            var editMeasuredIonList = ShowDialog<EditListDlg<SettingsListBase<MeasuredIon>, MeasuredIon>>(tranSettings.EditSpecialTransitionsList);
            var editMeasuredIon = ShowDialog<EditMeasuredIonDlg>(editMeasuredIonList.AddItem);
            RunUI(() =>
            {
                editMeasuredIon.SwitchToCustom();
                editMeasuredIon.TextName = "Test";
                editMeasuredIon.Charge = 2;
                editMeasuredIon.AverageMass = 20;
                editMeasuredIon.MonoIsotopicMass = 21;
            });
            OkDialog(editMeasuredIon, editMeasuredIon.OkDialog);
            // Now update the charge, verifying that this changes the mass but not the mz
            var editMeasuredIon2 = ShowDialog<EditMeasuredIonDlg>(editMeasuredIonList.EditItem);
            RunUI(() =>
            {
                editMeasuredIon2.Charge = 3;
            });
            OkDialog(editMeasuredIon2, editMeasuredIon2.OkDialog);
            var editMeasuredIon3 = ShowDialog<EditMeasuredIonDlg>(editMeasuredIonList.EditItem);
            RunUI(() =>
            {
                double averageMass = editMeasuredIon3.AverageMass;
                double monoMass = editMeasuredIon3.MonoIsotopicMass;
                Assert.AreEqual(30, averageMass, 1e-5);
                Assert.AreEqual(31.49999, monoMass, 1e-5);
            });
            OkDialog(editMeasuredIon3, editMeasuredIon3.OkDialog);

            OkDialog(editMeasuredIonList, editMeasuredIonList.OkDialog);
            OkDialog(tranSettings, tranSettings.OkDialog);
        }

        private void TestSettingIonsUI()
        {
            var ions = new MeasuredIon[0];
            var ionList = new MeasuredIonList();
            ionList.AddRange(ions);
            Settings.Default.MeasuredIonList = ionList;
            RunUI(() => SkylineWindow.ModifyDocument("Change measured ions", document => document.ChangeSettings(
                document.Settings.ChangeTransitionFilter(filter => filter.ChangeMeasuredIons(ions)))));

            var tranSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            var editMeasuredIonList =
                ShowDialog<EditListDlg<SettingsListBase<MeasuredIon>, MeasuredIon>>(
                    tranSettings.EditSpecialTransitionsList);
            var editMeasuredIon1 = ShowDialog<EditMeasuredIonDlg>(editMeasuredIonList.AddItem);
            RunUI(() =>
            {
                editMeasuredIon1.SwitchToCustom();
                editMeasuredIon1.TextName = "Water";
                editMeasuredIon1.Formula = "H2O";
            });
            var errorDlg = ShowDialog<MessageDlg>(() => editMeasuredIon1.Charge = -1); // Negative charge states are valid for small molecule only, not for reporter ions
            AssertEx.AreComparableStrings(String.Format(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_greater_than_or_equal_to__1__, String.Empty, 1), errorDlg.Message);
            RunUI(() => errorDlg.OkDialog()); // Dismiss the warning
            RunUI(() =>
            {
                editMeasuredIon1.Charge = 1;
            });
            OkDialog(editMeasuredIon1,editMeasuredIon1.OkDialog);
            var editMeasuredIon2 = ShowDialog<EditMeasuredIonDlg>(editMeasuredIonList.AddItem);
            RunUI(() =>
            {
                editMeasuredIon2.SwitchToCustom();
                editMeasuredIon2.TextName = "Carbon";
                editMeasuredIon2.Charge = 1;
                editMeasuredIon2.Formula = "CO2";
            });
            OkDialog(editMeasuredIon2,editMeasuredIon2.OkDialog);
            OkDialog(editMeasuredIonList,editMeasuredIonList.OkDialog);
            RunUI(() =>
            {
                tranSettings.SetListAlwaysAdd(0,true);  // optional water
                tranSettings.SetListAlwaysAdd(0,true);  // default water
                tranSettings.SetListAlwaysAdd(1,true);  // optional carbon
            });
            OkDialog(tranSettings,tranSettings.OkDialog);
            const string fasta = ">peptide1\nPEPMCIDEPR";
            RunUI(() => SkylineWindow.ImportFasta(new StringReader(fasta), 2, true, string.Empty,
                new SkylineWindow.ImportFastaInfo(false, fasta)));
            var newDoc = SkylineWindow.Document;

            var water = new MeasuredIon("Water", "H2O", null, null, Adduct.M_PLUS); // Charge-only adduct, ionizing elements assumed to be part of formula
            var carbon = new MeasuredIon("Carbon", "CO2", null, null, Adduct.M_PLUS, true);

            var nodeTranFirst = newDoc.MoleculeTransitions.ElementAt(0);
            Assert.IsTrue(nodeTranFirst.Transition.IsCustom());
            Assert.AreEqual(water.SettingsCustomIon, nodeTranFirst.Transition.CustomIon);

            // Sort-of unit test forray away from actually using the UI
            var nodeGroup = newDoc.PeptideTransitionGroups.ElementAt(0);

            var filteredWaterNodes = nodeGroup.GetPrecursorChoices(newDoc.Settings,
                    newDoc.Peptides.ElementAt(0).ExplicitMods, true)
                    .Cast<TransitionDocNode>()
                    .Where(node => Equals(node.Transition.CustomIon, water.SettingsCustomIon));

            Assert.AreEqual(1,filteredWaterNodes.Count());
            
            var filteredCarbonNodes =
                nodeGroup.GetPrecursorChoices(newDoc.Settings,
                    newDoc.Peptides.ElementAt(0).ExplicitMods, true)
                    .Cast<TransitionDocNode>()
                    .Where(node => Equals(node.Transition.CustomIon, carbon.SettingsCustomIon));

            var unfilteredCarbonNodes =
                nodeGroup.GetPrecursorChoices(newDoc.Settings,
                    newDoc.Peptides.ElementAt(0).ExplicitMods, false)
                    .Cast<TransitionDocNode>()
                    .Where(node => Equals(node.Transition.CustomIon, carbon.SettingsCustomIon));

            Assert.AreEqual(0, filteredCarbonNodes.Count());
            Assert.AreEqual(1, unfilteredCarbonNodes.Count());

            // Back to the UI: make sure removing the custom ion from settings
            // removes it from the document, which was once an issue that lead
            // to a document that could not be roundtripped
            RunUI(() => SkylineWindow.SelectedPath = newDoc.GetPathTo((int) SrmDocument.Level.TransitionGroups, 0));
            // Add carbon ion
            RunDlg<PopupPickList>(SkylineWindow.ShowPickChildrenInTest, dlg =>
            {
                dlg.ApplyFilter(false);
                dlg.ToggleItem(2);  // precursor, water, carbon
                dlg.OnOk();
            });
            var docWithCarbon = WaitForDocumentChange(newDoc);
            var nodeTranCarbon = docWithCarbon.MoleculeTransitions.ElementAt(1);
            Assert.IsTrue(nodeTranCarbon.Transition.IsCustom());
            Assert.AreEqual(carbon.SettingsCustomIon, nodeTranCarbon.Transition.CustomIon);

            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, tranSettings2 =>
            {
                tranSettings2.SetListAlwaysAdd(0, false);  // remove water
                tranSettings2.OkDialog();
            });
            var docWithoutWater = WaitForDocumentChange(docWithCarbon);
            Assert.AreEqual(0, docWithoutWater.PeptideTransitions.Count(t => Equals(water.SettingsCustomIon, t.Transition.CustomIon)));
            Assert.AreEqual(1, docWithoutWater.PeptideTransitions.Count(t => Equals(carbon.SettingsCustomIon, t.Transition.CustomIon)));
            AssertEx.RoundTrip(docWithoutWater);

            RunDlg<PopupPickList>(SkylineWindow.ShowPickChildrenInTest, dlg =>
            {
                dlg.AutoManageChildren = true;
                dlg.OnOk();
            });
            var docWithoutCustom = WaitForDocumentChange(docWithoutWater);
            Assert.AreEqual(0, docWithoutCustom.PeptideTransitions.Count(t => t.Transition.IsCustom()));
            AssertEx.RoundTrip(docWithoutCustom);
        }
    }
}
