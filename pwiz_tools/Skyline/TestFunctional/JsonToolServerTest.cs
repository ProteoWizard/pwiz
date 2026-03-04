/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class JsonToolServerTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestJsonToolServer()
        {
            TestFilesZip = @"TestFunctional\FilesTreeFormTest.data";
            RunFunctionalTest();
        }

        private const string DOCUMENT_NAME = @"Rat_plasma.sky";
        private const string REPORT_AREAS = @"Peptide Normalized Areas";
        private const string CULTURE_INVARIANT = @"invariant";
        private const string LEVEL_GROUP = @"group";
        private const string LEVEL_MOLECULE = @"molecule";
        private const string LEVEL_PRECURSOR = @"precursor";
        private const string LEVEL_TRANSITION = @"transition";
        private const string COL_PROTEIN_NAME = @"ProteinName";
        private const string COL_PRECURSOR_MZ = @"PrecursorMz";
        private const string COL_PEPTIDE_SEQUENCE = @"PeptideModifiedSequenceMonoisotopicMasses";
        private const string COL_TOTAL_AREA = @"TotalArea";

        protected override void DoTest()
        {
            OpenDocument(DOCUMENT_NAME);

            var toolService = new ToolService(@"test-" + Guid.NewGuid(), SkylineWindow);
            var server = new JsonToolServer(toolService);

            // Read-only tools
            TestDocumentInfo(server);
            TestSelection(server);
            TestLocations(server);
            TestReplicates(server);
            TestSettingsLists(server);
            TestReportDocumentation(server);
            TestNamedReports(server);
            TestReportFromDefinition(server);
            TestDocumentSettings(server);
            TestAvailableTutorials(server);
            TestCliHelp(server);
            TestOpenForms(server);

            // Document-modifying tools
            TestAddReport(server);
            // TestImportFasta(server);
            TestInsertSmallMoleculeTransitionList(server);
            TestImportProperties(server);
            TestRunCommand(server);
        }

        private void TestDocumentInfo(JsonToolServer server)
        {
            // GetDocumentPath - should contain the .sky filename with forward slashes
            string path = server.GetDocumentPath();
            AssertEx.AreEqual(TestFilesDir.GetTestPath(DOCUMENT_NAME).ToForwardSlashPath(), path);

            // GetVersion - should return non-empty version string
            string version = server.GetVersion();
            Assert.AreEqual(Install.Version, version);

            // GetDocumentStatus - verify counts match document
            string status = server.GetDocumentStatus();
            var doc = SkylineWindow.Document;
            AssertEx.Contains(status, doc.MoleculeGroupCount.ToString());
            AssertEx.Contains(status, doc.MoleculeCount.ToString());
            AssertEx.Contains(status, doc.MoleculeTransitionGroupCount.ToString());
            AssertEx.Contains(status, doc.MoleculeTransitionCount.ToString());
            AssertEx.Contains(status, doc.Settings.MeasuredResults?.Chromatograms.Count.ToString());

            // GetProcessId - should match current process
            string pidStr = server.GetProcessId();
            int pid = int.Parse(pidStr);
            Assert.AreEqual(Process.GetCurrentProcess().Id, pid);
        }

        private void TestSelection(JsonToolServer server)
        {
            // Get initial selection
            string sel = server.GetSelection();
            Assert.IsFalse(string.IsNullOrEmpty(sel));

            // Navigate to a specific peptide and verify selection changes
            var doc = SkylineWindow.Document;
            var firstGroup = doc.MoleculeGroups.First();
            var firstMolecule = firstGroup.Molecules.First();
            var moleculePath = new IdentityPath(firstGroup.Id, firstMolecule.Id);
            RunUI(() => SkylineWindow.SelectedPath = moleculePath);
            WaitForConditionUI(() => SkylineWindow.SelectedPath.Equals(moleculePath));

            string sel2 = server.GetSelection();
            Assert.IsFalse(string.IsNullOrEmpty(sel2));

            // SetSelectedElement - navigate to a different location via locator
            string locations = server.GetLocations(LEVEL_MOLECULE);
            Assert.AreEqual(doc.MoleculeCount, Helpers.CountLinesInString(locations));

            // Pick the last molecule and navigate to it
            var lines = TextUtil.ReadLines(locations);
            string lastLocator = lines.Last().ParseDsvFields(TextUtil.SEPARATOR_TSV)[1];
            server.SetSelectedElement(lastLocator);
            WaitForConditionUI(() => server.GetSelection().Contains(lastLocator));
        }

        private void TestLocations(JsonToolServer server)
        {
            var doc = SkylineWindow.Document;

            // Group level
            string groups = server.GetLocations(LEVEL_GROUP);
            Assert.AreEqual(doc.MoleculeGroupCount, Helpers.CountLinesInString(groups));

            // Molecule level
            string molecules = server.GetLocations(LEVEL_MOLECULE);
            Assert.AreEqual(doc.MoleculeCount, Helpers.CountLinesInString(molecules));

            // Precursor level
            string precursors = server.GetLocations(LEVEL_PRECURSOR);
            Assert.AreEqual(doc.MoleculeTransitionGroupCount, Helpers.CountLinesInString(precursors));

            // Transition level
            string transitions = server.GetLocations(LEVEL_TRANSITION);
            Assert.AreEqual(doc.MoleculeTransitionCount, Helpers.CountLinesInString(transitions));

            // Scoped enumeration - molecules under first group
            var groupLines = groups.ReadLines();
            string firstGroupLocator = groupLines.First().ParseDsvFields(TextUtil.SEPARATOR_TSV)[1];
            string scopedMolecules = server.GetLocations(LEVEL_MOLECULE, firstGroupLocator);
            Assert.AreEqual(doc.MoleculeGroups.First().MoleculeCount, Helpers.CountLinesInString(scopedMolecules));

            // Error: invalid level
            AssertEx.ThrowsException<ArgumentException>(() => server.GetLocations(@"invalid"));
        }

        private void TestReplicates(JsonToolServer server)
        {
            var doc = SkylineWindow.Document;
            var chromatograms = doc.Settings.MeasuredResults.Chromatograms;

            // GetReplicateName - should return current replicate
            string currentRep = server.GetReplicateName();
            Assert.IsFalse(string.IsNullOrEmpty(currentRep));

            // GetReplicateNames - count should match
            string names = server.GetReplicateNames();
            Assert.AreEqual(chromatograms.Count, Helpers.CountLinesInString(names));

            // SetReplicate - navigate to a different replicate
            string targetRep = names.ReadLines().Last();
            server.SetReplicate(targetRep);
            WaitForConditionUI(() => server.GetReplicateName() == targetRep);

            // Error: nonexistent replicate - SetReplicate returns error message (not exception)
            string errorResult = server.SetReplicate(@"NonexistentReplicate_12345");
            Assert.AreNotEqual(@"OK", errorResult);
        }

        private void TestSettingsLists(JsonToolServer server)
        {
            // GetSettingsListTypes - should contain known types
            string types = server.GetSettingsListTypes();
            AssertEx.Contains(types, nameof(EnzymeList));
            AssertEx.Contains(types, nameof(PersistedViews));
            // Each line should be tab-separated
            foreach (var line in types.ReadLines())
                AssertEx.Contains(line, TextUtil.SEPARATOR_TSV_STR); // Tab character

            // GetSettingsListNames - enzymes should contain the default enzyme
            var defaultEnzyme = EnzymeList.GetDefault();
            string enzymes = server.GetSettingsListNames(nameof(EnzymeList));
            AssertEx.Contains(enzymes, defaultEnzyme.Name);

            // GetSettingsListItem - should return valid XML containing the enzyme name
            string enzymeXml = server.GetSettingsListItem(nameof(EnzymeList), defaultEnzyme.GetKey());
            AssertEx.Contains(enzymeXml, string.Format(@"name={0}", defaultEnzyme.Name.Quote()));

            // Error: nonexistent list
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.GetSettingsListNames(@"NonexistentList"));

            // Error: nonexistent item
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.GetSettingsListItem(nameof(EnzymeList), @"NotAnEnzyme"));
        }

        private void TestReportDocumentation(JsonToolServer server)
        {
            // GetReportDocTopics - should list available entity types
            string topics = server.GetReportDocTopics();
            Assert.IsFalse(string.IsNullOrEmpty(topics));
            // Should contain common entity types
            Assert.IsTrue(Helpers.CountLinesInString(topics) > 5);
            // Each line should have tab-separated DisplayName and QualifiedTypeName
            foreach (var line in topics.ReadLines())
                AssertEx.Contains(line, @"	");

            // GetReportDocTopic - should return column documentation
            string moleculeTopic = server.GetReportDocTopic(@"Peptide");
            Assert.IsNotNull(moleculeTopic);
            AssertEx.Contains(moleculeTopic, @"Name");

            // Case-insensitive matching
            string precursorTopic = server.GetReportDocTopic(@"precursor");
            Assert.IsNotNull(precursorTopic);

            // Nonexistent topic - returns null
            Assert.IsNull(server.GetReportDocTopic(@"CompletelyBogusTopicName"));
        }

        private void TestNamedReports(JsonToolServer server)
        {
            string tempPath = TestFilesDir.GetTestPath(@"report_test.csv");

            // ExportReport - export a built-in report
            string result = server.ExportReport(REPORT_AREAS, tempPath, CULTURE_INVARIANT);
            Assert.IsFalse(string.IsNullOrEmpty(result));

            // Parse JSON metadata
            var metadata = JObject.Parse(result);
            Assert.AreEqual(16, GetRowCount(metadata));
            Assert.IsNotNull(metadata[@"columns"]);

            // Verify file was created
            Assert.IsTrue(File.Exists(tempPath));
            var lines = File.ReadAllLines(tempPath);
            Assert.IsTrue(lines.Length > 1); // Header + data rows

            // Error: nonexistent report
            string tempPath2 = TestFilesDir.GetTestPath(@"report_bad.csv");
            AssertEx.ThrowsException<Exception>(() =>
                server.ExportReport(@"NonexistentReport_12345", tempPath2, CULTURE_INVARIANT));
        }

        private void TestReportFromDefinition(JsonToolServer server)
        {
            // Simple select
            string tempPath = TestFilesDir.GetTestPath(@"report_def_simple.csv");
            string json = BuildSelectJson(COL_PROTEIN_NAME, COL_PEPTIDE_SEQUENCE, COL_PRECURSOR_MZ);
            string result = server.ExportReportFromDefinition(json, tempPath, CULTURE_INVARIANT);
            var metadata = JObject.Parse(result);
            Assert.AreEqual(13, GetRowCount(metadata));
            var lines = File.ReadAllLines(tempPath);
            string header = lines[0];
            AssertEx.Contains(header, COL_PROTEIN_NAME);
            AssertEx.Contains(header, @"PeptideModifiedSequence");
            AssertEx.Contains(header, COL_PRECURSOR_MZ);

            // With filter: PrecursorMz > 500
            string tempPathFilter = TestFilesDir.GetTestPath(@"report_def_filter.csv");
            string filterJson = string.Format(
                @"{{""select"": [{0}, {1}], ""filter"": [{{""column"": {1}, ""op"": "">"", ""value"": ""500""}}]}}",
                COL_PROTEIN_NAME.Quote(), COL_PRECURSOR_MZ.Quote());
            string filterResult = server.ExportReportFromDefinition(filterJson, tempPathFilter, CULTURE_INVARIANT);
            var filterMetadata = JObject.Parse(filterResult);
            Assert.AreEqual(11, GetRowCount(filterMetadata));
            // Verify all rows satisfy the filter
            var filterLines = File.ReadAllLines(tempPathFilter);
            int mzColIndex = Array.IndexOf(filterLines[0].ParseDsvFields(TextUtil.SEPARATOR_CSV), COL_PRECURSOR_MZ);
            Assert.IsTrue(mzColIndex >= 0);
            for (int i = 1; i < filterLines.Length; i++)
            {
                string[] cols = filterLines[i].ParseDsvFields(TextUtil.SEPARATOR_CSV);
                if (cols.Length <= mzColIndex || string.IsNullOrEmpty(cols[mzColIndex]))
                    continue;
                double mz = double.Parse(cols[mzColIndex], CultureInfo.InvariantCulture);
                Assert.IsTrue(mz > 500, @"Row {0}: PrecursorMz {1} should be > 500", i, mz);
            }

            // With sort: PrecursorMz descending
            string tempPathSort = TestFilesDir.GetTestPath(@"report_def_sort.csv");
            string sortJson = string.Format(
                @"{{""select"": [{0}, {1}], ""sort"": [{{""column"": {1}, ""direction"": ""desc""}}]}}",
                COL_PROTEIN_NAME.Quote(), COL_PRECURSOR_MZ.Quote());
            string sortResult = server.ExportReportFromDefinition(sortJson, tempPathSort, CULTURE_INVARIANT);
            var sortMetadata = JObject.Parse(sortResult);
            Assert.AreEqual(13, GetRowCount(sortMetadata));
            // Verify descending order
            var sortLines = File.ReadAllLines(tempPathSort);
            int sortMzIndex = Array.IndexOf(sortLines[0].ParseDsvFields(TextUtil.SEPARATOR_CSV), COL_PRECURSOR_MZ);
            double prevMz = double.MaxValue;
            for (int i = 1; i < sortLines.Length; i++)
            {
                string[] cols = sortLines[i].ParseDsvFields(TextUtil.SEPARATOR_CSV);
                if (cols.Length <= sortMzIndex || string.IsNullOrEmpty(cols[sortMzIndex]))
                    continue;
                double mz = double.Parse(cols[sortMzIndex], CultureInfo.InvariantCulture);
                Assert.IsTrue(mz <= prevMz, @"Row {0}: {1} should be <= {2}", i, mz, prevMz);
                prevMz = mz;
            }

            // With pivotReplicate
            string tempPathPivot = TestFilesDir.GetTestPath(@"report_def_pivot.csv");
            string pivotJson = BuildSelectPivotJson(COL_PEPTIDE_SEQUENCE, COL_TOTAL_AREA);
            string pivotResult = server.ExportReportFromDefinition(pivotJson, tempPathPivot, CULTURE_INVARIANT);
            var pivotMetadata = JObject.Parse(pivotResult);
            Assert.AreEqual(5, GetRowCount(pivotMetadata));
            // Pivoted header should contain replicate names as column suffixes
            var pivotHeader = File.ReadLines(tempPathPivot).First();
            // The document has replicates, so the header should have more columns than the 2 we selected
            int pivotColCount = pivotHeader.ParseDsvFields(TextUtil.SEPARATOR_CSV).Length;
            Assert.IsTrue(pivotColCount > 2);

            // Error: unknown column with "did you mean" suggestion
            string badColJson = BuildSelectJson(COL_PRECURSOR_MZ + "z", COL_PROTEIN_NAME);
            string tempPathBad = TestFilesDir.GetTestPath(@"report_def_bad.csv");
            AssertEx.ThrowsException<ArgumentException>(() =>
                    server.ExportReportFromDefinition(badColJson, tempPathBad, CULTURE_INVARIANT),
                ex => AssertEx.Contains(ex.Message, COL_PRECURSOR_MZ));
        }

        private static int GetRowCount(JObject jObject)
        {
            return (int)jObject[@"row_count"];
        }

        private static string BuildSelectJson(params string[] columns)
        {
            return string.Format(@"{{""select"": [{0}]}}",
                string.Join(@", ", columns.Select(c => c.Quote())));
        }

        private static string BuildSelectPivotJson(params string[] columns)
        {
            return string.Format(@"{{""select"": [{0}], ""pivotReplicate"": true}}",
                string.Join(@", ", columns.Select(c => c.Quote())));
        }

        private void TestDocumentSettings(JsonToolServer server)
        {
            string docSettingsPath = TestFilesDir.GetTestPath(@"doc_settings.xml");
            string result = server.GetDocumentSettings(docSettingsPath);
            Assert.IsTrue(File.Exists(docSettingsPath));
            string xml = File.ReadAllText(docSettingsPath);
            AssertEx.Contains(xml, @"<settings_summary");

            string defSettingsPath = TestFilesDir.GetTestPath(@"default_settings.xml");
            string defResult = server.GetDefaultSettings(defSettingsPath);
            Assert.IsTrue(File.Exists(defSettingsPath));
            string defXml = File.ReadAllText(defSettingsPath);
            AssertEx.Contains(defXml, @"<settings_summary");

            // Document settings and default settings should differ
            Assert.AreNotEqual(xml, defXml);
        }

        private void TestAvailableTutorials(JsonToolServer server)
        {
            string catalog = server.GetAvailableTutorials();
            Assert.IsFalse(string.IsNullOrEmpty(catalog));

            Assert.AreEqual(TutorialCatalog.Tutorials.Length, Helpers.CountLinesInString(catalog));

            // Each line should have 6 tab-separated fields
            foreach (var line in catalog.ReadLines())
            {
                var fields = line.ParseDsvFields(TextUtil.SEPARATOR_TSV);
                Assert.AreEqual(6, fields.Length,
                    @"Expected 6 tab-separated fields, got {0}: {1}", fields.Length, line);
            }
        }

        private void TestCliHelp(JsonToolServer server)
        {
            // RunCommandSilent with --help should return sections list
            string sections = server.RunCommandSilent(@"--help=sections --help=no-borders");
            Assert.IsFalse(string.IsNullOrEmpty(sections));
        }

        private void TestOpenForms(JsonToolServer server)
        {
            string forms = server.GetOpenForms();
            Assert.IsFalse(string.IsNullOrEmpty(forms));
            // The Targets panel (SequenceTreeForm) should always be open
            AssertEx.Contains(forms, @"SequenceTreeForm");
        }

        private void TestAddReport(JsonToolServer server)
        {
            const string reportName = @"TestMcpReport";
            string json = string.Format(@"{{""name"": {0}, ""select"": [{1}, {2}]}}",
                reportName.Quote(), COL_PROTEIN_NAME.Quote(), COL_PRECURSOR_MZ.Quote());
            string result = server.AddReportFromDefinition(json);
            Assert.IsFalse(string.IsNullOrEmpty(result));

            // Verify the report was persisted
            var viewSpecList = Settings.Default.PersistedViews.GetViewSpecList(
                PersistedViews.MainGroup.Id);
            Assert.IsTrue(viewSpecList.ViewSpecs.Any(v => v.Name == reportName));
        }

        private void TestImportFasta(JsonToolServer server)
        {
            var doc = SkylineWindow.Document;
            int groupsBefore = doc.MoleculeGroupCount;
            int moleculesBefore = doc.MoleculeCount;

            // Select the insert node so FASTA import has a valid location
            RunUI(() => SkylineWindow.SequenceTree.SelectedNode =
                SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 1]);

            string result = server.ImportFasta(TEXT_FASTA);
            WaitForProteinMetadataBackgroundLoaderCompletedUI();

            var docAfter = SkylineWindow.Document;
            Assert.IsTrue(docAfter.MoleculeGroupCount > groupsBefore);
            Assert.IsTrue(docAfter.MoleculeCount > moleculesBefore);
        }

        private void TestInsertSmallMoleculeTransitionList(JsonToolServer server)
        {
            var doc = SkylineWindow.Document;
            int groupsBefore = doc.MoleculeGroupCount;

            string csvText = GetSmallMoleculeTransitionsText();
            string result = server.InsertSmallMoleculeTransitionList(csvText);

            var docAfter = SkylineWindow.Document;
            Assert.IsTrue(docAfter.MoleculeGroupCount > groupsBefore);
        }

        private void TestImportProperties(JsonToolServer server)
        {
            // Add an annotation definition to the document
            var annotationDef = new AnnotationDef(@"TestAnnotation",
                AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.protein),
                AnnotationDef.AnnotationType.text,
                null);
            RunUI(() => SkylineWindow.ModifyDocument(@"Add test annotation",
                d => d.ChangeSettings(d.Settings.ChangeAnnotationDefs(
                    defs => defs.Concat(new[] { annotationDef }).ToList()))));

            // Get protein locators
            string groups = server.GetLocations(LEVEL_GROUP);
            Assert.IsTrue(Helpers.CountLinesInString(groups) > 0);

            // Build CSV for ImportProperties: ElementLocator,TestAnnotation
            string firstGroupLocator = groups.ReadLines().First().ParseDsvFields(TextUtil.SEPARATOR_TSV)[1];
            string csvText = @"ElementLocator,TestAnnotation" + Environment.NewLine +
                             firstGroupLocator + @",test_value";

            string result = server.ImportProperties(csvText);
            Assert.IsFalse(string.IsNullOrEmpty(result));
        }

        private void TestRunCommand(JsonToolServer server)
        {
            string reportPath = TestFilesDir.GetTestPath(@"run_command_report.csv");
            string args = string.Format(@"--report-name={0} --report-file={1}",
                REPORT_AREAS.Quote(), reportPath.ToForwardSlashPath().Quote());
            string result = server.RunCommand(args);
            Assert.IsTrue(File.Exists(reportPath));
        }

        // Small FASTA sequence for import test
        private const string TEXT_FASTA =
            @">sp|P02769|ALBU_BOVIN Albumin - Bos taurus (Bovine)
