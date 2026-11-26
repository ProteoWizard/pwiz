/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.BiblioSpec;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Builds a library from a Spectronaut report export (<c>.sn-report.zip</c>) and imports its
    /// peptides. Spectronaut cannot be run here the way DiannSearchTest runs DIA-NN, so the export
    /// stands in for the search: the container holds the library and search parquet files
    /// Spectronaut writes, and BlibBuild reads both in place through DiaNNSpecLibReader.
    /// </summary>
    [TestClass]
    public class SpectronautImportTest : AbstractFunctionalTest
    {
        private const string LIBRARY_NAME = "SpectronautLibrary";

        /// <summary>Precursors in the export, over 25 distinct modified sequences.</summary>
        private const int PRECURSOR_COUNT = 27;
        private const int PEPTIDE_COUNT = 25;

        [TestMethod]
        public void TestSpectronautImport()
        {
            TestFilesZip = @"TestFunctional\SpectronautImportTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            BuildLibrary();
            VerifyLibrary();
            VerifyPeptidesImport();
        }

        private void BuildLibrary()
        {
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                // The export has peptides up to 31 residues; the default 25 would drop 6 of them
                // when they are added to the document below.
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Filter;
                peptideSettingsUi.TextMaxLength = 50;
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Library;
            });

            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettingsUi.ShowBuildLibraryDlg);
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = LIBRARY_NAME;
                buildLibraryDlg.LibraryPath = TestFilesDir.GetTestPath(LIBRARY_NAME + BiblioSpecLiteSpec.EXT);
                buildLibraryDlg.OkWizardPage();
                // The container itself is the input - neither parquet file is named or extracted.
                buildLibraryDlg.AddInputFiles(new[] { TestFilesDir.GetTestPath("sgs-sample1.sn-report.zip") });
            });
            // Reading the score type runs BlibBuild against the container, so this covers Skyline
            // accepting the extension before the build itself does.
            WaitForConditionUI(() => buildLibraryDlg.Grid.ScoreTypesLoaded);
            RunUI(() => AssertEx.AreEqual(ScoreType.GENERIC_QVALUE,
                buildLibraryDlg.Grid.Files.First().ScoreType.NameInvariant));

            OkDialog(buildLibraryDlg, buildLibraryDlg.OkWizardPage);
            WaitForConditionUI(() => peptideSettingsUi.PickedLibraries.Contains(LIBRARY_NAME));
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            WaitForDocumentLoaded();
        }

        private void VerifyLibrary()
        {
            RunUI(() =>
            {
                var libraries = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
                AssertEx.IsTrue(libraries.IsLoaded);
                AssertEx.AreEqual(1, libraries.Libraries.Count);

                var library = libraries.Libraries[0];
                AssertEx.AreEqual(PRECURSOR_COUNT, library.SpectrumCount);

                var keys = library.Keys.ToList();
                AssertEx.AreEqual(PRECURSOR_COUNT, keys.Count);
                var peptideKeys = keys.Select(k => (PeptideLibraryKey) k.LibraryKey).ToList();
                AssertEx.AreEqual(PEPTIDE_COUNT, peptideKeys.Select(k => k.ModifiedSequence).Distinct().Count());

                // Spectronaut exports charges outside the 2,3 the document filters to by default.
                CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 },
                    keys.Select(k => k.Charge).Distinct().OrderBy(c => c).ToArray());

                // Mods arrive as Unimod titles ("[Carbamidomethyl (C)]") rather than delta masses,
                // so these assert the title lookup resolved them to the right mass and position.
                // The masses are the full-precision Unimod values, not the rounded forms the
                // library's own peptideModSeq column shows.
                var modifiedSequences = new HashSet<string>(peptideKeys.Select(k => k.ModifiedSequence));
                var expected = new[]
                {
                    "AAAHHYGAQC[+57.02146]DKPNKEFMLC[+57.02146]R",   // two Carbamidomethyl
                    "ADM[+15.99491]VIEAVFEDLSLK",                    // Oxidation
                    "A[+42.01056]AAAAAAAAAGAAGGR",                   // N-terminal Acetyl
                };
                var missing = expected.Where(s => !modifiedSequences.Contains(s)).ToArray();
                AssertEx.IsTrue(missing.Length == 0,
                    $@"missing {string.Join(@", ", missing)} from {string.Join(@" ", modifiedSequences.OrderBy(s => s))}");

                // Retention times come from the search parquet, the second file in the container,
                // so spectra without times would mean only the first entry had been read.
                var liteLibrary = (BiblioSpecLiteLibrary) library;
                AssertEx.AreEqual(3, liteLibrary.ListRetentionTimeSources().Count);
            });
        }

        private void VerifyPeptidesImport()
        {
            var transitionSettingsUi = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.Filter;
                transitionSettingsUi.PrecursorCharges = "1,2,3,4";
            });
            OkDialog(transitionSettingsUi, transitionSettingsUi.OkDialog);
            WaitForDocumentLoaded();

            var viewLibraryDlg = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            // The export's mods are not in the document's modification list yet, so the explorer
            // offers to add them before it can match anything.
            var addModificationsDlg = WaitForOpenForm<AddModificationsDlg>();
            OkDialog(addModificationsDlg, addModificationsDlg.OkDialogAll);

            var filterMatchedPeptidesDlg = ShowDialog<FilterMatchedPeptidesDlg>(viewLibraryDlg.AddAllPeptides);
            OkDialog(filterMatchedPeptidesDlg, filterMatchedPeptidesDlg.OkDialog);
            var confirmDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(confirmDlg, confirmDlg.OkDialog);
            OkDialog(viewLibraryDlg, viewLibraryDlg.Close);
            WaitForDocumentLoaded();

            RunUI(() =>
            {
                var document = SkylineWindow.Document;
                AssertEx.AreEqual(PEPTIDE_COUNT, document.PeptideCount);
                AssertEx.AreEqual(PRECURSOR_COUNT, document.PeptideTransitionGroupCount);
            });
        }
    }
}
