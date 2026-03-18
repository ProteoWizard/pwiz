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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests for settings list tools in <see cref="JsonToolServer"/>:
    /// enumerate, inspect, add, and select settings list items.
    /// Runs with a blank document — no test data ZIP needed.
    /// </summary>
    [TestClass]
    public class JsonToolServerSettingsTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestJsonToolServerSettings()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Ensure proteomic mode for report column resolution
            RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.proteomic));

            string testGuid = @"test-" + Guid.NewGuid();
            var toolService = new ToolService(testGuid, SkylineWindow);
            var server = new JsonToolServer(toolService, testGuid);

            TestSettingsLists(server);
            TestAddSettingsListItem(server);
            TestAddReport(server);
            TestSettingsListSelection(server);

            // Molecule mode: switch and verify uimode on report definitions
            RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules));
            TestAddReportMoleculeMode(server);
        }

        #region Settings list enumeration and inspection

        private void TestSettingsLists(JsonToolServer server)
        {
            string enzymesName = JsonToolServer.GetSettingsListName<EnzymeList>();
            string reportsName = JsonToolServer.GetSettingsListName<PersistedViews>();
            string heavyModsName = JsonToolServer.GetSettingsListName<HeavyModList>();

            // GetSettingsListTypes - should return LlmName values, not property names
            string types = server.GetSettingsListTypes();
            AssertEx.Contains(types, enzymesName);
            AssertEx.Contains(types, reportsName);
            AssertEx.Contains(types, heavyModsName);
            // Should NOT contain internal property names
            AssertEx.IsFalse(types.Contains(nameof(EnzymeList)),
                @"GetSettingsListTypes should return LlmName values, not property names");

            // GetSettingsListNames - using LlmName
            var defaultEnzyme = EnzymeList.GetDefault();
            string enzymes = server.GetSettingsListNames(enzymesName);
            AssertEx.Contains(enzymes, defaultEnzyme.Name);

            // GetSettingsListItem - using LlmName
            string enzymeXml = server.GetSettingsListItem(enzymesName, defaultEnzyme.GetKey());
            AssertEx.Contains(enzymeXml, string.Format(@"name={0}", defaultEnzyme.Name.Quote()));

            // Backward compatibility - property names still work
            string enzymes2 = server.GetSettingsListNames(nameof(EnzymeList));
            AssertEx.Contains(enzymes2, defaultEnzyme.Name);
            string enzymeXml2 = server.GetSettingsListItem(nameof(EnzymeList), defaultEnzyme.GetKey());
            AssertEx.Contains(enzymeXml2, string.Format(@"name={0}", defaultEnzyme.Name.Quote()));

            // GetSettingsListNames for PersistedViews (reports) - using LlmName
            string viewNames = server.GetSettingsListNames(reportsName);
            AssertEx.Contains(viewNames, @"# Main");

            // Backward compatibility - PersistedViews property name still works
            string viewNames2 = server.GetSettingsListNames(nameof(PersistedViews));
            AssertEx.Contains(viewNames2, @"# Main");

            // Error: nonexistent list
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.GetSettingsListNames(@"NonexistentList"));

            // Error: nonexistent item
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.GetSettingsListItem(enzymesName, @"NotAnEnzyme"));

            // Error: nonexistent persisted view
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.GetSettingsListItem(reportsName, @"NotAView_12345"));
        }

        #endregion

        #region AddSettingsListItem

        private void TestAddSettingsListItem(JsonToolServer server)
        {
            string enzymesName = JsonToolServer.GetSettingsListName<EnzymeList>();

            // Create a new enzyme, serialize via the same path as GetSettingsListItem
            var enzyme = new Enzyme(@"TestMcpEnzyme", @"KR", @"P");
            Settings.Default.EnzymeList.Add(enzyme);
            string enzymeXml = server.GetSettingsListItem(enzymesName, enzyme.GetKey());
            Settings.Default.EnzymeList.Remove(enzyme);

            // Add via AddSettingsListItem and verify typed roundtrip
            string result = server.AddSettingsListItem(enzymesName, enzymeXml);
            AssertEx.Contains(result, enzyme.Name);
            AssertEx.IsTrue(Settings.Default.EnzymeList.TryGetValue(enzyme.GetKey(), out var retrieved));
            AssertEx.Cloned(enzyme, retrieved);

            // Verify XML roundtrip
            string retrievedXml = server.GetSettingsListItem(enzymesName, enzyme.GetKey());
            AssertEx.NoDiff(enzymeXml, retrievedXml);

            // Verify it appears in names list
            AssertEx.Contains(server.GetSettingsListNames(enzymesName), enzyme.Name);

            // Error: duplicate name without overwrite
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.AddSettingsListItem(enzymesName, enzymeXml));

            // Overwrite: replace existing item
            string overwriteResult = server.AddSettingsListItem(enzymesName, enzymeXml, true);
            AssertEx.Contains(overwriteResult, enzyme.Name);

            // Error: invalid XML
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.AddSettingsListItem(enzymesName, @"not xml"));

            // Error: PersistedViews should redirect to skyline_add_report
            string reportsName = JsonToolServer.GetSettingsListName<PersistedViews>();
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.AddSettingsListItem(reportsName, @"<view />"));

            // Error: unknown list type
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.AddSettingsListItem(@"NonexistentList", @"<item />"));

            // Add a structural modification to verify a second list type works
            string structModsName = JsonToolServer.GetSettingsListName<StaticModList>();
            var mod = new StaticMod(@"TestMcpMod", @"C", null, @"C2H3NO");
            Settings.Default.StaticModList.Add(mod);
            string modXml = server.GetSettingsListItem(structModsName, mod.GetKey());
            Settings.Default.StaticModList.Remove(mod);

            server.AddSettingsListItem(structModsName, modXml);
            AssertEx.IsTrue(Settings.Default.StaticModList.TryGetValue(mod.GetKey(), out var retrievedMod));
            AssertEx.Cloned(mod, retrievedMod);
        }

        #endregion

        #region AddReport

        private void TestAddReport(JsonToolServer server)
        {
            const string reportName = @"TestMcpReport";
            var definition = new ReportDefinition
            {
                Name = reportName,
                Select = new[] { @"ProteinName", @"PrecursorMz" }
            };
            string result = server.AddReportFromDefinition(definition);
            Assert.IsFalse(string.IsNullOrEmpty(result));

            // Verify the report was persisted with correct uimode
            var viewSpecList = Settings.Default.PersistedViews.GetViewSpecList(
                PersistedViews.MainGroup.Id);
            var viewSpec = viewSpecList.ViewSpecs.FirstOrDefault(v => v.Name == reportName);
            AssertEx.IsNotNull(viewSpec, @"Report should be persisted");
            AssertEx.AreEqual(UiModes.PROTEOMIC, viewSpec?.UiMode,
                @"Report should have proteomic uimode matching current SkylineWindow mode");
        }

        private void TestAddReportMoleculeMode(JsonToolServer server)
        {
            const string reportName = @"TestMcpMoleculeReport";
            var definition = new ReportDefinition
            {
                Name = reportName,
                Select = new[] { @"MoleculeListName", @"MoleculeFormula" }
            };
            string result = server.AddReportFromDefinition(definition);
            Assert.IsFalse(string.IsNullOrEmpty(result));

            // Verify molecule mode report gets small_molecules uimode
            var viewSpecList = Settings.Default.PersistedViews.GetViewSpecList(
                PersistedViews.MainGroup.Id);
            var viewSpec = viewSpecList.ViewSpecs.FirstOrDefault(v => v.Name == reportName);
            AssertEx.IsNotNull(viewSpec, @"Molecule report should be persisted");
            AssertEx.AreEqual(UiModes.SMALL_MOLECULES, viewSpec?.UiMode,
                @"Report should have small_molecules uimode matching current SkylineWindow mode");
        }

        #endregion

        #region Settings list selection (Get/Set selected items)

        private void TestSettingsListSelection(JsonToolServer server)
        {
            // Systematically test every ISettingsListDocumentSelection implementation
            TestAllDocumentSelectors(server);

            // Error: unsupported list type (environment-only)
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.GetSettingsListSelectedItems(@"ToolList"));

            // Error: nonexistent item name
            string enzymesName = JsonToolServer.GetSettingsListName<EnzymeList>();
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.SelectSettingsListItems(enzymesName,
                    new[] { @"NonexistentEnzyme_12345" }));

            // Error: multiple items for single-select list
            var twoEnzymes = Settings.Default.EnzymeList.Take(2).Select(e => e.GetKey()).ToArray();
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.SelectSettingsListItems(enzymesName, twoEnzymes));

            // Error: empty array for single-select list
            AssertEx.ThrowsException<ArgumentException>(() =>
                server.SelectSettingsListItems(enzymesName, new string[0]));

            // Clear selection: empty array for multi-select list
            string staticModsName = JsonToolServer.GetSettingsListName<StaticModList>();
            string[] originalMods = Settings.Default.StaticModList.GetSelectedItems(
                SkylineWindow.Document.Settings);
            server.SelectSettingsListItems(staticModsName, new string[0]);
            AssertEx.AreEqual(0, SkylineWindow.Document.Settings.PeptideSettings
                .Modifications.StaticModifications.Count);
            // Restore
            server.SelectSettingsListItems(staticModsName, originalMods);
        }

        /// <summary>
        /// Uses reflection to discover all Settings.Default properties that implement
        /// <see cref="ISettingsListDocumentSelection"/>, then tests each one:
        /// get selected → select different items → verify document changed → restore.
        /// </summary>
        private void TestAllDocumentSelectors(JsonToolServer server)
        {
            var selectorInfos = GetSelectors().ToArray();
            Assert.AreEqual(12, selectorInfos.Length);
            foreach (var t in selectorInfos)
            {
                string listType = GetLlmName(t.prop) ?? t.prop.Name;

                // Get current selection
                string[] originalSelected = t.selector.GetSelectedItems(
                    SkylineWindow.Document.Settings);

                // Also verify the server method works
                string serverResult = server.GetSettingsListSelectedItems(listType);

                // Get available items to select from
                var availableKeys = GetAvailableKeys(t.selector);
                if (availableKeys.Length == 0)
                    continue; // Skip lists with no items

                // Find an item that's not currently selected, excluding default placeholders
                // (e.g., "None" for BackgroundProteome, LegacyScoringModel for PeakScoringModelList)
                int excludeCount = GetExcludeDefaults(t.selector);
                var selectableKeys = availableKeys.Skip(excludeCount).ToArray();
                var unselected = selectableKeys
                    .Where(k => !originalSelected.Contains(k)).ToArray();
                if (unselected.Length == 0)
                    continue; // All items already selected or only "None" available, skip

                // Select a different set of items
                string[] newSelection;
                if (t.selector.SingleSelect)
                {
                    // Single-select: pick one unselected item
                    newSelection = new[] { unselected[0] };
                }
                else
                {
                    // Multi-select: pick the first unselected item
                    var newSelectionList = new List<string>{unselected[0]};
                    if (!ReferenceEquals(unselected[0], unselected.Last()))
                        newSelectionList.Add(unselected.Last());
                    newSelection = newSelectionList.ToArray();
                }

                server.SelectSettingsListItems(listType, newSelection);

                // Verify the selection changed
                string[] afterSelect = t.selector.GetSelectedItems(
                    SkylineWindow.Document.Settings);
                AssertEx.AreEqual(newSelection.Length, afterSelect.Length,
                    string.Format(@"Selection count mismatch for {0}", listType));
                foreach (var key in newSelection)
                {
                    AssertEx.IsTrue(afterSelect.Contains(key),
                        string.Format(@"Expected {0} to be selected in {1}", key, listType));
                }

                // Restore original selection
                server.SelectSettingsListItems(listType, originalSelected);
                string[] restored = t.selector.GetSelectedItems(
                    SkylineWindow.Document.Settings);
                AssertEx.AreEqualDeep(originalSelected, restored,
                    string.Format(@"Failed to restore original selection for {0}", listType));
            }
        }

        private static IEnumerable<(ISettingsListDocumentSelection selector, PropertyInfo prop)> GetSelectors()
        {
            foreach (var prop in typeof(Settings).GetProperties()
                         .Where(p => p.GetIndexParameters().Length == 0))
            {
                var value = prop.GetValue(Settings.Default);
                if (value is ISettingsListDocumentSelection selector)
                    yield return (selector, prop);
            }
        }

        private static string GetLlmName(PropertyInfo prop)
        {
            var attr = prop.PropertyType.GetCustomAttribute<LlmNameAttribute>();
            return attr?.Name;
        }

        private static string[] GetAvailableKeys(object settingsList)
        {
            var keys = new List<string>();
            foreach (var item in (IEnumerable)settingsList)
            {
                if (item is IKeyContainer<string> kc)
                    keys.Add(kc.GetKey());
            }
            return keys.ToArray();
        }

        private static int GetExcludeDefaults(object settingsList)
        {
            var prop = settingsList.GetType().GetProperty(@"ExcludeDefaults");
            return prop != null ? (int)prop.GetValue(settingsList) : 0;
        }

        #endregion
    }
}
