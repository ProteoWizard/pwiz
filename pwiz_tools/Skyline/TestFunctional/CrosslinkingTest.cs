using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class CrosslinkingTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestCrosslinking()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string crosslinkerName = "MyCrosslinker";
            //DDSPDLPKLK[SLGKVGTR+C8H10O2]PDPNTLC[Carbamidomethyl (C)]DEFK
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(()=>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Digest;
                peptideSettingsUi.MaxMissedCleavages = 2;
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications;
            });
            var editModListDlg = ShowEditStaticModsDlg(peptideSettingsUi);
            RunUI(()=>editModListDlg.AddItem(new StaticMod(crosslinkerName, "K", null, "C8H10O2").ChangeCrosslinkerSettings(CrosslinkerSettings.EMPTY)));
            OkDialog(editModListDlg, editModListDlg.OkDialog);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            var transitionSettingsUi = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(()=>
            {
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.Filter;
                transitionSettingsUi.PrecursorCharges = "4,3,2";
                transitionSettingsUi.ProductCharges = "4,3,2,1";
                transitionSettingsUi.FragmentTypes = "y";
            });
            OkDialog(transitionSettingsUi, transitionSettingsUi.OkDialog);
            RunUI(()=>
            {
                SkylineWindow.Paste("DDSPDLPKLKPDPNTLCDEFK");
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo(1, 0);
            });

            var modifyPeptideDlg = ShowDialog<EditPepModsDlg>(SkylineWindow.ModifyPeptide);
            RunUI(() =>
            {
                modifyPeptideDlg.SelectModification(IsotopeLabelType.light, 9, crosslinkerName);
            });
            var editCrosslinkModDlg = ShowDialog<EditCrosslinkModDlg>(() => modifyPeptideDlg.EditLinkedPeptide(9));
            RunUI(() =>
            {
                editCrosslinkModDlg.PeptideSequence = "SLGKVGTR";
                editCrosslinkModDlg.AttachmentOrdinal = 4;
            });
            OkDialog(editCrosslinkModDlg, editCrosslinkModDlg.OkDialog);
            OkDialog(modifyPeptideDlg, modifyPeptideDlg.OkDialog);
            PauseTest();
        }
    }
}
