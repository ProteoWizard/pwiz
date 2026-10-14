/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.BiblioSpec;
using pwiz.Common.Database;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Builds libraries whose .blib carries global (experiment-wide) q-values, checks that Skyline
    /// reads them, and checks that the Library Explorer's global cutoff decides which precursors
    /// Add All puts in the document. DIA-NN supplies precursor and protein-group q-values on both
    /// sides of the cutoff. Proteome Discoverer supplies peptide-group values in both of its file
    /// formats; its protein q-values in these files are all 0, so DIA-NN covers the protein cutoff.
    /// </summary>
    [TestClass]
    public class GlobalQValueFilterTest : AbstractFunctionalTestEx
    {
        private const string LIBRARY_NAME = "GlobalQValues";
        private const string DIANN_SPECLIB = @"diann\diann-hela-diapasef-lib.speclib";
        private const string PD_RESULT = @"pd\230807_P1_Neo_ES904_TMTProPrecision_1ug_DIA1Th_HCD30_Survey.pdResult";
        private const string PD_LEGACY_RESULT = @"pd-legacy\example.pdResult";

        // The DIA-NN build keeps entries a 0.01 cutoff removes. BlibBuild applies the build threshold
        // to DIA-NN's global precursor q-value, so a library built at 0.01 leaves nothing to filter.
        private const double DIANN_BUILD_THRESHOLD = 0.05;
        private const double CUTOFF = 0.01;
        // Every peptide q-value in the Proteome Discoverer test files is below 0.01
        private const double PEPTIDE_CUTOFF = 0.001;

        private Action _test;

        [TestMethod]
        public void TestDiannGlobalQValueFilter()
        {
            RunGlobalQValueTest(TestDiann);
        }

        [TestMethod]
        public void TestDiannGlobalValuesSurviveMinimize()
        {
            RunGlobalQValueTest(TestDiannMinimize);
        }

        [TestMethod]
        public void TestDiannGlobalQValueFilterDocument()
        {
            RunGlobalQValueTest(TestDiannFilterDocument);
        }

        [TestMethod]
        public void TestPdResultGlobalValues()
        {
            // One peptide in this result has ambiguous matches, which the build reports
            RunGlobalQValueTest(() => TestPdResult(PD_RESULT, @"qValue", @"PEP", true,
                UniMod.GetModification("TMTpro (K)", true), UniMod.GetModification("TMTpro (N-term)", true)));
        }

        [TestMethod]
        public void TestPdResultLegacyGlobalValues()
        {
            RunGlobalQValueTest(() => TestPdResult(PD_LEGACY_RESULT, @"Qvalityqvalue", @"QvalityPEP", false));
        }

        protected override void DoTest()
        {
            _test();
        }

        private void TestDiann()
        {
            var library = BuildLibrary(DIANN_SPECLIB, DIANN_BUILD_THRESHOLD, false);
            AddModifications(OxidationVariable());
            var entries = ReadLibraryEntries();
            AssertLibraryValues(library, entries);

            var viewLibUI = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() => CollectionAssert.AreEqual(new[] { GlobalQValueLevel.protein, GlobalQValueLevel.precursor },
                viewLibUI.QValueFilterControls.Levels.ToArray()));

            AddAllAndVerify(viewLibUI, entries, GlobalQValueLevel.precursor, CUTOFF, false, true);
            RunUI(SkylineWindow.Undo);
            AddAllAndVerify(viewLibUI, entries, GlobalQValueLevel.protein, CUTOFF, true, true);
            OkDialog(viewLibUI, viewLibUI.CancelDialog);
        }

        /// <summary>
        /// Sharing the document as minimal rewrites its library, which must keep each remaining
        /// entry's global values and library protein groups.
        /// </summary>
        private void TestDiannMinimize()
        {
            var library = BuildLibrary(DIANN_SPECLIB, DIANN_BUILD_THRESHOLD, false);
            AddModifications(OxidationVariable());
            var entries = ReadLibraryEntries();
            var viewLibUI = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            AddAllAndVerify(viewLibUI, entries, GlobalQValueLevel.protein, CUTOFF, true, true);
            OkDialog(viewLibUI, viewLibUI.CancelDialog);

            var minimizedZip = TestFilesDir.GetTestPath(@"Minimized.sky.zip");
            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.SaveDocument(TestFilesDir.GetTestPath(@"BeforeMinimize.sky")));
                Assert.IsTrue(SkylineWindow.ShareDocument(minimizedZip, ShareType.MINIMAL));
                Assert.IsTrue(SkylineWindow.OpenSharedFile(minimizedZip));
            });
            var document = WaitForDocumentLoaded();
            var minimized = document.Settings.PeptideSettings.Libraries.Libraries.Single();
            CollectionAssert.AreEqual(library.GlobalQValueLevels.ToArray(), minimized.GlobalQValueLevels.ToArray());

            var entriesByPrecursor = entries.ToDictionary(entry => entry.PrecursorKey);
            int precursorCount = 0;
            foreach (var nodePep in document.Peptides)
            {
                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    var entry = entriesByPrecursor[LibraryEntry.MakePrecursorKey(nodePep.ModifiedTarget.Sequence,
                        nodeGroup.PrecursorCharge)];
                    var scores = minimized.GetGlobalScores(new LibKey(entry.ModifiedSequence, entry.Charge));
                    Assert.AreEqual(entry.PrecursorQValue, scores.PrecursorQValue, entry.ToString());
                    CollectionAssert.AreEquivalent(entry.ProteinGroups.ToList(),
                        scores.ProteinGroups.Select(group => new KeyValuePair<string, double>(group.Name, group.QValue ?? -1)).ToList(),
                        entry.ToString());
                    precursorCount++;
                }
            }
            Assert.AreNotEqual(0, precursorCount);
        }

        /// <summary>
        /// The filter the Import FASTA page applies: a peptide is kept when one of its library
        /// precursors passes the cutoff, with its passing library precursors and every precursor no
        /// library contains, such as the heavy-labeled ones.
        /// </summary>
        private void TestDiannFilterDocument()
        {
            BuildLibrary(DIANN_SPECLIB, DIANN_BUILD_THRESHOLD, false);
            var entries = ReadLibraryEntries();
            RunUI(() => SkylineWindow.ModifyDocument("Pick library peptides with heavy labels", doc =>
                doc.ChangeSettings(doc.Settings
                    .ChangePeptideLibraries(libraries => libraries.ChangePick(PeptidePick.library))
                    .ChangePeptideModifications(mods => mods
                        .ChangeStaticModifications(mods.StaticModifications.Append(OxidationVariable()).ToList())
                        .ChangeModifications(IsotopeLabelType.heavy, new[]
                        {
                            UniMod.GetModification("Label:13C(6)15N(2) (C-term K)", false),
                            UniMod.GetModification("Label:13C(6)15N(4) (C-term R)", false)
                        })))));
            var fasta = MakeFasta(entries);
            var docBefore = SkylineWindow.Document;
            // Some made-up proteins digest to no library peptide; drop them without asking
            RunUI(() => SkylineWindow.ImportFasta(new StringReader(fasta), fasta.Split('\n').Length, false,
                Resources.SkylineWindow_ImportFastaFile_Import_FASTA, new SkylineWindow.ImportFastaInfo(false, fasta), false));
            var document = WaitForDocumentChangeLoaded(docBefore);

            var filter = new GlobalQValueFilter(GlobalQValueLevel.precursor, CUTOFF);
            var filtered = filter.FilterDocument(document, document.PeptideGroups);

            var entriesByPrecursor = entries.ToDictionary(entry => entry.PrecursorKey);
            var keptPeptides = filtered.Peptides.ToDictionary(nodePep => nodePep.Id.GlobalIndex);
            int removedWithHeavy = 0, keptWithHeavy = 0;
            foreach (var nodePep in document.Peptides)
            {
                // Heavy precursors are never in the library
                var listed = nodePep.TransitionGroups.Where(nodeGroup => nodeGroup.LabelType.IsLight &&
                    entriesByPrecursor.ContainsKey(LibraryEntry.MakePrecursorKey(nodePep.ModifiedTarget.Sequence, nodeGroup.PrecursorCharge)))
                    .ToList();
                var passing = listed.Where(nodeGroup => entriesByPrecursor[LibraryEntry.MakePrecursorKey(
                    nodePep.ModifiedTarget.Sequence, nodeGroup.PrecursorCharge)].PassesCutoff(GlobalQValueLevel.precursor, CUTOFF)).ToList();
                bool hasHeavy = nodePep.TransitionGroups.Any(nodeGroup => !nodeGroup.LabelType.IsLight);
                var expected = listed.Count == 0
                    ? nodePep.TransitionGroups.ToList()
                    : passing.Count == 0
                        ? new List<TransitionGroupDocNode>()
                        : nodePep.TransitionGroups.Where(nodeGroup => passing.Contains(nodeGroup) || !listed.Contains(nodeGroup)).ToList();
                keptPeptides.TryGetValue(nodePep.Id.GlobalIndex, out var keptPep);
                var kept = keptPep?.TransitionGroups.ToList() ?? new List<TransitionGroupDocNode>();
                CollectionAssert.AreEquivalent(expected.Select(nodeGroup => nodeGroup.Id.GlobalIndex).ToList(),
                    kept.Select(nodeGroup => nodeGroup.Id.GlobalIndex).ToList(), nodePep.ModifiedSequenceDisplay);
                if (hasHeavy && listed.Count > 0)
                {
                    if (passing.Count == 0)
                        removedWithHeavy++;
                    else
                        keptWithHeavy++;
                }
            }
            Assert.AreNotEqual(0, removedWithHeavy, "No failing peptide had a heavy precursor to remove.");
            Assert.AreNotEqual(0, keptWithHeavy, "No passing peptide had a heavy precursor to keep.");
        }

        /// <summary>
        /// A FASTA with one protein per library protein group, made of the group's peptides end to end,
        /// so a tryptic digest gives back the library's peptides.
        /// </summary>
        private static string MakeFasta(IEnumerable<LibraryEntry> entries)
        {
            var peptidesByProtein = new Dictionary<string, SortedSet<string>>();
            foreach (var entry in entries)
            {
                // A peptide that starts with P would not be cleaved from the one before it
                var protein = entry.ProteinGroups.Keys.FirstOrDefault();
                if (protein == null || entry.Sequence.StartsWith(@"P"))
                    continue;
                if (!peptidesByProtein.TryGetValue(protein, out var peptides))
                    peptidesByProtein.Add(protein, peptides = new SortedSet<string>());
                peptides.Add(entry.Sequence);
            }
            return string.Concat(peptidesByProtein.Select(kvp =>
                @">" + kvp.Key + Environment.NewLine + string.Concat(kvp.Value) + Environment.NewLine));
        }

        private void TestPdResult(string pdResult, string qValueColumn, string pepColumn, bool hasAmbiguousMatches,
            params StaticMod[] mods)
        {
            var library = BuildLibrary(pdResult, null, hasAmbiguousMatches);
            AddModifications(mods.Append(OxidationVariable()).ToArray());
            var entries = ReadLibraryEntries();
            AssertLibraryValues(library, entries);
            AssertMatchesProteomeDiscoverer(entries, TestFilesDir.GetTestPath(pdResult), qValueColumn, pepColumn);

            var viewLibUI = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() => CollectionAssert.AreEqual(new[] { GlobalQValueLevel.protein, GlobalQValueLevel.peptide },
                viewLibUI.QValueFilterControls.Levels.ToArray()));

            AddAllAndVerify(viewLibUI, entries, GlobalQValueLevel.peptide, PEPTIDE_CUTOFF, true, true);
            OkDialog(viewLibUI, viewLibUI.CancelDialog);
        }

        private void RunGlobalQValueTest(Action test)
        {
            TestFilesZip = @"TestFunctional\GlobalQValueFilterTest.zip";
            _test = test;
            RunFunctionalTest();
        }

        private static StaticMod OxidationVariable()
        {
            return UniMod.GetModification("Oxidation (M)", true).ChangeVariable(true);
        }

        /// <summary>
        /// Adds the modifications the library uses to the document, so every entry matches a peptide.
        /// </summary>
        private static void AddModifications(params StaticMod[] mods)
        {
            RunUI(() => SkylineWindow.ModifyDocument("Add library modifications", doc =>
                doc.ChangeSettings(doc.Settings.ChangePeptideModifications(modifications =>
                    modifications.ChangeStaticModifications(modifications.StaticModifications.Concat(mods).ToList())))));
            var documentMods = SkylineWindow.Document.Settings.PeptideSettings.Modifications.StaticModifications;
            foreach (var mod in mods)
                Assert.IsTrue(documentMods.Contains(mod), mod.Name);
        }

        /// <summary>
        /// Builds a library from one search result through the Build Library dialog, choosing the
        /// file with the native Open dialog, and returns it once the document has loaded it. A null
        /// threshold keeps the dialog's default.
        /// </summary>
        private Library BuildLibrary(string inputFile, double? qValueThreshold, bool hasAmbiguousMatches)
        {
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettingsUI.ShowBuildLibraryDlg);
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = LIBRARY_NAME;
                buildLibraryDlg.LibraryPath = LibraryPath;
                buildLibraryDlg.OkWizardPage();
            });
            RunLongNativeDlg<NativeOpenFileDialog>(buildLibraryDlg.ClickAddFile, dlg =>
            {
                dlg.EnterPath(TestFilesDir.GetTestPath(inputFile));
                dlg.Accept();
            });
            WaitForConditionUI(() => buildLibraryDlg.Grid.ScoreTypesLoaded);
            if (qValueThreshold.HasValue)
            {
                RunUI(() => buildLibraryDlg.Grid.SetScoreThreshold(scoreType =>
                    scoreType.ProbabilityType == ScoreType.EnumProbabilityType.probability_incorrect
                        ? qValueThreshold.Value
                        : scoreType.DefaultValue));
            }
            OkDialog(buildLibraryDlg, buildLibraryDlg.OkWizardPage);
            if (hasAmbiguousMatches)
            {
                var ambiguousDlg = WaitForOpenForm<MessageDlg>();
                OkDialog(ambiguousDlg, ambiguousDlg.OkDialog);
            }

            WaitForConditionUI(() => peptideSettingsUI.AvailableLibraries.Contains(LIBRARY_NAME) &&
                                     !peptideSettingsUI.IsBuildingLibrary);
            RunUI(() => peptideSettingsUI.PickedLibraries = new[] { LIBRARY_NAME });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            var document = WaitForDocumentLoaded();
            return document.Settings.PeptideSettings.Libraries.Libraries.Single();
        }

        private string LibraryPath
        {
            get { return TestFilesDir.GetTestPath(LIBRARY_NAME + BiblioSpecLiteSpec.EXT); }
        }

        /// <summary>
        /// Reads every entry's global values straight from the built .blib, as the expected values
        /// for what Skyline reads and filters.
        /// </summary>
        private List<LibraryEntry> ReadLibraryEntries()
        {
            using var connection = OpenReadOnly(LibraryPath);
            string Optional(string column) =>
                SqliteOperations.ColumnExists(connection, @"RefSpectra", column) ? column : @"NULL";
            var entries = new Dictionary<int, LibraryEntry>();
            using (var command = new SQLiteCommand(
                       @"SELECT id, peptideSeq, peptideModSeq, precursorCharge, " +
                       Optional(@"PrecursorQValue") + @", " + Optional(@"PeptideQValue") + @", " + Optional(@"PeptidePep") +
                       @" FROM RefSpectra", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    entries.Add(reader.GetInt32(0), new LibraryEntry(reader.GetString(1), reader.GetString(2),
                        reader.GetInt32(3), GetNullableDouble(reader, 4), GetNullableDouble(reader, 5),
                        GetNullableDouble(reader, 6)));
                }
            }
            using (var command = new SQLiteCommand(
                       @"SELECT rp.RefSpectraId, p.accession, p.QValue FROM RefSpectraProteins rp " +
                       @"JOIN Proteins p ON p.id = rp.ProteinId", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    entries[reader.GetInt32(0)].ProteinGroups.Add(reader.GetString(1), reader.GetDouble(2));
            }
            return entries.Values.ToList();
        }

        /// <summary>
        /// Checks that Skyline's library reports the values the .blib holds, for every entry.
        /// </summary>
        private static void AssertLibraryValues(Library library, IList<LibraryEntry> entries)
        {
            Assert.AreEqual(entries.Count, library.Keys.Count());
            foreach (var entry in entries)
            {
                var scores = library.GetGlobalScores(new LibKey(entry.ModifiedSequence, entry.Charge));
                Assert.AreEqual(entry.PrecursorQValue, scores.PrecursorQValue, entry.ToString());
                Assert.AreEqual(entry.PeptideQValue, scores.PeptideQValue, entry.ToString());
                Assert.AreEqual(entry.PeptidePep, scores.PeptidePep, entry.ToString());
                CollectionAssert.AreEquivalent(entry.ProteinGroups.ToList(),
                    scores.ProteinGroups.Select(group => new KeyValuePair<string, double>(group.Name, group.QValue ?? -1)).ToList(),
                    entry.ToString());
            }
        }

        /// <summary>
        /// Checks the library against the Proteome Discoverer result it was built from: each entry
        /// has its peptide group's q-value and PEP, and each of its protein groups is a master protein
        /// with its experiment q-value. PD treats I and L as the same residue when it groups peptides.
        /// </summary>
        private static void AssertMatchesProteomeDiscoverer(IList<LibraryEntry> entries, string pdResultPath,
            string qValueColumn, string pepColumn)
        {
            var peptideGroupValues = new Dictionary<string, HashSet<Tuple<double, double>>>();
            var masterProteinQValues = new Dictionary<string, double>();
            using (var connection = OpenReadOnly(pdResultPath))
            {
                using (var command = new SQLiteCommand(
                           @"SELECT Sequence, " + qValueColumn + @", " + pepColumn + @" FROM TargetPeptideGroups", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var sequence = IsoleucineAsLeucine(reader.GetString(0));
                        if (!peptideGroupValues.TryGetValue(sequence, out var values))
                            peptideGroupValues.Add(sequence, values = new HashSet<Tuple<double, double>>());
                        values.Add(Tuple.Create(reader.GetDouble(1), reader.GetDouble(2)));
                    }
                }
                using (var command = new SQLiteCommand(
                           @"SELECT Accession, Expqvalue FROM TargetProteins WHERE IsMasterProtein = 0", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        masterProteinQValues.Add(reader.GetString(0), ReadPdNullableDouble(reader.GetValue(1)));
                }
            }

            foreach (var entry in entries)
            {
                Assert.IsTrue(peptideGroupValues.TryGetValue(IsoleucineAsLeucine(entry.Sequence), out var values),
                    entry.ToString());
                Assert.IsTrue(entry.PeptideQValue.HasValue && entry.PeptidePep.HasValue, entry.ToString());
                Assert.IsTrue(values.Contains(Tuple.Create(entry.PeptideQValue.Value, entry.PeptidePep.Value)),
                    entry.ToString());
                // An entry whose protein failed PD's protein FDR has no group
                foreach (var proteinGroup in entry.ProteinGroups)
                    Assert.AreEqual(masterProteinQValues[proteinGroup.Key], proteinGroup.Value, entry.ToString());
            }
        }

        /// <summary>
        /// Runs Add All with the global cutoff and checks the document: exactly the passing entries
        /// become precursors, and with Associate proteins each one sits under its library protein groups.
        /// </summary>
        private void AddAllAndVerify(ViewLibraryDlg viewLibUI, IList<LibraryEntry> entries,
            GlobalQValueLevel level, double cutoff, bool associateProteins, bool expectFilterDialog)
        {
            var passing = entries.Where(entry => entry.PassesCutoff(level, cutoff)).ToList();
            Assert.AreNotEqual(0, passing.Count);
            Assert.AreNotEqual(entries.Count, passing.Count, "The cutoff must remove some entries to test anything.");

            RunUI(() =>
            {
                viewLibUI.QValueFilterControls.Level = level;
                viewLibUI.QValueFilterControls.CutoffText = cutoff.ToString(CultureInfo.CurrentCulture);
                viewLibUI.AssociateMatchingProteins = associateProteins;
            });
            var docBefore = WaitForProteinMetadataBackgroundLoaderCompletedUI();
            int unmatchedCount = -1;
            Action<MultiButtonMsgDlg> confirmAddAll = confirmDlg =>
            {
                unmatchedCount = (int) confirmDlg.Tag;
                confirmDlg.Btn1Click();
            };
            if (expectFilterDialog)
            {
                // Some precursors are outside the document's filter, or share protein groups
                var filterDlg = ShowDialog<FilterMatchedPeptidesDlg>(() => viewLibUI.AddAllPeptides());
                RunUI(() =>
                {
                    filterDlg.DuplicateProteinsFilter = BackgroundProteome.DuplicateProteinsFilter.AddToAll;
                    filterDlg.AddUnmatched = true;
                    filterDlg.AddFiltered = true;
                });
                RunDlg(filterDlg.OkDialog, confirmAddAll);
            }
            else
            {
                RunDlg(() => viewLibUI.AddAllPeptides(), confirmAddAll);
            }
            var document = WaitForDocumentChange(docBefore);

            var entriesByPrecursor = entries.ToDictionary(entry => entry.PrecursorKey);
            var documentPrecursors = new HashSet<string>();
            foreach (var nodePepGroup in document.PeptideGroups)
            {
                foreach (var nodePep in nodePepGroup.Peptides)
                {
                    foreach (var nodeGroup in nodePep.TransitionGroups)
                    {
                        var precursorKey = LibraryEntry.MakePrecursorKey(nodePep.ModifiedTarget.Sequence,
                            nodeGroup.PrecursorCharge);
                        Assert.IsTrue(entriesByPrecursor.TryGetValue(precursorKey, out var entry), precursorKey);
                        Assert.IsTrue(entry.PassesCutoff(level, cutoff), entry.ToString());
                        if (associateProteins)
                        {
                            // An entry with no protein group goes in the unmatched peptides list
                            Assert.IsTrue(entry.ProteinGroups.Count == 0
                                    ? IsUnmatchedList(nodePepGroup)
                                    : entry.ProteinGroups.ContainsKey(nodePepGroup.Name),
                                entry + @" in " + nodePepGroup.Name);
                        }
                        documentPrecursors.Add(precursorKey);
                    }
                }
            }
            var missing = passing.Select(entry => entry.PrecursorKey).Where(key => !documentPrecursors.Contains(key)).ToList();
            Assert.AreEqual(0, missing.Count, @"Missing from the document: " + string.Join(@", ", missing));
            Assert.AreEqual(0, unmatchedCount);
            CollectionAssert.AreEquivalent(passing.Select(entry => entry.PrecursorKey).ToList(), documentPrecursors.ToList());
            if (associateProteins)
            {
                CollectionAssert.AreEquivalent(passing.SelectMany(entry => entry.ProteinGroups.Keys).Distinct().ToList(),
                    document.PeptideGroups.Where(nodePepGroup => !IsUnmatchedList(nodePepGroup))
                        .Select(nodePepGroup => nodePepGroup.Name).ToList());
            }
        }

        /// <summary>
        /// Library protein groups are peptide lists too, so the list of entries with no protein
        /// group is told apart by its name.
        /// </summary>
        private static bool IsUnmatchedList(PeptideGroupDocNode nodePepGroup)
        {
            return nodePepGroup.Name == SettingsUIResources.ViewLibraryPepMatching_AddPeptidesToLibraryGroup_Library_Peptides;
        }

        private static SQLiteConnection OpenReadOnly(string path)
        {
            var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder
                { DataSource = path, ReadOnly = true, Pooling = false }.ToString());
            connection.Open();
            return connection;
        }

        private static double? GetNullableDouble(SQLiteDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? (double?) null : reader.GetDouble(ordinal);
        }

        /// <summary>
        /// Reads a Proteome Discoverer nullable double: a REAL, or 8 little-endian bytes followed by a
        /// has-value flag.
        /// </summary>
        private static double ReadPdNullableDouble(object value)
        {
            if (value is byte[] blob)
            {
                Assert.AreEqual(sizeof(double) + 1, blob.Length);
                Assert.AreNotEqual(0, blob[sizeof(double)]);
                return BinaryPrimitives.ReadDoubleLittleEndian(blob);
            }
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static string IsoleucineAsLeucine(string sequence)
        {
            return sequence.ToUpperInvariant().Replace('I', 'L');
        }

        private class LibraryEntry
        {
            // A modification mass in brackets, e.g. [+57.021464] or [+57.0]
            private static readonly Regex MOD_MASS = new Regex(@"\[([+-]?\d+(?:\.\d+)?)\]");

            /// <summary>
            /// A precursor key that does not depend on how precisely a modification mass is written.
            /// </summary>
            public static string MakePrecursorKey(string modifiedSequence, int charge)
            {
                var sequence = MOD_MASS.Replace(modifiedSequence, match =>
                    string.Format(CultureInfo.InvariantCulture, @"[{0:+0.0;-0.0}]",
                        double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)));
                return sequence + @"/" + charge.ToString(CultureInfo.InvariantCulture);
            }

            public LibraryEntry(string sequence, string modifiedSequence, int charge, double? precursorQValue,
                double? peptideQValue, double? peptidePep)
            {
                Sequence = sequence;
                ModifiedSequence = modifiedSequence;
                Charge = charge;
                PrecursorQValue = precursorQValue;
                PeptideQValue = peptideQValue;
                PeptidePep = peptidePep;
                ProteinGroups = new Dictionary<string, double>();
            }

            public string Sequence { get; }
            public string ModifiedSequence { get; }
            public int Charge { get; }
            public double? PrecursorQValue { get; }
            public double? PeptideQValue { get; }
            public double? PeptidePep { get; }
            public Dictionary<string, double> ProteinGroups { get; }

            public string PrecursorKey
            {
                get { return MakePrecursorKey(ModifiedSequence, Charge); }
            }

            public bool PassesCutoff(GlobalQValueLevel level, double cutoff)
            {
                double? qValue;
                switch (level)
                {
                    case GlobalQValueLevel.precursor:
                        qValue = PrecursorQValue;
                        break;
                    case GlobalQValueLevel.peptide:
                        qValue = PeptideQValue;
                        break;
                    default:
                        qValue = ProteinGroups.Count == 0 ? (double?) null : ProteinGroups.Values.Min();
                        break;
                }
                return qValue.HasValue && qValue.Value <= cutoff;
            }

            public override string ToString()
            {
                return PrecursorKey;
            }
        }
    }
}
