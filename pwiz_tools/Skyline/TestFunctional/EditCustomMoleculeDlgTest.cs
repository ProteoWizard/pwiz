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
        const string testNametext = "Molecule";
        const string testNametextA = "moleculeA";
        const string COOO13H = "COOO13H";
        const double averageMass100 = 100;
        const double monoMass105 = 105;
        const double testRT = 234.56;
        const double testRTWindow = 4.56;

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
            var docNext = WaitForDocumentChange(origDoc);
            TestAddingSmallMolecule();
            docNext = WaitForDocumentChange(docNext);
            TestAddingTransition();
            docNext = WaitForDocumentChange(docNext);
            TestEditingTransitionGroup();
            docNext = WaitForDocumentChange(docNext);
            TestEditingSmallMolecule();
            docNext = WaitForDocumentChange(docNext);
            TestEditingTransition();
            docNext = WaitForDocumentChange(docNext);
            TestAddingSmallMoleculePrecursor();
            WaitForDocumentChange(docNext);
        }

        private static void TestEditingSmallMolecule()
        {
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].FirstNode;
            } );
            var docA = SkylineWindow.Document;

            var editMoleculeDlgA = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.ModifyPeptide);
            // Retention time window with no retention time should cause an error
            RunUI(() =>
            {
                editMoleculeDlgA.RetentionTimeWindow = testRTWindow;
            });
            RunDlg<MessageDlg>(editMoleculeDlgA.OkDialog, dlg =>
            {
                AssertEx.AreComparableStrings(
                    Resources.Peptide_ExplicitRetentionTimeWindow_Explicit_retention_time_window_requires_an_explicit_retention_time_value_,
                    dlg.Message);
                dlg.OkDialog(); // Dismiss the warning
            });
            // Set retention time, and this should be OK
            RunUI(() => editMoleculeDlgA.RetentionTime = testRT);
            OkDialog(editMoleculeDlgA, editMoleculeDlgA.OkDialog);
            var doc = WaitForDocumentChange(docA);
            Assert.IsTrue(doc.Molecules.ElementAt(0).EqualsId(docA.Molecules.ElementAt(0))); // No Id change
            Assert.IsTrue(doc.MoleculeTransitionGroups.ElementAt(0).EqualsId(docA.MoleculeTransitionGroups.ElementAt(0)));  // No change to Id node or its child Ids
            Assert.AreEqual(testRT, doc.Molecules.ElementAt(0).ExplicitRetentionTime.RetentionTime);
            Assert.AreEqual(testRTWindow, doc.Molecules.ElementAt(0).ExplicitRetentionTime.RetentionTimeWindow);
            Assert.AreEqual(ExplicitTransitionGroupValues.TEST, docA.MoleculeTransitionGroups.ElementAt(0).ExplicitValues);

            var editMoleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.ModifyPeptide);
            double massPrecisionTolerance = Math.Pow(10, -SequenceMassCalc.MassPrecision);
            var nameText = "testname";
            RunUI(() =>
            {
                Assert.AreEqual(COOO13H, editMoleculeDlg.NameText);
                editMoleculeDlg.NameText = nameText;  // Simulate user changing the name
            });
            OkDialog(editMoleculeDlg, editMoleculeDlg.OkDialog);
            var newdoc = WaitForDocumentChange(doc);
            // Molecule and transitions have independent formulas
            Assert.AreEqual(253.00979, newdoc.Molecules.ElementAt(0).CustomIon.AverageMass, massPrecisionTolerance);
            Assert.AreNotEqual(averageMass100, newdoc.Molecules.ElementAt(0).CustomIon.AverageMass, massPrecisionTolerance);
            Assert.AreEqual(252.931544, newdoc.Molecules.ElementAt(0).CustomIon.MonoisotopicMass, massPrecisionTolerance);
            Assert.AreNotEqual(monoMass105, newdoc.Molecules.ElementAt(0).CustomIon.MonoisotopicMass, massPrecisionTolerance);
            Assert.AreEqual(nameText, newdoc.Molecules.ElementAt(0).CustomIon.Name);
            Assert.AreEqual(testNametext, newdoc.MoleculeTransitionGroups.ElementAt(0).CustomIon.Name);  // Should have no effect on child name
            Assert.IsNull(newdoc.MoleculeTransitionGroups.ElementAt(0).CustomIon.Formula);
            Assert.AreEqual(monoMass105 - BioMassCalc.MassElectron, newdoc.MoleculeTransitionGroups.ElementAt(0).PrecursorMz, massPrecisionTolerance);

            // Verify that RT overrides work
            Assert.AreEqual(testRT, newdoc.Molecules.ElementAt(0).ExplicitRetentionTime.RetentionTime, massPrecisionTolerance);
            Assert.AreEqual(testRTWindow, newdoc.Molecules.ElementAt(0).ExplicitRetentionTime.RetentionTimeWindow.Value, massPrecisionTolerance);

        }

        private static void TestEditingTransitionGroup()
        {
            RunUI(() =>
            {
                // Select the first precursor
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].FirstNode.Nodes[0];
            });
            var docA = SkylineWindow.Document;

            var editMoleculeDlgA = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.ModifySmallMoleculeTransitionGroup);
            RunUI(() =>
            {
                // Test the "set" part of "Issue 371: Small molecules: need to be able to import and/or set CE, RT and DT for individual precursors and products"
                editMoleculeDlgA.DriftTimeMsec = ExplicitTransitionGroupValues.TEST.DriftTimeMsec.Value;
                editMoleculeDlgA.DriftTimeHighEnergyOffsetMsec = ExplicitTransitionGroupValues.TEST.DriftTimeHighEnergyOffsetMsec.Value;
                editMoleculeDlgA.CollisionEnergy = ExplicitTransitionGroupValues.TEST.CollisionEnergy.Value;
                editMoleculeDlgA.SLens = ExplicitTransitionGroupValues.TEST.SLens.Value;
                editMoleculeDlgA.ConeVoltage = ExplicitTransitionGroupValues.TEST.ConeVoltage;
                editMoleculeDlgA.DeclusteringPotential = ExplicitTransitionGroupValues.TEST.DeclusteringPotential;
                editMoleculeDlgA.CompensationVoltage = ExplicitTransitionGroupValues.TEST.CompensationVoltage;
            });
            OkDialog(editMoleculeDlgA, editMoleculeDlgA.OkDialog);
            var doc = WaitForDocumentChange(docA);
            var peptideDocNode = doc.Molecules.ElementAt(0);
            Assert.IsNotNull(peptideDocNode);
            Assert.IsTrue(peptideDocNode.EqualsId(docA.Molecules.ElementAt(0))); // No Id change
            Assert.IsTrue(doc.MoleculeTransitionGroups.ElementAt(0).EqualsId(docA.MoleculeTransitionGroups.ElementAt(0)));  // No change to Id node or its child Ids
            Assert.AreEqual(ExplicitTransitionGroupValues.TEST, doc.MoleculeTransitionGroups.ElementAt(0).ExplicitValues);
            Assert.IsNull(peptideDocNode.ExplicitRetentionTime);
            var editMoleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.ModifySmallMoleculeTransitionGroup);
            double massPrecisionTolerance = Math.Pow(10, -SequenceMassCalc.MassPrecision);
            double massAverage = 0;
            double massMono = 0;
            RunUI(() =>
            {
                Assert.AreEqual(COOO13H, editMoleculeDlg.NameText); // Comes from the docnode
                Assert.AreEqual(COOO13H, editMoleculeDlg.FormulaBox.Formula); // Comes from the docnode
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(COOO13H), editMoleculeDlg.FormulaBox.MonoMass ?? -1, massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateMassFromFormula(COOO13H), editMoleculeDlg.FormulaBox.AverageMass ?? -1, massPrecisionTolerance);
                // Now try something crazy
                editMoleculeDlg.FormulaBox.Formula = "H500000000";
            });
            for (int loop = 0; loop < 2; loop++)
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

            // Less extreme values should trigger a warning about instrument limits
            RunUI(() =>
            {
                editMoleculeDlg.FormulaBox.MonoMass = CustomIon.MAX_MASS - 100;
                editMoleculeDlg.FormulaBox.AverageMass = editMoleculeDlg.FormulaBox.MonoMass;
            });
            RunDlg<MessageDlg>(editMoleculeDlg.OkDialog, dlg =>
            {
                AssertEx.AreComparableStrings(
                    Resources.SkylineWindow_AddMolecule_The_precursor_m_z_for_this_molecule_is_out_of_range_for_your_instrument_settings_,
                    dlg.Message);
                dlg.OkDialog(); // Dismiss the warning
            });

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

                editMoleculeDlg.FormulaBox.AverageMass = averageMass100;
                editMoleculeDlg.FormulaBox.MonoMass = monoMass105;
                editMoleculeDlg.NameText = testNametext;
                Assert.IsTrue(string.IsNullOrEmpty(editMoleculeDlg.FormulaBox.Formula));
                Assert.AreEqual(averageMass100, editMoleculeDlg.FormulaBox.AverageMass);
                Assert.AreEqual(monoMass105, editMoleculeDlg.FormulaBox.MonoMass);
                var monoText = editMoleculeDlg.FormulaBox.MonoText;
                var averageText = editMoleculeDlg.FormulaBox.AverageText;
                double oldMzMono = double.Parse(monoText);
                double oldMzAverage = double.Parse(averageText);
                editMoleculeDlg.Charge = 3;
                Assert.AreEqual(monoText, editMoleculeDlg.FormulaBox.MonoText); // m/z readout should not change
                Assert.AreEqual(averageText, editMoleculeDlg.FormulaBox.AverageText);
                Assert.AreEqual(3 * (oldMzAverage + BioMassCalc.MassElectron),
                    editMoleculeDlg.FormulaBox.AverageMass.Value, massPrecisionTolerance);
                // Mass should change, since mz is what the user is declaring
                Assert.AreEqual(3 * (oldMzMono + BioMassCalc.MassElectron), editMoleculeDlg.FormulaBox.MonoMass.Value,
                    massPrecisionTolerance);
                editMoleculeDlg.Charge = 1;
                massAverage = editMoleculeDlg.FormulaBox.AverageMass.Value;
                massMono = editMoleculeDlg.FormulaBox.MonoMass.Value;
                Assert.AreEqual(averageMass100, massAverage, massPrecisionTolerance); // Mass should change back
                Assert.AreEqual(monoMass105, massMono, massPrecisionTolerance);
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
                Assert.AreEqual(averageMass100, massAverage, massPrecisionTolerance); // Mass should change back
                Assert.AreEqual(monoMass105, massMono, massPrecisionTolerance);
            });
            OkDialog(editMoleculeDlg, editMoleculeDlg.OkDialog);
            var newdoc = WaitForDocumentChange(doc);
            Assert.AreEqual(massAverage, newdoc.MoleculeTransitionGroups.ElementAt(0).CustomIon.AverageMass, massPrecisionTolerance);
            Assert.AreEqual(massMono, newdoc.MoleculeTransitionGroups.ElementAt(0).CustomIon.MonoisotopicMass, massPrecisionTolerance);
            Assert.IsNotNull(newdoc.Molecules.ElementAt(0).CustomIon.Formula); // Molecule and children do not share formula
            Assert.IsNull(newdoc.MoleculeTransitionGroups.ElementAt(0).CustomIon.Formula);
            Assert.AreEqual(massMono - BioMassCalc.MassElectron, newdoc.MoleculeTransitionGroups.ElementAt(0).PrecursorMz);
            Assert.IsFalse(newdoc.MoleculeTransitionGroups.ElementAt(0).EqualsId(peptideDocNode));  // Changing the mass changes the Id node
            // Verify that CE overrides work
            Assert.AreEqual(ExplicitTransitionGroupValues.TEST, newdoc.MoleculeTransitionGroups.ElementAt(0).ExplicitValues);
            Assert.IsNull(newdoc.Molecules.ElementAt(0).ExplicitRetentionTime);  // Not set yet

            // Verify that the explicitly set drift time overides any calculations
            double windowDT;
            var centerDriftTime = newdoc.Settings.PeptideSettings.Prediction.GetDriftTime(
                                       newdoc.Molecules.First(), newdoc.MoleculeTransitionGroups.First(), null, out windowDT);
            Assert.AreEqual(ExplicitTransitionGroupValues.TEST.DriftTimeMsec.Value, centerDriftTime.DriftTimeMsec(false) ?? 0, .0001);
            Assert.AreEqual(ExplicitTransitionGroupValues.TEST.DriftTimeMsec.Value + ExplicitTransitionGroupValues.TEST.DriftTimeHighEnergyOffsetMsec.Value, centerDriftTime.DriftTimeMsec(true) ?? 0, .0001);
            Assert.AreEqual(0, windowDT, .0001);

            // Verify that tree selection doesn't change just because we changed an ID object
            // (formerly the tree node would collapse and focus would jump up a level)
            RunUI(() =>
            {
                Assert.AreEqual(SkylineWindow.SequenceTree.SelectedNode, SkylineWindow.SequenceTree.Nodes[0].FirstNode);
            });
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

            // Verify that tree selection doesn't change just because we changed an ID object
            // (formerly the tree node would collapse and focus would jump up a level)
            RunUI(() =>
            {
                Assert.AreEqual(SkylineWindow.SequenceTree.SelectedNode, SkylineWindow.SequenceTree.Nodes[0].FirstNode.FirstNode.FirstNode);
            });

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

            // Verify that tree selection doesn't change just because we changed an ID object
            // (formerly the tree node would collapse and focus would jump up a level)
            RunUI(() =>
            {
                Assert.AreEqual(SkylineWindow.SequenceTree.SelectedNode, SkylineWindow.SequenceTree.Nodes[0].FirstNode.FirstNode.FirstNode);
            });
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
                moleculeDlg.NameText = testNametextA;
                moleculeDlg.Charge = 1;
            });
            OkDialog(moleculeDlg, moleculeDlg.OkDialog);
            var newDoc = SkylineWindow.Document;
            var compareIon = new DocNodeCustomIon(C12H12, testNametextA);
            Assert.AreEqual(compareIon,newDoc.MoleculeTransitions.ElementAt(0).Transition.CustomIon);
            Assert.AreEqual(1,newDoc.MoleculeTransitions.ElementAt(0).Transition.Charge);

            // Verify that tree selection doesn't change just because we changed an ID object
            // (formerly the tree node would collapse and focus would jump up a level)
            RunUI(() =>
            {
                Assert.AreEqual(SkylineWindow.SequenceTree.SelectedNode, SkylineWindow.SequenceTree.Nodes[0].FirstNode.FirstNode);
            });
        }

        private static void TestAddingSmallMolecule()
        {
            RunUI(() => SkylineWindow.SelectedPath = new IdentityPath(SkylineWindow.Document.MoleculeGroups.ElementAt(0).Id));
            var moleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.AddSmallMolecule);
            const int charge = 1;
            RunUI(() =>
            {
                moleculeDlg.NameText = COOO13H;
                moleculeDlg.FormulaBox.Formula = COOO13H;
                moleculeDlg.Charge = charge;
            });
            OkDialog(moleculeDlg, moleculeDlg.OkDialog);
            var compareIon = new DocNodeCustomIon(COOO13H, COOO13H);
            var newDoc = SkylineWindow.Document;
            Assert.AreEqual(compareIon, newDoc.Molecules.ElementAt(0).CustomIon);
            Assert.AreEqual(compareIon, newDoc.MoleculeTransitionGroups.ElementAt(0).CustomIon);
            Assert.AreEqual(charge, newDoc.MoleculeTransitionGroups.ElementAt(0).PrecursorCharge);
            var predictedMz = BioMassCalc.MONOISOTOPIC.CalculateIonMz(COOO13H, charge);
            var actualMz = newDoc.MoleculeTransitionGroups.ElementAt(0).PrecursorMz;
            Assert.AreEqual(predictedMz, actualMz, Math.Pow(10, -SequenceMassCalc.MassPrecision));
        }

        private void TestAddingSmallMoleculePrecursor()
        {
            // Position ourselves on the first molecule
            var newDoc = SkylineWindow.Document;
            SelectNode(SrmDocument.Level.Molecules, 0);
            var moleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.AddSmallMolecule);
            const int charge = 1;
            var heavyFormula = COOO13H.Replace("O13", "O12O'");
            RunUI(() =>
            {
                moleculeDlg.NameText = heavyFormula;
                moleculeDlg.FormulaBox.Formula = heavyFormula;
                moleculeDlg.IsotopeLabelType = IsotopeLabelType.light; // This should provoke a failure - can't have two of the same label and charge
            });
            RunDlg<MessageDlg>(moleculeDlg.OkDialog, dlg =>
            {
                // Trying to exit the dialog should cause a warning about charge
                Assert.AreEqual(
                Resources.EditCustomMoleculeDlg_OkDialog_A_precursor_with_that_charge_and_label_type_already_exists_, dlg.Message);
                dlg.OkDialog(); // Dismiss the warning
            });
            RunUI(() =>
            {
                moleculeDlg.IsotopeLabelType = IsotopeLabelType.heavy; 
            });
            OkDialog(moleculeDlg, moleculeDlg.OkDialog);

            var compareIonHeavy = new DocNodeCustomIon(heavyFormula, heavyFormula);
            newDoc = WaitForDocumentChange(newDoc);
            Assert.AreEqual(heavyFormula, newDoc.MoleculeTransitionGroups.ElementAt(1).CustomIon.Name);
            Assert.AreEqual(compareIonHeavy, newDoc.MoleculeTransitionGroups.ElementAt(1).CustomIon);
            Assert.AreEqual(charge, newDoc.MoleculeTransitionGroups.ElementAt(1).TransitionGroup.PrecursorCharge);
            var predictedMz = BioMassCalc.MONOISOTOPIC.CalculateIonMz(heavyFormula, charge);
            var actualMz = newDoc.MoleculeTransitionGroups.ElementAt(1).PrecursorMz;
            Assert.AreEqual(predictedMz, actualMz, Math.Pow(10, -SequenceMassCalc.MassPrecision));

            // Now verify that we are OK with different charge same label
            SelectNode(SrmDocument.Level.Molecules, 0);
            var moleculeDlg2 = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.AddSmallMolecule);
            RunUI(() =>
            {
                moleculeDlg2.FormulaBox.Formula = COOO13H + "H";
                moleculeDlg2.Charge = charge + 1;
                moleculeDlg2.IsotopeLabelType = IsotopeLabelType.light; // This should not provoke a failure
            });
            OkDialog(moleculeDlg2, moleculeDlg2.OkDialog);

            // Verify that the transition group sort order is enforced in the document
            // Select the last precursor, bump its charge state to drop its mz - should result in doc node reordering
            CheckTransitionGroupSortOrder(newDoc);
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].FirstNode.LastNode;
            });
            var editMoleculeDlgB = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.ModifySmallMoleculeTransitionGroup);
            RunUI(() =>
            {
                editMoleculeDlgB.Charge = 5;
            });
            OkDialog(editMoleculeDlgB, editMoleculeDlgB.OkDialog);
            newDoc = WaitForDocumentChange(newDoc);
            CheckTransitionGroupSortOrder(newDoc);
            RunUI(SkylineWindow.Undo);
            newDoc = WaitForDocumentChange(newDoc);
            CheckTransitionGroupSortOrder(newDoc);

        }

        private static void CheckTransitionGroupSortOrder(SrmDocument newDoc)
        {
            foreach (var mol in newDoc.Molecules)
            {
                double mz = -1;
                int charge = -99999;
                foreach (var group in mol.TransitionGroups)
                {
                    if (charge != @group.PrecursorCharge)
                    {
                        Assert.IsTrue(@group.PrecursorCharge > charge, "Transition groups should be sorted on charge");
                        charge = @group.PrecursorCharge;
                        mz = -1;
                    }
                    else
                    {
                        Assert.IsTrue(@group.PrecursorMz > mz, "Transition groups should be sorted on m/z");
                        mz = @group.PrecursorMz;
                    }
                }
            }
        }
    }
}
