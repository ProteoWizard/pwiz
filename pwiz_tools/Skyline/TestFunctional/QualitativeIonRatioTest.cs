using System.Collections.Generic;
using System.Linq;
using System.Net.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class QualitativeIonRatioTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestQualitativeIonRatio()
        {
            TestFilesZip = @"TestFunctional\QualitativeIonRatioTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("QualitativeIonRatioTest.sky"));
                SkylineWindow.ShowDocumentGrid(true);
            });

            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() =>
            {
                documentGrid.DataboundGridControl.ChooseView(ViewGroup.BUILT_IN.Id
                    .ViewName(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
            });
            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor=>
            {
                var columnsTab = viewEditor.ChooseColumnsTab;
                columnsTab.RemoveColumns(0, columnsTab.ColumnCount);
                PropertyPath ppPeptides = PropertyPath.Root.Property(nameof(SkylineDocument.Proteins)).LookupAllItems()
                    .Property(nameof(Protein.Peptides)).LookupAllItems();
                columnsTab.AddColumn(ppPeptides);
                columnsTab.AddColumn(PropertyPath.Root.Property(nameof(SkylineDocument.Replicates)).LookupAllItems());
                columnsTab.AddColumn(ppPeptides.Property(nameof(Peptide.FiguresOfMerit)).Property(nameof(FiguresOfMerit.TargetIonRatio)));
                PropertyPath ppPeptideResults =
                    ppPeptides.Property(nameof(Peptide.Results)).DictionaryValues();
                PropertyPath ppQuantification = ppPeptideResults.Property(nameof(PeptideResult.Quantification));
                columnsTab.AddColumn(ppQuantification.Property(nameof(PeptideQuantificationResult.IonRatio)));
                columnsTab.AddColumn(ppQuantification.Property(nameof(PeptideQuantificationResult.IonRatioStatus)));
                viewEditor.ViewName = "IonRatios";
                viewEditor.OkDialog();
            });
            WaitForConditionUI(() => documentGrid.IsComplete);
            PropertyPath ppTargetIonRatio = PropertyPath.Root.Property(nameof(Peptide.FiguresOfMerit)).Property(nameof(FiguresOfMerit.TargetIonRatio));
            RunUI(() =>
            {
                var colTargetIonRatio = documentGrid.FindColumn(ppTargetIonRatio);
                Assert.IsNotNull(colTargetIonRatio);
                for (int i = 0; i < documentGrid.RowCount; i++)
                {
                    Assert.IsNull(documentGrid.DataGridView.Rows[i].Cells[colTargetIonRatio.Index].Value);
                }
            });
            var document = SkylineWindow.Document;
            var identityPathsToSelect = new List<IdentityPath>();
            // Select the first transition in each transition group and mark it quantitative
            foreach (var moleculeList in document.MoleculeGroups)
            {
                foreach (var molecule in moleculeList.Molecules)
                {
                    Assert.AreEqual(1, molecule.Children.Count);
                    var precursor = molecule.TransitionGroups.First();
                    Assert.AreEqual(2, precursor.Children.Count);
                    identityPathsToSelect.Add(new IdentityPath(moleculeList.PeptideGroup, molecule.Peptide,
                        precursor.TransitionGroup, precursor.Transitions.First().Transition));
                }
            }
            RunUI(()=>
            {
                SkylineWindow.SequenceTree.SelectedPaths = identityPathsToSelect;
                SkylineWindow.MarkQuantitative(false);
            });
            WaitForConditionUI(() => documentGrid.IsComplete);
            var ppIonRatio = PropertyPath.Root.Property(nameof(Peptide.Results))
                .DictionaryValues()
                .Property(nameof(PeptideResult.Quantification))
                .Property(nameof(PeptideQuantificationResult.IonRatio));
            var ppReplicate = PropertyPath.Root.Property(nameof(Peptide.Results))
                .DictionaryValues()
            RunUI(() =>
            {
                var colIonRatio = documentGrid.DataboundGridControl.FindColumn(ppIonRatio);
                var colPeptide = 
            });
        }
    }
}
