/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for TransitionSettings07Test
    /// </summary>
    [TestClass]
    public class TransitionSettings07Test : AbstractUnitTest
    {
        /// <summary>
        /// Test special fragment support with MeasuredIons in TransitionFilter
        /// </summary>
        [TestMethod]
        public void SpecialFragmentTest()
        {
            var docOriginal = new SrmDocument(SrmSettingsList.GetDefault());
            IdentityPath path;
            var docPeptide = docOriginal.ImportFasta(new StringReader(">peptide1\nPEPMCIDEPR"),
                true, IdentityPath.ROOT, out path);
            // One of the prolines should have caused an extra transition
            Assert.AreEqual(4, docPeptide.PeptideTransitionCount);
            Assert.IsTrue(docPeptide.PeptideTransitions.Contains(nodeTran => nodeTran.Transition.Ordinal == 8)); // y8
            
            // Switch to the legacy version of the proline fragment where min length is 1
            var docLegacyProline = docPeptide.ChangeSettings(docPeptide.Settings.ChangeTransitionFilter(filter =>
                filter.ChangeMeasuredIons(new[] { MeasuredIonList.NTERM_PROLINE_LEGACY })));
            Assert.AreEqual(5, docLegacyProline.PeptideTransitionCount);
            // Allow b-ions
            var docLegacyProlineB = docLegacyProline.ChangeSettings(docLegacyProline.Settings.ChangeTransitionFilter(filter =>
                filter.ChangeIonTypes(new[] { IonType.b, IonType.y })));
            Assert.AreEqual(9, docLegacyProlineB.PeptideTransitionCount);

            // Add C-terminal Glu and Asp to the original peptide document
            var docGluAsp = docPeptide.ChangeSettings(docPeptide.Settings.ChangeTransitionFilter(filter =>
                filter.ChangeMeasuredIons(new List<MeasuredIon>(filter.MeasuredIons) { MeasuredIonList.CTERM_GLU_ASP })));
            Assert.AreEqual(5, docGluAsp.PeptideTransitionCount);
            Assert.IsTrue(docGluAsp.PeptideTransitions.Contains(nodeTran => nodeTran.Transition.Ordinal == 3)); // y3

            // Use the legacy version instead
            var docGluAspLegacy = docPeptide.ChangeSettings(docPeptide.Settings.ChangeTransitionFilter(filter =>
                filter.ChangeMeasuredIons(new List<MeasuredIon>(filter.MeasuredIons) { MeasuredIonList.CTERM_GLU_ASP_LEGACY })));
            Assert.AreEqual(6, docGluAspLegacy.PeptideTransitionCount);
            Assert.IsTrue(docGluAspLegacy.PeptideTransitions.Contains(nodeTran => nodeTran.Transition.Ordinal == 2)); // y2

            // Add Proline to the C-terminal modification
            var docGluAspPro = docPeptide.ChangeSettings(docPeptide.Settings.ChangeTransitionFilter(filter =>
                filter.ChangeMeasuredIons(new[] { new MeasuredIon("C-term GAP", "EDP", null, SequenceTerminus.C, 1) })));
            Assert.AreEqual(8, docGluAspPro.PeptideTransitionCount);
            Assert.IsTrue(docGluAspPro.PeptideTransitions.Contains(nodeTran =>
                nodeTran.Transition.Ordinal == 2 || // y2
                nodeTran.Transition.Ordinal == 8)); // y8

            // Restrict
            var docGluAspProRestrict = docPeptide.ChangeSettings(docPeptide.Settings.ChangeTransitionFilter(filter =>
                filter.ChangeMeasuredIons(new[] { new MeasuredIon("C-term GAP", "EDP", "P", SequenceTerminus.C, 1) })));
            Assert.AreEqual(6, docGluAspProRestrict.PeptideTransitionCount);
            Assert.IsFalse(docGluAspProRestrict.PeptideTransitions.Contains(nodeTran =>
                nodeTran.Transition.Ordinal == 2 || // not y2
                nodeTran.Transition.Ordinal == 8)); // not y8
        }

        /// <summary>
        /// Test reporter ion support in MeasuredIons in TransitionFilter
        /// </summary>
        [TestMethod]
        public void ReporterIonTest()
        {
            // Test the code that updates old-style formulas
            Assert.AreEqual("C5C'H13N2", BioMassCalc.AddH("C5C'H12N2"));
            Assert.AreEqual("CO2H", BioMassCalc.AddH("CO2"));

            var docOriginal = new SrmDocument(SrmSettingsList.GetDefault().ChangeTransitionInstrument(instrument => instrument.ChangeMinMz(10)));  // H2O2 is not very heavy!
            IdentityPath path;
            SrmDocument docPeptide = docOriginal.ImportFasta(new StringReader(">peptide1\nPEPMCIDEPR"),
                true, IdentityPath.ROOT, out path);
            // One of the prolines should have caused an extra transition
            Assert.AreEqual(4, docPeptide.PeptideTransitionCount);
            Assert.IsTrue(docPeptide.PeptideTransitions.Contains(nodeTran => nodeTran.Transition.Ordinal == 8));

            const string formula = "H2O2";  // This was H2O, but that falls below mz=10 at z > 1
            const string hydrogenPeroxide = "Hydrogen Perxoide";
            var reporterIons = new[] { new MeasuredIon(hydrogenPeroxide, formula, null, null, 1), new MeasuredIon(hydrogenPeroxide, formula, null, null, 2), new MeasuredIon(hydrogenPeroxide, formula, null, null, 3), MeasuredIonList.NTERM_PROLINE };
            SrmDocument docReporterIon = docPeptide.ChangeSettings(docPeptide.Settings.ChangeTransitionFilter(filter =>
                filter.ChangeMeasuredIons(reporterIons)));
            AssertEx.IsDocumentTransitionCount(docReporterIon, 7);

            //Check With Monoisotopic
            double mass = BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(formula);
            for (int i = 0; i < 3; i ++)
            {
                TransitionDocNode tranNode = docReporterIon.MoleculeTransitions.ElementAt(i);
                Transition tran = tranNode.Transition;
                Assert.AreEqual(reporterIons[i].CustomIon, tran.CustomIon);
                Assert.AreEqual(tran.Charge, i + 1);
                Assert.AreEqual(BioMassCalc.MONOISOTOPIC.CalculateIonMz(formula, i + 1), tranNode.Mz, BioMassCalc.MassElectron/100);
                Assert.AreEqual(BioMassCalc.CalculateIonMz(mass, i + 1), tranNode.Mz, BioMassCalc.MassElectron / 100);
            }

            //Check with Average
            TransitionPrediction predSettings =
                docReporterIon.Settings.TransitionSettings.Prediction.ChangeFragmentMassType(MassType.Average);
            TransitionSettings tranSettings = docReporterIon.Settings.TransitionSettings.ChangePrediction(predSettings);
            SrmSettings srmSettings = docReporterIon.Settings.ChangeTransitionSettings(tranSettings);
            SrmDocument averageDocument = docReporterIon.ChangeSettings(srmSettings);
            mass = BioMassCalc.AVERAGE.CalculateMassFromFormula(formula);
            for (int i = 0; i < 3; i++)
            {
                TransitionDocNode tranNode = averageDocument.MoleculeTransitions.ElementAt(i);
                Transition tran = tranNode.Transition;
                Assert.AreEqual(reporterIons[i].CustomIon, tran.CustomIon);
                Assert.AreEqual(tran.Charge, i + 1);
                Assert.AreEqual(BioMassCalc.AVERAGE.CalculateIonMz(formula, i + 1), tranNode.Mz, BioMassCalc.MassElectron / 100);
                Assert.AreEqual(BioMassCalc.CalculateIonMz(mass, i + 1), tranNode.Mz, BioMassCalc.MassElectron / 100);
            }

            //Make sure the rest of the transitions aren't reporter ions
            for (int i = 3; i < 7; i ++)
            {
                Transition tran = docReporterIon.MoleculeTransitions.ElementAt(i).Transition;
                Assert.AreNotEqual(tran.CustomIon, reporterIons);
            }
            var optionalIon = new MeasuredIon(hydrogenPeroxide, formula, null, null,1, true);
            SrmDocument optionalDoc = docPeptide.ChangeSettings(docPeptide.Settings.ChangeTransitionFilter(filter =>
                filter.ChangeMeasuredIons(new[] {optionalIon})));
            Assert.AreEqual(3, optionalDoc.PeptideTransitionCount);
            optionalDoc = optionalDoc.ChangeSettings(optionalDoc.Settings.ChangeTransitionFilter(filter =>
                filter.ChangeMeasuredIons(new[] {optionalIon.ChangeIsOptional(false)})));
            AssertEx.IsDocumentTransitionCount(optionalDoc, 4);
            Assert.AreEqual(optionalIon.ChangeIsOptional(false).CustomIon,
                optionalDoc.MoleculeTransitions.ElementAt(0).Transition.CustomIon);
            optionalDoc =
                optionalDoc.ChangeSettings(
                    optionalDoc.Settings.ChangeTransitionFilter(
                        filter => filter.ChangeMeasuredIons(new[] {optionalIon.ChangeIsOptional(true)})));


            TransitionGroupDocNode nodeGroup = optionalDoc.MoleculeTransitionGroups.ElementAt(0);
            var filteredNodes =
                TransitionGroupTreeNode.GetChoices(nodeGroup, optionalDoc.Settings,
                    optionalDoc.Molecules.ElementAt(0).ExplicitMods, true)
                    .Cast<TransitionDocNode>()
                    .Where(node => Equals(node.Transition.CustomIon, optionalIon.CustomIon));

            var unfilteredNodes =
                TransitionGroupTreeNode.GetChoices(nodeGroup, optionalDoc.Settings,
                    optionalDoc.Molecules.ElementAt(0).ExplicitMods, false)
                    .Cast<TransitionDocNode>()
                    .Where(node => Equals(node.Transition.CustomIon, optionalIon.CustomIon));

            Assert.AreEqual(0,filteredNodes.Count());
            Assert.AreEqual(1,unfilteredNodes.Count());
        }

        /// <summary>
        /// Test support for the new dynamic minimum m/z flag in TransitionInstrument
        /// </summary>
        [TestMethod]
        public void DynamicMinMzTest()
        {
            var revisionIndex = 1;
            var settings = SrmSettingsList.GetDefault().ChangeTransitionFilter(filter =>
                filter.ChangeIonTypes(new[] { IonType.y, IonType.b })
                      .ChangeFragmentRangeFirstName("ion 1")
                      .ChangeFragmentRangeLastName("last ion"));
            var docOriginal = new SrmDocument(settings);
            IdentityPath path;
            var docPeptides = docOriginal.ImportFasta(new StringReader(">peptides\nESTIGNSAFELLLEVAK\nTVYHAGTK"),
                true, IdentityPath.ROOT, out path);
            AssertEx.IsDocumentState(docPeptides, revisionIndex++, 1, 2, 2, 40);
            // Both precursors should contain 1 and 2 ions
            foreach (var nodeGroup in docPeptides.PeptideTransitionGroups)
            {
                Assert.IsTrue(nodeGroup.Children.Contains(node => ((TransitionDocNode)node).Transition.Ordinal == 1));
                Assert.IsTrue(nodeGroup.Children.Contains(node => ((TransitionDocNode)node).Transition.Ordinal == 2));
            }

            // Switch to using dynamic minimum for product ions
            settings = settings.ChangeTransitionInstrument(instrument => instrument.ChangeIsDynamicMin(true));
            var docDynamicMin = docPeptides.ChangeSettings(settings);
            AssertEx.IsDocumentState(docDynamicMin, revisionIndex++, 1, 2, 35);
            var arrayNodeGroups = docDynamicMin.PeptideTransitionGroups.ToArray();
            // The larger precursor should no longer have 1 or 2 ions
            Assert.IsFalse(arrayNodeGroups[0].Children.Contains(node =>
                ((TransitionDocNode)node).Transition.Ordinal == 1 ||
                ((TransitionDocNode)node).Transition.Ordinal == 2));
            // But the smaller precursor should retain at least one of both
            Assert.IsTrue(arrayNodeGroups[1].Children.Contains(node => ((TransitionDocNode)node).Transition.Ordinal == 1));
            Assert.IsTrue(arrayNodeGroups[1].Children.Contains(node => ((TransitionDocNode)node).Transition.Ordinal == 2));

            // Use a minimum that is high enough to exclude the smaller precursor
            settings = settings.ChangeTransitionInstrument(instrument => instrument.ChangeMinMz(500));
            var docHighMinDynamicMz = docDynamicMin.ChangeSettings(settings);
            AssertEx.IsDocumentState(docHighMinDynamicMz, revisionIndex++, 1, 2, 1, 22);
            // Because of the dynamic minimum for product ions, the remaining precursor should not have changed
            var nodeGroupHigh = docHighMinDynamicMz.PeptideTransitionGroups.ToArray()[0];
            Assert.AreSame(arrayNodeGroups[0], nodeGroupHigh);

            // Remove dynamic minimum, and verify that product ions are removed
            settings = settings.ChangeTransitionInstrument(instrument => instrument.ChangeIsDynamicMin(false));
            var docHighMinMz = docHighMinDynamicMz.ChangeSettings(settings);
            AssertEx.IsDocumentState(docHighMinMz, revisionIndex, 1, 2, 1, 17);

            // No library test, because it is hard to test.  Many of the library
            // spectra are likely gathered with LTQs which apply the same dynamic
            // minimum to the ms/ms peaks they measure.
        }

        /// <summary>
        /// Test support for the new precursor m/z exclusion window in TransitionFilter
        /// </summary>
        [TestMethod]
        public void TransitionLibrary07Test()
        {
            // Create a document with the necessary library spectrum
            LibraryManager libraryManager;
            TestDocumentContainer docContainer;
            int startRev;
            SrmDocument document = LibrarySettingsTest.CreateNISTLibraryDocument(
                ">peptide1\nLECTDTLPDILENR",
                true,
                TEXT_LIB_YEAST_NIST_PRECURSOR,
                out libraryManager,
                out docContainer,
                out startRev);

            // Open up the transition filter settings before testing the precursor m/z window
            var settings = document.Settings
                .ChangeTransitionLibraries(library =>
                                           library.ChangePick(TransitionLibraryPick.all).
                                               ChangeIonCount(10))
                .ChangeTransitionFilter(filter =>
                                        filter.ChangeIonTypes(new[] {IonType.y, IonType.b})
                                            .ChangeFragmentRangeFirstName("ion 4")
                                            .ChangeFragmentRangeLastName("last ion - 3"));
            var docLibUnfiltered = document.ChangeSettings(settings);
            Assert.AreEqual(10, docLibUnfiltered.PeptideTransitionCount);
            Assert.AreEqual(1, docLibUnfiltered.PeptideTransitions.Count(nodeTran =>
                nodeTran.Transition.Ordinal < 4 || nodeTran.Transition.Ordinal > nodeTran.Transition.Group.Peptide.Sequence.Length - 4));

            // Using a precursor filter should have no impact now, since the filter is being ignored
            var settingsP = settings.ChangeTransitionFilter(filter =>
                filter.ChangePrecursorMzWindow(TransitionFilter.MAX_EXCLUSION_WINDOW));
            var docIgnorePrecursorMzWindow = docLibUnfiltered.ChangeSettings(settingsP);
            Assert.AreSame(docLibUnfiltered.Children, docIgnorePrecursorMzWindow.Children);
            
            // Switch to filtered picking
            settings = settings.ChangeTransitionLibraries(library => library.ChangePick(TransitionLibraryPick.filter));
            var docLibFiltered = docLibUnfiltered.ChangeSettings(settings);
            Assert.AreEqual(10, docLibFiltered.PeptideTransitionCount);
            // Should have removed the ions outside the previously specified range
            Assert.IsFalse(docLibFiltered.PeptideTransitions.Contains(nodeTran =>
                nodeTran.Transition.Ordinal < 4 || nodeTran.Transition.Ordinal > nodeTran.Transition.Group.Peptide.Sequence.Length - 4));
            // But should contain ions within the precursor window
            double precursorMz = docLibFiltered.PeptideTransitionGroups.ToArray()[0].PrecursorMz;
            const int countInWindow = 2;
            const double maxDelta = TransitionFilter.MAX_EXCLUSION_WINDOW/2;
            Assert.AreEqual(countInWindow, docLibFiltered.PeptideTransitions.Count(nodeTran =>
                Math.Abs(precursorMz - nodeTran.Mz) < maxDelta));

            // Set a precursor m/z window for real this time
            settingsP = settings.ChangeTransitionFilter(filter =>
                filter.ChangePrecursorMzWindow(TransitionFilter.MAX_EXCLUSION_WINDOW));
            var docPrecursorMzWindow = docLibFiltered.ChangeSettings(settingsP);
            Assert.AreEqual(10, docPrecursorMzWindow.PeptideTransitionCount);
            // Make sure transitions in the window were removed
            Assert.IsFalse(docPrecursorMzWindow.PeptideTransitions.Contains(nodeTran =>
                Math.Abs(precursorMz - nodeTran.Mz) < maxDelta));
            // But other transitions remain unchanged
            Assert.AreEqual(docPrecursorMzWindow.PeptideTransitionCount - countInWindow,
                docPrecursorMzWindow.PeptideTransitions.ToList().ConvertAll(nodeTran => nodeTran.Transition)
                    .Intersect(docLibFiltered.PeptideTransitions.ToList().ConvertAll(nodeTran => nodeTran.Transition)).Count());

            // Reset the settings, and make sure the transitions return to previous state
            var docReset = docPrecursorMzWindow.ChangeSettings(settings);
            Assert.AreEqual(docLibFiltered, docReset);

            // Restrict ion count chosen from the library
            settings = settings.ChangeTransitionLibraries(library => library.ChangeIonCount(3))
                               .ChangeTransitionFilter(filter => filter.ChangeIonTypes(new[] {IonType.y}));
            var docThreeTran = docReset.ChangeSettings(settings);
            Assert.AreEqual(3, docThreeTran.PeptideTransitionCount);
            // Make sure ions below precursor m/z are present
            Assert.AreEqual(2, docThreeTran.PeptideTransitions.Count(nodeTran => nodeTran.Mz < precursorMz));

            // Change the filter and picking to library-plus-filter
            // with a more restricted filter.
            settings = settings
                .ChangeTransitionLibraries(library =>
                                           library.ChangePick(TransitionLibraryPick.all_plus))
                .ChangeTransitionFilter(filter =>
                                        filter.ChangeFragmentRangeFirstName("m/z > precursor")
                                            .ChangeFragmentRangeLastName("3 ions"));
            var docLibPlus = docThreeTran.ChangeSettings(settings);
            Assert.AreEqual(5, docLibPlus.PeptideTransitionCount);
            // Make sure ions below precursor m/z are present
            Assert.AreEqual(3, docLibPlus.PeptideTransitions.Count(nodeTran => nodeTran.Mz > precursorMz));
        }

        /// <summary>
        /// Test support for the DIA precursor exclusion window (exclusion of fragments falling into the precursor window)
        /// </summary>
        [TestMethod]
        public void TransitionLibraryDIAPrecursorExclusionTest()
        {
            // Create a document with the necessary library spectrum
            LibraryManager libraryManager;
            TestDocumentContainer docContainer;
            int startRev;
            SrmDocument document = LibrarySettingsTest.CreateNISTLibraryDocument(
                ">peptide1\nLECTDTLPDILENR",
                true,
                TEXT_LIB_YEAST_NIST_PRECURSOR,
                out libraryManager,
                out docContainer,
                out startRev);

            // Open up the transition filter settings and add a new isolation window scheme 
            List<IsolationWindow> isolationWindows = new List<IsolationWindow>();
            // Parent mass is 844
            isolationWindows.Add(new IsolationWindow(600, 700)); // simple windows
            isolationWindows.Add(new IsolationWindow(700, 800)); // simple windows
            isolationWindows.Add(new IsolationWindow(800, 900)); // simple windows
           
            var settings = document.Settings
                .ChangeTransitionLibraries(library =>
                                           library.ChangePick(TransitionLibraryPick.all).
                                               ChangeIonCount(10))
                .ChangeTransitionFilter(filter =>
                                        filter.ChangeIonTypes(new[] { IonType.y, IonType.b })
                                            .ChangeFragmentRangeFirstName("ion 1")
                                            .ChangeFragmentRangeLastName("last ion")
                                            .ChangeExclusionUseDIAWindow(true))
                .ChangeTransitionFullScan(fs => 
                                          fs.ChangeAcquisitionMethod(FullScanAcquisitionMethod.DIA, 
                                                new IsolationScheme("Test", isolationWindows)));
            var docLibUnfiltered = document.ChangeSettings(settings);
    
            Assert.AreEqual(10, docLibUnfiltered.PeptideTransitionCount);
            Assert.AreEqual(1, docLibUnfiltered.PeptideTransitions.Count(nodeTran =>
                nodeTran.Transition.Ordinal < 4 || nodeTran.Transition.Ordinal > nodeTran.Transition.Group.Peptide.Sequence.Length - 4));

            // Using a precursor filter should have no impact now, since the filter is being ignored
            var settingsP = settings.ChangeTransitionFilter(filter =>
                filter.ChangePrecursorMzWindow(TransitionFilter.MAX_EXCLUSION_WINDOW));
            var docIgnorePrecursorMzWindow = docLibUnfiltered.ChangeSettings(settingsP);
            Assert.AreSame(docLibUnfiltered.Children, docIgnorePrecursorMzWindow.Children);

            // Should contain ions within the precursor window      
            const int countInWindow = 2;
            const int countInOverlapWindow = 5;
            Assert.AreEqual(countInWindow, docLibUnfiltered.PeptideTransitions.Count(nodeTran =>
                nodeTran.Mz > 800 && nodeTran.Mz < 900));
            Assert.AreEqual(countInOverlapWindow, docLibUnfiltered.PeptideTransitions.Count(nodeTran =>
                nodeTran.Mz > 750 && nodeTran.Mz < 1250));

            // Switch to filtered picking
            settings = settings.ChangeTransitionLibraries(library => library.ChangePick(TransitionLibraryPick.filter));
            var docLibFiltered = docLibUnfiltered.ChangeSettings(settings);
            Assert.AreEqual(10, docLibFiltered.PeptideTransitionCount);

            // Should not contain any more ions in the specified DIA window
            Assert.AreEqual(0, docLibFiltered.PeptideTransitions.Count(nodeTran =>
                nodeTran.Mz > 800 && nodeTran.Mz < 900));

            // But should contain ions within the extended precursor window
            const int countInOverlapWindowNew = 4;
            Assert.AreEqual(countInOverlapWindowNew, docLibFiltered.PeptideTransitions.Count(nodeTran =>
                nodeTran.Mz > 750 && nodeTran.Mz < 1250));
            // But other transitions remain unchanged
            Assert.AreEqual(docLibUnfiltered.PeptideTransitionCount - countInWindow,
                docLibFiltered.PeptideTransitions.ToList().ConvertAll(nodeTran => nodeTran.Transition)
                    .Intersect(docLibUnfiltered.PeptideTransitions.ToList().ConvertAll(nodeTran => nodeTran.Transition)).Count());

            // Add more windows to the document
            isolationWindows.Add(new IsolationWindow(750, 850));  // overlapping windows
            isolationWindows.Add(new IsolationWindow(840, 950));  // overlapping windows
            isolationWindows.Add(new IsolationWindow(840, 1250)); // overlapping windows
            var settingsNew = settings.ChangeTransitionFullScan(fs => 
                                          fs.ChangeAcquisitionMethod(FullScanAcquisitionMethod.DIA, 
                                                new IsolationScheme("Test", isolationWindows)));
            var docFilterAdditionalWindows = docLibFiltered.ChangeSettings(settingsNew);
            
            // Check that now no more transitions are between 750 and 1250
            Assert.AreEqual(8, docFilterAdditionalWindows.PeptideTransitionCount);
            Assert.AreEqual(0, docFilterAdditionalWindows.PeptideTransitions.Count(nodeTran =>
                nodeTran.Mz > 750 && nodeTran.Mz < 1250));
            // But other transitions remain unchanged
            Assert.AreEqual(docLibUnfiltered.PeptideTransitionCount - countInOverlapWindow,
                docFilterAdditionalWindows.PeptideTransitions.ToList().ConvertAll(nodeTran => nodeTran.Transition)
                    .Intersect(docLibUnfiltered.PeptideTransitions.ToList().ConvertAll(nodeTran => nodeTran.Transition)).Count());

            // Reset the settings, and make sure the transitions return to previous state
            var docReset = docFilterAdditionalWindows.ChangeSettings(settings);
            Assert.AreEqual(docLibFiltered, docReset);

            // The filter should be turned off again when setting ExclusionUseDIAWindow to false
            var settingsNoFilter = settings.ChangeTransitionFilter(filter =>
                                       filter.ChangeExclusionUseDIAWindow(false));
            var docNoFilterAgain = docLibFiltered.ChangeSettings(settingsNoFilter);
            Assert.AreEqual(10, docNoFilterAgain.PeptideTransitionCount);
            Assert.AreEqual(countInOverlapWindow, docNoFilterAgain.PeptideTransitions.Count(nodeTran =>
                nodeTran.Mz > 750 && nodeTran.Mz < 1250));
        }

        private const string TEXT_LIB_YEAST_NIST_PRECURSOR =
            "Name: LECTDTLPDILENR/2\n" +
            "MW: 1689.824\n" +
            "Comment: Spec=Consensus Pep=Tryptic Fullname=K.LECTDTLPDILENR.G/2 Mods=1/2,C,Carbamidomethyl Parent=844.912 Inst=it Mz_diff=0.578 Mz_exact=844.9122 Mz_av=845.443 Protein=\"gi|6319317|ref|NP_009400.1| Largest of six subunits of the RNA polymerase III transcription initiation factor complex (TFIIIC); part of the TauB domain of TFIIIC that binds DNA at the BoxB promoter sites of tRNA and similar genes; cooperates with Tfc6p in DNA binding [Saccharomyces cerevisiae]; gi|170944|gb|AAA34378.1| tau138; gi|385245|gb|AAC04956.1| Tfc3p: Transcription factor tau (TFIIIC) subunit 138 [Saccharomyces cerevisiae]; gi|422198|pir||A46423 transcription factor IIIC B-block-binding chain - yeast  (Saccharomyces cerevisiae); gi|464875|sp|P34111|TFC3_YEAST Transcription factor tau 138 kDa subunit (TFIIIC 138 kDa subunit)\" Pseq=7 Organism=\"yeast\" Se=3^X2:ex=6.7e-008/3e-008,td=9.43e+007/4.07e+007,sd=0/0,hs=51.45/3.65,bs=3.7e-008,b2=9.7e-008,bd=1.35e+008^O2:ex=2.045e-005/2.045e-005,td=2.895e+292/2.895e+292,pr=2.665e-009/2.665e-009,bs=8.64e-292,b2=4.09e-005,bd=5.79e+292^P2:sc=23.35/3.75,dc=14.1/3,ps=2.9/0.33,bs=0 Sample=2/cbs00174_01_07_cam,1,1/cbs00174_01_10_cam,1,1 Nreps=2/2 Missing=0.2486/0.0394 Parent_med=845.49/0.09 Max2med_orig=62.2/27.8 Dotfull=0.700/0.000 Dot_cons=0.852/0.029 Unassign_all=0.169 Unassigned=0.080 Dotbest=0.88 Flags=0,0,0 Naa=14 DUScorr=10/1.1/2.9 Dottheory=0.94 Pfin=6.7e+011 Probcorr=1 Tfratio=1e+008 Pfract=0.11\n" +
            "Num peaks: 105\n" +
            "272.2	416	\"y2-17/0.04 2/2 0.3\"\n" +
            "289.2	1574	\"y2/0.04 2/2 0.5\"\n" +
            "377.1	611	\"? 2/2 3.8\"\n" +
            "383.2	325	\"? 2/2 3.2\"\n" +
            "403.3	814	\"b3/0.14 2/2 0.9\"\n" +
            "418.3	2397	\"y3/0.09 2/2 5.4\"\n" +
            "504.2	463	\"b4/-0.00 2/2 2.4\"\n" +
            "522.5	316	\"? 2/1 2.4\"\n" +
            "531.4	6849	\"y4/0.11 2/2 8.8\"\n" +
            "532.4	1272	\"y4i/1.11 2/2 5.8\"\n" +
            "544.4	340	\"? 2/2 2.5\"\n" +
            "568.4	399	\"? 2/2 1.5\"\n" +
            "573.4	755	\"b5-46/0.17 2/2 6.1\"\n" +
            "589.3	274	\"? 2/1 2.2\"\n" +
            "591.4	959	\"b11-91*^2/0.59,a5/0.17 2/2 3.3\"\n" +
            "599.4	517	\"y5-45/0.03 2/2 5.7\"\n" +
            "602.1	406	\"b5-17/-0.13 2/2 2.8\"\n" +
            "607.3	806	\"? 2/2 14.7\"\n" +
            "609.3	842	\"? 2/2 16.5\"\n" +
            "611.6	471	\"? 2/2 1.0\"\n" +
            "614.7	314	\"b11-44*^2/0.39 2/1 2.9\"\n" +
            "615.3	269	\"? 2/1 1.8\"\n" +
            "619.3	1801	\"b5/0.07 2/2 5.2\"\n" +
            "620.3	479	\"b5i/1.07 2/2 1.6\"\n" +
            "626.9	544	\"b11-18^2/-0.41,y5-17/-0.47 2/2 0.6\"\n" +
            "627.4	570	\"y5-17/0.03,b11-18^2/0.09 2/2 0.2\"\n" +
            "632.4	1112	\"? 2/2 7.4\"\n" +
            "633.4	530	\"?i 2/2 4.1\"\n" +
            "644.4	5259	\"y5/0.03 2/2 6.6\"\n" +
            "645.5	1414	\"y5i/1.13 2/2 8.6\"\n" +
            "647.7	988	\"? 2/2 1.4\"\n" +
            "680.3	524	\"? 2/2 7.0\"\n" +
            "684.3	586	\"? 2/2 0.9\"\n" +
            "685.4	364	\"?i 2/2 1.2\"\n" +
            "702.3	1217	\"b6-18/0.02 2/2 4.3\"\n" +
            "706.1	454	\"? 2/2 3.0\"\n" +
            "707.6	414	\"? 2/2 0.6\"\n" +
            "714.8	1382	\"y12-18^2/-0.05,y6-45/0.40 2/2 13.9\"\n" +
            "715.3	1407	\"y12-17^2/-0.05,y12-18^2/0.45 2/2 1.2\"\n" +
            "720.2	2445	\"b6/-0.08 2/2 4.2\"\n" +
            "721.3	605	\"b6i/1.02 2/2 2.3\"\n" +
            "724.0	2701	\"y12^2/0.15 2/2 7.6\"\n" +
            "725.2	1497	\"y12i^2/1.35 2/2 13.9\"\n" +
            "731.0	370	\"? 2/1 1.1\"\n" +
            "739.5	454	\"? 2/2 0.2\"\n" +
            "747.6	871	\"? 2/2 17.0\"\n" +
            "752.1	416	\"? 2/2 0.3\"\n" +
            "759.4	944	\"y6/-0.00 2/2 5.0\"\n" +
            "761.3	505	\"? 2/2 2.6\"\n" +
            "769.1	265	\"? 2/1 1.4\"\n" +
            "770.4	1895	\"? 2/2 18.7\"\n" +
            "771.5	461	\"?i 2/2 2.9\"\n" +
            "773.0	251	\"? 2/1 1.6\"\n" +
            "780.3	1359	\"y13-17^2/0.43 2/2 14.7\"\n" +
            "781.3	1215	\"y13-17i^2/1.43 2/1 24.6\"\n" +
            "782.3	448	\"y13-17i^2/2.43 2/2 1.6\"\n" +
            "787.5	433	\"b7-46/0.14 2/2 0.9\"\n" +
            "789.3	383	\"b7-44/-0.06 2/2 0.5\"\n" +
            "790.8	2623	\"? 2/2 27.9\"\n" +
            "792.5	769	\"? 2/2 1.2\"\n" +
            "797.2	1084	\"? 2/2 8.3\"\n" +
            "798.2	782	\"?i 2/2 1.0\"\n" +
            "801.7	393	\"? 2/1 3.1\"\n" +
            "805.4	1289	\"a7/0.04 2/2 4.5\"\n" +
            "806.5	287	\"a7i/1.14 2/1 1.9\"\n" +
            "812.0	439	\"y7-44/-0.45,y7-45/0.55 2/2 3.7\"\n" +
            "815.3	8890	\"b7-18/-0.06 2/2 1.8\"\n" +
            "816.3	4749	\"b7-17/-0.06 2/2 17.2\"\n" +
            "817.3	1053	\"b7-17i/0.94 2/2 9.8\"\n" +
            "818.7	603	\"? 2/2 5.8\"\n" +
            "823.7	432	\"? 2/2 1.0\"\n" +
            "826.7	3704	\"p-36/-0.20 2/2 49.7\"\n" +
            "827.6	3810	\"p-35/0.19 2/2 0.4\"\n" +
            "828.6	535	\"p-35i/1.19 2/1 5.9\"\n" +
            "833.3	10000	\"b7/-0.06 2/2 0.0\"\n" +
            "834.4	1463	\"b7i/1.04 2/2 4.1\"\n" +
            "856.6	4755	\"y7/0.15 2/2 4.0\"\n" +
            "857.4	2047	\"y7i/0.95 2/2 0.7\"\n" +
            "858.5	324	\"y7i/2.05 2/1 1.8\"\n" +
            "969.5	4733	\"y8/-0.04 2/2 6.0\"\n" +
            "970.5	3125	\"y8i/0.96 2/2 18.1\"\n" +
            "1027.0	311	\"b9-18/-0.44,y9-44/0.42 2/1 0.6\"\n" +
            "1045.2	712	\"b9/-0.24 2/2 1.7\"\n" +
            "1070.6	3827	\"y9/0.02 2/2 2.5\"\n" +
            "1071.6	2706	\"y9i/1.02 2/2 10.4\"\n" +
            "1139.7	430	\"y10-46/0.09 2/2 2.9\"\n" +
            "1140.7	1128	\"b10-18/0.17,y10-45/0.09 2/2 2.0\"\n" +
            "1141.6	619	\"y10-44/-0.01,b10-17/0.07 2/2 0.3\"\n" +
            "1155.2	1927	\"? 2/2 27.6\"\n" +
            "1158.6	2187	\"b10/0.07 2/2 8.8\"\n" +
            "1159.4	986	\"b10i/0.87 2/2 0.5\"\n" +
            "1185.5	1764	\"y10/-0.11 2/2 5.0\"\n" +
            "1186.7	1253	\"y10i/1.09 2/2 8.9\"\n" +
            "1202.5	922	\"? 2/2 7.9\"\n" +
            "1254.3	887	\"b11-17/-0.31 2/2 6.8\"\n" +
            "1272.6	706	\"? 2/2 4.6\"\n" +
            "1286.7	1317	\"y11/0.04 2/2 0.8\"\n" +
            "1287.7	787	\"y11i/1.04 2/2 4.2\"\n" +
            "1338.7	307	\"? 2/1 3.0\"\n" +
            "1400.4	489	\"b12/-0.25,y12-46/-0.29 2/2 6.7\"\n" +
            "1401.6	530	\"y12-45/-0.09 2/1 5.5\"\n" +
            "1446.4	530	\"y12/-0.29 2/2 0.6\"\n" +
            "1447.8	414	\"? 2/2 0.6\"\n" +
            "1514.5	364	\"b13/-0.20 2/2 1.2\"\n" +
            "1515.6	650	\"b13i/0.90 2/2 4.4\"\n";
    }
}