/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Filtering;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// End-to-end test that a <see cref="SpectrumClassFilter"/> on an uninterpreted mzML CV/user
    /// parameter (the "Phase 2" feature) actually restricts the spectra used for chromatogram
    /// extraction. Reuses the Ms1SpectrumFilterTest data, whose spectra carry base peak intensity
    /// (MS:1000505) and the Thermo filter string (MS:1000512) - terms Skyline does not interpret into
    /// its own fields, so they exercise the dynamic-column path rather than a typed property.
    /// </summary>
    [TestClass]
    public class CvSpectrumFilterTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestCvSpectrumFilter()
        {
            TestFilesZip = @"TestFunctional\Ms1SpectrumFilterTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Ms1SpectrumFilterTest.sky")));
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeTransitionGroupCount);
            Assert.IsTrue(SkylineWindow.Document.MoleculeTransitions.All(t => t.IsMs1));

            // A copy of the document as it is now, before any CV filter exists. Importing into this copy at
            // the end of the test gives a document whose cache captured no CV terms, which is the case the
            // scanner exists for and the only one where the head-of-file read answers the CV columns.
            // Copied on disk rather than saved through the window, which would be a Save As and would carry
            // the live document - and everything done to it below - along to the new path.
            File.Copy(TestFilesDir.GetTestPath("Ms1SpectrumFilterTest.sky"),
                TestFilesDir.GetTestPath("NoCvFilter.sky"));

            var precursorPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups, 0);

            // The Thermo filter string (MS:1000512) carries the FAIMS compensation voltage as text
            // ("cv=-50.00" / "cv=-70.00"), so two "contains" filters on it partition the MS1 spectra the
            // same way the interpreted CompensationVoltage does. A numeric filter on base peak intensity
            // (MS:1000505, reported in scientific notation, e.g. "5.49898375e05") that admits every
            // positive value matches them all - exercising the numeric path and the invariant value parse.
            var filterCv50 = StringCvFilter(@"cv=-50");
            var filterCv70 = StringCvFilter(@"cv=-70");
            var filterBpiAll = NumericBpiFilter(FilterOperations.OP_IS_GREATER_THAN, @"0");

            // For a CV term, blank means absent. The filter string term (MS:1000512) is present on every MS1
            // spectrum, so Is Not Blank on it matches them all; the zoom scan term (MS:1000497) is present
            // on none of this data, so Is Blank on it also matches them all. Both therefore reproduce the
            // unfiltered chromatogram, exercising the presence predicate through the real extraction
            // pipeline (including term capture).
            var filterPresent = BlanknessCvFilter(@"MS:1000512", @"filter string", FilterOperations.OP_IS_NOT_BLANK);
            var filterAbsent = BlanknessCvFilter(@"MS:1000497", @"zoom scan", FilterOperations.OP_IS_BLANK);

            RunUI(() =>
            {
                SkylineWindow.EditMenu.ChangeSpectrumFilter(new[] { precursorPath }, filterCv50, true);
                SkylineWindow.EditMenu.ChangeSpectrumFilter(new[] { precursorPath }, filterCv70, true);
                SkylineWindow.EditMenu.ChangeSpectrumFilter(new[] { precursorPath }, filterBpiAll, true);
                SkylineWindow.EditMenu.ChangeSpectrumFilter(new[] { precursorPath }, filterPresent, true);
                SkylineWindow.EditMenu.ChangeSpectrumFilter(new[] { precursorPath }, filterAbsent, true);
            });
            Assert.AreEqual(6, SkylineWindow.Document.MoleculeTransitionGroupCount);

            ImportResultsFile(TestFilesDir.GetTestPath("Ms1SpectrumFilterTest.mzML"));

            var document = SkylineWindow.Document;
            var peptideDocNode = document.Molecules.First();
            Assert.AreEqual(6, peptideDocNode.TransitionGroupCount);

            int Points(SpectrumClassFilter filter)
            {
                var transitionGroup = peptideDocNode.TransitionGroups.First(tg => Equals(tg.SpectrumClassFilter, filter));
                Assert.IsTrue(document.Settings.MeasuredResults.TryLoadChromatogram(0, peptideDocNode, transitionGroup,
                    (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance, out var infoSet));
                Assert.AreEqual(1, infoSet.Length);
                return infoSet[0].GetRawTransitionInfo(0).RawTimes.Count;
            }

            int unfilteredPoints = Points(default);
            int cv50Points = Points(filterCv50);
            int cv70Points = Points(filterCv70);
            Assert.AreNotEqual(0, cv50Points);
            Assert.AreNotEqual(0, cv70Points);
            // The two string CV filters partition the MS1 spectra: together they match exactly the
            // unfiltered set. This only holds if the CV terms were captured during extraction (they are
            // otherwise dropped) and the string predicate evaluated them.
            Assert.AreEqual(unfilteredPoints, cv50Points + cv70Points);
            // A numeric CV filter admitting every base peak intensity matches all spectra.
            Assert.AreEqual(unfilteredPoints, Points(filterBpiAll));
            // Presence filters resolved through extraction: Is Not Blank on the always-present filter string
            // term, and Is Blank on the never-present zoom scan term, each match every MS1 spectrum, so
            // both reproduce the unfiltered chromatogram.
            Assert.AreEqual(unfilteredPoints, Points(filterPresent));
            Assert.AreEqual(unfilteredPoints, Points(filterAbsent));

            VerifyEditorOffersCvColumns();
            VerifyEditorPreservesUnofferedCvClause();
            VerifyEditorAcceptsStringOperatorOnNumericCvColumn();
            VerifyCvFilterReconstructsFriendlyName();
            VerifyScannerReadsCvTermsFromFile();
            VerifyScannerReadsCvTermsWithoutCapture();
            VerifyStylingHelpOpens();
        }

        /// <summary>
        /// The help that explains the Property column's styling opens and closes. It describes a
        /// convention no assertion can check - that is the point of a person reading it - so this covers
        /// only that the button reaches a form that builds without throwing.
        /// </summary>
        private void VerifyStylingHelpOpens()
        {
            RunUI(() => SkylineWindow.SelectedPath =
                SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups, 0));
            var editDlg = ShowDialog<EditSpectrumFilterDlg>(SkylineWindow.EditMenu.EditSpectrumFilter);
            var helpDlg = ShowDialog<SpectrumFilterStylingHelpDlg>(editDlg.ShowStylingHelp);
            OkDialog(helpDlg, helpDlg.Close);
            OkDialog(editDlg, editDlg.Close);
        }

        /// <summary>
        /// The bootstrap case the scanner exists for: a document with results whose import captured no CV
        /// terms, because it carried no CV filter at the time. Nothing is stored to discover, so the only
        /// way the editor can know what the data carries is to read the file.
        ///
        /// The learned accessions are cleared first, so this exercises the read rather than being served
        /// an answer an earlier scan already cached.
        /// </summary>
        private void VerifyScannerReadsCvTermsWithoutCapture()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("NoCvFilter.sky")));
            ImportResultsFile(TestFilesDir.GetTestPath("Ms1SpectrumFilterTest.mzML"));

            // Import captured nothing, so there is no CV column to discover from the cache at all.
            Assert.AreEqual(0, SpectrumClassColumn.DiscoverCvColumns(SkylineWindow.Document).Count,
                @"a document imported without a CV filter should have captured no CV terms");

            var bpiColumn = SpectrumClassColumn.CvParam(@"MS:1000505", @"base peak intensity", true);
            var zoomScanColumn = SpectrumClassColumn.CvParam(@"MS:1000497", @"zoom scan", false);
            var candidates = SpectrumClassColumn.ALL.Concat(new[] { bpiColumn, zoomScanColumn }).ToList();
            // Cleared so the file is genuinely read rather than answered from an earlier scan - opening
            // the editor above already scanned this data, and a warm cache would serve the answer without
            // exercising the read at all. No document path is passed either, which is what an unsaved
            // document with imported results gives, so this also pins that such a document still gets its
            // CV terms determined.
            SpectrumColumnScanner.ResetCacheForTest();
            var availability = new SpectrumColumnScanner(
                    SkylineWindow.Document.Settings.MeasuredResults, null).GetAvailability(candidates, null);

            // ...and yet the terms are still known, because the file was read for them.
            Assert.AreEqual(SpectrumColumnScanner.Standing.answerable,
                availability.GetStanding(bpiColumn.PropertyPath, true),
                @"base peak intensity is in the file, so reading it must answer the column");
            Assert.AreEqual(SpectrumColumnScanner.Standing.unanswerable,
                availability.GetStanding(zoomScanColumn.PropertyPath, true),
                @"zoom scan is absent from the file, which reading it establishes");
            // The interpreted columns never depended on capture, so they answer from the cache as always.
            Assert.AreEqual(SpectrumColumnScanner.Standing.answerable,
                availability.GetStanding(SpectrumClassColumn.MsLevel.PropertyPath, false),
                @"MS level is answered from the per-file metadata, with no file read needed");
        }

        /// <summary>
        /// The terms a file carries can be learned by reading the head of it, which is how the filter
        /// editor marks the worthwhile terms for a document that has never filtered on a CV param and so
        /// has none of them persisted in its cache. Reads the file directly, since going only through the
        /// document would be answered from what import already stored and would not exercise the read.
        /// </summary>
        private void VerifyScannerReadsCvTermsFromFile()
        {
            var dataFilePath = TestFilesDir.GetTestPath("Ms1SpectrumFilterTest.mzML");
            using (var dataFile = new MsDataFileImpl(dataFilePath))
            {
                var accessions = dataFile
                    .GetDistinctOtherParams(SpectrumColumnScanner.MAX_SPECTRA_PER_FILE, CancellationToken.None)
                    .Select(term => term.Accession).ToHashSet();
                Assert.IsTrue(accessions.Contains(@"MS:1000505"),
                    @"base peak intensity not found by reading the head of the file");
                Assert.IsTrue(accessions.Contains(@"MS:1000512"),
                    @"filter string not found by reading the head of the file");
                // Absent from this data, so it must not be marked - the whole point of the marking is that
                // it distinguishes the terms this file actually carries from the rest of the ontology.
                Assert.IsFalse(accessions.Contains(@"MS:1000497"),
                    @"zoom scan is not in this data and must not be reported");
            }

            // The document-level scan judges the interpreted columns and the CV terms the same way, and
            // tolerates a null wait broker.
            var bpiColumn = SpectrumClassColumn.CvParam(@"MS:1000505", @"base peak intensity", true);
            var zoomScanColumn = SpectrumClassColumn.CvParam(@"MS:1000497", @"zoom scan", false);
            var candidates = SpectrumClassColumn.ALL
                .Concat(new[] { bpiColumn, zoomScanColumn }).ToList();
            var availability = new SpectrumColumnScanner(
                    SkylineWindow.Document.Settings.MeasuredResults, SkylineWindow.DocumentFilePath)
                .GetAvailability(candidates, null);

            SpectrumColumnScanner.Standing Standing(SpectrumClassColumn column) => availability.GetStanding(
                column.PropertyPath, SpectrumClassColumn.IsCvParamColumn(column));

            Assert.AreEqual(SpectrumColumnScanner.Standing.answerable, Standing(bpiColumn),
                @"base peak intensity is in this data");
            Assert.AreEqual(SpectrumColumnScanner.Standing.unanswerable, Standing(zoomScanColumn),
                @"zoom scan was looked for and is absent from this data");
            // This is MS1-only data, so MS level is answerable but the MS2 precursor, dissociation and
            // collision energy columns are not - the distinction the styling exists to draw.
            Assert.AreEqual(SpectrumColumnScanner.Standing.answerable, Standing(SpectrumClassColumn.MsLevel),
                @"MS level is answerable for any data, and is what teaches the reader what the accent means");
            Assert.AreEqual(SpectrumColumnScanner.Standing.unanswerable, Standing(SpectrumClassColumn.Ms2Precursors),
                @"MS2 precursors are absent from MS1-only data");
            Assert.AreEqual(SpectrumColumnScanner.Standing.unanswerable, Standing(SpectrumClassColumn.DissociationMethod),
                @"dissociation method is absent from MS1-only data");
            Assert.AreEqual(SpectrumColumnScanner.Standing.unanswerable, Standing(SpectrumClassColumn.CollisionEnergy),
                @"collision energy is absent from data that records none");

            // A document with no results establishes nothing, which is neither of the other two states: the
            // columns are not known to be absent, they were never examined. Conflating this with absence
            // would style an entire filter written before importing anything.
            var emptyAvailability = new SpectrumColumnScanner(
                    new SrmDocument(SrmSettingsList.GetDefault()).Settings.MeasuredResults, null)
                .GetAvailability(candidates, null);
            Assert.AreEqual(SpectrumColumnScanner.Standing.undetermined,
                emptyAvailability.GetStanding(zoomScanColumn.PropertyPath, true),
                @"a document with no results has established nothing about a CV term");
            Assert.AreEqual(SpectrumColumnScanner.Standing.undetermined,
                emptyAvailability.GetStanding(SpectrumClassColumn.Ms2Precursors.PropertyPath, false),
                @"a document with no results has established nothing about an interpreted column");
        }

        /// <summary>
        /// The filter editor discovers the imported CV terms (persisted in the cache) and offers them as
        /// filterable columns, and a filter created on one through the dialog is applied to the document.
        /// </summary>
        private void VerifyEditorOffersCvColumns()
        {
            var bpiColumn = SpectrumClassColumn.CvParam(@"MS:1000505", @"base peak intensity", true);
            var filterStringColumn = SpectrumClassColumn.CvParam(@"MS:1000512", @"filter string", false);

            // The ontology catalog offers the standard spectrum CV terms with no import at all, and
            // excludes terms Skyline already interprets into typed fields (e.g. total ion current).
            var catalog = SpectrumClassColumn.GetCvColumnCatalog();
            Assert.IsTrue(catalog.Any(c => Equals(c.PropertyPath, bpiColumn.PropertyPath)), @"catalog missing base peak intensity");
            Assert.IsTrue(catalog.Any(c => Equals(c.PropertyPath, filterStringColumn.PropertyPath)), @"catalog missing filter string");
            var ticColumn = SpectrumClassColumn.CvParam(@"MS:1000285", @"total ion current", false);
            Assert.IsFalse(catalog.Any(c => Equals(c.PropertyPath, ticColumn.PropertyPath)),
                @"catalog should exclude the interpreted total ion current term");
            // Grouping/category terms (parents in the ontology) are not offered - only leaf terms.
            var spectrumAttribute = SpectrumClassColumn.CvParam(@"MS:1000499", @"spectrum attribute", false);
            Assert.IsFalse(catalog.Any(c => Equals(c.PropertyPath, spectrumAttribute.PropertyPath)),
                @"catalog should exclude the grouping term spectrum attribute");

            var discovered = SpectrumClassColumn.DiscoverCvColumns(SkylineWindow.Document);
            Assert.IsTrue(discovered.Any(c => Equals(c.PropertyPath, bpiColumn.PropertyPath)),
                @"base peak intensity column not discovered");
            Assert.IsTrue(discovered.Any(c => Equals(c.PropertyPath, filterStringColumn.PropertyPath)),
                @"filter string column not discovered");

            RunUI(() => SkylineWindow.SelectedPath =
                SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups, 0));
            RunDlg<EditSpectrumFilterDlg>(SkylineWindow.EditMenu.EditSpectrumFilter, dlg =>
            {
                dlg.CreateCopy = true;
                var row = dlg.RowBindingList.AddNew();
                Assert.IsNotNull(row);
                // The discovered CV column is offered under its friendly name; select it by that caption.
                row.Property = filterStringColumn.GetLocalizedColumnName(CultureInfo.CurrentCulture);
                row.SetOperation(FilterOperations.OP_CONTAINS);
                row.SetValue(@"cv=-70");
                dlg.OkDialog();
            });

            var expected = new SpectrumClassFilter(new FilterClause(new[]
                { new FilterSpec(filterStringColumn.PropertyPath, FilterOperations.OP_CONTAINS, @"cv=-70") }));
            Assert.IsTrue(SkylineWindow.Document.MoleculeTransitionGroups.Any(tg => Equals(tg.SpectrumClassFilter, expected)),
                @"the CV filter created through the editor was not applied to any transition group");
        }

        /// <summary>
        /// A filter referencing a CV/user-parameter column the editor does not currently offer (here a
        /// userParam absent from the loaded data, so it is neither in the ontology catalog nor discovered)
        /// is preserved when the editor is opened and confirmed, rather than being silently dropped.
        /// </summary>
        private void VerifyEditorPreservesUnofferedCvClause()
        {
            var userParamColumn = SpectrumClassColumn.CvParam(@"vendorSetting", @"vendorSetting", false);
            Assert.IsFalse(SpectrumClassColumn.DiscoverCvColumns(SkylineWindow.Document)
                    .Any(c => Equals(c.PropertyPath, userParamColumn.PropertyPath)),
                @"the userParam should not be a discoverable column");
            var userParamCaption = userParamColumn.GetLocalizedColumnName(CultureInfo.CurrentCulture);
            var userParamFilter = new SpectrumClassFilter(new FilterClause(new[]
                { new FilterSpec(userParamColumn.PropertyPath, FilterOperations.OP_IS_NOT_BLANK, (string)null) }));

            var precursorPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups, 0);
            RunUI(() => SkylineWindow.EditMenu.ChangeSpectrumFilter(new[] { precursorPath }, userParamFilter, true));
            var groupPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups,
                SkylineWindow.Document.MoleculeTransitionGroupCount - 1);
            RunUI(() => SkylineWindow.SelectedPath = groupPath);

            // The editor must show a row for the un-offered userParam (reconstructed from its encoded path)
            // rather than silently dropping it, so confirming the dialog cannot lose the clause.
            var dlg = ShowDialog<EditSpectrumFilterDlg>(SkylineWindow.EditMenu.EditSpectrumFilter);
            bool rowShown = false;
            RunUI(() => rowShown = dlg.RowBindingList.Any(row => Equals(row.Property, userParamCaption)));
            OkDialog(dlg, dlg.Close);
            Assert.IsTrue(rowShown, @"the editor dropped the un-offered userParam filter row");

            // A userParam whose name collides with a built-in property's caption. The offered list is keyed
            // by caption, so the colliding column was dropped from it - and with it the row - meaning simply
            // opening the editor and confirming wrote the clause back without the criterion it was opened
            // to edit. Reconstructed from a saved path the term has no friendly name, so its caption is the
            // bare name, which is what collides.
            var collidingColumn = SpectrumClassColumn.CvParam(@"Analyzer", null, false);
            var collidingFilter = new SpectrumClassFilter(new FilterClause(new[]
                { new FilterSpec(collidingColumn.PropertyPath, FilterOperations.OP_IS_NOT_BLANK, (string)null) }));
            RunUI(() => SkylineWindow.EditMenu.ChangeSpectrumFilter(new[] { precursorPath }, collidingFilter, true));
            // Located by its filter rather than by position: a copy is inserted next to the precursor it
            // was made from, not appended.
            int collidingIndex = SkylineWindow.Document.MoleculeTransitionGroups.ToList()
                .FindIndex(tg => Equals(tg.SpectrumClassFilter, collidingFilter));
            Assert.AreNotEqual(-1, collidingIndex, @"the colliding filter was not applied to any precursor");
            var collidingPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups,
                collidingIndex);
            RunUI(() => SkylineWindow.SelectedPath = collidingPath);

            var collidingDlg = ShowDialog<EditSpectrumFilterDlg>(SkylineWindow.EditMenu.EditSpectrumFilter);
            int criteriaShown = 0;
            RunUI(() => criteriaShown = collidingDlg.RowBindingList.Count(row => !string.IsNullOrEmpty(row.Property)));
            OkDialog(collidingDlg, collidingDlg.OkDialog);
            Assert.AreNotEqual(0, criteriaShown, @"the colliding userParam criterion was not shown for editing");
            Assert.IsTrue(
                SkylineWindow.Document.MoleculeTransitionGroups.Any(tg => Equals(tg.SpectrumClassFilter, collidingFilter)),
                @"confirming the editor dropped the colliding userParam criterion");
        }

        /// <summary>
        /// A CV term the ontology declares numeric is offered with a numeric ValueType, but a string
        /// operator (Contains) with a non-numeric operand must still be accepted, because chromatogram
        /// extraction types the operand by the operator rather than the column - so the editor must not
        /// reject a filter that extraction would run.
        /// </summary>
        private void VerifyEditorAcceptsStringOperatorOnNumericCvColumn()
        {
            var bpiColumn = SpectrumClassColumn.CvParam(@"MS:1000505", @"base peak intensity", true);
            var discovered = SpectrumClassColumn.DiscoverCvColumns(SkylineWindow.Document)
                .First(c => Equals(c.PropertyPath, bpiColumn.PropertyPath));
            Assert.AreEqual(typeof(double), discovered.ValueType,
                @"base peak intensity should be discovered as a numeric column");

            var precursorPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups, 0);
            RunUI(() => SkylineWindow.SelectedPath = precursorPath);
            RunDlg<EditSpectrumFilterDlg>(SkylineWindow.EditMenu.EditSpectrumFilter, dlg =>
            {
                dlg.CreateCopy = true;
                var row = dlg.RowBindingList.AddNew();
                Assert.IsNotNull(row);
                row.Property = discovered.GetLocalizedColumnName(CultureInfo.CurrentCulture);
                row.SetOperation(FilterOperations.OP_CONTAINS);
                row.SetValue(@"e05");
                // A string operator on a numeric-discovered CV column is accepted (typed by the operator),
                // so OkDialog closes rather than blocking with a "not a number" error.
                dlg.OkDialog();
            });

            var expected = new SpectrumClassFilter(new FilterClause(new[]
                { new FilterSpec(discovered.PropertyPath, FilterOperations.OP_CONTAINS, @"e05") }));
            Assert.IsTrue(SkylineWindow.Document.MoleculeTransitionGroups.Any(tg => Equals(tg.SpectrumClassFilter, expected)),
                @"a Contains filter on a numeric-discovered CV column was not accepted by the editor");
        }

        /// <summary>
        /// A CV filter reconstructed from its saved encoded path (with no discovery context) resolves its
        /// friendly name from the ontology catalog, so it reads the same in the document tree / filter
        /// summaries as in the editor, rather than as a bare accession.
        /// </summary>
        private void VerifyCvFilterReconstructsFriendlyName()
        {
            var bpiColumn = SpectrumClassColumn.CvParam(@"MS:1000505", @"base peak intensity", true);
            var catalogEntry = SpectrumClassColumn.GetCvColumnCatalog()
                .First(c => Equals(c.PropertyPath, bpiColumn.PropertyPath));
            var reconstructed = SpectrumClassColumn.FindColumn(bpiColumn.PropertyPath);
            Assert.IsNotNull(reconstructed);
            Assert.AreEqual(catalogEntry.GetLocalizedColumnName(CultureInfo.CurrentCulture),
                reconstructed.GetLocalizedColumnName(CultureInfo.CurrentCulture),
                @"a reconstructed CV column should display the same friendly name as the catalog entry");
        }

        private static SpectrumClassFilter StringCvFilter(string containsText)
        {
            var column = SpectrumClassColumn.CvParam(@"MS:1000512", @"filter string", false);
            return new SpectrumClassFilter(new FilterClause(new[]
                { new FilterSpec(column.PropertyPath, FilterOperations.OP_CONTAINS, containsText) }));
        }

        private static SpectrumClassFilter NumericBpiFilter(IFilterOperation op, string operand)
        {
            var column = SpectrumClassColumn.CvParam(@"MS:1000505", @"base peak intensity", true);
            return new SpectrumClassFilter(new FilterClause(new[]
                { new FilterSpec(column.PropertyPath, op, operand) }));
        }

        private static SpectrumClassFilter BlanknessCvFilter(string accession, string name, IFilterOperation op)
        {
            var column = SpectrumClassColumn.CvParam(accession, name, false);
            return new SpectrumClassFilter(new FilterClause(new[]
                { new FilterSpec(column.PropertyPath, op, (string)null) }));
        }
    }
}
