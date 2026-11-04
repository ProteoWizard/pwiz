/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
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

using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// The search settings presets of every engine in one list: a generic preset keeps the generic
    /// search settings, an Osprey preset Osprey's settings page alone, and presets saved before either
    /// shape still load. The combo that lists them shows the presets of one workflow; Osprey's own
    /// saved presets move into the list.
    /// </summary>
    [TestClass]
    public class SearchSettingsPresetTest : AbstractUnitTest
    {
        private const string OSPREY_NAME = @"Stellar lab";
        private const string DIA_NAME = @"DIA lab";
        private const string DDA_NAME = @"DDA lab";

        [TestMethod]
        public void TestSearchSettingsPresetEngines()
        {
            TestGenericPreset();
            TestOspreyPreset();
            TestOlderPresets();
            TestWorkflowFilter();
            TestMoveInOspreyPresets();
        }

        private static OspreySettingsPreset MakeOspreySettings(string name)
        {
            return new OspreySettingsPreset(name, false, 0.5, false, 0.05, 0.02, 0.1, null);
        }

        private static SearchSettingsPreset MakeGeneric(string name, SearchWorkflowType? workflow)
        {
            return new SearchSettingsPreset(name, SearchEngine.Comet, new MzTolerance(20, MzTolerance.Units.ppm),
                new MzTolerance(0.5), 2, null, null, 0.01, (string) null,
                enzymeName: @"Trypsin", maxMissedCleavages: 1, workflowType: workflow);
        }

        private static void TestGenericPreset()
        {
            var preset = MakeGeneric(DDA_NAME, SearchWorkflowType.dda);
            var copy = AssertEx.RoundTrip(preset);
            AssertEx.AreEqual(preset, copy);
            AssertEx.AreEqual(20.0, copy.PrecursorToleranceValue);
            AssertEx.IsNull(copy.OspreySettings);
        }

        private static void TestOspreyPreset()
        {
            var preset = SearchSettingsPreset.CreateOsprey(OSPREY_NAME, MakeOspreySettings(OSPREY_NAME));
            AssertEx.AreEqual(SearchEngine.Osprey, preset.SearchEngine);
            AssertEx.AreEqual(SearchWorkflowType.dia, preset.Workflow);
            // Nothing the wizard applies without being given: no FASTA, enzyme, explicit modifications or iRT standard
            AssertEx.IsNull(preset.FastaFilePath);
            AssertEx.IsNull(preset.EnzymeName);
            AssertEx.IsFalse(preset.HasExplicitModifications);
            AssertEx.IsTrue(preset.HasEngineSettingsOnly);
            AssertEx.IsTrue(AssertEx.RoundTrip(preset).HasEngineSettingsOnly);
            // The built-in Default sets the digestion, so it is not a preset of engine settings alone
            AssertEx.IsFalse(new SearchSettingsPresetList().GetDefaults().First().HasEngineSettingsOnly);

            // Its engine part is Osprey's settings alone, and it equals itself read back
            string xml = null;
            var copy = AssertEx.RoundTrip(preset, ref xml);
            AssertEx.Contains(xml, @"<osprey_settings_preset ");
            AssertEx.DoesNotContain(xml, @"precursor_tolerance_value");
            AssertEx.DoesNotContain(xml, @"cutoff_score");
            AssertEx.AreEqual(preset, copy);
            AssertEx.AreEqual(0.05, copy.OspreySettings.RunFdr);
            AssertEx.IsFalse(copy.OspreySettings.IsHighResolution);

            // New Osprey settings for a preset keep its other parts
            var withFasta = SearchSettingsPreset.CreateOsprey(OSPREY_NAME, MakeOspreySettings(OSPREY_NAME), @"C:\db\human.fasta");
            var changed = withFasta.ChangeOspreySettings(new OspreySettingsPreset(OSPREY_NAME, true, 10, true, 0.01, 0.01, 0.01, null));
            AssertEx.AreEqual(@"C:\db\human.fasta", changed.FastaFilePath);
            AssertEx.IsTrue(changed.OspreySettings.IsHighResolution);
            AssertEx.AreEqual(changed, AssertEx.RoundTrip(changed));
        }

        /// <summary>
        /// A preset saved before presets had workflows (generic attributes only), and an Osprey preset as
        /// Run Peptide Search first saved them (the generic attributes as well as Osprey's element).
        /// </summary>
        private static void TestOlderPresets()
        {
            var legacy = AssertEx.Deserialize<SearchSettingsPreset>(
                @"<search_workflow name=""Old"" search_engine=""MSAmanda"" precursor_tolerance_value=""10"" precursor_tolerance_unit=""ppm""" +
                @" fragment_tolerance_value=""0.02"" fragment_tolerance_unit=""mz"" max_variable_mods=""2"" cutoff_score=""0.05"" />");
            AssertEx.AreEqual(SearchEngine.MSAmanda, legacy.SearchEngine);
            AssertEx.IsNull(legacy.Workflow);
            AssertEx.AreEqual(0.05, legacy.CutoffScore);

            var withGeneric = AssertEx.Deserialize<SearchSettingsPreset>(
                @"<search_workflow name=""Stellar lab"" search_engine=""Osprey"" precursor_tolerance_value=""5"" precursor_tolerance_unit=""ppm""" +
                @" fragment_tolerance_value=""0.5"" fragment_tolerance_unit=""mz"" max_variable_mods=""2"" cutoff_score=""0.01""" +
                @" fasta_file_path="""" workflow_type=""dia"">" +
                @"<osprey_settings_preset name=""Stellar lab"" high_resolution=""false"" fragment_tolerance=""0.5"" fragment_tolerance_ppm=""false""" +
                @" run_fdr=""0.05"" experiment_fdr=""0.02"" protein_fdr=""0.1"" /></search_workflow>");
            // The empty FASTA it was saved with clears the FASTA, as it did
            AssertEx.AreEqual(string.Empty, withGeneric.FastaFilePath);
            AssertEx.AreEqual(SearchSettingsPreset.CreateOsprey(OSPREY_NAME, MakeOspreySettings(OSPREY_NAME), string.Empty), withGeneric);
            AssertEx.IsFalse(withGeneric.HasEngineSettingsOnly);
        }

        /// <summary>
        /// The combo lists the items its filter accepts, maps its selection through them, and with no item
        /// to select leaves nothing selected.
        /// </summary>
        private static void TestWorkflowFilter()
        {
            var list = new SearchSettingsPresetList
            {
                MakeGeneric(DDA_NAME, null),
                SearchSettingsPreset.CreateOsprey(OSPREY_NAME, MakeOspreySettings(OSPREY_NAME)),
                MakeGeneric(DIA_NAME, SearchWorkflowType.dia)
            };
            // The workflow the filter lists, which the test changes
            var workflow = new[] { SearchWorkflowType.dia };
            using var combo = new ComboBox();
            var driver = new SettingsListComboDriver<SearchSettingsPreset>(combo, list, false)
            {
                Filter = p => (p.Workflow ?? SearchWorkflowType.dda) == workflow[0],
                SelectFirstItem = false
            };
            driver.LoadList(null);
            AssertEx.AreEqualDeep(new object[] { OSPREY_NAME, DIA_NAME }, combo.Items.Cast<object>().ToArray());
            AssertEx.AreEqual(-1, combo.SelectedIndex);
            // The last selection, which Edit list... returns to when canceled, is none too
            AssertEx.AreEqual(-1, driver.SelectedIndexLast);
            AssertEx.IsNull(driver.SelectedItem);

            driver.LoadList(DIA_NAME);
            AssertEx.AreEqual(DIA_NAME, driver.SelectedItem.Name);

            // A preset with no workflow is a DDA preset; the DIA one chosen is not listed there
            workflow[0] = SearchWorkflowType.dda;
            driver.LoadList(driver.SelectedItem.Name);
            AssertEx.AreEqualDeep(new object[] { DDA_NAME }, combo.Items.Cast<object>().ToArray());
            AssertEx.IsNull(driver.SelectedItem);
            AssertEx.AreEqual(-1, driver.SelectedIndexLast);
            combo.SelectedIndex = 0;
            AssertEx.AreEqual(DDA_NAME, driver.SelectedItem.Name);
        }

        /// <summary>The presets Osprey's settings page saved become Osprey presets of the DIA workflow.</summary>
        private static void TestMoveInOspreyPresets()
        {
            var presets = Settings.Default.SearchSettingsPresets;
            var ospreyPresets = Settings.Default.OspreySettingsPresets;
            var presetsBefore = presets.ToArray();
            try
            {
                // One name already taken by a preset of another engine
                presets.Add(MakeGeneric(OSPREY_NAME, null));
                ospreyPresets.Add(MakeOspreySettings(OSPREY_NAME));
                ospreyPresets.Add(MakeOspreySettings(DIA_NAME));
                var presetsBeforeMove = presets.ToArray();
                SearchSettingsPresetList.MoveInOspreyPresets();
                AssertEx.AreEqual(0, ospreyPresets.Count);
                var moved = presets.Except(presetsBeforeMove).ToArray();
                AssertEx.AreEqual(2, moved.Length);
                AssertEx.IsTrue(moved.All(p => p.Workflow == SearchWorkflowType.dia && p.OspreySettings.RunFdr == 0.05));
                AssertEx.IsTrue(moved.Any(p => p.Name == DIA_NAME));
                AssertEx.IsFalse(moved.Any(p => p.Name == OSPREY_NAME), @"a moved preset took the name of another engine's");
            }
            finally
            {
                ospreyPresets.Clear();
                presets.Clear();
                presets.AddRange(presetsBefore);
            }
        }
    }
}
