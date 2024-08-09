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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class EditCustomMoleculeDlgTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestEditCustomMoleculeDlg()
        {
            RunFunctionalTest();
        }

        const string C12H12 = "C12H12";
        private static readonly Adduct ADDUCT_HEAVY_M_PLUS_H = Adduct.FromString(@"[M6C13+H]", Adduct.ADDUCT_TYPE.non_proteomic, null);
        const string C12H12_HEAVY = "C6C'6H12";
        const string C12H12_HEAVY_PLUS_H = "C6C'6H13"; // C12H12[M6C13+H]
        const string testNametextA = "moleculeA";
        const string COOO13H = "COOO13H";
        const double averageMass100 = 100;
        const double monoMass105 = 105;
        const double testRT = 234.56;
        const double testRTWindow = 4.56;


        public static readonly ExplicitTransitionGroupValues TESTVALUES_GROUP = ExplicitTransitionGroupValues.Create(1.234, 2.34, eIonMobilityUnits.drift_time_msec, 345.6); // Using this helps catch untested functionality as we add members
        public static readonly ExplicitTransitionValues TESTVALUES_TRAN =  ExplicitTransitionValues.Create(1.23, -.345, 5.67, 6.78, 7.89);

        protected override void DoTest()
        {
            TestEditBogusMolecule();
            TestEditMassWithPrecursorTransitions();
            TestEditWithIsotopeDistribution();
            AsMasses();
            AsFormulas();
            TestEditTransitionNoFormula();
        }

        protected void AsMasses()
        {
            RunUI(() => SkylineWindow.NewDocument(true));
            var origDoc = SkylineWindow.Document;
            RunUI(() =>
                SkylineWindow.ModifyDocument("", doc =>
                {
                    return doc.AddPeptideGroups(new[]
                    {
                        new PeptideGroupDocNode(new PeptideGroup(), doc.Annotations, "Molecule Group", "",
                            new PeptideDocNode[0])
                    }, true, IdentityPath.ROOT, out _, out _);
                }));
            var docNext = WaitForDocumentChange(origDoc);
            TestAddingSmallMoleculeAsMasses();
            docNext = WaitForDocumentChange(docNext);
            TestAddingTransitionAsMasses();
            docNext = WaitForDocumentChange(docNext);
            TestEditingSmallMoleculeAsMasses();
            docNext = WaitForDocumentChange(docNext);
            TestEditingTransitionAsMasses();
            WaitForDocumentChange(docNext);
        }

        protected void AsFormulas()
        {
            RunUI(() => SkylineWindow.NewDocument(true));
            var origDoc = SkylineWindow.Document;
            RunUI(() =>
                SkylineWindow.ModifyDocument("", doc =>
                {
                    return doc.AddPeptideGroups(new[]
                    {
                        new PeptideGroupDocNode(new PeptideGroup(), doc.Annotations, "Molecule Group", "",
                            new PeptideDocNode[0])
                    }, true, IdentityPath.ROOT, out _, out _);
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
            TestEditingMoleculeName(); // Test fix for double application of heavy labels in adduct
            docNext = WaitForDocumentChange(docNext);
            TestAddingSmallMoleculePrecursor();
            WaitForDocumentChange(docNext);
            TestMoleculeEditError(); // Test handling of edits to molecule that don't make sense for child ions
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
            Assert.AreEqual(TESTVALUES_GROUP, docA.MoleculeTransitionGroups.ElementAt(0).ExplicitValues);

            var editMoleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.ModifyPeptide);
            double massPrecisionTolerance = Math.Pow(10, -SequenceMassCalc.MassPrecision);
            var nameText = "testname";
            RunUI(() =>
            {
                Assert.AreEqual(COOO13H, editMoleculeDlg.NameText);
                editMoleculeDlg.NameText = nameText;  // Simulate user changing the name
                Assert.AreEqual(COOO13H, editMoleculeDlg.FormulaBox.NeutralFormula); // Comes from the docnode
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
                        string.Format(Resources.EditCustomMoleculeDlg_OkDialog_Custom_molecules_must_have_a_mass_less_than_or_equal_to__0__, CustomMolecule.MAX_MASS),
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
                editMoleculeDlg.FormulaBox.Formula = COOO13H;
            });

            OkDialog(editMoleculeDlg, editMoleculeDlg.OkDialog);
            var newdoc = WaitForDocumentChange(doc);
            // Molecule and transitions have same formula
            Assert.AreEqual(253.00979, newdoc.Molecules.ElementAt(0).CustomMolecule.AverageMass, massPrecisionTolerance);
            Assert.AreNotEqual(averageMass100, newdoc.Molecules.ElementAt(0).CustomMolecule.AverageMass, massPrecisionTolerance);
            var monoMass = 252.931544;
            Assert.AreEqual(monoMass, newdoc.Molecules.ElementAt(0).CustomMolecule.MonoisotopicMass, massPrecisionTolerance);
            Assert.AreEqual(nameText, newdoc.Molecules.ElementAt(0).CustomMolecule.Name);
            Assert.AreEqual(nameText, newdoc.MoleculeTransitionGroups.ElementAt(0).CustomMolecule.Name);
            Assert.AreEqual("[M+2H]", newdoc.MoleculeTransitionGroups.ElementAt(0).PrecursorAdduct.AdductFormula);
            Assert.AreEqual(.5 * monoMass + BioMassCalc.MassProton, newdoc.MoleculeTransitionGroups.ElementAt(0).PrecursorMz, massPrecisionTolerance);

            // Verify that RT overrides work
            Assert.AreEqual(testRT, newdoc.Molecules.ElementAt(0).ExplicitRetentionTime.RetentionTime, massPrecisionTolerance);
            Assert.AreEqual(testRTWindow, newdoc.Molecules.ElementAt(0).ExplicitRetentionTime.RetentionTimeWindow.Value, massPrecisionTolerance);

        }
        private static void TestEditBogusMolecule()
        {
            // Add a legit molecule, then try to modify it with garbage
            RunUI(() => SkylineWindow.NewDocument(true));
            var origDoc = SkylineWindow.Document;
            RunUI(() =>
                SkylineWindow.ModifyDocument("", mdoc =>
                {
                    return mdoc.AddPeptideGroups(new[]
                    {
                        new PeptideGroupDocNode(new PeptideGroup(), mdoc.Annotations, "Molecule Group", "",
                            new PeptideDocNode[0])
                    }, true, IdentityPath.ROOT, out _, out _);
                }));
            var doc = WaitForDocumentChange(origDoc);
            RunUI(() => SkylineWindow.SelectedPath = new IdentityPath(SkylineWindow.Document.MoleculeGroups.ElementAt(0).Id));
            var editMoleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.AddSmallMolecule);
            RunUI(() =>
            {
                editMoleculeDlg.FormulaBox.Formula = "C3H7NO2[M+H]";
            });
            OkDialog(editMoleculeDlg, editMoleculeDlg.OkDialog);
            doc = WaitForDocumentChange(doc);
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].FirstNode;
            });
            editMoleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.ModifyPeptide);
            RunUI(() =>
            {
                editMoleculeDlg.FormulaBox.Formula = "[13C]3H7[15N]O2"; // CONSIDER: we should really support this nomenclature but we don't (yet?)
            });
            var errorDlg = ShowDialog<MessageDlg>(editMoleculeDlg.OkDialog); // This shouldn't actually close the dialog since the formula is in error and highlighted red
            OkDialog(errorDlg, errorDlg.OkDialog);
            OkDialog(editMoleculeDlg, editMoleculeDlg.CancelDialog);
            RunUI(() => SkylineWindow.NewDocument(true));
        }

        private static void TestEditingSmallMoleculeAsMasses()
        {
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].FirstNode;
            });
            var editMoleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.ModifyPeptide);
            double massPrecisionTolerance = Math.Pow(10, -SequenceMassCalc.MassPrecision);
            var nameText = "testname";
            RunUI(() =>
            {
                Assert.AreEqual(COOO13H, editMoleculeDlg.NameText);
                editMoleculeDlg.NameText = nameText;  // Simulate user changing the name
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(COOO13H), editMoleculeDlg.FormulaBox.MonoMass ?? -1, massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateMassFromFormula(COOO13H), editMoleculeDlg.FormulaBox.AverageMass ?? -1, massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(COOO13H), double.Parse(editMoleculeDlg.FormulaBox.MonoText), massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateMassFromFormula(COOO13H), double.Parse(editMoleculeDlg.FormulaBox.AverageText), massPrecisionTolerance);
                // Now try something crazy
                editMoleculeDlg.FormulaBox.MonoMass = CustomMolecule.MAX_MASS + 1000;
            });
            for (int loop = 0; loop < 2; loop++)
            {
                // Trying to exit the dialog should cause a warning about mass
                RunDlg<MessageDlg>(editMoleculeDlg.OkDialog, dlg =>
                {
                    AssertEx.AreComparableStrings(
                        string.Format(Resources.EditCustomMoleculeDlg_OkDialog_Custom_molecules_must_have_a_mass_less_than_or_equal_to__0__, CustomMolecule.MAX_MASS),
                        dlg.Message);
                    dlg.OkDialog(); // Dismiss the warning
                });
                RunUI(() =>
                {
                    editMoleculeDlg.FormulaBox.MonoMass = BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(COOO13H);
                    editMoleculeDlg.FormulaBox.AverageMass = CustomMolecule.MAX_MASS + 1000; 
                });
            }
            RunUI(() =>
            {
                editMoleculeDlg.FormulaBox.AverageMass = BioMassCalc.AVERAGE.CalculateMassFromFormula(COOO13H);
            });

            OkDialog(editMoleculeDlg, editMoleculeDlg.OkDialog);

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
                editMoleculeDlgA.PrecursorCollisionEnergy = TESTVALUES_GROUP.CollisionEnergy.Value;
                // Test the "set" part of "Issue 371: Small molecules: need to be able to import and/or set CE, RT and DT for individual precursors and products"
                editMoleculeDlgA.IonMobility = TESTVALUES_GROUP.IonMobility.Value;
                editMoleculeDlgA.IonMobilityUnits = eIonMobilityUnits.none; // Simulate user forgets to declare units
                editMoleculeDlgA.CollisionalCrossSectionSqA = TESTVALUES_GROUP.CollisionalCrossSectionSqA.Value;
            });
            RunDlg<MessageDlg>(editMoleculeDlgA.OkDialog, errorDlg =>
            {
                Assert.IsTrue(errorDlg.Message.Contains(Resources.EditCustomMoleculeDlg_OkDialog_Please_specify_the_ion_mobility_units_));
                errorDlg.OkDialog();
                editMoleculeDlgA.IonMobilityUnits = TESTVALUES_GROUP.IonMobilityUnits;
            });
            OkDialog(editMoleculeDlgA, editMoleculeDlgA.OkDialog);
            var doc = WaitForDocumentChange(docA);

            // Negative drift times not allowed
            editMoleculeDlgA = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.ModifySmallMoleculeTransitionGroup);
            RunUI(() =>
            {
                editMoleculeDlgA.IonMobility = -TESTVALUES_GROUP.IonMobility.Value;
            });
            RunDlg<MessageDlg>(editMoleculeDlgA.OkDialog, errorDlg =>
            {
                Assert.IsTrue(errorDlg.Message.Contains(
                    string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_ion_mobility_value__0_,
                        -TESTVALUES_GROUP.IonMobility.Value)));
                errorDlg.OkDialog();
            });
            RunUI(() => editMoleculeDlgA.IonMobilityUnits = eIonMobilityUnits.compensation_V); // But negative CoV is allowed
            OkDialog(editMoleculeDlgA, editMoleculeDlgA.OkDialog);
            var docB = WaitForDocumentChange(doc);
            // Undo that last change
            RunUI(() => SkylineWindow.Undo());
            doc = WaitForDocumentChange(docB);

            var peptideDocNode = doc.Molecules.ElementAt(0);
            Assert.IsNotNull(peptideDocNode);
            Assert.IsTrue(peptideDocNode.EqualsId(docA.Molecules.ElementAt(0))); // No Id change
            Assert.IsTrue(doc.MoleculeTransitionGroups.ElementAt(0).EqualsId(docA.MoleculeTransitionGroups.ElementAt(0)));  // No change to Id node or its child Ids
            Assert.AreEqual(TESTVALUES_GROUP, doc.MoleculeTransitionGroups.ElementAt(0).ExplicitValues);
            Assert.IsNull(peptideDocNode.ExplicitRetentionTime);
            var editMoleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.ModifySmallMoleculeTransitionGroup);
            double massPrecisionTolerance = Math.Pow(10, -SequenceMassCalc.MassPrecision);
            double massAverage = 0;
            double massMono = 0;
            RunUI(() =>
            {
                Assert.AreEqual(COOO13H, editMoleculeDlg.NameText); // Comes from the docnode
                Assert.AreEqual(COOO13H, editMoleculeDlg.FormulaBox.NeutralFormula); // Comes from the docnode
                Assert.AreEqual(COOO13H+"[M+H]", editMoleculeDlg.FormulaBox.Formula); // Comes from the docnode
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(COOO13H), editMoleculeDlg.FormulaBox.MonoMass ?? -1, massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateMassFromFormula(COOO13H), editMoleculeDlg.FormulaBox.AverageMass ?? -1, massPrecisionTolerance);
                // Now try something crazy
                editMoleculeDlg.FormulaBox.Formula = "[M+500H]";
            });
            // Trying to exit the dialog should cause a warning about charge state
            RunDlg<MessageDlg>(editMoleculeDlg.OkDialog, dlg =>
            {
                AssertEx.AreComparableStrings(
                    string.Format(Resources.MessageBoxHelper_ValidateSignedNumberTextBox_Value__0__must_be_between__1__and__2__or__3__and__4__, 
                        500,
                        -TransitionGroup.MAX_PRECURSOR_CHARGE,
                        -TransitionGroup.MIN_PRECURSOR_CHARGE,
                        TransitionGroup.MIN_PRECURSOR_CHARGE,
                        TransitionGroup.MAX_PRECURSOR_CHARGE),
                    dlg.Message);
                dlg.OkDialog(); // Dismiss the warning
            });
            RunUI(() =>
            {
                editMoleculeDlg.FormulaBox.Formula = "[M+H]";
            });

            // Test charge state limits
            RunUI(() => editMoleculeDlg.Adduct = Adduct.FromChargeProtonated(TransitionGroup.MAX_PRECURSOR_CHARGE + 1));
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
            RunUI(() => editMoleculeDlg.Adduct = Adduct.FromChargeProtonated(-(TransitionGroup.MAX_PRECURSOR_CHARGE + 1)));
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
                Assert.AreEqual(2.115335, double.Parse(editMoleculeDlg.FormulaBox.MonoText), .0001);
                Assert.AreEqual(2.116302, double.Parse(editMoleculeDlg.FormulaBox.AverageText), .0001);

                editMoleculeDlg.Adduct = Adduct.NonProteomicProtonatedFromCharge(2); // Back to sanity

                massAverage = editMoleculeDlg.FormulaBox.AverageMass.Value;
                massMono = editMoleculeDlg.FormulaBox.MonoMass.Value;
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateMassFromFormula(COOO13H), massAverage, massPrecisionTolerance); 
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(COOO13H), massMono, massPrecisionTolerance);
                Assert.AreEqual(.5*massMono + AminoAcidFormulas.ProtonMass, double.Parse(editMoleculeDlg.FormulaBox.MonoText), .001);
                Assert.AreEqual(.5*massAverage + AminoAcidFormulas.ProtonMass, double.Parse(editMoleculeDlg.FormulaBox.AverageText), .001);
            });
            OkDialog(editMoleculeDlg, editMoleculeDlg.OkDialog);
            var newdoc = WaitForDocumentChange(doc);
            Assert.AreEqual(massAverage, newdoc.MoleculeTransitionGroups.ElementAt(0).CustomMolecule.AverageMass, massPrecisionTolerance);
            Assert.AreEqual(massMono, newdoc.MoleculeTransitionGroups.ElementAt(0).CustomMolecule.MonoisotopicMass, massPrecisionTolerance);
            Assert.IsNotNull(newdoc.Molecules.ElementAt(0).CustomMolecule.Formula); // Molecule and children share base molecule 
            Assert.AreEqual(.5 * massMono + BioMassCalc.MassProton, newdoc.MoleculeTransitionGroups.ElementAt(0).PrecursorMz, .001);

            Assert.IsFalse(newdoc.MoleculeTransitionGroups.ElementAt(0).EqualsId(peptideDocNode));  // Changing the adduct changes the Id node
            // Verify that CE overrides work
            Assert.AreEqual(TESTVALUES_GROUP, newdoc.MoleculeTransitionGroups.ElementAt(0).ExplicitValues);
            Assert.IsNull(newdoc.Molecules.ElementAt(0).ExplicitRetentionTime);  // Not set yet

            // Verify that tree selection doesn't change just because we changed an ID object
            // (formerly the tree node would collapse and focus would jump up a level)
            RunUI(() =>
            {
                Assert.AreEqual(SkylineWindow.SequenceTree.SelectedNode, SkylineWindow.SequenceTree.Nodes[0].FirstNode);
            });
        }

        private void TestEditingTransition()
        {
            double massPrecisionTolerance = Math.Pow(10, -SequenceMassCalc.MassPrecision);
            double displayPrecisionTolerance = 1.0E-3;
            var doc = SkylineWindow.Document;

            // Use transition filter settings to set ion mobility filter width calculator
            var transitionSettingsDlg = ShowTransitionSettings(TransitionSettingsUI.TABS.IonMobility);
            RunUI(() =>
            {
                transitionSettingsDlg.IonMobilityControl.WindowWidthType =
                    IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power;
                transitionSettingsDlg.IonMobilityControl.IonMobilityFilterResolvingPower = 30;
            });
            OkDialog(transitionSettingsDlg, transitionSettingsDlg.OkDialog);
            doc = WaitForDocumentChange(doc);
            AssertEx.AreEqual(30, doc.Settings.TransitionSettings.IonMobilityFiltering.FilterWindowWidthCalculator.ResolvingPower);

            RunUI(() =>
            {
                SkylineWindow.ExpandPrecursors();
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].FirstNode.FirstNode.FirstNode;
            });
            var editMoleculeDlg =
                ShowDialog<EditCustomMoleculeDlg>(
                    () => SkylineWindow.ModifyTransition((TransitionTreeNode) SkylineWindow.SequenceTree.SelectedNode));
            var monoMass = new TypedMass(805, MassType.Monoisotopic);
            RunUI(() =>
            {
                // Check neutral mass calculation
                Assert.AreEqual(C12H12 + ADDUCT_HEAVY_M_PLUS_H.AdductFormula, editMoleculeDlg.FormulaBox.Formula);
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H12),
                    editMoleculeDlg.FormulaBox.MonoMass ?? -1, massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateMassFromFormula(C12H12),
                    editMoleculeDlg.FormulaBox.AverageMass ?? -1, massPrecisionTolerance);

                // Check m/z calculation
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H12_HEAVY_PLUS_H),
                    double.Parse(editMoleculeDlg.FormulaBox.MonoText), displayPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateMassFromFormula(C12H12_HEAVY_PLUS_H),
                    double.Parse(editMoleculeDlg.FormulaBox.AverageText), displayPrecisionTolerance);

                Assert.AreEqual(ADDUCT_HEAVY_M_PLUS_H.AdductFormula, editMoleculeDlg.FormulaBox.Adduct.AdductFormula);
                editMoleculeDlg.FormulaBox.Formula = editMoleculeDlg.FormulaBox.Adduct.AdductFormula; // Remove neutral formula, should leave masses unchanged
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H12_HEAVY),
                    editMoleculeDlg.FormulaBox.MonoMass ?? -1, massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateMassFromFormula(C12H12_HEAVY),
                    editMoleculeDlg.FormulaBox.AverageMass ?? -1, massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H12_HEAVY_PLUS_H),
                    double.Parse(editMoleculeDlg.FormulaBox.MonoText), displayPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateMassFromFormula(C12H12_HEAVY_PLUS_H),
                    double.Parse(editMoleculeDlg.FormulaBox.AverageText), displayPrecisionTolerance);
                editMoleculeDlg.FormulaBox.AverageMass = 800;
                editMoleculeDlg.FormulaBox.MonoMass = monoMass.Value;
                editMoleculeDlg.NameText = "Fragment";

                // Transition level explicit values
                editMoleculeDlg.IonMobilityHighEnergyOffset = TESTVALUES_TRAN.IonMobilityHighEnergyOffset.Value;
                editMoleculeDlg.CollisionEnergy = TESTVALUES_TRAN.CollisionEnergy.Value;
                editMoleculeDlg.SLens = TESTVALUES_TRAN.SLens.Value;
                editMoleculeDlg.ConeVoltage = TESTVALUES_TRAN.ConeVoltage;
                editMoleculeDlg.DeclusteringPotential = TESTVALUES_TRAN.DeclusteringPotential;
            });
            OkDialog(editMoleculeDlg,editMoleculeDlg.OkDialog);
            var newdoc = WaitForDocumentChange(doc);
            Assert.AreEqual("Fragment", newdoc.MoleculeTransitions.ElementAt(0).Transition.CustomIon.ToString());
            Assert.AreEqual(BioMassCalc.CalculateIonMz(monoMass, editMoleculeDlg.Adduct), newdoc.MoleculeTransitions.ElementAt(0).Mz, massPrecisionTolerance);
            Assert.IsFalse(ReferenceEquals(doc.MoleculeTransitions.ElementAt(0).Id, newdoc.MoleculeTransitions.ElementAt(0).Id)); // Changing the mass changes the Id

            // Verify that the explicitly set drift time overrides any calculations
            double driftTimeMax = 1000.0;
            var centerDriftTime = newdoc.Settings.GetIonMobilityFilter(
                newdoc.Molecules.First(), newdoc.MoleculeTransitionGroups.First(), newdoc.MoleculeTransitions.First(), 
                null, null, driftTimeMax);
            Assert.AreEqual(TESTVALUES_GROUP.IonMobility.Value, centerDriftTime.IonMobilityAndCCS.IonMobility.Mobility.Value, .0001);
            Assert.AreEqual(TESTVALUES_TRAN.IonMobilityHighEnergyOffset.Value, centerDriftTime.HighEnergyIonMobilityOffset ?? 0, .0001);
            Assert.AreEqual(0.156, centerDriftTime.IonMobilityExtractionWindowWidth??0, .0001);

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
            Assert.AreNotEqual(BioMassCalc.CalculateIonMz(monoMass, editMoleculeDlg.Adduct), newdoc.MoleculeTransitions.ElementAt(0).Mz, massPrecisionTolerance);
            Assert.IsTrue(ReferenceEquals(doc.MoleculeTransitions.ElementAt(0).Id, newdoc.MoleculeTransitions.ElementAt(0).Id));
            RunUI(() => SkylineWindow.Redo());
            newdoc = WaitForDocumentChange(newdoc);
            Assert.AreEqual("Fragment", newdoc.MoleculeTransitions.ElementAt(0).Transition.CustomIon.ToString());
            Assert.AreEqual(BioMassCalc.CalculateIonMz(monoMass, editMoleculeDlg.Adduct), newdoc.MoleculeTransitions.ElementAt(0).Mz, massPrecisionTolerance);

            // Verify that tree selection doesn't change just because we changed an ID object
            // (formerly the tree node would collapse and focus would jump up a level)
            RunUI(() =>
            {
                Assert.AreEqual(SkylineWindow.SequenceTree.SelectedNode, SkylineWindow.SequenceTree.Nodes[0].FirstNode.FirstNode.FirstNode);
            });

        }
        // Verify the fix for the problem where a molecule rename would doubly apply label masses due to needlessly complicated mass-from-mz calculation
        private static void TestEditingMoleculeName()
        {
            var doc = SkylineWindow.Document;
            var editMoleculeDlg =
                ShowDialog<EditCustomMoleculeDlg>(
                    () => SkylineWindow.ModifyTransition((TransitionTreeNode)SkylineWindow.SequenceTree.SelectedNode));
            RunUI(() => { editMoleculeDlg.FormulaBox.Formula = C12H12 + ADDUCT_HEAVY_M_PLUS_H.AdductFormula; });
            OkDialog(editMoleculeDlg, editMoleculeDlg.OkDialog);
            var newdoc = WaitForDocumentChange(doc);
            RunUI(() => { SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].FirstNode; });
            editMoleculeDlg = ShowDialog<EditCustomMoleculeDlg>(() => SkylineWindow.ModifyPeptide());
            RunUI(() => { editMoleculeDlg.NameText = "different_name";});
            OkDialog(editMoleculeDlg, editMoleculeDlg.OkDialog);
            newdoc = WaitForDocumentChange(doc);
            Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H12_HEAVY_PLUS_H),
                newdoc.MoleculeTransitions.First().Mz, 1.0E-3);
        }

        private static void TestEditingTransitionAsMasses()
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
                    () => SkylineWindow.ModifyTransition((TransitionTreeNode)SkylineWindow.SequenceTree.SelectedNode));
            var monoMass = new TypedMass(805, MassType.Monoisotopic);
            RunUI(() =>
            {
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H12),
                    editMoleculeDlg.FormulaBox.MonoMass ?? -1, massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateMassFromFormula(C12H12),
                    editMoleculeDlg.FormulaBox.AverageMass ?? -1, massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.CalculateIonMz(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H12), editMoleculeDlg.Adduct), double.Parse(editMoleculeDlg.FormulaBox.MonoText), massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.CalculateIonMz(BioMassCalc.AVERAGE.CalculateMassFromFormula(C12H12), editMoleculeDlg.Adduct), double.Parse(editMoleculeDlg.FormulaBox.AverageText), massPrecisionTolerance);

                Assert.AreEqual(Adduct.M_PLUS.AdductFormula, editMoleculeDlg.FormulaBox.Adduct.AdductFormula);
                editMoleculeDlg.FormulaBox.AverageMass = 800;
                editMoleculeDlg.FormulaBox.MonoMass = monoMass.Value;
                editMoleculeDlg.NameText = "Fragment";
            });
            OkDialog(editMoleculeDlg, editMoleculeDlg.OkDialog);
            var newdoc = WaitForDocumentChange(doc);
            Assert.AreEqual("Fragment", newdoc.MoleculeTransitions.ElementAt(0).Transition.CustomIon.ToString());
            Assert.AreEqual(BioMassCalc.CalculateIonMz(monoMass, editMoleculeDlg.Adduct), newdoc.MoleculeTransitions.ElementAt(0).Mz, massPrecisionTolerance);
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
            Assert.AreNotEqual(BioMassCalc.CalculateIonMz(monoMass, editMoleculeDlg.Adduct), newdoc.MoleculeTransitions.ElementAt(0).Mz, massPrecisionTolerance);
            Assert.IsTrue(ReferenceEquals(doc.MoleculeTransitions.ElementAt(0).Id, newdoc.MoleculeTransitions.ElementAt(0).Id));
            RunUI(() => SkylineWindow.Redo());
            newdoc = WaitForDocumentChange(newdoc);
            Assert.AreEqual("Fragment", newdoc.MoleculeTransitions.ElementAt(0).Transition.CustomIon.ToString());
            Assert.AreEqual(BioMassCalc.CalculateIonMz(monoMass, editMoleculeDlg.Adduct), newdoc.MoleculeTransitions.ElementAt(0).Mz, massPrecisionTolerance);

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
                moleculeDlg.Adduct = ADDUCT_HEAVY_M_PLUS_H;
            });
            OkDialog(moleculeDlg, moleculeDlg.OkDialog);
            var newDoc = SkylineWindow.Document;
            var compareIon = new CustomIon(C12H12, ADDUCT_HEAVY_M_PLUS_H, null, null, testNametextA);
            Assert.AreEqual(compareIon,newDoc.MoleculeTransitions.ElementAt(0).Transition.CustomIon);
            Assert.AreEqual(1,newDoc.MoleculeTransitions.ElementAt(0).Transition.Charge);

            // Verify that tree selection doesn't change just because we changed an ID object
            // (formerly the tree node would collapse and focus would jump up a level)
            RunUI(() =>
            {
                Assert.AreEqual(SkylineWindow.SequenceTree.SelectedNode, SkylineWindow.SequenceTree.Nodes[0].FirstNode.FirstNode);
            });
        }
        private static void TestAddingTransitionAsMasses()
        {
            RunUI(() =>
            {
                SkylineWindow.ExpandPrecursors();
                var node = SkylineWindow.SequenceTree.Nodes[0].FirstNode.FirstNode;
                SkylineWindow.SequenceTree.SelectedNode = node;
            });
            var editMoleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.AddSmallMolecule);
            var formula = C12H12;
            var monoMass = BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(formula);
            var averageMass = BioMassCalc.AVERAGE.CalculateMassFromFormula(formula);
            var adduct = Adduct.M_PLUS;
            RunUI(() =>
            {
                // Verify the interaction of explicitly set mz and charge without formula
                editMoleculeDlg.NameText = testNametextA;
                editMoleculeDlg.Adduct = adduct;
                var mzMono = editMoleculeDlg.Adduct.MzFromNeutralMass(monoMass);
                var mzAverage = editMoleculeDlg.Adduct.MzFromNeutralMass(averageMass);
                editMoleculeDlg.FormulaBox.MonoMass = monoMass;
                editMoleculeDlg.FormulaBox.AverageMass = averageMass;
                var massPrecisionTolerance = 0.00001;
                Assert.AreEqual(mzMono, double.Parse(editMoleculeDlg.FormulaBox.MonoText), massPrecisionTolerance);
                Assert.AreEqual(mzAverage, double.Parse(editMoleculeDlg.FormulaBox.AverageText), massPrecisionTolerance);
            });
            OkDialog(editMoleculeDlg, editMoleculeDlg.OkDialog);

            var newDoc = SkylineWindow.Document;
            var compareIon = new CustomIon(null, adduct, monoMass, averageMass, testNametextA);
            Assert.AreEqual(compareIon, newDoc.MoleculeTransitions.ElementAt(0).Transition.CustomIon);
            Assert.AreEqual(1, newDoc.MoleculeTransitions.ElementAt(0).Transition.Charge);

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
            var doc = SkylineWindow.Document;
            var editMoleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.AddSmallMolecule);

            // Less extreme values should trigger a warning about instrument limits
            RunUI(() =>
            {
                editMoleculeDlg.FormulaBox.MonoMass = CustomMolecule.MAX_MASS - 100;
                editMoleculeDlg.FormulaBox.AverageMass = editMoleculeDlg.FormulaBox.MonoMass;
            });
            RunDlg<MessageDlg>(editMoleculeDlg.OkDialog, dlg =>
            {
                AssertEx.AreComparableStrings(
                    Resources
                        .SkylineWindow_AddMolecule_The_precursor_m_z_for_this_molecule_is_out_of_range_for_your_instrument_settings_,
                    dlg.Message);
                dlg.OkDialog(); // Dismiss the warning
            });


            RunUI(() =>
            {
                // Verify the interaction of explicitly set formula, mz and charge
                editMoleculeDlg.FormulaBox.Formula = C12H12;
                var mono = editMoleculeDlg.FormulaBox.MonoMass ?? -1;
                var average = editMoleculeDlg.FormulaBox.AverageMass ?? -1;
                var massPrecisionTolerance = Math.Pow(10, -SequenceMassCalc.MassPrecision);
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H12), mono, massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateMassFromFormula(C12H12), average, massPrecisionTolerance);
                Assert.AreEqual(mono, double.Parse(editMoleculeDlg.FormulaBox.MonoText), massPrecisionTolerance);
                Assert.AreEqual(average, double.Parse(editMoleculeDlg.FormulaBox.AverageText), massPrecisionTolerance);
                editMoleculeDlg.Adduct = Adduct.NonProteomicProtonatedFromCharge(3);
                Assert.AreEqual(
                    Math.Round(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H12), SequenceMassCalc.MassPrecision),
                    mono); // Masses should not change
                Assert.AreEqual(
                    Math.Round(BioMassCalc.AVERAGE.CalculateMassFromFormula(C12H12), SequenceMassCalc.MassPrecision),
                    average);
                editMoleculeDlg.Adduct = Adduct.FromChargeProtonated(1);
                Assert.AreEqual(
                    Math.Round(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H12), SequenceMassCalc.MassPrecision),
                    mono); // Masses should not change
                Assert.AreEqual(
                    Math.Round(BioMassCalc.AVERAGE.CalculateMassFromFormula(C12H12), SequenceMassCalc.MassPrecision),
                    average);
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateIonMz(C12H12, editMoleculeDlg.Adduct),
                    mono + BioMassCalc.MassProton,
                    massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateIonMz(C12H12, editMoleculeDlg.Adduct),
                    average + BioMassCalc.MassProton,
                    massPrecisionTolerance);
                editMoleculeDlg.Adduct = Adduct.NonProteomicProtonatedFromCharge(-1);  // Validate negative charges
                Assert.AreEqual(
                    Math.Round(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(C12H12), SequenceMassCalc.MassPrecision),
                    mono); // Masses should not change
                Assert.AreEqual(
                    Math.Round(BioMassCalc.AVERAGE.CalculateMassFromFormula(C12H12), SequenceMassCalc.MassPrecision),
                    average);
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateIonMz(C12H12, editMoleculeDlg.Adduct),
                    mono - BioMassCalc.MassProton,
                    massPrecisionTolerance);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateIonMz(C12H12, editMoleculeDlg.Adduct),
                    average - BioMassCalc.MassProton,
                    massPrecisionTolerance);

                editMoleculeDlg.FormulaBox.Formula = string.Empty; // Simulate user blanking out the formula
                Assert.AreEqual(mono, editMoleculeDlg.FormulaBox.MonoMass); // Leaves masses untouched
                Assert.AreEqual(average, editMoleculeDlg.FormulaBox.AverageMass);
                editMoleculeDlg.FormulaBox.AverageMass = averageMass100;
                editMoleculeDlg.FormulaBox.MonoMass = monoMass105;
                editMoleculeDlg.NameText = "test";
                Assert.IsTrue(string.IsNullOrEmpty(editMoleculeDlg.FormulaBox.Formula));
                Assert.AreEqual(averageMass100, editMoleculeDlg.FormulaBox.AverageMass);
                Assert.AreEqual(monoMass105, editMoleculeDlg.FormulaBox.MonoMass);
                var monoMzText = editMoleculeDlg.FormulaBox.MonoText;
                var averageMzText = editMoleculeDlg.FormulaBox.AverageText;
                var mzMonoAtMminusH = double.Parse(monoMzText);
                var mzAverageAtMminusH = double.Parse(averageMzText);
                editMoleculeDlg.Adduct = Adduct.NonProteomicProtonatedFromCharge(3);
                Assert.AreEqual(monoMzText, editMoleculeDlg.FormulaBox.MonoText); // m/z readout should not change
                Assert.AreEqual(averageMzText, editMoleculeDlg.FormulaBox.AverageText);
                // If this mz is now said to be due to higher charge, then mass must greater
                Assert.AreEqual(3 * (mzAverageAtMminusH - BioMassCalc.MassProton), 
                    editMoleculeDlg.FormulaBox.AverageMass.Value, massPrecisionTolerance);
                Assert.AreEqual(3 * (mzMonoAtMminusH - BioMassCalc.MassProton), 
                    editMoleculeDlg.FormulaBox.MonoMass.Value, massPrecisionTolerance);
                // If this mz is now said to be due to lesser z, then mass must smaller
                editMoleculeDlg.Adduct = Adduct.NonProteomicProtonatedFromCharge(1);
                Assert.AreEqual(monoMzText, editMoleculeDlg.FormulaBox.MonoText); // m/z readout should not change
                Assert.AreEqual(averageMzText, editMoleculeDlg.FormulaBox.AverageText);
                var massAverage = editMoleculeDlg.FormulaBox.AverageMass.Value;
                var massMono = editMoleculeDlg.FormulaBox.MonoMass.Value;
                Assert.AreEqual(averageMass100 - BioMassCalc.MassProton, massAverage, massPrecisionTolerance); // Mass should change back
                Assert.AreEqual(monoMass105 - BioMassCalc.MassProton, massMono, massPrecisionTolerance);
            });

            var adduct = Adduct.NonProteomicProtonatedFromCharge(1);
            RunUI(() =>
            {
                editMoleculeDlg.NameText = COOO13H;
                editMoleculeDlg.FormulaBox.Formula = COOO13H + adduct.AdductFormula;
            });
            OkDialog(editMoleculeDlg, editMoleculeDlg.OkDialog);
            var compareIon = new CustomMolecule(COOO13H, COOO13H);
            var newDoc = WaitForDocumentChange(doc);
            Assert.AreEqual(compareIon, newDoc.Molecules.ElementAt(0).CustomMolecule);
            Assert.AreEqual(compareIon, newDoc.MoleculeTransitionGroups.ElementAt(0).CustomMolecule);
            Assert.AreEqual(adduct.AdductCharge, newDoc.MoleculeTransitionGroups.ElementAt(0).PrecursorCharge);
            var predictedMz = BioMassCalc.MONOISOTOPIC.CalculateIonMz(COOO13H, adduct);
            var actualMz = newDoc.MoleculeTransitionGroups.ElementAt(0).PrecursorMz;
            Assert.AreEqual(predictedMz, actualMz, Math.Pow(10, -SequenceMassCalc.MassPrecision));
        }

        private static void TestAddingSmallMoleculeAsMasses()
        {
            RunUI(() => SkylineWindow.SelectedPath = new IdentityPath(SkylineWindow.Document.MoleculeGroups.ElementAt(0).Id));
            var doc = SkylineWindow.Document;
            var formula = COOO13H;
            var editMoleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.AddSmallMolecule);
            var monoMass =  BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(formula);
            var averageMass = BioMassCalc.AVERAGE.CalculateMassFromFormula(formula);
            var adduct = Adduct.M_PLUS_Na;
            RunUI(() =>
            {
                // Verify the interaction of explicitly set mz and charge without formula
                editMoleculeDlg.NameText = formula;
                editMoleculeDlg.Adduct = adduct;
                var mzMono = editMoleculeDlg.Adduct.MzFromNeutralMass(monoMass);
                var mzAverage = editMoleculeDlg.Adduct.MzFromNeutralMass(averageMass);
                editMoleculeDlg.FormulaBox.MonoMass = monoMass;
                editMoleculeDlg.FormulaBox.AverageMass = averageMass;
                var massPrecisionTolerance = 0.00001;
                Assert.AreEqual(mzMono, double.Parse(editMoleculeDlg.FormulaBox.MonoText), massPrecisionTolerance);
                Assert.AreEqual(mzAverage, double.Parse(editMoleculeDlg.FormulaBox.AverageText), massPrecisionTolerance);
            });
            OkDialog(editMoleculeDlg, editMoleculeDlg.OkDialog);
            var newDoc = WaitForDocumentChange(doc);
            var compareIon = new CustomMolecule(new TypedMass(monoMass, MassType.Monoisotopic), new TypedMass(averageMass, MassType.Average), formula);
            Assert.AreEqual(compareIon, newDoc.Molecules.ElementAt(0).CustomMolecule);
            Assert.AreEqual(compareIon, newDoc.MoleculeTransitionGroups.ElementAt(0).CustomMolecule);
            Assert.AreEqual(adduct.AdductCharge, newDoc.MoleculeTransitionGroups.ElementAt(0).PrecursorCharge);
            var predictedMz = BioMassCalc.MONOISOTOPIC.CalculateIonMz(formula, adduct);
            var actualMz = newDoc.MoleculeTransitionGroups.ElementAt(0).PrecursorMz;
            Assert.AreEqual(predictedMz, actualMz, Math.Pow(10, -SequenceMassCalc.MassPrecision));
        }

        private void TestAddingSmallMoleculePrecursor()
        {
            // Position ourselves on the first molecule
            var newDoc = SkylineWindow.Document;
            SelectNode(SrmDocument.Level.Molecules, 0);
            var moleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.AddSmallMolecule);
            var adduct = moleculeDlg.FormulaBox.Adduct.ChangeIsotopeLabels(new Dictionary<string, int> { { "O'", 1 } }); // Not L10N
            RunUI(() =>
            {
                moleculeDlg.IsotopeLabelType = IsotopeLabelType.light; // This should provoke a failure - can't have two of the same label and charge
            });
            RunDlg<MessageDlg>(moleculeDlg.OkDialog, dlg =>
            {
                // Trying to exit the dialog should cause a warning about adduct and label conflict
                Assert.AreEqual(Resources.EditCustomMoleculeDlg_OkDialog_A_precursor_with_that_adduct_and_label_type_already_exists_, dlg.Message);
                dlg.OkDialog(); // Dismiss the warning
            });
            RunUI(() =>
            {
                moleculeDlg.FormulaBox.Adduct = adduct;
                moleculeDlg.IsotopeLabelType = IsotopeLabelType.heavy; 
            });
            OkDialog(moleculeDlg, moleculeDlg.OkDialog);

            newDoc = WaitForDocumentChange(newDoc);

            Assert.AreEqual(adduct, newDoc.MoleculeTransitionGroups.ElementAt(1).TransitionGroup.PrecursorAdduct);
            var predictedMz = BioMassCalc.MONOISOTOPIC.CalculateIonMz(COOO13H, adduct);
            var actualMz = newDoc.MoleculeTransitionGroups.ElementAt(1).PrecursorMz;
            Assert.AreEqual(predictedMz, actualMz, Math.Pow(10, -SequenceMassCalc.MassPrecision));

            // Now verify that we are OK with different charge same label
            SelectNode(SrmDocument.Level.Molecules, 0);
            var moleculeDlg2 = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.AddSmallMolecule);
            RunUI(() =>
            {
                moleculeDlg2.Adduct = Adduct.FromChargeProtonated(adduct.AdductCharge + 1);
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
                editMoleculeDlgB.Adduct = Adduct.NonProteomicProtonatedFromCharge(5); 
            });
            OkDialog(editMoleculeDlgB, editMoleculeDlgB.OkDialog);
            newDoc = WaitForDocumentChange(newDoc);
            CheckTransitionGroupSortOrder(newDoc);
            RunUI(SkylineWindow.Undo);
            newDoc = WaitForDocumentChange(newDoc);
            CheckTransitionGroupSortOrder(newDoc);

        }

        // Test handling of changes to molecule that make no sense with its child ions
        // e.g. removing atoms that the child ions also want to remove
        private void TestMoleculeEditError()
        {
            // Position ourselves on the first precursor
            var newDoc = SkylineWindow.Document;
            SelectNode(SrmDocument.Level.TransitionGroups, 0);
            var moleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.ModifySmallMoleculeTransitionGroup);
            var adduct = moleculeDlg.FormulaBox.Adduct.ChangeIonFormula("-C+2H");
            RunUI(() => moleculeDlg.Adduct = adduct);
            OkDialog(moleculeDlg, moleculeDlg.OkDialog);

            // The first precursor now has no Carbon - if we try to remove Carbon from parent molecule too that should error out
            SelectNode(SrmDocument.Level.Molecules, 0);
            moleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.ModifyPeptide);
            RunUI(() =>
            {
                moleculeDlg.FormulaBox.Formula = "HO15"; 
            });
            // First precursor's adduct now describes removing a Carbon that doesn't exist
            RunDlg<MessageDlg>(moleculeDlg.OkDialog, dlg =>
            {
                // Trying to exit the dialog should cause a warning about adduct and formula conflict
                var expected =
                    string.Format(Resources.Adduct_ApplyToMolecule_Adduct___0___calls_for_removing_more__1__atoms_than_are_found_in_the_molecule__2_,
                        "[M-C+2H]", "C", "HO15");
                Assert.AreEqual(expected, dlg.Message);
                dlg.OkDialog(); // Dismiss the warning
            });
            OkDialog(moleculeDlg, moleculeDlg.CancelDialog); // Abandon silly change to molecule
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

        /// <summary>
        /// Test the fix for editing a transition with no formula - we were passing in the parent molecule
        /// instead of the transition molecule, a hangover from the pre-adduct days
        /// </summary>
        private void TestEditTransitionNoFormula()
        {
            // Clear out the document
            RunUI(() =>
            {
                SkylineWindow.NewDocument(true);
            });
            var docCurrent = SkylineWindow.Document;
            var transitionList =
                "Molecule List Name,Precursor Name,Precursor Formula,Precursor Adduct,Precursor m/z,Precursor Charge,Product Name,Product Formula,Product Adduct,Product m/z,Product Charge,Note\n"+
                "Cer,Cer 12:0;2/12:0,C24H49NO3,[M-H]1-,398.3639681499,-1,F,C12H22O,[M-H]1-,181.1597889449,-1,\n"+
                "Cer,Cer 12:0;2/12:0,C24H49NO3,[M-H]1-,398.3639681499,-1,V',,[M-H]1-,186.1863380499,-1,";
            SetClipboardText(transitionList);
            PasteSmallMoleculeListNoAutoManage(); // Paste the clipboard text, dismiss the offer to enable automanage
            VerifyFragmentTransitionMz(186.18633804, 186.18633804, 1);
        }

        private void VerifyFragmentTransitionMz(double mzMono, double mzAverage, int index)
        {
            // Position ourselves on the nth transition
            SelectNode(SrmDocument.Level.Transitions, index);
            var editMoleculeDlg = ShowDialog<EditCustomMoleculeDlg>(
                () => SkylineWindow.ModifyTransition((TransitionTreeNode)SkylineWindow.SequenceTree.SelectedNode));
            RunUI(() =>
            {
                var massPrecisionTolerance = 0.00001;
                Assert.AreEqual(mzMono, double.Parse(editMoleculeDlg.FormulaBox.MonoText), massPrecisionTolerance);
                Assert.AreEqual(mzAverage, double.Parse(editMoleculeDlg.FormulaBox.AverageText), massPrecisionTolerance);
            });
            OkDialog(editMoleculeDlg, editMoleculeDlg.OkDialog);
        }

        private void VerifyPrecursorTransitionMz(double mz, int index)
        {
            // Position ourselves on the nth transition
            SelectNode(SrmDocument.Level.Transitions, index);
            RunUI(() =>
            {
                var node = (TransitionTreeNode)SkylineWindow.SequenceTree.SelectedNode;
                var massPrecisionTolerance = 0.00001;
                Assert.AreEqual(mz, node.DocNode.Mz, massPrecisionTolerance);
            });
        }

        /// <summary>
        /// Test the fix for updating isotope distributions a auto-managed precursors
        /// </summary>
        private void TestEditWithIsotopeDistribution()
        {
            // Clear out the document
            RunUI(() =>
            {
                SkylineWindow.NewDocument(true);
            });
            var docCurrent = SkylineWindow.Document;
            var transitionList =
                "Molecule List Name,Precursor Name,Precursor Formula,Precursor Adduct,Precursor m/z,Precursor Charge,Product Name,Product Formula,Product Adduct,Product m/z,Product Charge,Note\n" +
                "Cer,Cer 12:0;2/12:0,C24H49NO3,[M-H]1-,398.3639681499,-1,F,C12H22O,[M-H]1-,181.1597889449,-1,\n" +
                "Cer,Cer 12:0;2/12:0,C24H49NO3,[M-H]1-,398.3639681499,-1,V',,[M-H]1-,186.1863380499,-1,\n" +
                "Cer,Cer 12:0;2/12:0,C24H49NO3,[M2C13-H]1-,,-1,F,C12H22O,[M-H]1-,181.1597889449,-1,\n" +
                "Cer,Cer 12:0;2/12:0,C24H49NO3,[M2C13-H]1-,,-1,V',,[M-H]1-,186.1863380499,-1,";
            var doc =    PasteSmallMoleculeList(transitionList); // Paste the text
            AssertEx.IsDocumentState(doc, null, 1, 1, 2, 4);

            // Now turn on auto manage children, so settings change has an effect on doc structure
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
            {
                refineDlg.AutoPrecursors = true;
                refineDlg.AutoTransitions = true;
                refineDlg.OkDialog();
            });
            doc = WaitForDocumentChange(doc);
            AssertEx.IsDocumentState(doc, null, 1, 1, 2, 4); // No changes yet, though

            // Use transition filter settings to add isotope distribution
            var fullScanDlg = ShowTransitionSettings(TransitionSettingsUI.TABS.Filter);
            // Switch isolation scheme.
            RunUI(() =>
            {
                fullScanDlg.SmallMoleculePrecursorAdducts = "[M+H],[M-H]";
                // Intentionally omit "f,p" to check that it gets added for us due to full scan being enabled
                fullScanDlg.SmallMoleculeFragmentTypes = "";
                fullScanDlg.SetAutoSelect = true;
                fullScanDlg.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                fullScanDlg.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                fullScanDlg.Peaks = 3;
                fullScanDlg.AcquisitionMethod = FullScanAcquisitionMethod.PRM;
            });
            OkDialog(fullScanDlg, fullScanDlg.OkDialog);
            doc = WaitForDocumentChange(doc);
            Assert.IsTrue(SkylineWindow.Document.Settings.TransitionSettings.Filter.SmallMoleculeIonTypes.Contains(IonType.custom));
            Assert.IsTrue(SkylineWindow.Document.Settings.TransitionSettings.Filter.SmallMoleculeIonTypes.Contains(IonType.precursor));
            using (new CheckDocumentState(1, 1, 2, 10))
            {
                RunUI(() => SkylineWindow.ExpandPrecursors());
                VerifyPrecursorTransitionMz(398.3639681499, 0); // M
                VerifyPrecursorTransitionMz(399.367318, 1); // M+1
                VerifyPrecursorTransitionMz(400.37031047, 2); // M+2
                VerifyFragmentTransitionMz(186.18633804, 186.18633804, 4); // fragment

                VerifyPrecursorTransitionMz(400.3706782806, 5); // M heavy
                VerifyPrecursorTransitionMz(401.3740266997, 6); // M+1 heavy
                VerifyPrecursorTransitionMz(402.376963848659, 7); // M+2 heavy
                VerifyFragmentTransitionMz(186.18633804, 186.18633804, 9); // fragment

            }

            // Position ourselves on the molecule, then edit its chemical formula
            SelectNode(SrmDocument.Level.Molecules, 0);
            var editMoleculeDlg = ShowDialog<EditCustomMoleculeDlg>(
                () => SkylineWindow.ModifyPeptide());
            RunUI(() =>
            {
                var massPrecisionTolerance = 0.00001;
                Assert.AreEqual(399.371245, double.Parse(editMoleculeDlg.FormulaBox.MonoText), massPrecisionTolerance);
                Assert.AreEqual(399.65436, double.Parse(editMoleculeDlg.FormulaBox.AverageText), massPrecisionTolerance);
                editMoleculeDlg.FormulaBox.Formula = "C24H50NO3"; // Change formula adding another Hydrogen
                Assert.AreEqual(400.37907, double.Parse(editMoleculeDlg.FormulaBox.MonoText), massPrecisionTolerance);
                Assert.AreEqual(400.6623, double.Parse(editMoleculeDlg.FormulaBox.AverageText), massPrecisionTolerance);
            });
            OkDialog(editMoleculeDlg, editMoleculeDlg.OkDialog);
            doc = WaitForDocumentChange(doc);

            // Verify that this updated all the precursor isotope mz values
            VerifyPrecursorTransitionMz(399.37179364, 0); // M
            VerifyPrecursorTransitionMz(400.375144672365, 1); // M+1
            VerifyPrecursorTransitionMz(401.378138442236, 2); // M+2
            VerifyFragmentTransitionMz(186.18633804, 186.18633804, 4); // fragment should not change

            VerifyPrecursorTransitionMz(401.3785033156, 5); // M heavy
            VerifyPrecursorTransitionMz(402.381853413823, 6); // M+1 heavy
            VerifyPrecursorTransitionMz(403.38479203566, 7); // M+2 heavy
            VerifyFragmentTransitionMz(186.18633804, 186.18633804, 4); // fragment should not change

            // Change the adduct 
            SelectNode(SrmDocument.Level.TransitionGroups, 0);
            var editTransitionGroupDlg = ShowDialog<EditCustomMoleculeDlg>(
                () => SkylineWindow.ModifySmallMoleculeTransitionGroup());
            RunUI(() =>
            {
                var massPrecisionTolerance = 0.00001;
                Assert.AreEqual(399.37179364, double.Parse(editTransitionGroupDlg.FormulaBox.MonoText), massPrecisionTolerance);
                Assert.AreEqual(399.655024, double.Parse(editTransitionGroupDlg.FormulaBox.AverageText), massPrecisionTolerance);
                editTransitionGroupDlg.Adduct = Adduct.M_MINUS_2H;
                Assert.AreEqual(199.182259, double.Parse(editTransitionGroupDlg.FormulaBox.MonoText), massPrecisionTolerance);
                Assert.AreEqual(199.323874, double.Parse(editTransitionGroupDlg.FormulaBox.AverageText), massPrecisionTolerance);

            });
            OkDialog(editTransitionGroupDlg, editTransitionGroupDlg.OkDialog);
            doc = WaitForDocumentChange(doc);

            // Verify that this updated all the precursor isotope mz values
            VerifyPrecursorTransitionMz(199.182259, 5); // M
            VerifyPrecursorTransitionMz(199.683934336183, 6); // M+1
            VerifyPrecursorTransitionMz(200.185431221118, 7); // M+2
            VerifyFragmentTransitionMz(186.18633804, 186.18633804, 9); // fragment should not change

            // But the heavy adduct wasn't changed, make sure no change to mz
            VerifyPrecursorTransitionMz(401.3785033156, 0); // M heavy
            VerifyPrecursorTransitionMz(402.381853413823, 1); // M+1 heavy
            VerifyPrecursorTransitionMz(403.38479203566, 2); // M+2 heavy
            VerifyFragmentTransitionMz(186.18633804, 186.18633804, 4); // fragment should not change

        }


        /// <summary>
        /// Test the fix for updating non-auto-managed precursor transitions
        /// </summary>
        private void TestEditMassWithPrecursorTransitions()
        {
            // Clear out the document
            RunUI(() => { SkylineWindow.NewDocument(true); });
            var docCurrent = SkylineWindow.Document;
            var transitionList =
                "Molecule List Name,Precursor Name,Precursor Formula,Precursor Adduct,Precursor m/z,Precursor Charge,Product Name,Product Formula,Product Adduct,Product m/z,Product Charge,Note\n" +
                "Cer,Cer 12:0;2/12:0,,[M-H]1-,398.3639681499,,,,,,\n" + // Precursor transition
                "Cer,Cer 12:0;2/12:0,,[M-H]1-,398.3639681499,-1,F,C12H22O,[M-H]1-,181.1597889449,-1,\n" +
                "Cer,Cer 12:0;2/12:0,,[M-H]1-,398.3639681499,-1,V',,[M-H]1-,186.1863380499,-1,\n" +
                "Cer,Cer 12:0;2/12:0,,[M2C13-H]1-,400.370678,,,,,,\n" + // Precursor transition
                "Cer,Cer 12:0;2/12:0,,[M2C13-H]1-,400.370678,-1,F,C12H22O,[M-H]1-,181.1597889449,-1,\n" +
                "Cer,Cer 12:0;2/12:0,,[M2C13-H]1-,400.370678,-1,V',,[M-H]1-,186.1863380499,-1,";
            var doc = PasteSmallMoleculeListNoAutoManage(transitionList); // Paste the text
            AssertEx.IsDocumentState(doc, null, 1, 1, 2, 6);

            // Position ourselves on the molecule, then edit its mass
            SelectNode(SrmDocument.Level.Molecules, 0);
            var editMoleculeDlg = ShowDialog<EditCustomMoleculeDlg>(
                () => SkylineWindow.ModifyPeptide());
            RunUI(() =>
            {
                var massPrecisionTolerance = 0.0001;
                Assert.AreEqual(399.371245, double.Parse(editMoleculeDlg.FormulaBox.MonoText), massPrecisionTolerance);
                Assert.AreEqual(399.371245, double.Parse(editMoleculeDlg.FormulaBox.AverageText),
                    massPrecisionTolerance);
                editMoleculeDlg.FormulaBox.MonoMass = 499.371245; // Change mass
                editMoleculeDlg.FormulaBox.AverageMass = 499.65436; // Change mass
                Assert.AreEqual(499.371245, double.Parse(editMoleculeDlg.FormulaBox.MonoText), massPrecisionTolerance);
                Assert.AreEqual(499.65436, double.Parse(editMoleculeDlg.FormulaBox.AverageText), massPrecisionTolerance);
            });
            OkDialog(editMoleculeDlg, editMoleculeDlg.OkDialog);
            doc = WaitForDocumentChange(doc);
            
            // Verify that this updated all the precursor mz values
            VerifyPrecursorTransitionMz(498.363969, 0); // M
            VerifyFragmentTransitionMz(186.18633804, 186.18633804, 2); // fragment should not change

            VerifyPrecursorTransitionMz(500.370679, 3); // M heavy
            VerifyFragmentTransitionMz(186.18633804, 186.18633804, 5); // fragment should not change

        }
    }
}
