using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class EditCustomMoleculeDlgTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMethod1()
        {
            RunFunctionalTest();
        }

        const string testFormula = "C12H12";
        const string testNametext = "moleculeA";

        protected override void DoTest()
        {
            var origDoc = SkylineWindow.Document;
            RunUI(() =>
                SkylineWindow.ModifyDocument("", doc =>
                {
                    IdentityPath first;
                    IdentityPath next;
                    return doc.AddPeptideGroups(new[]
                    {
                        new PeptideGroupDocNode(new PeptideGroup(), doc.Annotations, "Molecule Group", "",
                            new PeptideDocNode[0])
                    }, true, IdentityPath.ROOT, out first, out next);
                }));
            var doc2 = WaitForDocumentChange(origDoc);
            TestAddingSmallMolecule();
            var doc3 = WaitForDocumentChange(doc2);
            TestAddingTransition();
            var doc4 = WaitForDocumentChange(doc3);
            TestEditingPeptide();
            WaitForDocumentChange(doc4);
            TestEditingTransition();
        }

        private static void TestEditingPeptide()
        {
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].FirstNode;
            } );
            var doc = SkylineWindow.Document;
            var editMoleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.ModifyPeptide);
            RunUI(() =>
            {
                const string COOO13H = "COOO13H";
                Assert.AreEqual(COOO13H,editMoleculeDlg.FormulaBox.Formula);
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(COOO13H), (double)editMoleculeDlg.FormulaBox.MonoMass, BioMassCalc.MassElectron/100);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateMassFromFormula(COOO13H), (double)editMoleculeDlg.FormulaBox.AverageMass, BioMassCalc.MassElectron/100);

                // Verify the interaction of explicitly set formulas, masses
                editMoleculeDlg.FormulaBox.Formula = null;
                Assert.IsNull(editMoleculeDlg.FormulaBox.MonoMass);
                Assert.IsNull(editMoleculeDlg.FormulaBox.AverageMass);

                editMoleculeDlg.FormulaBox.Formula = COOO13H;
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(COOO13H), (double)editMoleculeDlg.FormulaBox.MonoMass, BioMassCalc.MassElectron / 100);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateMassFromFormula(COOO13H), (double)editMoleculeDlg.FormulaBox.AverageMass, BioMassCalc.MassElectron / 100);

                editMoleculeDlg.FormulaBox.Formula = null;
                Assert.IsNull(editMoleculeDlg.FormulaBox.MonoMass);
                Assert.IsNull(editMoleculeDlg.FormulaBox.AverageMass);

                editMoleculeDlg.FormulaBox.Formula = COOO13H;
                editMoleculeDlg.FormulaBox.AverageMass = 100;
                editMoleculeDlg.FormulaBox.MonoMass = 105;
                editMoleculeDlg.NameText = "Molecule";
                Assert.AreEqual(string.Empty, editMoleculeDlg.FormulaBox.Formula); // Should be cleared when mass is set
                Assert.AreEqual(100, editMoleculeDlg.FormulaBox.AverageMass); 
                Assert.AreEqual(105, editMoleculeDlg.FormulaBox.MonoMass);
            });
            OkDialog(editMoleculeDlg,editMoleculeDlg.OkDialog);
            var newdoc = WaitForDocumentChange(doc);
            Assert.AreEqual(100, newdoc.Molecules.ElementAt(0).Peptide.CustomIon.AverageMass);
            Assert.AreEqual(105, newdoc.Molecules.ElementAt(0).Peptide.CustomIon.MonoisotopicMass);
            Assert.IsTrue(string.IsNullOrEmpty(newdoc.Molecules.ElementAt(0).Peptide.CustomIon.Formula));
            Assert.AreEqual(105 - BioMassCalc.MassElectron, newdoc.MoleculeTransitionGroups.ElementAt(0).PrecursorMz);
        }

        private static void TestEditingTransition()
        {
            RunUI(() =>
            {
                SkylineWindow.ExpandPrecursors();
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].FirstNode.FirstNode.FirstNode;
            });
            var editMoleculeDlg =
                ShowDialog<EditCustomMoleculeDlg>(
                    () => SkylineWindow.ModifyTransition((TransitionTreeNode) SkylineWindow.SequenceTree.SelectedNode));
            RunUI(() =>
            {
                Assert.AreEqual(testFormula, editMoleculeDlg.FormulaBox.Formula);
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(testFormula), (double)editMoleculeDlg.FormulaBox.MonoMass, BioMassCalc.MassElectron / 100);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateMassFromFormula(testFormula), (double)editMoleculeDlg.FormulaBox.AverageMass, BioMassCalc.MassElectron / 100);
                editMoleculeDlg.FormulaBox.Formula = null;
                editMoleculeDlg.FormulaBox.AverageMass = 800;
                editMoleculeDlg.FormulaBox.MonoMass = 805;
                editMoleculeDlg.Charge = 2;
                editMoleculeDlg.NameText = "Fragment";
            });
            OkDialog(editMoleculeDlg,editMoleculeDlg.OkDialog);
            Assert.AreEqual("Fragment",SkylineWindow.Document.MoleculeTransitions.ElementAt(0).Transition.CustomIon.ToString());
            Assert.AreEqual(BioMassCalc.CalculateMz(805,2),SkylineWindow.Document.MoleculeTransitions.ElementAt(0).Mz);
        }

        private static void TestAddingTransition()
        {
            RunUI(() =>
            {
                SkylineWindow.ExpandPrecursors();
                var node = SkylineWindow.SequenceTree.Nodes[0].FirstNode.FirstNode;
                SkylineWindow.SequenceTree.SelectedNode = node;
            });
            var moleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.AddSmallMolecule);
            RunUI(() =>
            {
                moleculeDlg.FormulaBox.Formula = testFormula;
                moleculeDlg.NameText = testNametext;
                moleculeDlg.Charge = 1;
            });
            OkDialog(moleculeDlg, moleculeDlg.OkDialog);
            var newDoc = SkylineWindow.Document;
            var compareIon = new DocNodeCustomIon(testFormula, testNametext);
            Assert.AreEqual(compareIon,newDoc.MoleculeTransitions.ElementAt(0).Transition.CustomIon);
            Assert.AreEqual(1,newDoc.MoleculeTransitions.ElementAt(0).Transition.Charge);
        }

        private static void TestAddingSmallMolecule()
        {
            RunUI(() => SkylineWindow.SelectedPath = new IdentityPath(SkylineWindow.Document.MoleculeGroups.ElementAt(0).Id));
            var moleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.AddSmallMolecule);
            const string formula = "COOO13H";
            RunUI(() =>
            {
                moleculeDlg.FormulaBox.Formula = formula;
                moleculeDlg.Charge = 1;
            });
            OkDialog(moleculeDlg, moleculeDlg.OkDialog);
            var compareIon = new DocNodeCustomIon(formula, "");
            var newDoc = SkylineWindow.Document;
            Assert.AreEqual(compareIon, newDoc.Molecules.ElementAt(0).Peptide.CustomIon);
            Assert.AreEqual(1, newDoc.MoleculeTransitionGroups.ElementAt(0).PrecursorCharge);
            var predictedMz = BioMassCalc.MONOISOTOPIC.CalculateMz(formula, moleculeDlg.Charge);
            Assert.AreEqual(predictedMz, newDoc.MoleculeTransitionGroups.ElementAt(0).PrecursorMz, BioMassCalc.MassElectron/100);
        }
    }
}
