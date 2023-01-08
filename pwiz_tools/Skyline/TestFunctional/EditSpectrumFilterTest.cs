using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class EditSpectrumFilterTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestEditSpectrumFilter()
        {
            TestFilesZip = @"TestFunctional\SpectrumClassFilterTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SpectrumClassFilterTestDocument.sky"));
                Assert.AreEqual(1, SkylineWindow.Document.MoleculeCount);
                Assert.AreEqual(1, SkylineWindow.Document.MoleculeTransitionGroupCount);
                SkylineWindow.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.TransitionGroups, 0);
            });
            PauseTest();
            RunDlg<EditSpectrumFilterDlg>(SkylineWindow.EditMenu.EditSpectrumFilter, dlg =>
            {
                dlg.CreateCopy = true;
                var row = dlg.RowBindingList.AddNew();
                Assert.IsNotNull(row);
                row.Property = SpectrumClassColumn.PresetScanConfiguration;
                row.SetOperation(FilterOperations.OP_EQUALS);
                row.SetValue(3);
                dlg.OkDialog();
            });
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeCount);
            Assert.AreEqual(2, SkylineWindow.Document.MoleculeTransitionGroupCount);
            
            RunUI(() =>
            {
                SkylineWindow.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.TransitionGroups, 1);
                var selectedTreeNode = SkylineWindow.SelectedNode;
                Assert.IsInstanceOfType(selectedTreeNode, typeof(TransitionGroupTreeNode));
                var transitionGroupDocNode = ((TransitionGroupTreeNode) selectedTreeNode).DocNode;
                Assert.IsNotNull(transitionGroupDocNode.SpectrumClassFilter);
                Assert.IsFalse(transitionGroupDocNode.SpectrumClassFilter.IsEmpty);
                Assert.AreEqual(1, transitionGroupDocNode.SpectrumClassFilter.FilterSpecs.Count);
                Assert.AreEqual(3.0,
                    transitionGroupDocNode.SpectrumClassFilter.FilterSpecs[0].Predicate
                        .GetOperandValue(new DataSchema(), typeof(double)));
            });
            RunDlg<EditSpectrumFilterDlg>(SkylineWindow.EditMenu.EditSpectrumFilter, dlg =>
            {
                dlg.CreateCopy = true;
                Assert.AreEqual(1, dlg.RowBindingList.Count);
                dlg.RowBindingList[0].SetValue(2);
                dlg.OkDialog();
            });
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeCount);
            Assert.AreEqual(3, SkylineWindow.Document.MoleculeTransitionGroupCount);
            AssertEx.Serializable(SkylineWindow.Document);

            ImportResultsFile(TestFilesDir.GetTestPath("SpectrumClassFilterTest.mzML"));
            var document = SkylineWindow.Document;
            var peptideDocNode = document.Molecules.First();
            Assert.AreEqual(3, peptideDocNode.TransitionGroupCount);
            var unfilteredTransitionGroup = peptideDocNode.TransitionGroups.First();
            Assert.IsNull(unfilteredTransitionGroup.SpectrumClassFilter?.FilterSpecs.FirstOrDefault());
            var unfilteredChromatogramGroup = LoadChromatogram(document, peptideDocNode, unfilteredTransitionGroup);
            var unfilteredMs2Chromatogram =
                unfilteredChromatogramGroup.TransitionPointSets.FirstOrDefault(c => c.Source == ChromSource.fragment);
            Assert.IsNotNull(unfilteredMs2Chromatogram);
            Assert.AreNotEqual(0, unfilteredMs2Chromatogram.RawTimes.Count);
            var scanConfiguration2ChromatogramGroup = LoadChromatogram(document, peptideDocNode,
                peptideDocNode.TransitionGroups.ElementAt(1));
            Assert.IsNotNull(scanConfiguration2ChromatogramGroup);
            var scanConfiguration2Chromatogram =
                scanConfiguration2ChromatogramGroup.TransitionPointSets.FirstOrDefault(c =>
                    c.Source == ChromSource.fragment);
            Assert.IsNotNull(scanConfiguration2Chromatogram);
            Assert.AreNotEqual(0, scanConfiguration2Chromatogram.RawTimes.Count);
            var scanConfiguration3ChromatogramGroup = LoadChromatogram(document, peptideDocNode,
                peptideDocNode.TransitionGroups.ElementAt(2));
            Assert.IsNotNull(scanConfiguration3ChromatogramGroup);
            var scanConfiguration3Chromatogram =
                scanConfiguration3ChromatogramGroup.TransitionPointSets.FirstOrDefault(c =>
                    c.Source == ChromSource.fragment);
            Assert.IsNotNull(scanConfiguration3Chromatogram);
            Assert.AreNotEqual(0, scanConfiguration3Chromatogram.RawTimes.Count);
            Assert.AreEqual(unfilteredMs2Chromatogram.RawTimes.Count, scanConfiguration2Chromatogram.RawTimes.Count + scanConfiguration3Chromatogram.RawTimes.Count);
            RunUI(()=>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Molecules, 0);
                SkylineWindow.ShowSplitChromatogramGraph(true);
                SkylineWindow.ShowProductTransitions();
                SkylineWindow.SetTransformChrom(TransformChrom.raw);
            });
            PauseTest();
        }

        private ChromatogramGroupInfo LoadChromatogram(SrmDocument document, PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroupDocNode)
        {
            Assert.IsTrue(document.Settings.MeasuredResults.TryLoadChromatogram(0, peptideDocNode, transitionGroupDocNode,
                (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance, out var infoSet));
            Assert.AreEqual(1, infoSet.Length);
            return infoSet[0];
        }
    }
}
