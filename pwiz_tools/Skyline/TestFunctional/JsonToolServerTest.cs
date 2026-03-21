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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using SkylineTool;
using JSON_RPC = SkylineTool.JsonToolConstants.JSON_RPC;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;
using Transition = pwiz.Skyline.Model.Databinding.Entities.Transition;

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
        private const string COL_PROTEIN_NAME = @"ProteinName";
        private const string COL_PRECURSOR_MZ = @"PrecursorMz";
        private const string COL_PEPTIDE_SEQUENCE = @"PeptideModifiedSequenceMonoisotopicMasses";
        private const string COL_TOTAL_AREA = @"TotalArea";

        protected override void DoTest()
        {
            OpenDocument(DOCUMENT_NAME);

            string testGuid = @"test-" + Guid.NewGuid();
            var toolService = new ToolService(testGuid, SkylineWindow);
            var server = new JsonToolServer(toolService, testGuid);

            // Read-only tools
            TestDispatch(server);
            TestDocumentInfo(server);
            TestSelection(server);
            TestLocations(server);
            TestReplicates(server);
            TestReportDocumentation(server);
            TestNamedReports(server);
            TestReportFromDefinition(server);
            TestDocumentSettings(server);
            TestAvailableTutorials(server);
            TestCliHelp(server);
            TestDiagnosticLogging(server);
            TestOpenForms(server);
            TestScreenCapturePermissionDlg(server);
            TestFormImage(server);
            TestGraphDataAndImage(server);
            TestTutorialFetch(server);
            TestTutorialFetchErrors(server);

            // Document-modifying tools
            TestInsertSmallMoleculeTransitionList(server);
            TestImportProperties(server);
            var doc = TestImportFasta(server);
            TestRunCommand(server, doc);

            TestDocumentOperations(server);

            // Molecule mode: File > New via run_command, switch to small molecules, verify uimode
            RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules));
        }

        /// <summary>
        /// Test the HandleRequest dispatch path that the MCP server uses over the named pipe.
        /// </summary>
        private void TestDispatch(JsonToolServer server)
        {
            // Helper to build a JSON-RPC 2.0 request like the MCP server sends
            string buildRequest(string method, params string[] args)
            {
                var obj = new JObject
                {
                    [nameof(JSON_RPC.jsonrpc)] = JsonToolConstants.JSONRPC_VERSION,
                    [nameof(JSON_RPC.method)] = method,
                    [nameof(JSON_RPC.id)] = 1
                };
                if (args.Length > 0)
                    obj[nameof(JSON_RPC.@params)] = new JArray(args);
                return obj.ToString();
            }

            // Successful call: GetVersion (0-arg method)
            string versionResponse = server.HandleRequest(
                Encoding.UTF8.GetBytes(buildRequest(nameof(IJsonToolService.GetVersion))));
            var versionResult = JObject.Parse(versionResponse);
            AssertEx.AreEqual(JsonToolConstants.JSONRPC_VERSION,
                (string)versionResult[nameof(JSON_RPC.jsonrpc)]);
            Assert.IsNotNull(versionResult[nameof(JSON_RPC.result)]);
            AssertEx.Contains((string)versionResult[nameof(JSON_RPC.result)], Install.Version);

            // Successful call: GetSelectedElementLocator (1-arg method with default)
            string locatorResponse = server.HandleRequest(
                Encoding.UTF8.GetBytes(buildRequest(nameof(IJsonToolService.GetSelectedElementLocator), @"Molecule")));
            var locatorResult = JObject.Parse(locatorResponse);
            Assert.IsNotNull(locatorResult[nameof(JSON_RPC.result)]);

            // QueryAvailableMethods - special dispatch path (not on the interface)
            string methodsResponse = server.HandleRequest(
                Encoding.UTF8.GetBytes(buildRequest(@"QueryAvailableMethods")));
            var methodsResult = JObject.Parse(methodsResponse);
            string methods = (string)methodsResult[nameof(JSON_RPC.result)];
            AssertEx.Contains(methods, nameof(IJsonToolService.GetVersion));
            AssertEx.Contains(methods, nameof(IJsonToolService.GetSelection));
            AssertEx.Contains(methods, nameof(IJsonToolService.ExportReport));

            // Error: unknown method (caught by Dispatch, returned as error JSON)
            string unknownResponse = server.HandleRequest(
                Encoding.UTF8.GetBytes(buildRequest(@"NotARealMethod")));
            var unknownResult = JObject.Parse(unknownResponse);
            Assert.IsNotNull(unknownResult[nameof(JSON_RPC.error)]);
            AssertEx.AreEqual(JsonToolConstants.ERROR_METHOD_NOT_FOUND,
                (int)unknownResult[nameof(JSON_RPC.error)][nameof(JSON_RPC.code)]);

            // Error: too few arguments for a method that requires them
            string tooFewResponse = server.HandleRequest(
                Encoding.UTF8.GetBytes(buildRequest(nameof(IJsonToolService.ExportReport))));
            var tooFewResult = JObject.Parse(tooFewResponse);
            Assert.IsNotNull(tooFewResult[nameof(JSON_RPC.error)]);
            AssertEx.AreEqual(JsonToolConstants.ERROR_INVALID_PARAMS,
                (int)tooFewResult[nameof(JSON_RPC.error)][nameof(JSON_RPC.code)]);

            // Error: malformed JSON request
            string badJsonResponse = server.HandleRequest(
                Encoding.UTF8.GetBytes(@"not json at all"));
            var badJsonResult = JObject.Parse(badJsonResponse);
            Assert.IsNotNull(badJsonResult[nameof(JSON_RPC.error)]);

            // Null params array (no params key in request)
            var noParamsRequest = new JObject
            {
                [nameof(JSON_RPC.jsonrpc)] = JsonToolConstants.JSONRPC_VERSION,
                [nameof(JSON_RPC.method)] = nameof(IJsonToolService.GetVersion),
                [nameof(JSON_RPC.id)] = 1
            };
            string noParamsResponse = server.HandleRequest(
                Encoding.UTF8.GetBytes(noParamsRequest.ToString()));
            var noParamsResult = JObject.Parse(noParamsResponse);
            Assert.IsNotNull(noParamsResult[nameof(JSON_RPC.result)]);
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
            var status = server.GetDocumentStatus();
            var doc = SkylineWindow.Document;
            Assert.AreEqual(doc.MoleculeGroupCount, status.Groups);
            Assert.AreEqual(doc.MoleculeCount, status.Molecules);
            Assert.AreEqual(doc.MoleculeTransitionGroupCount, status.Precursors);
            Assert.AreEqual(doc.MoleculeTransitionCount, status.Transitions);
            Assert.AreEqual(doc.Settings.MeasuredResults?.Chromatograms.Count ?? 0, status.Replicates);

            // GetProcessId - should match current process
            string pidStr = server.GetProcessId();
            int pid = int.Parse(pidStr);
            Assert.AreEqual(Process.GetCurrentProcess().Id, pid);
        }

        private void TestSelection(JsonToolServer server)
        {
            // Get initial selection
            var sel = server.GetSelection();
            Assert.IsTrue(sel.Locators.Length > 0);

            // GetSelectionText - human-readable location name
            string selText = server.GetSelectionText();
            Assert.IsFalse(string.IsNullOrEmpty(selText));

            // Navigate to a specific peptide and verify selection changes
            var doc = SkylineWindow.Document;
            var firstGroup = doc.MoleculeGroups.First();
            var firstMolecule = firstGroup.Molecules.First();
            var moleculePath = new IdentityPath(firstGroup.Id, firstMolecule.Id);
            RunUI(() => SkylineWindow.SelectedPath = moleculePath);
            WaitForConditionUI(() => SkylineWindow.SelectedPath.Equals(moleculePath));

            var sel2 = server.GetSelection();
            Assert.IsTrue(sel2.Locators.Length > 0);

            // GetSelectedElementLocator - get locator for selected molecule
            string moleculeLocator = server.GetSelectedElementLocator(@"Molecule");
            Assert.IsFalse(string.IsNullOrEmpty(moleculeLocator));

            // SetSelectedElement - navigate to a different location via locator
            var locations = server.GetLocations(JsonToolConstants.LEVEL_MOLECULE);
            Assert.AreEqual(doc.MoleculeCount, locations.Length);

            // Pick the last molecule and navigate to it
            string lastMoleculeLocator = locations.Last().Locator;
            server.SetSelectedElement(lastMoleculeLocator);
            WaitForCondition(() => server.GetSelection().Locators.Contains(lastMoleculeLocator));

            // Multi-selection: select multiple molecules at once
            string firstMoleculeLocator = locations.First().Locator;
            string secondMoleculeLocator = locations.Skip(1).First().Locator;
            server.SetSelectedElement(firstMoleculeLocator,
                TextUtil.LineSeparate(secondMoleculeLocator, lastMoleculeLocator));
            var multiSel = server.GetSelection();
            // Multi-selection should return multiple locators
            Assert.AreEqual(3, multiSel.Locators.Length,
                @"Multi-selection should return 3 locators");
            Assert.IsTrue(multiSel.Locators.Contains(firstMoleculeLocator));
            Assert.IsTrue(multiSel.Locators.Contains(secondMoleculeLocator));
            Assert.IsTrue(multiSel.Locators.Contains(lastMoleculeLocator));

            // Select the insertion node and verify round-trip
            server.SetSelectedElement(JsonUiService.INSERT_NODE_LOCATOR);
            var insertSel = server.GetSelection();
            Assert.IsTrue(insertSel.Locators.Contains(JsonUiService.INSERT_NODE_LOCATOR),
                @"Insertion node selection should round-trip through GetSelection");

            // Navigate back to a regular element to leave things in a known state
            server.SetSelectedElement(firstMoleculeLocator);
            WaitForCondition(() => server.GetSelection().Locators.Contains(firstMoleculeLocator));
        }

        private void TestLocations(JsonToolServer server)
        {
            var doc = SkylineWindow.Document;

            // Group level
            var groups = server.GetLocations(JsonToolConstants.LEVEL_GROUP);
            Assert.AreEqual(doc.MoleculeGroupCount, groups.Length);

            // Molecule level
            var molecules = server.GetLocations(JsonToolConstants.LEVEL_MOLECULE);
            Assert.AreEqual(doc.MoleculeCount, molecules.Length);

            // Precursor level
            var precursors = server.GetLocations(JsonToolConstants.LEVEL_PRECURSOR);
            Assert.AreEqual(doc.MoleculeTransitionGroupCount, precursors.Length);

            // Transition level
            var transitions = server.GetLocations(JsonToolConstants.LEVEL_TRANSITION);
            Assert.AreEqual(doc.MoleculeTransitionCount, transitions.Length);

            // Scoped enumeration - molecules under first group
            string firstGroupLocator = groups.First().Locator;
            var scopedMolecules = server.GetLocations(JsonToolConstants.LEVEL_MOLECULE, firstGroupLocator);
            Assert.AreEqual(doc.MoleculeGroups.First().MoleculeCount, scopedMolecules.Length);

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
            string[] names = server.GetReplicateNames();
            AssertEx.AreEqual(chromatograms.Count, names.Length);

            // SetReplicate - navigate to a different replicate
            string targetRep = names.Last();
            server.SetReplicate(targetRep);
            WaitForCondition(() => server.GetReplicateName() == targetRep);

            // Error: nonexistent replicate
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.SetReplicate(@"NonexistentReplicate_12345"));
        }

        private void TestReportDocumentation(JsonToolServer server)
        {
            var topics = server.GetReportDocTopics();
            AssertEx.IsTrue(topics.Length > 0);

            // Expect multiple entity topics from Document Grid scope (Protein, Peptide, etc.)
            AssertEx.IsTrue(topics.Length >= 10 && topics.Length <= 16,
                @"Expected 10-16 topics, got " + topics.Length);

            // No IList`1 or raw generic type names
            AssertEx.IsFalse(topics.Any(t => t.Name.Contains(@"IList") || t.Name.Contains(@"`")),
                @"Raw type names should not appear: " + string.Join(@", ", topics.Select(t => t.Name)));

            // Each topic should have a positive column count
            foreach (var topic in topics)
            {
                AssertEx.IsTrue(topic.ColumnCount > 0,
                    @"Expected positive column count for " + topic.Name);
            }

            // Extract just names for ordering checks
            var topicNames = topics.Select(t => t.Name).ToList();

            // Check hierarchy ordering (using Contains for UI-mode flexibility)
            int proteinsIdx = topicNames.FindIndex(t => t.Contains(@"Protein") || t.Contains(@"MoleculeList"));
            int peptidesIdx = topicNames.FindIndex(t =>
                (t.Contains(@"Peptide") || t.Contains(@"Molecule")) &&
                !t.Contains(@"Result") && !t.Contains(@"List"));
            int precursorsIdx = topicNames.FindIndex(t =>
                t.Contains(@"Precursor") && !t.Contains(@"Result") && !t.Contains(@"Summary"));
            int transitionsIdx = topicNames.FindIndex(t =>
                t.Contains(@"Transition") && !t.Contains(@"Result") && !t.Contains(@"Summary"));
            int replicateIdx = topicNames.FindIndex(t => t.Contains(@"Replicate"));

            AssertEx.IsTrue(proteinsIdx >= 0, @"Expected Protein/MoleculeList topic");
            AssertEx.IsTrue(peptidesIdx >= 0, @"Expected Peptides/Molecules topic");
            AssertEx.IsTrue(precursorsIdx >= 0, @"Expected Precursors topic");
            AssertEx.IsTrue(transitionsIdx >= 0, @"Expected Transitions topic");
            AssertEx.IsTrue(replicateIdx >= 0, @"Expected Replicate topic");
            AssertEx.IsTrue(proteinsIdx < peptidesIdx, @"Protein should come before Peptides");
            AssertEx.IsTrue(peptidesIdx < precursorsIdx, @"Peptides before Precursors");
            AssertEx.IsTrue(precursorsIdx < transitionsIdx, @"Precursors before Transitions");

            // Topic detail retrieval
            var firstTopic = server.GetReportDocTopic(topicNames[0]);
            AssertEx.IsNotNull(firstTopic);
            Assert.IsTrue(firstTopic.Columns.Length > 0);
            AssertEx.IsNotNull(firstTopic.Columns[0].Name);

            // Case-insensitive matching
            AssertEx.IsNotNull(server.GetReportDocTopic(topicNames[0].ToLowerInvariant()));

            // Nonexistent topic
            AssertEx.IsNull(server.GetReportDocTopic(@"CompletelyBogusTopicName"));

            // NormalizedArea discoverable (folded into a topic)
            bool foundNormalizedArea = false;
            foreach (var name in topicNames)
            {
                var detail = server.GetReportDocTopic(name);
                if (detail != null && detail.Columns.Any(c => c.Name == @"NormalizedArea"))
                {
                    foundNormalizedArea = true;
                    break;
                }
            }
            AssertEx.IsTrue(foundNormalizedArea, @"NormalizedArea should appear in some topic");

            // Audit log topics available via scope
            var auditTopics = server.GetReportDocTopics(ReportDefinitionReader.SCOPE_AUDIT_LOG);
            AssertEx.IsTrue(auditTopics.Any(t => t.Name.Contains(@"Audit")));

            // Spot-check: resolve a few columns from first topic
            var document = SkylineWindow.Document;
            var dataSchema = SkylineDataSchema.MemoryDataSchema(document, DataSchemaLocalizer.INVARIANT);
            var reader = new ReportDefinitionReader(dataSchema);
            var sampleColumns = firstTopic.Columns.Skip(3).Take(3)
                .Select(c => c.Name).ToList();
            if (sampleColumns.Count > 0)
                AssertEx.IsNotNull(reader.CreateViewSpec(BuildSelectDef(sampleColumns.ToArray())));

            // Entity references and nested parents must be resolvable (bug fix:
            // ViewEditor lets users check these nodes, so they appear in reports)
            AssertEx.IsNotNull(reader.CreateViewSpec(BuildSelectDef(@"Precursor")),
                @"Root entity 'Precursor' should be resolvable");
            AssertEx.IsNotNull(reader.CreateViewSpec(BuildSelectDef(@"Peptide")),
                @"Parent entity 'Peptide' should be resolvable from Precursor");
            AssertEx.IsNotNull(reader.CreateViewSpec(BuildSelectDef(@"ModifiedSequence")),
                @"Nested parent 'ModifiedSequence' should be resolvable");

            // Row source selection: deepest entity level touched by any column wins
            // (matching DocumentViewTransformer.ConvertFromDocumentView behavior)
            var resolver = new ColumnResolver(dataSchema);
            VerifyRowSource(resolver, new[] { @"PeptideModifiedSequence", @"PrecursorMz", @"Area" },
                typeof(Transition), @"Area is TransitionResult -> Transition row source");
            VerifyRowSource(resolver, new[] { @"PeptideModifiedSequence", @"TotalArea" },
                typeof(Precursor), @"TotalArea is PrecursorResult -> Precursor row source");
            VerifyRowSource(resolver, new[] { @"ProteinName", @"PeptideModifiedSequence" },
                typeof(Peptide), @"PeptideModifiedSequence is Peptide -> Peptide row source");
            VerifyRowSource(resolver, new[] { @"ProteinName", @"ProteinDescription" },
                typeof(Protein), @"Only Protein columns -> Protein row source");

            var aquaMods = new[]
            {
                UniMod.GetModification("Label:13C(6)15N(4) (C-term R)", out _),
                UniMod.GetModification("Label:13C(6)15N(2) (C-term K)", out _),
            };
            RunUI(() => SkylineWindow.ModifyDocument("Add K and R labeling", doc =>
                doc.ChangeSettings(doc.Settings.ChangePeptideModifications(pm =>
                        pm.AddHeavyModifications(aquaMods))
                    .ChangeTransitionFilter(tf => tf.ChangeAutoSelect(true))), AuditLogEntry.SettingsLogFunction));

            // Isotope label type pivoting: Precursor and ModifiedSequence should pivot
            // into label-prefixed columns (e.g., "light Precursor", "heavy Precursor")
            string isotopePivotPath = TestFilesDir.GetTestPath(@"report_isotope_pivot.csv");
            var isotopePivotDef = new ReportDefinition
            {
                Select = new[] { @"Peptide", @"Precursor", @"ModifiedSequence" },
                PivotIsotopeLabel = true
            };
            var isotopePivotMetadata = server.ExportReportFromDefinition(
                isotopePivotDef, isotopePivotPath, JsonToolConstants.CULTURE_INVARIANT);
            // With light + heavy labels, pivoting produces one row per peptide
            // with fewer rows than the unpivoted 2-per-peptide result
            int isotopePivotRows = GetRowCount(isotopePivotMetadata);
            AssertEx.IsTrue(isotopePivotRows == 13,
                string.Format(@"Expected 13 pivoted rows (one per peptide), got {0}", isotopePivotRows));
            // Header should contain label-prefixed columns
            var isotopePivotHeader = File.ReadLines(isotopePivotPath).First();
            AssertEx.Contains(isotopePivotHeader, @"light");
            AssertEx.Contains(isotopePivotHeader, @"heavy");
            // Should have more columns than the 3 selected (pivoted into label x value columns)
            int isotopePivotColCount = isotopePivotHeader.ParseDsvFields(TextUtil.SEPARATOR_CSV).Length;
            AssertEx.IsTrue(isotopePivotColCount > 3,
                string.Format(@"Expected pivoted columns > 3, got {0}", isotopePivotColCount));

            // Audit log scope: verify columns resolve and topic content is accessible
            string auditLogPath = TestFilesDir.GetTestPath(@"report_audit_log.csv");
            var columns = new[] { @"SummaryMessage", @"User" };
            var auditLogDef = new ReportDefinition
            {
                Select = columns,
                DataSource = ReportDefinitionReader.SCOPE_AUDIT_LOG
            };
            var auditLogViewSpec = reader.CreateViewSpec(auditLogDef, ReportDefinitionReader.SCOPE_AUDIT_LOG);
            AssertEx.IsNotNull(auditLogViewSpec, @"Audit log columns should resolve");

            // Verify audit log topic has columns that match what we resolved
            var auditLogTopicDetail = server.GetReportDocTopic(@"AuditLog",
                ReportDefinitionReader.SCOPE_AUDIT_LOG);
            foreach (var column in columns)
                Assert.IsTrue(auditLogTopicDetail.Columns.Any(c => c.Name == column));

            // Verify ModifyDocument entry appears in audit log report, then undo and verify it goes away
            var auditLogSummary = server.ExportReportFromDefinition(
                auditLogDef, auditLogPath, JsonToolConstants.CULTURE_INVARIANT);
            AssertEx.AreEqual(2, Helpers.CountLinesInString(auditLogSummary.Preview));
            AssertEx.Contains(auditLogSummary.Preview, columns.ToDsvLine(TextUtil.SEPARATOR_CSV), "Settings changed");

            RunUI(SkylineWindow.Undo);

            auditLogSummary = server.ExportReportFromDefinition(
                auditLogDef, auditLogPath, JsonToolConstants.CULTURE_INVARIANT);
            AssertEx.AreEqual(1, Helpers.CountLinesInString(auditLogSummary.Preview));
            AssertEx.Contains(auditLogSummary.Preview, columns.ToDsvLine(TextUtil.SEPARATOR_CSV));
        }

        private void TestNamedReports(JsonToolServer server)
        {
            string tempPath = TestFilesDir.GetTestPath(@"report_test.csv");

            // ExportReport - export a built-in report
            var metadata = server.ExportReport(REPORT_AREAS, tempPath, JsonToolConstants.CULTURE_INVARIANT);
            AssertEx.IsNotNull(metadata);

            Assert.AreEqual(16, GetRowCount(metadata));
            AssertEx.IsNotNull(metadata.Columns);

            // Verify file was created
            Assert.IsTrue(File.Exists(tempPath));
            var lines = File.ReadAllLines(tempPath);
            Assert.IsTrue(lines.Length > 1); // Header + data rows

            // Error: nonexistent report
            string tempPath2 = TestFilesDir.GetTestPath(@"report_bad.csv");
            AssertEx.ThrowsException<Exception>(() =>
                server.ExportReport(@"NonexistentReport_12345", tempPath2, JsonToolConstants.CULTURE_INVARIANT));
        }

        private void TestReportFromDefinition(JsonToolServer server)
        {
            // Simple select
            string tempPath = TestFilesDir.GetTestPath(@"report_def_simple.csv");
            var def = BuildSelectDef(COL_PROTEIN_NAME, COL_PEPTIDE_SEQUENCE, COL_PRECURSOR_MZ);
            var metadata = server.ExportReportFromDefinition(def, tempPath, JsonToolConstants.CULTURE_INVARIANT);
            Assert.AreEqual(13, GetRowCount(metadata));
            var lines = File.ReadAllLines(tempPath);
            string header = lines[0];
            AssertEx.Contains(header, COL_PROTEIN_NAME);
            AssertEx.Contains(header, @"PeptideModifiedSequence");
            AssertEx.Contains(header, COL_PRECURSOR_MZ);

            // With filter: PrecursorMz > 500
            string tempPathFilter = TestFilesDir.GetTestPath(@"report_def_filter.csv");
            var filterDef = new ReportDefinition
            {
                Select = new[] { COL_PROTEIN_NAME, COL_PRECURSOR_MZ },
                Filter = new[]
                {
                    new ReportFilter { Column = COL_PRECURSOR_MZ, Op = @">", Value = @"500" }
                }
            };
            var filterMetadata = server.ExportReportFromDefinition(filterDef, tempPathFilter, JsonToolConstants.CULTURE_INVARIANT);
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
            var sortDef = new ReportDefinition
            {
                Select = new[] { COL_PROTEIN_NAME, COL_PRECURSOR_MZ },
                Sort = new[]
                {
                    new ReportSort { Column = COL_PRECURSOR_MZ, Direction = JsonToolConstants.SORT_DESC }
                }
            };
            var sortMetadata = server.ExportReportFromDefinition(sortDef, tempPathSort, JsonToolConstants.CULTURE_INVARIANT);
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

            // With pivot_replicate
            string tempPathPivot = TestFilesDir.GetTestPath(@"report_def_pivot.csv");
            var pivotDef = BuildSelectPivotDef(COL_PEPTIDE_SEQUENCE, COL_TOTAL_AREA);
            var pivotMetadata = server.ExportReportFromDefinition(pivotDef, tempPathPivot, JsonToolConstants.CULTURE_INVARIANT);
            // Peptide row source: one row per peptide (13), not per protein (5)
            Assert.AreEqual(13, GetRowCount(pivotMetadata));
            // Pivoted header should contain replicate names as column suffixes
            var pivotHeader = File.ReadLines(tempPathPivot).First();
            // The document has replicates, so the header should have more columns than the 2 we selected
            int pivotColCount = pivotHeader.ParseDsvFields(TextUtil.SEPARATOR_CSV).Length;
            Assert.IsTrue(pivotColCount > 2);

            // With sort ascending (exercises "asc" path in ParseSortDirection)
            string tempPathSortAsc = TestFilesDir.GetTestPath(@"report_def_sort_asc.csv");
            var sortAscDef = new ReportDefinition
            {
                Select = new[] { COL_PROTEIN_NAME, COL_PRECURSOR_MZ },
                Sort = new[]
                {
                    new ReportSort { Column = COL_PRECURSOR_MZ, Direction = JsonToolConstants.SORT_ASC }
                }
            };
            server.ExportReportFromDefinition(sortAscDef, tempPathSortAsc, JsonToolConstants.CULTURE_INVARIANT);
            var sortAscLines = File.ReadAllLines(tempPathSortAsc);
            int sortAscMzIndex = Array.IndexOf(sortAscLines[0].ParseDsvFields(TextUtil.SEPARATOR_CSV), COL_PRECURSOR_MZ);
            double prevAscMz = 0;
            for (int i = 1; i < sortAscLines.Length; i++)
            {
                string[] cols = sortAscLines[i].ParseDsvFields(TextUtil.SEPARATOR_CSV);
                if (cols.Length <= sortAscMzIndex || string.IsNullOrEmpty(cols[sortAscMzIndex]))
                    continue;
                double mz = double.Parse(cols[sortAscMzIndex], CultureInfo.InvariantCulture);
                Assert.IsTrue(mz >= prevAscMz, @"Row {0}: {1} should be >= {2}", i, mz, prevAscMz);
                prevAscMz = mz;
            }

            // With unary filter: isnullorblank (exercises unary op path in ParseFilterSpecs)
            string tempPathUnary = TestFilesDir.GetTestPath(@"report_def_unary.csv");
            var unaryDef = new ReportDefinition
            {
                Select = new[] { COL_PROTEIN_NAME, COL_TOTAL_AREA },
                Filter = new[]
                {
                    new ReportFilter { Column = COL_TOTAL_AREA, Op = @"isnotnullorblank" }
                }
            };
            var unaryMetadata = server.ExportReportFromDefinition(unaryDef, tempPathUnary, JsonToolConstants.CULTURE_INVARIANT);
            Assert.IsTrue(GetRowCount(unaryMetadata) > 0);

            // Error: empty select
            string tempPathBad = TestFilesDir.GetTestPath(@"report_def_bad.csv");
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.ExportReportFromDefinition(new ReportDefinition { Select = new string[0] },
                    tempPathBad, JsonToolConstants.CULTURE_INVARIANT));

            // Error: unknown column with "did you mean" suggestion
            var badColDef = BuildSelectDef(COL_PRECURSOR_MZ + @"z", COL_PROTEIN_NAME);
            AssertEx.ThrowsException<ArgumentException>(() =>
                    server.ExportReportFromDefinition(badColDef, tempPathBad, JsonToolConstants.CULTURE_INVARIANT),
                ex => AssertEx.Contains(ex.Message, COL_PRECURSOR_MZ));

            // Error: unknown filter column
            var badFilterColDef = new ReportDefinition
            {
                Select = new[] { COL_PROTEIN_NAME },
                Filter = new[]
                {
                    new ReportFilter { Column = @"NotAColumn_xyz", Op = @">", Value = @"1" }
                }
            };
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.ExportReportFromDefinition(badFilterColDef, tempPathBad, JsonToolConstants.CULTURE_INVARIANT));

            // Error: unknown filter operation
            var badFilterOpDef = new ReportDefinition
            {
                Select = new[] { COL_PROTEIN_NAME },
                Filter = new[]
                {
                    new ReportFilter { Column = COL_PROTEIN_NAME, Op = @"bogus", Value = @"1" }
                }
            };
            AssertEx.ThrowsException<ArgumentException>(() =>
                    server.ExportReportFromDefinition(badFilterOpDef, tempPathBad, JsonToolConstants.CULTURE_INVARIANT),
                ex => AssertEx.Contains(ex.Message, @"bogus"));

            // Error: missing value for binary filter op
            var missingValDef = new ReportDefinition
            {
                Select = new[] { COL_PROTEIN_NAME },
                Filter = new[]
                {
                    new ReportFilter { Column = COL_PROTEIN_NAME, Op = @">" }
                }
            };
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.ExportReportFromDefinition(missingValDef, tempPathBad, JsonToolConstants.CULTURE_INVARIANT));

            // Error: invalid sort direction
            var badSortDef = new ReportDefinition
            {
                Select = new[] { COL_PROTEIN_NAME },
                Sort = new[]
                {
                    new ReportSort { Column = COL_PROTEIN_NAME, Direction = @"sideways" }
                }
            };
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.ExportReportFromDefinition(badSortDef, tempPathBad, JsonToolConstants.CULTURE_INVARIANT));
        }

        private static int GetRowCount(ReportMetadata metadata)
        {
            return metadata.RowCount ?? 0;
        }

        private static void VerifyRowSource(ColumnResolver resolver, string[] columns,
            Type expectedRowSource, string message)
        {
            var result = resolver.Resolve(columns);
            AssertEx.AreEqual(expectedRowSource, result.RowSourceType,
                string.Format(@"Row source mismatch: {0}", message));
        }

        private static ReportDefinition BuildSelectDef(params string[] columns)
        {
            return new ReportDefinition { Select = columns };
        }

        private static ReportDefinition BuildSelectPivotDef(params string[] columns)
        {
            return new ReportDefinition { Select = columns, PivotReplicate = true };
        }

        private void TestDocumentSettings(JsonToolServer server)
        {
            string elementName = typeof(SrmSettings)
                .GetCustomAttribute<XmlRootAttribute>()
                ?.ElementName; // "settings_summary"
            string expectedTag = @"<" + elementName;

            string docSettingsPath = TestFilesDir.GetTestPath(@"doc_settings.xml");
            string result = server.GetDocumentSettings(docSettingsPath);
            Assert.IsTrue(File.Exists(docSettingsPath));
            string xml = File.ReadAllText(docSettingsPath);
            AssertEx.Contains(xml, expectedTag);

            string defSettingsPath = TestFilesDir.GetTestPath(@"default_settings.xml");
            string defResult = server.GetDefaultSettings(defSettingsPath);
            Assert.IsTrue(File.Exists(defSettingsPath));
            string defXml = File.ReadAllText(defSettingsPath);
            AssertEx.Contains(defXml, expectedTag);

            // Document settings and default settings should differ
            Assert.AreNotEqual(xml, defXml);
        }

        private void TestAvailableTutorials(JsonToolServer server)
        {
            var tutorials = server.GetAvailableTutorials();
            Assert.IsTrue(tutorials.Length > 0);

            Assert.AreEqual(TutorialCatalog.Tutorials.Length, tutorials.Length);

            // Each tutorial should have non-null properties
            foreach (var tutorial in tutorials)
            {
                Assert.IsFalse(string.IsNullOrEmpty(tutorial.Name),
                    @"Tutorial Name should not be null or empty");
                Assert.IsFalse(string.IsNullOrEmpty(tutorial.Title),
                    @"Tutorial Title should not be null or empty");
            }
        }

        private void TestCliHelp(JsonToolServer server)
        {
            // RunCommandSilent with --help should return sections list
            string iwBefore = GetImmediateWindowText();
            string sections = server.RunCommandSilent(new[] { @"--help=sections", @"--help=no-borders" });
            Assert.IsFalse(string.IsNullOrEmpty(sections));
            // Silent mode should not write to Immediate Window
            Assert.AreEqual(iwBefore, GetImmediateWindowText());
        }

        private void TestDiagnosticLogging(JsonToolServer server)
        {
            // Helper to build a JSON-RPC 2.0 request with optional logging
            byte[] buildRequest(string method, bool log, params string[] args)
            {
                var obj = new JObject
                {
                    [nameof(JSON_RPC.jsonrpc)] = JsonToolConstants.JSONRPC_VERSION,
                    [nameof(JSON_RPC.method)] = method,
                    [nameof(JSON_RPC.id)] = 1
                };
                if (args.Length > 0)
                    obj[nameof(JSON_RPC.@params)] = new JArray(args);
                if (log)
                    obj[nameof(JSON_RPC._log)] = true;
                return Encoding.UTF8.GetBytes(obj.ToString());
            }

            // Logging disabled by default - no _log field in response
            string normalResult = server.HandleRequest(
                buildRequest(nameof(IJsonToolService.GetVersion), false));
            var normalJson = JObject.Parse(normalResult);
            Assert.IsNotNull(normalJson[nameof(JSON_RPC.result)]);
            Assert.IsNull(normalJson[nameof(JSON_RPC._log)]);

            // Logging enabled but GetVersion has no Log() calls - no _log field
            string loggedResult = server.HandleRequest(
                buildRequest(nameof(IJsonToolService.GetVersion), true));
            var loggedJson = JObject.Parse(loggedResult);
            Assert.IsNotNull(loggedJson[nameof(JSON_RPC.result)]);
            Assert.IsNull(loggedJson[nameof(JSON_RPC._log)]);

            // Error response without logging - no _log field
            string errorResult = server.HandleRequest(
                buildRequest(@"NotARealMethod", false));
            var errorJson = JObject.Parse(errorResult);
            Assert.IsNotNull(errorJson[nameof(JSON_RPC.error)]);
            Assert.IsNull(errorJson[nameof(JSON_RPC._log)]);

            // ExportReportFromDefinition with logging - _log field should appear
            string reportPath = TestFilesDir.GetTestPath(@"report_log_test.csv");
            string reportJson = new JObject { [@"select"] = new JArray(COL_PROTEIN_NAME, COL_PRECURSOR_MZ) }.ToString();
            var reportRequest = new JObject
            {
                [nameof(JSON_RPC.jsonrpc)] = JsonToolConstants.JSONRPC_VERSION,
                [nameof(JSON_RPC.method)] = nameof(IJsonToolService.ExportReportFromDefinition),
                [nameof(JSON_RPC.@params)] = new JArray(reportJson, reportPath,
                    JsonToolConstants.CULTURE_INVARIANT),
                [nameof(JSON_RPC._log)] = true,
                [nameof(JSON_RPC.id)] = 1
            };
            string reportResult = server.HandleRequest(
                Encoding.UTF8.GetBytes(reportRequest.ToString()));
            var reportResultJson = JObject.Parse(reportResult);
            Assert.IsNotNull(reportResultJson[nameof(JSON_RPC.result)]);
            string logContent = (string)reportResultJson[nameof(JSON_RPC._log)];
            Assert.IsNotNull(logContent, @"Log should appear for method with Log() calls");
            AssertEx.Contains(logContent, @"ms");
            AssertEx.Contains(logContent, @"Resolved");

            // Same call without logging - no _log field
            string reportPath2 = TestFilesDir.GetTestPath(@"report_nolog_test.csv");
            var noLogRequest = new JObject
            {
                [nameof(JSON_RPC.jsonrpc)] = JsonToolConstants.JSONRPC_VERSION,
                [nameof(JSON_RPC.method)] = nameof(IJsonToolService.ExportReportFromDefinition),
                [nameof(JSON_RPC.@params)] = new JArray(reportJson, reportPath2,
                    JsonToolConstants.CULTURE_INVARIANT),
                [nameof(JSON_RPC.id)] = 1
            };
            string noLogResult = server.HandleRequest(
                Encoding.UTF8.GetBytes(noLogRequest.ToString()));
            var noLogResultJson = JObject.Parse(noLogResult);
            Assert.IsNotNull(noLogResultJson[nameof(JSON_RPC.result)]);
            Assert.IsNull(noLogResultJson[nameof(JSON_RPC._log)]);
        }

        private void TestOpenForms(JsonToolServer server)
        {
            var forms = server.GetOpenForms();
            Assert.IsTrue(forms.Length > 0);
            // The Targets panel (SequenceTreeForm) should always be open
            Assert.IsTrue(forms.Any(f => f.Type == nameof(SequenceTreeForm)));
        }

        private void TestScreenCapturePermissionDlg(JsonToolServer server)
        {
            // Ensure permission is not pre-granted
            RunUI(() =>
            {
                Settings.Default.AllowMcpScreenCapture = false;
                ScreenCapture.ResetSessionPermission();
            });

            string formId = server.GetOpenForms()
                .First(f => f.Type == nameof(SequenceTreeForm)).Id;

            bool desktopAvailable = ScreenCapture.IsDesktopAvailable();

            // Test Deny - dialog should return denial message
            // Run server call on a background thread (like the real pipe server thread)
            // so InvokeOnUiThread marshals to the UI thread correctly.
            string imagePath = TestFilesDir.GetTestPath(@"deny_test.png");
            string denyResult = null;
            ActionUtil.RunAsync(() => denyResult = server.GetFormImage(formId, imagePath));
            var dlg = WaitForOpenForm<ScreenCapturePermissionDlg>();
            Assert.IsFalse(dlg.DoNotAskAgain);
            CancelDialog(dlg);
            WaitForCondition(() => denyResult != null);
            AssertEx.Contains(denyResult, @"denied");
            Assert.IsFalse(File.Exists(imagePath));

            // Test Allow - dialog should grant session permission
            string allowPath = TestFilesDir.GetTestPath(@"allow_test.png");
            string allowResult = null;
            ActionUtil.RunAsync(() => allowResult = server.GetFormImage(formId, allowPath));
            dlg = WaitForOpenForm<ScreenCapturePermissionDlg>();
            Assert.IsFalse(dlg.DoNotAskAgain);
            OkDialog(dlg);
            WaitForCondition(() => allowResult != null);
            if (desktopAvailable)
            {
                // After Allow, file should be created
                Assert.IsTrue(File.Exists(allowPath));
                Assert.IsTrue(new FileInfo(allowPath).Length > 0);
            }
            else
            {
                AssertEx.Contains(allowResult, @"not available");
            }

            // Session permission is now granted - subsequent calls should not show dialog
            string sessionPath = TestFilesDir.GetTestPath(@"session_test.png");
            string sessionResult = server.GetFormImage(formId, sessionPath);
            if (desktopAvailable)
                Assert.IsTrue(File.Exists(sessionPath));
            else
                AssertEx.Contains(sessionResult, @"not available");

            // Test Allow + DoNotAskAgain - should persist the setting
            RunUI(() =>
            {
                Settings.Default.AllowMcpScreenCapture = false;
                ScreenCapture.ResetSessionPermission();
            });

            string persistPath = TestFilesDir.GetTestPath(@"persist_test.png");
            string persistResult = null;
            ActionUtil.RunAsync(() => persistResult = server.GetFormImage(formId, persistPath));
            dlg = WaitForOpenForm<ScreenCapturePermissionDlg>();
            RunUI(() => dlg.DoNotAskAgain = true);
            OkDialog(dlg);
            WaitForCondition(() => persistResult != null);
            if (desktopAvailable)
                Assert.IsTrue(File.Exists(persistPath));
            else
                AssertEx.Contains(persistResult, @"not available");
            Assert.IsTrue(Settings.Default.AllowMcpScreenCapture);

            // Clean up setting for other tests
            RunUI(() =>
            {
                Settings.Default.AllowMcpScreenCapture = false;
                ScreenCapture.ResetSessionPermission();
            });
        }

        private void TestFormImage(JsonToolServer server)
        {
            // Grant permission so we can test image capture without dialog
            RunUI(() => Settings.Default.AllowMcpScreenCapture = true);

            // Capture the Targets panel
            string formId = server.GetOpenForms()
                .First(f => f.Type == nameof(SequenceTreeForm)).Id;
            string imageName = @"form_image_test.png";
            string imagePath = TestFilesDir.GetTestPath(imageName);

            bool desktopAvailable = ScreenCapture.IsDesktopAvailable();

            string result = server.GetFormImage(formId, imagePath);
            if (desktopAvailable)
            {
                Assert.IsTrue(File.Exists(imagePath));
                Assert.IsTrue(new FileInfo(imagePath).Length > 0);
                // Result should be the file path (forward-slash format)
                AssertEx.Contains(result, imageName);

                // Verify the image is valid by loading it
                using (var img = Image.FromFile(imagePath))
                {
                    Assert.IsTrue(img.Width > 0);
                    Assert.IsTrue(img.Height > 0);
                }

                // Auto-generated path (null filePath) - exercise default path logic
                string autoImagePath = null;
                try
                {
                    autoImagePath = server.GetFormImage(formId);
                    Assert.IsTrue(File.Exists(autoImagePath));
                }
                finally
                {
                    if (autoImagePath != null)
                        FileEx.SafeDelete(autoImagePath);
                }
            }
            else
            {
                AssertEx.Contains(result, @"not available");
            }

            // Error: invalid form ID
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.GetFormImage(@"NonexistentForm:NoTitle",
                    TestFilesDir.GetTestPath(@"form_bad.png")));

            // Clean up
            RunUI(() => Settings.Default.AllowMcpScreenCapture = false);
        }

        private void TestGraphDataAndImage(JsonToolServer server)
        {
            // Find a graph form from the open forms list
            var forms = server.GetOpenForms();
            var graphForm = forms.FirstOrDefault(f => f.Type.Contains(@"GraphSummary"));
            if (graphForm == null)
            {
                // No graph open - skip gracefully (Rat_plasma.sky should have graphs)
                return;
            }

            string graphId = graphForm.Id;

            // GetGraphData - export to TSV
            string dataPath = TestFilesDir.GetTestPath(@"graph_data.tsv");
            string dataResult = server.GetGraphData(graphId, dataPath);
            Assert.IsTrue(File.Exists(dataPath));
            Assert.IsTrue(new FileInfo(dataPath).Length > 0);

            // GetGraphImage - export to PNG
            string imagePath = TestFilesDir.GetTestPath(@"graph_image.png");
            string imageResult = server.GetGraphImage(graphId, imagePath);
            Assert.IsTrue(File.Exists(imagePath));
            Assert.IsTrue(new FileInfo(imagePath).Length > 0);
            // Verify it's a valid image
            using (var img = Image.FromFile(imagePath))
            {
                Assert.IsTrue(img.Width > 0);
                Assert.IsTrue(img.Height > 0);
            }

            // Auto-generated path (null filePath) - exercise default path logic
            string autoDataPath = null;
            try
            {
                autoDataPath = server.GetGraphData(graphId);
                Assert.IsTrue(File.Exists(autoDataPath));
            }
            finally
            {
                if (autoDataPath != null)
                    FileEx.SafeDelete(autoDataPath);
            }

            // Error: invalid graph ID
            string badGraphPath = TestFilesDir.GetTestPath(@"graph_bad.tsv");
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.GetGraphData(@"NonexistentGraph:NoTitle", badGraphPath));
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.GetGraphImage(@"NonexistentGraph:NoTitle",
                    TestFilesDir.GetTestPath(@"graph_bad.png")));

            // Error: non-graph form used with graph methods
            string nonGraphId = forms.First(f => f.Type == nameof(SequenceTreeForm)).Id;
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.GetGraphData(nonGraphId, badGraphPath));
        }

        private const string TUTORIAL_NAME = @"MethodEdit";
        private const string TUTORIAL_EN = @"en";

        private void TestTutorialFetch(JsonToolServer server)
        {
            // Read real tutorial HTML from the repo (same content GitHub would serve)
            string tutorialsDir = TestContext.GetProjectDirectory(@"Documentation\Tutorials");
            string htmlPath = Path.Combine(tutorialsDir, TUTORIAL_NAME, TUTORIAL_EN, @"index.html");
            Assert.IsTrue(File.Exists(htmlPath), @"Tutorial HTML not found: " + htmlPath);
            string realHtml = File.ReadAllText(htmlPath);

            // Serve the real HTML via HttpClientTestHelper (no network access)
            string tutorialPath = TestFilesDir.GetTestPath(@"tutorial_test.md");
            using (HttpClientTestHelper.SimulateSuccessfulDownload(realHtml))
            {
                var metadata = server.GetTutorial(TUTORIAL_NAME, TUTORIAL_EN, tutorialPath);

                // Verify metadata
                Assert.AreEqual(TUTORIAL_NAME, metadata.Tutorial);
                Assert.AreEqual(TUTORIAL_EN, metadata.Language);
                Assert.IsTrue(metadata.Toc?.Length > 0);
                Assert.IsTrue(metadata.LineCount > 10);
            }

            // Verify markdown was written and has expected structure
            Assert.IsTrue(File.Exists(tutorialPath));
            string markdown = File.ReadAllText(tutorialPath);
            AssertEx.Contains(markdown, @"# ");  // Has headings
            AssertEx.Contains(markdown, @"[Screenshot:");  // Has image placeholders

            // Serve a real tutorial image from the repo
            string imageFilename = @"s-01.png";
            string repoImagePath = Path.Combine(tutorialsDir, TUTORIAL_NAME, TUTORIAL_EN, imageFilename);
            Assert.IsTrue(File.Exists(repoImagePath), @"Tutorial image not found: " + repoImagePath);
            byte[] realImageData = File.ReadAllBytes(repoImagePath);

            string imagePath = TestFilesDir.GetTestPath(@"tutorial_image.png");
            using (HttpClientTestHelper.SimulateSuccessfulDownload(realImageData))
            {
                var imageMetadata = server.GetTutorialImage(TUTORIAL_NAME, imageFilename, TUTORIAL_EN, imagePath);
                Assert.IsNotNull(imageMetadata.FilePath);
                Assert.AreEqual(imageFilename, imageMetadata.Image);
            }
            Assert.IsTrue(File.Exists(imagePath));
            Assert.AreEqual(realImageData.Length, new FileInfo(imagePath).Length);
        }

        private void TestTutorialFetchErrors(JsonToolServer server)
        {
            // Unknown tutorial name
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.GetTutorial(@"NonexistentTutorial"));

            // HTTP 404 - tutorial not found on GitHub
            string tempPath = TestFilesDir.GetTestPath(@"tutorial_404.md");
            using (HttpClientTestHelper.SimulateHttp404())
            {
                AssertEx.ThrowsException<IOException>(() =>
                    server.GetTutorial(TUTORIAL_NAME, TUTORIAL_EN, tempPath));
            }

            // Network failure
            using (HttpClientTestHelper.SimulateNoNetworkInterface())
            {
                AssertEx.ThrowsException<IOException>(() =>
                    server.GetTutorial(TUTORIAL_NAME, TUTORIAL_EN, tempPath));
            }

            // User cancellation
            using (HttpClientTestHelper.SimulateCancellation())
            {
                AssertEx.ThrowsException<IOException>(() =>
                    server.GetTutorial(TUTORIAL_NAME, TUTORIAL_EN, tempPath));
            }

            // Invalid image filename (path traversal prevention)
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.GetTutorialImage(TUTORIAL_NAME, @"../../../etc/passwd"));
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.GetTutorialImage(TUTORIAL_NAME, @"sub\dir\img.png"));
        }

        private void TestInsertSmallMoleculeTransitionList(JsonToolServer server)
        {
            var doc = SkylineWindow.Document;
            int groupsBefore = doc.MoleculeGroupCount;

            string csvText = GetSmallMoleculeTransitionsText();
            server.InsertSmallMoleculeTransitionList(csvText);

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
            var groups = server.GetLocations(JsonToolConstants.LEVEL_GROUP);
            Assert.IsTrue(groups.Length > 0);

            // Build CSV for ImportProperties: ElementLocator,TestAnnotation
            string firstGroupLocator = groups.First().Locator;
            string csvText = @"ElementLocator,TestAnnotation" + Environment.NewLine +
                             firstGroupLocator + @",test_value";

            server.ImportProperties(csvText);
        }

        private SrmDocument TestImportFasta(JsonToolServer server)
        {
            var docBefore = SkylineWindow.Document;

            // Select the insertion node to append at end (matching command-line behavior)
            server.SetSelectedElement(JsonUiService.INSERT_NODE_LOCATOR);

            // Import with keepEmptyProteins=true - protein has no peptides matching
            // the document filter criteria, so it will be empty
            server.ImportFasta(TEXT_FASTA, @"true");
            WaitForProteinMetadataBackgroundLoaderCompletedUI();
            var docAfterKeep = SkylineWindow.Document;
            Assert.IsTrue(docAfterKeep.MoleculeGroupCount > docBefore.MoleculeGroupCount,
                @"FASTA import should add protein groups");
            Assert.IsTrue(docAfterKeep.MoleculeGroups.Any(g => g.Name.Contains(@"ALBU_BOVIN")),
                @"Document should contain the imported ALBU_BOVIN protein");

            RunUI(() => SkylineWindow.Undo());
            Assert.AreSame(docBefore, SkylineWindow.Document);

            // Import with keepEmptyProteins=false - empty protein should be removed
            server.ImportFasta(TEXT_FASTA, @"false");
            var docAfterRemove = SkylineWindow.Document;
            Assert.IsFalse(docAfterRemove.MoleculeGroups.Any(g => g.Name.Contains(@"ALBU_BOVIN")),
                @"Empty ALBU_BOVIN protein should have been removed");
            Assert.AreSame(docBefore, SkylineWindow.Document,
                @"Document should be unchanged when all imported proteins are empty");
            return docAfterKeep;
        }

        private void TestRunCommand(JsonToolServer server, SrmDocument docAfterKeep)
        {
            // --version output should contain both parts of the version string
            string versionResult = server.RunCommandSilent(new[] { CommandArgs.ARG_VERSION.ArgumentText });
            // GetVersion returns "26.1.1.061-6c3244bc0a", --version shows "26.1.1.061 (6c3244bc0a)"
            AssertEx.Contains(versionResult, server.GetVersion().Split('-'));

            // Read operation: export a report via CLI (non-silent, writes to Immediate Window)
            string reportPath = TestFilesDir.GetTestPath(@"run_command_report.csv");
            var reportArgs = new[]
            {
                CommandArgs.ARG_REPORT_NAME + REPORT_AREAS,
                CommandArgs.ARG_REPORT_FILE + reportPath.ToForwardSlashPath()
            };
            server.RunCommand(reportArgs);
            Assert.IsTrue(File.Exists(reportPath));
            Assert.IsTrue(new FileInfo(reportPath).Length > 0);
            // Verify Immediate Window contains the command header and report output
            string iwAfterReport = GetImmediateWindowText();
            AssertEx.Contains(iwAfterReport, TextUtil.SpaceSeparate(reportArgs));
            // Resource: "Exporting report {0}..." and "Report {0} exported successfully to {1}."
            AssertEx.Contains(iwAfterReport,
                string.Format(SkylineResources.CommandLine_ExportLiveReport_Exporting_report__0____, REPORT_AREAS));
            AssertEx.Contains(iwAfterReport,
                string.Format(SkylineResources.CommandLine_ExportLiveReport_Report__0__exported_successfully_to__1__,
                    REPORT_AREAS, reportPath));

            // Write operation: refine to remove proteins with fewer than 100 peptides
            var docBeforeRefine = SkylineWindow.Document;
            var refineArgs = new[] { CommandArgs.ARG_REFINE_MIN_PEPTIDES + @"100" };
            server.RunCommand(refineArgs);
            Assert.AreEqual(0, SkylineWindow.Document.MoleculeGroupCount,
                @"Refine with min-peptides=100 should remove all proteins");
            string iwAfterRefine = GetImmediateWindowText();
            AssertEx.Contains(iwAfterRefine, TextUtil.SpaceSeparate(refineArgs));
            // Resource: "Refining document..."
            AssertEx.Contains(iwAfterRefine,
                Resources.CommandLine_RefineDocument_Refining_document___);

            // Undo and verify exact document identity is restored
            RunUI(() => SkylineWindow.Undo());
            Assert.AreSame(docBeforeRefine, SkylineWindow.Document);

            // Write operation: import FASTA via CLI
            string fastaPath = TestFilesDir.GetTestPath(@"import_test.fasta");
            File.WriteAllText(fastaPath, TEXT_FASTA);
            var docBeforeFasta = SkylineWindow.Document;
            var fastaArgs = new[]
            {
                CommandArgs.ARG_IMPORT_FASTA + fastaPath.ToForwardSlashPath(),
                CommandArgs.ARG_KEEP_EMPTY_PROTEINS.ArgumentText
            };
            server.RunCommand(fastaArgs);
            WaitForProteinMetadataBackgroundLoaderCompletedUI();
            var docAfterFasta = SkylineWindow.Document;
            Assert.IsTrue(docAfterFasta.MoleculeGroupCount > docBeforeFasta.MoleculeGroupCount,
                @"FASTA import should add protein groups");
            Assert.IsTrue(docAfterFasta.MoleculeGroups.Any(g => g.Name.Contains(@"ALBU_BOVIN")),
                @"Document should contain the imported ALBU_BOVIN protein");
            AssertEx.DocumentCloned(docAfterKeep, docAfterFasta);
            string iwAfterFasta = GetImmediateWindowText();
            AssertEx.Contains(iwAfterFasta, TextUtil.SpaceSeparate(fastaArgs));
            // Resource: "Importing FASTA file {0}..."
            AssertEx.Contains(iwAfterFasta,
                string.Format(Resources.CommandLine_ImportFasta_Importing_FASTA_file__0____,
                    Path.GetFileName(fastaPath)));

            // Undo and verify exact document identity is restored
            RunUI(() => SkylineWindow.Undo());
            Assert.AreSame(docBeforeFasta, SkylineWindow.Document);
        }

        /// <summary>
        /// Test document-level operations (--open, --new, --save, --save-as) through
        /// RunCommand, verifying DocumentFilePath updates, dirty state, and round-trip.
        /// </summary>
        private void TestDocumentOperations(JsonToolServer server)
        {
            string originalPath = SkylineWindow.DocumentFilePath;
            int originalGroups = SkylineWindow.Document.MoleculeGroupCount;

            // Save original document so on-disk state matches in-memory state
            // (prior tests may have modified the document without saving)
            server.RunCommand(new[] { CommandArgs.ARG_SAVE.ArgumentText });

            // --new: create empty document at a temp path
            // Note: paths must be quoted because test directories may contain spaces
            string newPath = TestFilesDir.GetTestPath(@"doc_ops_new.sky");
            string newResult = server.RunCommand(new[] { CommandArgs.ARG_NEW + newPath });
            AssertEx.Contains(newResult, Path.GetFileName(newPath));
            AssertEx.AreEqual(newPath, SkylineWindow.DocumentFilePath);
            AssertEx.AreEqual(0, SkylineWindow.Document.MoleculeGroupCount);

            // Import a FASTA to make the document non-trivial
            server.SetSelectedElement(JsonUiService.INSERT_NODE_LOCATOR);
            server.ImportFasta(TEXT_FASTA, @"true");
            WaitForProteinMetadataBackgroundLoaderCompletedUI();
            Assert.IsTrue(SkylineWindow.Document.MoleculeGroupCount > 0);
            Assert.IsTrue(SkylineWindow.Dirty);

            // --save: save the modified document, verify clean state
            string saveResult = server.RunCommand(new[] { CommandArgs.ARG_SAVE.ArgumentText });
            AssertEx.Contains(saveResult, Path.GetFileName(newPath));
            AssertEx.AreEqual(newPath, SkylineWindow.DocumentFilePath);
            Assert.IsFalse(SkylineWindow.Dirty);
            int savedGroups = SkylineWindow.Document.MoleculeGroupCount;

            // --save-as (synonym for --out): save to a different path
            string saveAsPath = TestFilesDir.GetTestPath(@"doc_ops_saveas.sky");
            string saveAsResult = server.RunCommand(new[] { CommandArgs.ARG_SAVE_AS + saveAsPath });
            AssertEx.Contains(saveAsResult, Path.GetFileName(saveAsPath));
            AssertEx.AreEqual(saveAsPath, SkylineWindow.DocumentFilePath);
            Assert.IsFalse(SkylineWindow.Dirty);
            Assert.IsTrue(File.Exists(saveAsPath));

            // --open (synonym for --in): re-open the first saved file, verify round-trip
            string openResult = server.RunCommand(new[] { CommandArgs.ARG_OPEN + newPath });
            AssertEx.Contains(openResult, Path.GetFileName(newPath));
            AssertEx.AreEqual(newPath, SkylineWindow.DocumentFilePath);
            AssertEx.AreEqual(savedGroups, SkylineWindow.Document.MoleculeGroupCount);
            Assert.IsTrue(SkylineWindow.Document.MoleculeGroups.Any(
                g => g.Name.Contains(@"ALBU_BOVIN")));

            // --in: re-open the original document from disk
            string inResult = server.RunCommand(new[] { CommandArgs.ARG_IN + originalPath });
            AssertEx.Contains(inResult, Path.GetFileName(originalPath));
            AssertEx.AreEqual(originalPath, SkylineWindow.DocumentFilePath);
            AssertEx.AreEqual(originalGroups, SkylineWindow.Document.MoleculeGroupCount);

            // Combined: --open + --refine + --out
            string combinedPath = TestFilesDir.GetTestPath(@"doc_ops_combined.sky");
            string combinedResult = server.RunCommand(new[]
            {
                CommandArgs.ARG_OPEN + newPath,
                CommandArgs.ARG_REFINE_MIN_PEPTIDES + @"100",
                CommandArgs.ARG_OUT + combinedPath
            });
            AssertEx.AreEqual(combinedPath, SkylineWindow.DocumentFilePath);
            AssertEx.AreEqual(0, SkylineWindow.Document.MoleculeGroupCount);
            Assert.IsTrue(File.Exists(combinedPath));

            // --new to leave a clean state for subsequent tests
            string tempPath = TestFilesDir.GetTestPath(@"doc_ops_temp.sky");
            server.RunCommand(new[]
            {
                CommandArgs.ARG_NEW + tempPath,
                CommandArgs.ARG_OVERWRITE.ArgumentText
            });
        }

        /// <summary>
        /// Get the current text content of the Immediate Window.
        /// </summary>
        private string GetImmediateWindowText()
        {
            string text = null;
            RunUI(() =>
            {
                SkylineWindow.ShowImmediateWindow();
                text = SkylineWindow.ImmediateWindow.TextContent;
            });
            return text;
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