MKWVTFISLLLLFSSAYSRGVFRRDTHKSEIAHRFKDLGEEHFKGLVLIAFSQYLQQCPF
DEHVKLVNELTEFAKTCVADESHAGCEKSLHTLFGDELCKVASLRETYGDMADCCEKQEPE
RNECFLSHKDDSPDLPKLKPDPNTLCDEFKADEKKFWGKYLYEIARRHPYFYAPELLYYA
NKYNGVFQECCQAEDKGACLLPKIETMREKVLASSARQRLRCASIQKFGERALKAWSVAR
";

        private static string GetSmallMoleculeTransitionsText()
        {
            var header = string.Join(@",", new[]
            {
                SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                SmallMoleculeTransitionListColumnHeaders.nameProduct,
                SmallMoleculeTransitionListColumnHeaders.labelType,
                SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                SmallMoleculeTransitionListColumnHeaders.formulaProduct,
                SmallMoleculeTransitionListColumnHeaders.mzPrecursor,
                SmallMoleculeTransitionListColumnHeaders.mzProduct,
                SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                SmallMoleculeTransitionListColumnHeaders.chargeProduct,
                SmallMoleculeTransitionListColumnHeaders.rtPrecursor,
            });
            return header + "\n" +
                   "TestSmallMol,Ala,,light,,,225,44,1,1,3\n" +
                   "TestSmallMol,Arg,,light,,,310,217,1,1,19\n";
        }
    }
}
