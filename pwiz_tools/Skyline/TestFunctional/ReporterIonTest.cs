using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
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
            TestPreloadedMeasuredIons();
            TestCustomIonMz();
            TestSettingIonsUI();
        }

        private void TestPreloadedMeasuredIons()
        {
            var ions = new[]
            {
                new MeasuredIon("Water", "H2O", null, null, 1),
                new MeasuredIon("Carbon", "CO2", null, null, 1, true)
            };
            var ionList = new MeasuredIonList();
            ionList.AddRange(ions);
            var originalDoc = SkylineWindow.Document;
            var measuredIonDoc = originalDoc.ChangeSettings(originalDoc.Settings.ChangeTransitionFilter(filter =>
                filter.ChangeMeasuredIons(ions)));
            Settings.Default.MeasuredIonList = ionList;

            SkylineWindow.SetDocument(measuredIonDoc, originalDoc);
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
            OkDialog(editMeasuredIonList, editMeasuredIonList.OkDialog);
            OkDialog(tranSettings, tranSettings.OkDialog);
        }

        private void TestSettingIonsUI()
        {
            var ions = new MeasuredIon[0];
            var ionList = new MeasuredIonList();
            ionList.AddRange(ions);
            var originalDoc = SkylineWindow.Document;
            var measuredIonDoc = originalDoc.ChangeSettings(originalDoc.Settings.ChangeTransitionFilter(filter =>
                filter.ChangeMeasuredIons(ions)));
            Settings.Default.MeasuredIonList = ionList;
            SkylineWindow.SetDocument(measuredIonDoc, originalDoc);

            var tranSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            var editMeasuredIonList =
                ShowDialog<EditListDlg<SettingsListBase<MeasuredIon>, MeasuredIon>>(
                    tranSettings.EditSpecialTransitionsList);
            var editMeasuredIon1 = ShowDialog<EditMeasuredIonDlg>(editMeasuredIonList.AddItem);
            RunUI(() =>
            {
                editMeasuredIon1.SwitchToCustom();
                editMeasuredIon1.TextName = "Water";
                editMeasuredIon1.Charge = -1;  // Negative charge states are valid for small molecule only, not for reporter ions
                editMeasuredIon1.Formula = "H2O";
            });
            RunDlg<MessageDlg>(editMeasuredIon1.OkDialog, dlg =>
            {
                // Trying to exit the dialog should cause a warning about charge
                AssertEx.AreComparableStrings(String.Format(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_greater_than_or_equal_to__1__, String.Empty, 1), dlg.Message);
                dlg.OkDialog(); // Dismiss the warning
            });
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
                tranSettings.SetListAlwaysAdd(0,true);
                tranSettings.SetListAlwaysAdd(0,true);
                tranSettings.SetListAlwaysAdd(1,true);
            });
            OkDialog(tranSettings,tranSettings.OkDialog);
            IdentityPath path;
            var newDoc = SkylineWindow.Document.ImportFasta(new StringReader(">peptide1\nPEPMCIDEPR"),
                true, IdentityPath.ROOT, out path);
            TransitionGroupDocNode nodeGroup = newDoc.PeptideTransitionGroups.ElementAt(0);

            var water = new MeasuredIon("Water", "H2O", null, null, 1);
            var carbon = new MeasuredIon("Carbon", "CO2", null, null, 1,true);

            var filteredWaterNodes = TransitionGroupTreeNode.GetChoices(nodeGroup, newDoc.Settings,
                    newDoc.Peptides.ElementAt(0).ExplicitMods, true)
                    .Cast<TransitionDocNode>()
                    .Where(node => Equals(node.Transition.CustomIon, water.CustomIon));

            Assert.AreEqual(1,filteredWaterNodes.Count());
            
            var filteredCarbonNodes =
                TransitionGroupTreeNode.GetChoices(nodeGroup, newDoc.Settings,
                    newDoc.Peptides.ElementAt(0).ExplicitMods, true)
                    .Cast<TransitionDocNode>()
                    .Where(node => Equals(node.Transition.CustomIon, carbon.CustomIon));

            var unfilteredCarbonNodes =
                TransitionGroupTreeNode.GetChoices(nodeGroup, newDoc.Settings,
                    newDoc.Peptides.ElementAt(0).ExplicitMods, false)
                    .Cast<TransitionDocNode>()
                    .Where(node => Equals(node.Transition.CustomIon, carbon.CustomIon));

            Assert.AreEqual(0, filteredCarbonNodes.Count());
            Assert.AreEqual(1, unfilteredCarbonNodes.Count());
        }
    }
}
