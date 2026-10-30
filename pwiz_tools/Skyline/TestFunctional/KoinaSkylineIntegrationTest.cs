/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Koina;
using pwiz.Skyline.Model.Koina.Communication;
using pwiz.Skyline.Model.Koina.Models;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using pwiz.Skyline.Alerts;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class KoinaSkylineIntegrationTest : AbstractFunctionalTestEx
    {
        private bool RecordData => false;

        private string ExpectedQueriesJsonFilepath => TestContext.GetProjectDirectory("TestFunctional/KoinaSkylineIntegrationTest2.data/koinaQueries.json");

        /// <summary>The queries of the Predict For route, recorded apart from the document's so that re-recording one leaves the other as it was.</summary>
        private string FastaQueriesJsonFilepath => TestContext.GetProjectDirectory("TestFunctional/KoinaSkylineIntegrationTest2.data/koinaFastaQueries.json");

        /// <summary>Three single-peptide proteins, none of them in the rat plasma document.</summary>
        private string PredictForFastaPath => TestContext.GetProjectDirectory("TestFunctional/KoinaSkylineIntegrationTest2.data/ThreeProteins.fasta");

        [TestMethod]
        public void TestKoinaSkylineIntegration()
        {
            TestFilesZip = "TestFunctional/KoinaSkylineIntegrationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OpenDocument(TestFilesDir.GetTestPath(@"Rat_plasma.sky"));

            /*var doc = SkylineWindow.Document;
            RunUI(() =>
            {
                // Add Koina supported mods
                SkylineWindow.ChangeSettings(SkylineWindow.Document.Settings.ChangePeptideModifications(pm =>
                {
                    return pm.ChangeStaticModifications(new[]
                    {
                        UniMod.DictStructuralModNames[@"Carbamidomethyl (C)"],
                        UniMod.DictStructuralModNames[@"Oxidation (M)"]
                    });
                }), false);
            });

            WaitForDocumentChange(doc);*/

            // Set up library match

            // Show all ions and charges of interest
            Settings.Default.ShowBIons = true;
            Settings.Default.ShowYIons = true;
            Settings.Default.ShowCharge1 = true;
            Settings.Default.ShowCharge2 = true;
            Settings.Default.ShowCharge3 = true;

            KoinaConstants.CACHE_PREV_PREDICTION = false;
            using (new FakeKoina(RecordData, ExpectedQueriesJsonFilepath))
            {
                TestKoinaOptions();
                TestKoinaSinglePrecursorPredictions();
                TestLiveKoinaMirrorPlots();
                Settings.Default.Koina = false; // Disable Koina to avoid that last query after building the library
                TestKoinaLibraryBuild(false);
                TestMissingRetentionTimePrediction();
                TestInvalidPepSequences(); // Do this at the end, otherwise it messes with the order of nodes
                //var expected = RecordData ? 0 : QUERIES.Count;
                //Assert.AreEqual(expected, ((FakeKoinaPredictionClient)KoinaPredictionClient.Current).QueryIndex);

                TestKoinaLibraryBuild(true);
            }
            // The Predict For route: a protein FASTA, with decoys and entrapment, predicted for a
            // document that differs from it
            using (new FakeKoina(RecordData, FastaQueriesJsonFilepath))
            {
                TestKoinaLibraryBuildForFasta();
            }
            KoinaConstants.CACHE_PREV_PREDICTION = true;
        }

        /// <summary>
        /// Regression test for issue #3971: when the RT model fails to return a result,
        /// the error should be properly reported instead of leaving the UI stuck on
        /// "Making predictions..." indefinitely due to an unhandled exception.
        /// Before the fix, KeyNotFoundException escaped to ActionUtil.RunAsync's generic
        /// handler, so neither Spectrum nor Exception was set on the KoinaRequest.
        /// After the fix, the exception is caught and reported to the UI.
        /// </summary>
        public void TestMissingRetentionTimePrediction()
        {
            var client = (FakeKoinaPredictionClient) KoinaPredictionClient.Current;
            client.SuppressRetentionTimePrediction = true;
            try
            {
                // Use NCE 25 for molecule 0, which already has an intensity query
                // in the expected queries from TestKoinaSinglePrecursorPredictions
                Settings.Default.KoinaNCE = 25;
                // LibMatchMirror is left enabled by TestLiveKoinaMirrorPlots;
                // disable it so the Koina error path is exercised instead of falling
                // back to the library spectrum.
                Settings.Default.LibMatchMirror = false;
                SelectNode(SrmDocument.Level.Molecules, 0);
                Settings.Default.Koina = true;
                RunUI(SkylineWindow.UpdateGraphPanes);
                WaitForGraphs();
                // The exception should be properly reported, not stuck on "Making predictions..."
                WaitForConditionUI(() =>
                    SkylineWindow.GraphSpectrum.GraphException != null &&
                    !(SkylineWindow.GraphSpectrum.GraphException is KoinaPredictingException));
                RunUI(() =>
                {
                    var ex = SkylineWindow.GraphSpectrum.GraphException;
                    Assert.IsInstanceOfType(ex, typeof(KoinaException));
                    Assert.IsNotNull(ex.InnerException,
                        "Expected KoinaException wrapping the original RT prediction failure");
                });
            }
            finally
            {
                client.SuppressRetentionTimePrediction = false;
                Settings.Default.Koina = false;
            }
        }

        public void TestInvalidPepSequences()
        {
            // Allow us to paste 'random' sequences
            /*RunUI(() =>
            {
                // Max missed cleavages
                SkylineWindow.ModifyDocument(null, doc => doc.ChangeSettings(doc.Settings.ChangePeptideSettings(
                    doc.Settings.PeptideSettings.ChangeDigestSettings(new DigestSettings(2,
                        doc.Settings.PeptideSettings.DigestSettings.ExcludeRaggedEnds)))));

                // Max len
                SkylineWindow.ModifyDocument(null, doc => doc.ChangeSettings(doc.Settings.ChangePeptideSettings(
                    doc.Settings.PeptideSettings.ChangeFilter(
                        doc.Settings.PeptideSettings.Filter.ChangeMaxPeptideLength(32)))));
            });*/

            // Unknown Amino Acid 'O'
            TestKoinaException("ROHDESKYLINE", typeof(KoinaUnsupportedAminoAcidException));
            // Too long
            TestKoinaException(string.Concat(Enumerable.Repeat("AAAA", 8)), typeof(KoinaPeptideTooLongException));
            // Unsupported mod
            TestKoinaException("S[+80]KYLINE", typeof(KoinaUnsupportedModificationException));
            // Small molecule
            // We are careful not to involve Koina with small molecules, so no exception expected
            TestKoinaException("Methionine", null);
        }

        private void SelectNodeBySeq(string seq)
        {
            seq = FastaSequence.StripModifications(seq);
            var found = false;
            RunUI(() =>
            {
                foreach (var node in SkylineWindow.SequenceTree.Nodes.OfType<TreeNodeMS>())
                {
                    foreach (var child in node.Nodes.OfType<TreeNodeMS>())
                    {
                        if (child is PeptideTreeNode pep &&
                            pep.DocNode.Peptide.Target.DisplayName == seq)
                        {
                            found = true;
                            SkylineWindow.SequenceTree.SelectedNode = child;
                            return;
                        }
                    }
                }
            });

            Assert.IsTrue(found, "Could not find and select sequence \"{0}\"", seq);
            WaitForConditionUI(() => SkylineWindow.SelectedNode is PeptideTreeNode pep &&
                                     pep.DocNode.Peptide.Target.DisplayName == seq);
        }

        private void TestKoinaException(string seq, Type expectedException)
        {
            /*var doc = SkylineWindow.Document;
            if (!addMods)
            {
                RunUI(() => SkylineWindow.Paste(seq));
            }
            else
            {
                RunDlg<MultiButtonMsgDlg>(() => SkylineWindow.Paste(seq), dlg =>
                {
                    dlg.OkDialog();
                });
            }
            WaitForDocumentChange(doc);*/

            SelectNodeBySeq(seq);
            Settings.Default.Koina = true;
            RunUI(SkylineWindow.UpdateGraphPanes);
            // Add first precursor
            /*RunDlg<PopupPickList>(SkylineWindow.ShowPickChildrenInTest, dlg =>
            {
                dlg.ToggleItem(0);
                dlg.OnOk();
            });

            WaitForConditionUI(() => SkylineWindow.SelectedNode.Nodes.Count == 1);*/
            WaitForGraphs();
            if (expectedException != null)
            {
                WaitForConditionUI(() =>
                    SkylineWindow.GraphSpectrum.GraphException != null &&
                    !(SkylineWindow.GraphSpectrum.GraphException is KoinaPredictingException));

                RunUI(() =>
                {
                    Assert.IsInstanceOfType(SkylineWindow.GraphSpectrum.GraphException, expectedException);
                });
            }
            Settings.Default.Koina = false;
        }

        public void TestKoinaLibraryBuild(bool newDocument)
        {
            var client = (FakeKoinaPredictionClient)KoinaPredictionClient.Current;
            var outBlib = TestFilesDir.GetTestPath("Koina.blib");

            if (newDocument)
                RunUI(() => SkylineWindow.NewDocument(true));

            var doc = SkylineWindow.Document;

            // An empty document builds no library, and the new document may be loading Koina.blib
            // from the last-used settings
            if (!newDocument)
                FileEx.SafeDelete(outBlib);

            // Open Peptide Settings -- Library
            var peptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var buildLibrary = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);

            RunUI(() =>
            {
                Assert.IsFalse(buildLibrary.Koina);
                buildLibrary.Koina = true;
            });

            RunUI(() =>
            {
                var nce = Settings.Default.KoinaNCE;
                Assert.AreEqual(buildLibrary.NCE, nce);
                buildLibrary.NCE = 32;
                // Don't want this to change the settings nce, this is just for building the library
                Assert.AreEqual(Settings.Default.KoinaNCE, nce);

                buildLibrary.LibraryName = "Koina";
                buildLibrary.LibraryPath = outBlib;
                // buildLibrary.IrtStandard = IrtStandard.BIOGNOSYS_11;
            });
            if (newDocument)
            {
                RunDlg<MessageDlg>(buildLibrary.OkWizardPage, dlg =>
                {
                    Assert.AreEqual(Resources.BuildLibraryDlg_ValidateBuilder_Add_peptide_precursors_to_the_document_to_build_a_library_from_Koina_predictions_,
                        dlg.Message);
                    dlg.OkDialog();
                });
                OkDialog(buildLibrary, buildLibrary.CancelButton.PerformClick);
                OkDialog(peptideSettings, peptideSettings.OkDialog);
                return;
            }

            OkDialog(buildLibrary, buildLibrary.OkWizardPage);

            // Wait for library build
            Assert.IsTrue(WaitForConditionUI(() =>
                peptideSettings.AvailableLibraries.Contains("Koina")));
            // Select new library
            RunUI(() => peptideSettings.PickedLibraries =
                peptideSettings.PickedLibraries.Concat(new[] { "Koina" }).ToArray());
            // Close pep settings
            OkDialog(peptideSettings, peptideSettings.OkDialog);

            // Wait for existing library
            WaitForCondition(() => File.Exists(outBlib));
            // Wait for stable document
            WaitForDocumentChangeLoaded(doc);

            var precursorCount = SkylineWindow.Document.PeptideTransitionGroupCount;
            var distinctPrecursorCount = 
                SkylineWindow.Document.Peptides.SelectMany(pep => pep.TransitionGroups
                    .Select(tg => Tuple.Create(pep.ModifiedTarget, tg.PrecursorCharge)))
                .Distinct().Count();

            const int notSupportedCount = 3;
            WaitForLibrary(distinctPrecursorCount - notSupportedCount, SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs.IndexOf(l => l.Name == "Koina"));

            var koinaLib = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Last();

            // Extract spectrum and rt info from the library and store it a way that
            // allows us to verify the spectra easily
            var spectrumDisplayInfos = new SpectrumDisplayInfo[precursorCount];
            var peptidesRepeat = new KoinaIntensityModel.PeptidePrecursorNCE[precursorCount];
            var peptides = SkylineWindow.Document.Peptides.ToArray();
            var idx = 0;
            var noMatchCount = 0;
            for (var i = 0; i < peptides.Length; ++i)
            {
                var precursors = peptides[i].TransitionGroups.ToArray();
                for (var j = 0; j < precursors.Length; ++j)
                {
                    var libKey = new LibKey(peptides[i].ModifiedSequence, precursors[j].PrecursorAdduct);
                    var spectra = koinaLib.GetSpectra(libKey, IsotopeLabelType.light, LibraryRedundancy.all).ToArray();
                    if (spectra.Length == 0)
                    {
                        ++noMatchCount;
                        spectrumDisplayInfos[idx] = null;
                    }
                    else
                    {
                        spectrumDisplayInfos[idx] = new SpectrumDisplayInfo(spectra[0], precursors[j], spectra[0].RetentionTime);
                    }

                    peptidesRepeat[idx++] = new KoinaIntensityModel.PeptidePrecursorNCE(peptides[i], precursors[j], IsotopeLabelType.light);
                }
            }

            Assert.AreEqual(notSupportedCount, noMatchCount);
            if (RecordData)
                return;


            // Get queries and make sure they match the actual spectra
            Assert.IsTrue(KoinaQuery.TryGetQuery(client.ExpectedQueries, doc.Settings, peptidesRepeat, buildLibrary.NCE, out var intensityQuery));
            intensityQuery.AssertMatchesSpectra(peptidesRepeat, spectrumDisplayInfos);

            Assert.IsTrue(KoinaQuery.TryGetQuery(client.ExpectedQueries, doc.Settings,
                peptidesRepeat.Select(p => new KoinaRetentionTimeModel.PeptideDocNodeWrapper(p.NodePep)).ToArray(),
                out var rtQuery));
            rtQuery.AssertMatchesSpectra(spectrumDisplayInfos);
        }

        /// <summary>
        /// The Predict For route: the library is predicted for a protein FASTA's digest, with a
        /// reverse-sequence decoy of every peptide and entrapment peptides shuffled from the FASTA's
        /// own, while the open document keeps its peptides. The FASTA shares no peptide with the
        /// rat plasma document, so a library holding the document's peptides, or lacking the FASTA's,
        /// would show the Predict For choices ignored - which a document digested from the same FASTA
        /// could not, its count being the FASTA's either way. The document also has auto-select off
        /// for peptides and transitions, which the digest for prediction must override: as the
        /// document's settings stand, a FASTA's proteins get no peptides. And it is given a library
        /// pick that ranks peptides, which the digest's pick by filter must clear first or be refused.
        /// </summary>
        private void TestKoinaLibraryBuildForFasta()
        {
            const string libraryName = "KoinaFasta";
            var outBlib = TestFilesDir.GetTestPath(libraryName + BiblioSpecLiteSpec.EXT);
            FileEx.SafeDelete(outBlib);
            // The rat plasma document again: the last library build left a new, empty document
            OpenDocument(TestFilesDir.GetTestPath(@"Rat_plasma.sky"));
            RunUI(() => SkylineWindow.ChangeSettings(SkylineWindow.Document.Settings.ChangePeptideLibraries(lib =>
                lib.ChangePick(PeptidePick.library).ChangeRankId(LibrarySpec.PEP_RANK_COPIES)), true));
            var docOpen = SkylineWindow.Document;
            AssertEx.IsFalse(docOpen.Settings.PeptideSettings.Filter.AutoSelect);
            AssertEx.IsFalse(docOpen.Settings.TransitionSettings.Filter.AutoSelect);
            AssertEx.IsNotNull(docOpen.Settings.PeptideSettings.Libraries.RankId);
            Settings.Default.Koina = false; // No live prediction for the selected node after the build

            var peptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var buildLibrary = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            RunUI(() =>
            {
                buildLibrary.Koina = true;
                buildLibrary.LibraryName = libraryName;
                buildLibrary.LibraryPath = outBlib;
                buildLibrary.NCE = 32;
                // The document's precursors are the default, with Finish on the first page; a FASTA's
                // precursors lead to the Predict For page
                AssertEx.IsTrue(buildLibrary.PredictForDocument);
                AssertEx.AreEqual(Resources.BuildLibraryDlg_OkWizardPage_Finish, buildLibrary.NextButtonText);
                buildLibrary.PredictForDocument = false;
                AssertEx.AreEqual(Resources.BuildLibraryDlg_btnPrevious_Click__Next__, buildLibrary.NextButtonText);
                buildLibrary.OkWizardPage();
                AssertEx.AreEqual(BuildLibraryDlg.Pages.predict_for, buildLibrary.CurrentPage);
                buildLibrary.PredictForFastaPath = PredictForFastaPath;
                buildLibrary.AddDecoys = true;
                buildLibrary.AddEntrapment = true;
                AssertEx.IsFalse(buildLibrary.EntrapmentFromFasta);
                AssertEx.AreEqual(Resources.BuildLibraryDlg_OkWizardPage_Finish, buildLibrary.NextButtonText);
            });
            RunUI(buildLibrary.OkWizardPage);
            WaitForCondition(() => buildLibrary.Builder != null);
            var builder = (KoinaLibraryBuilder)buildLibrary.Builder;
            WaitForClosedForm(buildLibrary);

            // The FASTA's document, not the open one: its peptides, the entrapment peptides and a
            // reverse-sequence decoy of each, and none of the open document's. The generator leaves
            // out a decoy that duplicates another or gets no precursor, so a few targets go unpaired.
            var docPredicted = builder.Document;
            AssertEx.IsFalse(ReferenceEquals(docOpen, docPredicted));
            AssertEx.AreEqual(BuildLibraryDlg.DECOY_METHOD_REVERSE, builder.DecoyMethod);
            AssertEx.IsNotNull(builder.EntrapmentTargets, @"the FASTA gave no entrapment peptides");
            var entrapmentSet = new HashSet<Target>(builder.EntrapmentTargets);
            AssertEx.IsTrue(entrapmentSet.Count > 0, @"the FASTA gave no entrapment peptides");
            var targets = docPredicted.Peptides.Where(p => !p.IsDecoy).ToArray();
            AssertEx.IsTrue(targets.Any(p => !entrapmentSet.Contains(p.Target)), @"the FASTA gave no peptides");
            var targetSequences = new HashSet<string>(targets.Select(p => p.Target.Sequence));
            var decoys = docPredicted.Peptides.Where(p => p.IsDecoy).ToArray();
            AssertEx.IsTrue(decoys.Length > 0 && decoys.Length <= targets.Length, string.Format(@"{0} decoys for {1} targets", decoys.Length, targets.Length));
            AssertEx.IsTrue(decoys.All(p => targetSequences.Contains(p.SourceUnmodifiedTarget.Sequence)), @"a decoy has no target in the document");
            var openSequences = new HashSet<string>(docOpen.Peptides.Select(p => p.Target.Sequence));
            AssertEx.IsFalse(targets.Any(p => openSequences.Contains(p.Target.Sequence)), @"the FASTA shares a peptide with the open document");

            // The library holds every precursor Koina can predict (a peptide too long for it, or whose
            // precursors the transition settings exclude, contributes nothing), none of the open
            // document's, and pairs the decoys with their targets and flags the entrapment peptides,
            // as the Carafe source's does
            WaitForConditionUI(() => peptideSettings.AvailableLibraries.Contains(libraryName));
            RunUI(() => peptideSettings.PickedLibraries = new[] { libraryName });
            OkDialog(peptideSettings, peptideSettings.OkDialog);
            WaitForCondition(() => File.Exists(outBlib));
            WaitForDocumentLoaded();
            var library = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Single(lib => lib != null && lib.Name == libraryName);
            var heldPrecursors = new List<(PeptideDocNode NodePep, TransitionGroupDocNode NodeGroup)>();
            int supportedCount = 0;
            foreach (var nodePep in docPredicted.Peptides)
            {
                bool supported = KoinaHelpers.EncodeSequence(docPredicted.Settings, nodePep, IsotopeLabelType.light, out _) != null;
                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    if (supported)
                        supportedCount++;
                    bool held = library.Contains(nodeGroup.GetLibKey(docPredicted.Settings, nodePep));
                    AssertEx.AreEqual(supported, held, nodeGroup.ToString());
                    if (held)
                        heldPrecursors.Add((nodePep, nodeGroup));
                }
            }
            AssertEx.IsTrue(supportedCount > 0, @"Koina supports none of the precursors");
            AssertEx.AreEqual(supportedCount, library.SpectrumCount);
            var nodePepOpen = docOpen.Peptides.First();
            AssertEx.IsFalse(library.Contains(nodePepOpen.TransitionGroups.First().GetLibKey(docOpen.Settings, nodePepOpen)),
                @"the library holds a peptide of the open document");
            // DecoyPairs finds a precursor by unmodified sequence and charge, and leaves out one the
            // library holds twice, which a variable oxidation makes of every methionine peptide here;
            // the library holds one spectrum per library key, however many document nodes share it
            AssertEx.IsTrue(OspreyHelpers.LibraryHasDecoyPairs(outBlib), @"the library has no DecoyPairs table");
            var uniquelyHeld = heldPrecursors.GroupBy(h => h.NodeGroup.GetLibKey(docPredicted.Settings, h.NodePep)).Select(g => g.First())
                .GroupBy(h => (h.NodePep.Target.Sequence, h.NodeGroup.PrecursorCharge))
                .Where(g => g.Count() == 1).Select(g => g.Single()).ToArray();
            var pairedTargets = new HashSet<(string, int)>(uniquelyHeld.Where(h => !h.NodePep.IsDecoy)
                .Select(h => (h.NodePep.Target.Sequence, h.NodeGroup.PrecursorCharge)));
            int expectedEntrapmentRows = pairedTargets.Count(t => entrapmentSet.Contains(new Target(t.Item1)));
            int expectedDecoyRows = uniquelyHeld.Count(h => h.NodePep.IsDecoy &&
                pairedTargets.Contains((h.NodePep.SourceUnmodifiedTarget.Sequence, h.NodeGroup.PrecursorCharge)));
            LibraryDecoyPairs.Count(outBlib, out int decoyRows, out int entrapmentRows);
            AssertEx.IsTrue(expectedDecoyRows > 0 && expectedEntrapmentRows > 0, @"the library pairs nothing");
            AssertEx.AreEqual(expectedDecoyRows, decoyRows);
            AssertEx.AreEqual(expectedEntrapmentRows, entrapmentRows);
        }

        public void TestKoinaOptions()
        {
            // Enable Koina
            Settings.Default.Koina = true;

            // For now just set all Koina settings
            var toolOptions = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Koina));

            var intensityModel = KoinaIntensityModel.Models.First();
            var rtModel = KoinaRetentionTimeModel.Models.First();
            RunUI(() =>
            {
                // Also set ip, otherwise we will keep getting exceptions about the server not being set,
                // although we are using the fake test client
                toolOptions.KoinaIntensityModelCombo = intensityModel;
                toolOptions.KoinaRetentionTimeModelCombo = rtModel;
                toolOptions.CECombo = 28;
            });

            WaitForConditionUI(() => toolOptions.KoinaServerStatus == ToolOptionsUI.ServerStatus.AVAILABLE);
            RunUI(() => toolOptions.DialogResult = DialogResult.OK);
            WaitForClosedForm(toolOptions);

            Assert.AreEqual(intensityModel, Settings.Default.KoinaIntensityModel);
            Assert.AreEqual(rtModel, Settings.Default.KoinaRetentionTimeModel);
            Assert.AreEqual(28, Settings.Default.KoinaNCE);
        }

        public void TestKoinaSinglePrecursorPredictions()
        {
            var client = (FakeKoinaPredictionClient) KoinaPredictionClient.Current;

            var baseCE = 28;
            Assert.AreEqual(baseCE, Settings.Default.KoinaNCE);

            // Selecting a protein should will make a prediction for its first precursor
            SelectNode(SrmDocument.Level.MoleculeGroups, 0);
            GraphSpectrum.SpectrumNodeSelection selection = null;
            RunUI(() => selection = GraphSpectrum.SpectrumNodeSelection.GetCurrent(SkylineWindow));
            WaitForKoinaSpectrum(selection.NodePepGroup.Peptides.First().TransitionGroups.First(), baseCE);

            // Select several peptides and make sure they are displayed correctly
            TestKoinaSinglePrecursorPredictions(client, SrmDocument.Level.Molecules, 0, 4);
            // Do the same for transition groups, we have one more of those because of the heavy precursor
            TestKoinaSinglePrecursorPredictions(client, SrmDocument.Level.TransitionGroups, 0, 5);
        }

        public void TestKoinaSinglePrecursorPredictions(FakeKoinaPredictionClient client, SrmDocument.Level level, int start, int end)
        {
            var baseCE = 25;
            Settings.Default.KoinaNCE = baseCE;

            // Select several peptides and make sure they are displayed correctly
            for (var i = start; i < end; ++i)
            {
                // Select node, causing koina predictions to be made
                SelectNode(level, i); // i'th peptide

                // Get selected node, since we need it for calculating MZs. Selecting a node is instant,
                // the prediction not
                GraphSpectrum.SpectrumNodeSelection selection = null;
                RunUI(() => selection = GraphSpectrum.SpectrumNodeSelection.GetCurrent(SkylineWindow));

                WaitForKoinaSpectrum(selection.NodeTranGroup, baseCE + i);

                if (!RecordData)
                    AssertIntensityAndIRTSpectrumCorrect((KoinaIntensityModel.PeptidePrecursorNCE)selection, client.QueryIndex);

                RunUI(() =>
                {
                    Assert.IsTrue(SkylineWindow.GraphSpectrum.NCEVisible);
                    Assert.IsFalse(SkylineWindow.GraphSpectrum.MirrorComboVisible);
                    Assert.AreEqual(Settings.Default.KoinaNCE, SkylineWindow.GraphSpectrum.KoinaNCE);

                    // Change NCE and predict again
                    ++SkylineWindow.GraphSpectrum.KoinaNCE;

                    Assert.AreEqual(Settings.Default.KoinaNCE, SkylineWindow.GraphSpectrum.KoinaNCE);
                });

                WaitForKoinaSpectrum(selection.NodeTranGroup, baseCE + i + 1);

                if (!RecordData)
                    AssertIntensityAndIRTSpectrumCorrect((KoinaIntensityModel.PeptidePrecursorNCE)selection, client.QueryIndex);
            }
        }

        /*public PeptidePrecursorPair GetSelectedPeptidePair()
        {
            TreeNodeMS treeNodeMS = null;
            RunUI(() => treeNodeMS = SkylineWindow.SelectedNode);
            var node = treeNodeMS as PeptideTreeNode;
            Assert.IsNotNull(node);
            var pep = node.Model as PeptideDocNode;
            Assert.IsNotNull(pep);
            var precursor = pep.TransitionGroups.First();

            return new PeptidePrecursorPair(pep, precursor);
        }*/

        public void AssertIntensityAndIRTSpectrumCorrect(KoinaIntensityModel.PeptidePrecursorNCE peptidePrecursorNCE, int index)
        {
            var client = (FakeKoinaPredictionClient) KoinaPredictionClient.Current;

            // We are interested in the queries just processed
            index -= 2;

            SpectrumDisplayInfo spectrumDisplayInfo = null;
            RunUI(() => spectrumDisplayInfo = SkylineWindow.GraphSpectrum.KoinaSpectrum);
            Assert.IsNotNull(spectrumDisplayInfo);

            // There need to be at least two queries (ms2, irt)
            AssertEx.IsGreaterThanOrEqual(client.ExpectedQueries.Count - index, 2);

            Assert.IsTrue(KoinaQuery.TryGetQuery(client.ExpectedQueries, SkylineWindow.Document.Settings,
                new[] { peptidePrecursorNCE }, Settings.Default.KoinaNCE,
                out var intensityQuery));
            intensityQuery.AssertMatchesSpectrum(peptidePrecursorNCE, spectrumDisplayInfo);

            Assert.IsTrue(KoinaQuery.TryGetQuery(client.ExpectedQueries, SkylineWindow.Document.Settings,
                new[] { new KoinaRetentionTimeModel.PeptideDocNodeWrapper(peptidePrecursorNCE.NodePep) },
                out var rtQuery));
            rtQuery.AssertMatchesSpectrum(spectrumDisplayInfo);
        }

        public void WaitForKoinaSpectrum(TransitionGroupDocNode precursor, int nce)
        {
            WaitForConditionUI(() =>
            {
                if (!SkylineWindow.GraphSpectrum.HasSpectrum ||
                    !(SkylineWindow.GraphSpectrum.KoinaSpectrum?.SpectrumInfo is SpectrumInfoKoina info))
                    return false;
                return ReferenceEquals(info.Precursor, precursor) && info.NCE == nce;
            });
        }

        public void TestLiveKoinaMirrorPlots()
        {
            var expectedPropertiesDict = new[]{
                new Dictionary<string, object>
                {
                    { "LibraryName", "Rat (NIST) (Rat_plasma2) (Rat_plasma)" },
                    { "PrecursorMz", 574.7844.ToString( CultureInfo.CurrentCulture) },
                    { "Charge", "2" },
                    { "Label", "light" },
                    { "SpectrumCount", "93" },
                    { "PeakCount","169"},
                    { "MirrorPeakCount","25"},
                    { "TotalIC", (1.0071E+5).ToString(@"0.0000E+0", CultureInfo.CurrentCulture)},
                    { "MirrorTotalIC",(6.1699E+4).ToString(@"0.0000E+0", CultureInfo.CurrentCulture)},
                    { "KoinaDotpMatch", string.Format(GraphsResources.GraphSpectrum_DoUpdate_dotp___0_0_0000_, 0.7402) },
                    { "KoinaDotpMatchFull", string.Format(GraphsResources.GraphSpectrum_DoUpdate_dotp___0_0_0000_, 0.7396) }
                },
                new Dictionary<string, object> {
                    {"LibraryName","Rat (NIST) (Rat_plasma2) (Rat_plasma)"},
                    {"PrecursorMz",710.8752.ToString( CultureInfo.CurrentCulture)},
                    {"Charge","2"},
                    {"Label","light"},
                    {"SpectrumCount","4"},
                    {"PeakCount","118"},
                    {"MirrorPeakCount","27"},
                    {"TotalIC", (1.9350E+5).ToString(@"0.0000E+0", CultureInfo.CurrentCulture)},
                    {"MirrorTotalIC", (5.3983E+4).ToString(@"0.0000E+0", CultureInfo.CurrentCulture)},
                    {"KoinaDotpMatch",string.Format(GraphsResources.GraphSpectrum_DoUpdate_dotp___0_0_0000_, 0.7349)},
                    {"KoinaDotpMatchFull",string.Format(GraphsResources.GraphSpectrum_DoUpdate_dotp___0_0_0000_, 0.6997)}
                }
            };

            var client = (FakeKoinaPredictionClient) KoinaPredictionClient.Current;

            // Enable mirror plots
            Settings.Default.LibMatchMirror = true;

            // Reset NCE
            Settings.Default.KoinaNCE = 27;

            // TODO: maybe somehow make checks more specific, since the checks here are very similar to
            // checks for the regular spectra
            for (var i = 1; i < 3; ++i)
            {
                // Select the i'th peptide. Only peptides 1 and 2 have library info
                SelectNode(SrmDocument.Level.Molecules, i);
                GraphSpectrum.SpectrumNodeSelection selection = null;
                RunUI(() => selection = GraphSpectrum.SpectrumNodeSelection.GetCurrent(SkylineWindow));
                WaitForKoinaSpectrum(selection.NodeTranGroup, Settings.Default.KoinaNCE);
                // These are the same if we are not displaying a mirror plot
                RunUI(() => Assert.AreNotSame(SkylineWindow.SelectedSpectrum, SkylineWindow.GraphSpectrum.KoinaSpectrum));

                if (!RecordData)
                    AssertIntensityAndIRTSpectrumCorrect((KoinaIntensityModel.PeptidePrecursorNCE)selection, client.QueryIndex);

                RunUI(() => { SkylineWindow.GraphSpectrum.ShowPropertiesSheet = true; });
                var graphExtension = SkylineWindow.GraphSpectrum.MsGraphExtension;
                WaitForConditionUI(() => graphExtension.PropertiesVisible);
                var currentProperties = graphExtension.PropertiesSheet.SelectedObject as SpectrumProperties;
                Assert.IsNotNull(currentProperties);
                // if property values change use the statement below to generate a new set.
                //Trace.Write(currentProperties.Serialize());
                var expectedProperties = new SpectrumProperties();
                expectedProperties.Deserialize(expectedPropertiesDict[i-1]);
                Assert.IsTrue(expectedProperties.IsSameAs(currentProperties));
            }
        }
    }

}