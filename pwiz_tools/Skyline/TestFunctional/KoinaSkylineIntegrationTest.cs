/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
                TestInvalidPepSequences(); // Do this at the end, otherwise it messes with the order of nodes
                //var expected = RecordData ? 0 : QUERIES.Count;
                //Assert.AreEqual(expected, ((FakeKoinaPredictionClient)KoinaPredictionClient.Current).QueryIndex);

                TestKoinaLibraryBuild(true);
            }
            KoinaConstants.CACHE_PREV_PREDICTION = true;
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