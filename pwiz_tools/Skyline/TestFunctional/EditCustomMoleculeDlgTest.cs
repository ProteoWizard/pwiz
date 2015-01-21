/*
 * Original author: Max Horowitz-Gelb <maxhg .at. u.washington.edu>,
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class EditCustomMoleculeDlgTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestEditCustomMoleculeDlg()
        {
            RunFunctionalTest();
        }

        const string C12H12 = "C12H12";
        const string testNametext = "moleculeA";
        const string COOO13H = "COOO13H";

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
            TestEditingSmallMolecule();
            WaitForDocumentChange(doc4);
            TestEditingTransition();
        }

        private static void TestEditingSmallMolecule()
        {
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].FirstNode;
            } );
            var docA = SkylineWindow.Document;

            // Verify that Id does not change if you leave the mass and formula alone
            // This is of interest because custom molecules are an unusual case where a user edit can change the Id of the "Peptide"
            var editMoleculeDlgA = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.ModifyPeptide);
            const double testDT = 123.45;
            const double testOffsetDT = -.45;
            const double testRT = 234.56;
            const double testCE = 345.67;
            RunUI(() =>
            {
                // Test the "set" part of "Issue 371: Small molecules: need to be able to import and/or set CE, RT and DT for individual precursors and products"
                editMoleculeDlgA.DriftTimeMsec = testDT;
                editMoleculeDlgA.DriftTimeHighEnergyOffsetMsec = testOffsetDT;
                editMoleculeDlgA.RetentionTime = testRT;
                editMoleculeDlgA.CollisionEnergy = testCE;
            });
            OkDialog(editMoleculeDlgA, editMoleculeDlgA.OkDialog);
            var doc = WaitForDocumentChange(docA);
            Assert.IsTrue(doc.Molecules.ElementAt(0).EqualsId(docA.Molecules.ElementAt(0))); // No Id change
            Assert.IsTrue(doc.MoleculeTransitionGroups.ElementAt(0).EqualsId(docA.MoleculeTransitionGroups.ElementAt(0)));  // No change to Id node or its child Ids
            Assert.AreEqual(testCE, doc.MoleculeTransitionGroups.ElementAt(0).ExplicitValues.CollisionEnergy.Value);
            Assert.IsFalse(docA.MoleculeTransitionGroups.ElementAt(0).ExplicitValues.CollisionEnergy.HasValue);

            var editMoleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.ModifyPeptide);
            double massPrecisionTolerance = Math.Pow(10, -SequenceMassCalc.MassPrecision);
            double massAverage = 0;
            double massMono = 0;
            RunUI(() =>
            {
                Assert.AreEqual(COOO13H, editMoleculeDlg.FormulaBox.Formula); // Comes from the docnode
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(COOO13H), editMoleculeDlg.FormulaBox.MonoMass ?? -1, massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateMassFromFormula(COOO13H), editMoleculeDlg.FormulaBox.AverageMass ?? -1, massPrecisionTolerance);
                // Now try something crazy
                editMoleculeDlg.FormulaBox.Formula = "H500000000";
            });
            for (int loop=0; loop < 2; loop++)
            {
                // Trying to exit the dialog should cause a warning about mass
                RunDlg<MessageDlg>(editMoleculeDlg.OkDialog, dlg =>
                {
                    AssertEx.AreComparableStrings(
                        string.Format(Resources.EditCustomMoleculeDlg_OkDialog_Custom_molecules_must_have_a_mass_less_than_or_equal_to__0__, CustomIon.MAX_MASS), 
                        dlg.Message);
                    dlg.OkDialog(); // Dismiss the warning
                });
                RunUI(() =>
                {
                    editMoleculeDlg.FormulaBox.Formula = ""; // Should leave ridiculous mz values in place, those should also trigger warning
                });
            }
            RunUI(() =>
            {
                // Verify the interaction of explicitly set formula, mz and charge
                editMoleculeDlg.FormulaBox.Formula = C12H12;
                double mono = editMoleculeDlg.FormulaBox.MonoMass ?? -1;
                double average = editMoleculeDlg.FormulaBox.AverageMass ?? -1;
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H12), mono, massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateMassFromFormula(C12H12), average, massPrecisionTolerance);
                Assert.AreEqual(mono - BioMassCalc.MassElectron, double.Parse(editMoleculeDlg.FormulaBox.MonoText),
                    massPrecisionTolerance);
                editMoleculeDlg.Charge = 3;
                Assert.AreEqual(
                    Math.Round(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H12), SequenceMassCalc.MassPrecision),
                    mono); // Masses should not change
                Assert.AreEqual(
                    Math.Round(BioMassCalc.AVERAGE.CalculateMassFromFormula(C12H12), SequenceMassCalc.MassPrecision),
                    average);
                editMoleculeDlg.Charge = -1; // Validate negative charges
                Assert.AreEqual(
                    Math.Round(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H12), SequenceMassCalc.MassPrecision),
                    mono); // Masses should not change
                Assert.AreEqual(
                    Math.Round(BioMassCalc.AVERAGE.CalculateMassFromFormula(C12H12), SequenceMassCalc.MassPrecision),
                    average);
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateIonMz(C12H12, -1), mono + BioMassCalc.MassElectron,
                    massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateIonMz(C12H12, -1), average + BioMassCalc.MassElectron,
                    massPrecisionTolerance);
                editMoleculeDlg.Charge = 1;
                Assert.AreEqual(
                    Math.Round(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H12), SequenceMassCalc.MassPrecision),
                    mono); // Masses should not change
                Assert.AreEqual(
                    Math.Round(BioMassCalc.AVERAGE.CalculateMassFromFormula(C12H12), SequenceMassCalc.MassPrecision),
                    average);
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateIonMz(C12H12, 1), mono - BioMassCalc.MassElectron,
                    massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateIonMz(C12H12, 1), average - BioMassCalc.MassElectron,
                    massPrecisionTolerance);

                editMoleculeDlg.FormulaBox.Formula = null; // Simulate user blanking out the formula
                Assert.AreEqual(mono, editMoleculeDlg.FormulaBox.MonoMass); // Leaves masses untouched
                Assert.AreEqual(average, editMoleculeDlg.FormulaBox.AverageMass);

                editMoleculeDlg.FormulaBox.AverageMass = 100;
                editMoleculeDlg.FormulaBox.MonoMass = 105;
                editMoleculeDlg.NameText = "Molecule";
                Assert.IsTrue(string.IsNullOrEmpty(editMoleculeDlg.FormulaBox.Formula));
                Assert.AreEqual(100, editMoleculeDlg.FormulaBox.AverageMass);
                Assert.AreEqual(105, editMoleculeDlg.FormulaBox.MonoMass);
                var monoText = editMoleculeDlg.FormulaBox.MonoText;
                var averageText = editMoleculeDlg.FormulaBox.AverageText;
                double oldMzMono = double.Parse(monoText);
                double oldMzAverage = double.Parse(averageText);
                editMoleculeDlg.Charge = 3;
                Assert.AreEqual(monoText, editMoleculeDlg.FormulaBox.MonoText); // m/z readout should not change
                Assert.AreEqual(averageText, editMoleculeDlg.FormulaBox.AverageText);
                Assert.AreEqual(3*(oldMzAverage + BioMassCalc.MassElectron),
                    editMoleculeDlg.FormulaBox.AverageMass.Value, massPrecisionTolerance);
                // Mass should change, since mz is what the user is declaring
                Assert.AreEqual(3*(oldMzMono + BioMassCalc.MassElectron), editMoleculeDlg.FormulaBox.MonoMass.Value,
                    massPrecisionTolerance);
                editMoleculeDlg.Charge = 1;
                massAverage = editMoleculeDlg.FormulaBox.AverageMass.Value;
                massMono = editMoleculeDlg.FormulaBox.MonoMass.Value;
                Assert.AreEqual(100, massAverage, massPrecisionTolerance); // Mass should change back
                Assert.AreEqual(105, massMono, massPrecisionTolerance);
            });

            // Test charge state limits
            RunUI(() => editMoleculeDlg.Charge = TransitionGroup.MAX_PRECURSOR_CHARGE + 1);
            RunDlg<MessageDlg>(editMoleculeDlg.OkDialog, dlg =>
            {
                // Trying to exit the dialog should cause a warning about charge
                Assert.AreEqual(
                String.Format(Resources.MessageBoxHelper_ValidateSignedNumberTextBox_Value__0__must_be_between__1__and__2__or__3__and__4__,
                   TransitionGroup.MAX_PRECURSOR_CHARGE + 1,
                     -TransitionGroup.MAX_PRECURSOR_CHARGE,
                     -TransitionGroup.MIN_PRECURSOR_CHARGE,
                     TransitionGroup.MIN_PRECURSOR_CHARGE,
                     TransitionGroup.MAX_PRECURSOR_CHARGE), dlg.Message);
                dlg.OkDialog(); // Dismiss the warning
            });
            RunUI(() => editMoleculeDlg.Charge = -(TransitionGroup.MAX_PRECURSOR_CHARGE + 1));
            RunDlg<MessageDlg>(editMoleculeDlg.OkDialog, dlg =>
            {
                // Trying to exit the dialog should cause a warning about charge
                Assert.AreEqual(
                String.Format(Resources.MessageBoxHelper_ValidateSignedNumberTextBox_Value__0__must_be_between__1__and__2__or__3__and__4__,
                   -(TransitionGroup.MAX_PRECURSOR_CHARGE + 1),
                     -TransitionGroup.MAX_PRECURSOR_CHARGE,
                     -TransitionGroup.MIN_PRECURSOR_CHARGE,
                     TransitionGroup.MIN_PRECURSOR_CHARGE,
                     TransitionGroup.MAX_PRECURSOR_CHARGE), dlg.Message);
                dlg.OkDialog(); // Dismiss the warning
            });

            // Restore
            RunUI(() =>
            {
                editMoleculeDlg.Charge = 1;
                massAverage = editMoleculeDlg.FormulaBox.AverageMass.Value;
                massMono = editMoleculeDlg.FormulaBox.MonoMass.Value;
                Assert.AreEqual(100, massAverage, massPrecisionTolerance); // Mass should change back
                Assert.AreEqual(105, massMono, massPrecisionTolerance);
            });
            OkDialog(editMoleculeDlg,editMoleculeDlg.OkDialog);
            var newdoc = WaitForDocumentChange(doc);
            Assert.AreEqual(massAverage, newdoc.Molecules.ElementAt(0).Peptide.CustomIon.AverageMass, massPrecisionTolerance);
            Assert.AreEqual(massMono, newdoc.Molecules.ElementAt(0).Peptide.CustomIon.MonoisotopicMass, massPrecisionTolerance);
            Assert.IsNull(newdoc.Molecules.ElementAt(0).Peptide.CustomIon.Formula);
            Assert.AreEqual(massMono - BioMassCalc.MassElectron, newdoc.MoleculeTransitionGroups.ElementAt(0).PrecursorMz);
            Assert.IsFalse(newdoc.Molecules.ElementAt(0).EqualsId(doc.Molecules.ElementAt(0)));  // Changing the mass changes the Id node
            Assert.IsFalse(newdoc.MoleculeTransitionGroups.ElementAt(0).EqualsId(doc.MoleculeTransitionGroups.ElementAt(0)));  // Changing the mass changes the Id node and its children

            // Verify that RT and CE overrides work
            Assert.AreEqual(testCE, newdoc.MoleculeTransitionGroups.ElementAt(0).ExplicitValues.CollisionEnergy.Value);
            Assert.AreEqual(testRT, newdoc.Molecules.ElementAt(0).ExplicitRetentionTime.Value);

            // Verify that the explicitly set drift time overides any calculations
            double windowDT;
            var centerDriftTime = newdoc.Settings.PeptideSettings.Prediction.GetDriftTime(
                                       newdoc.Molecules.First(), newdoc.MoleculeTransitionGroups.First(), null, out windowDT);
            Assert.AreEqual(testDT, centerDriftTime.DriftTimeMsec(false) ?? 0, .0001);
            Assert.AreEqual(testDT+testOffsetDT, centerDriftTime.DriftTimeMsec(true) ?? 0, .0001);
            Assert.AreEqual(0, windowDT, .0001);
        }

        private static void TestEditingTransition()
        {
            double massPrecisionTolerance = Math.Pow(10, -SequenceMassCalc.MassPrecision);
            var doc = SkylineWindow.Document;
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
                Assert.AreEqual(C12H12, editMoleculeDlg.FormulaBox.Formula);
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H12), editMoleculeDlg.FormulaBox.MonoMass ?? -1, massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateMassFromFormula(C12H12), editMoleculeDlg.FormulaBox.AverageMass ?? -1, massPrecisionTolerance);
                editMoleculeDlg.FormulaBox.Formula = null;
                editMoleculeDlg.Charge = 2; // If we set this after we set the mass, the mass will change since m/z is the actual input
                editMoleculeDlg.FormulaBox.AverageMass = 800;
                editMoleculeDlg.FormulaBox.MonoMass = 805;
                editMoleculeDlg.NameText = "Fragment";
            });
            OkDialog(editMoleculeDlg,editMoleculeDlg.OkDialog);
            var newdoc = WaitForDocumentChange(doc);
            Assert.AreEqual("Fragment", newdoc.MoleculeTransitions.ElementAt(0).Transition.CustomIon.ToString());
            Assert.AreEqual(BioMassCalc.CalculateIonMz(805, 2), newdoc.MoleculeTransitions.ElementAt(0).Mz, massPrecisionTolerance);
            Assert.IsFalse(ReferenceEquals(doc.MoleculeTransitions.ElementAt(0).Id, newdoc.MoleculeTransitions.ElementAt(0).Id)); // Changing the mass changes the Id

            // And test undo/redo
            RunUI(() => SkylineWindow.Undo());
            newdoc = WaitForDocumentChange(newdoc);
            Assert.AreNotEqual("Fragment", newdoc.MoleculeTransitions.ElementAt(0).Transition.CustomIon.ToString());
            Assert.AreNotEqual(BioMassCalc.CalculateIonMz(805, 2), newdoc.MoleculeTransitions.ElementAt(0).Mz, massPrecisionTolerance);
            Assert.IsTrue(ReferenceEquals(doc.MoleculeTransitions.ElementAt(0).Id, newdoc.MoleculeTransitions.ElementAt(0).Id)); 
            RunUI(() => SkylineWindow.Redo());
            newdoc = WaitForDocumentChange(newdoc);
            Assert.AreEqual("Fragment", newdoc.MoleculeTransitions.ElementAt(0).Transition.CustomIon.ToString());
            Assert.AreEqual(BioMassCalc.CalculateIonMz(805, 2), newdoc.MoleculeTransitions.ElementAt(0).Mz, massPrecisionTolerance);

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
                moleculeDlg.FormulaBox.Formula = C12H12;
                moleculeDlg.NameText = testNametext;
                moleculeDlg.Charge = 1;
            });
            OkDialog(moleculeDlg, moleculeDlg.OkDialog);
            var newDoc = SkylineWindow.Document;
            var compareIon = new DocNodeCustomIon(C12H12, testNametext);
            Assert.AreEqual(compareIon,newDoc.MoleculeTransitions.ElementAt(0).Transition.CustomIon);
            Assert.AreEqual(1,newDoc.MoleculeTransitions.ElementAt(0).Transition.Charge);
        }

        private static void TestAddingSmallMolecule()
        {
            RunUI(() => SkylineWindow.SelectedPath = new IdentityPath(SkylineWindow.Document.MoleculeGroups.ElementAt(0).Id));
            var moleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.AddSmallMolecule);
            const int charge = 1;
            RunUI(() =>
            {
                moleculeDlg.FormulaBox.Formula = COOO13H;
                moleculeDlg.Charge = charge;
            });
            OkDialog(moleculeDlg, moleculeDlg.OkDialog);
            var compareIon = new DocNodeCustomIon(COOO13H, "");
            var newDoc = SkylineWindow.Document;
            Assert.AreEqual(compareIon, newDoc.Molecules.ElementAt(0).Peptide.CustomIon);
            Assert.AreEqual(charge, newDoc.MoleculeTransitionGroups.ElementAt(0).PrecursorCharge);
            var predictedMz = BioMassCalc.MONOISOTOPIC.CalculateIonMz(COOO13H, charge);
            var actualMz = newDoc.MoleculeTransitionGroups.ElementAt(0).PrecursorMz;
            Assert.AreEqual(predictedMz, actualMz, Math.Pow(10,-SequenceMassCalc.MassPrecision));
        }
    }
}
