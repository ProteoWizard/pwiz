/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Lib;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// The Osprey settings presets: a preset's XML round trip with the additional settings it
    /// carries, and the user's list of them, which adds and replaces by name and persists as XML.
    /// The page that saves and applies them is covered by OspreySearchTest.
    /// </summary>
    [TestClass]
    public class OspreySettingsPresetTest : AbstractUnitTest
    {
        private const string SAMPLE_JSON = @"{
  ""tool"": ""Osprey"", ""version"": ""26.1.1.300"", ""culture"": ""en-US"",
  ""properties"": [
    { ""name"": ""verbose"", ""label"": ""Verbose output"", ""description"": ""Show more detail"", ""group"": ""Logging"", ""scope"": ""search"", ""type"": ""flag"" },
    { ""name"": ""rt-max"", ""label"": ""Gradient length (minutes)"", ""description"": ""The gradient length"", ""group"": ""Library Prediction"", ""scope"": ""library"", ""type"": ""number"", ""min"": 0 },
    { ""name"": ""activation"", ""label"": ""Activation"", ""description"": ""The activation predicted for"", ""group"": ""Library Prediction"", ""scope"": ""library"", ""type"": ""list"", ""values"": [""beam-type CID"", ""resonance CID""] }
  ]
}";

        private const string PRESET_NAME = @"Stellar lab";
        private const string OTHER_NAME = @"Orbitrap lab";
        private const string FLAG = @"verbose";
        private const string GRADIENT = @"rt-max";
        private const string GRADIENT_VALUE = @"45";

        [TestMethod]
        public void TestOspreySettingsPreset()
        {
            var properties = OspreyProperties.Parse(SAMPLE_JSON);
            TestRoundTrip(properties);
            TestPersistedShape(properties);
            TestList();
        }

        private static void TestRoundTrip(OspreyProperties properties)
        {
            var settings = new OspreyAdditionalSettings(properties);
            settings.Settings[FLAG].Value = true;
            settings.SetValue(settings.Settings[GRADIENT], GRADIENT_VALUE);
            var preset = new OspreySettingsPreset(PRESET_NAME, false, 0.5, false, 0.05, 0.02, 0.1, settings.ToXml());
            var copy = AssertEx.RoundTrip(preset);
            AssertEx.AreEqual(preset.AdditionalSettingsXml, copy.AdditionalSettingsXml);
            AssertEx.AreEqual(PRESET_NAME, copy.Name);
            AssertEx.IsFalse(copy.IsHighResolution);
            AssertEx.AreEqual(0.5, copy.FragmentTolerance);
            AssertEx.IsFalse(copy.IsFragmentTolerancePpm);
            AssertEx.AreEqual(0.05, copy.RunFdr);
            AssertEx.AreEqual(0.02, copy.ExperimentFdr);
            AssertEx.AreEqual(0.1, copy.ProteinFdr);
            AssertEx.Serializable(preset, 2, AssertEx.Cloned, false);
            // The additional settings the copy carries set settings at their defaults to the saved values
            var restored = new OspreyAdditionalSettings(properties);
            AssertEx.AreEqual(0, restored.ApplyXml(copy.AdditionalSettingsXml).Count);
            AssertEx.AreEqual(true, restored.Settings[FLAG].Value);
            AssertEx.AreEqual(GRADIENT_VALUE, restored.Settings[GRADIENT].Value);
            AssertEx.AreEqual(settings.ToXml(), restored.ToXml());
            // Reset returns every setting to its default, which is what a preset applies over
            restored.ResetToDefaults();
            AssertEx.IsNull(restored.ToXml());
            AssertEx.AreEqual(false, restored.Settings[FLAG].Value);

            // A preset saved with every additional setting at its default carries no XML
            var plain = new OspreySettingsPreset(OTHER_NAME, true, 10, true, 0.01, 0.01, 0.01, new OspreyAdditionalSettings(properties).ToXml());
            AssertEx.IsNull(plain.AdditionalSettingsXml);
            AssertEx.Serializable(plain, AssertEx.Cloned, false);
            AssertEx.IsNull(AssertEx.RoundTrip(plain).AdditionalSettingsXml);
            AssertEx.IsFalse(preset.Equals(plain));
        }

        /// <summary>The XML as user.config and an exported list hold it, by attribute name.</summary>
        private static void TestPersistedShape(OspreyProperties properties)
        {
            const string xml = @"<osprey_settings_preset name=""Stellar lab"" high_resolution=""false"" fragment_tolerance=""0.5""" +
                               @" fragment_tolerance_ppm=""false"" run_fdr=""0.05"" experiment_fdr=""0.02"" protein_fdr=""0.1"">" +
                               @"<additional_settings><AdditionalSettings><Setting name=""verbose"" value=""True"" /></AdditionalSettings></additional_settings>" +
                               @"</osprey_settings_preset>";
            var preset = AssertEx.Deserialize<OspreySettingsPreset>(xml);
            // The page's defaults (high resolution, ppm) are the attributes' defaults, written only when they differ
            var defaults = AssertEx.Deserialize<OspreySettingsPreset>(
                @"<osprey_settings_preset name=""Defaults"" fragment_tolerance=""10"" run_fdr=""0.01"" experiment_fdr=""0.01"" protein_fdr=""0.01"" />");
            AssertEx.IsTrue(defaults.IsHighResolution);
            AssertEx.IsTrue(defaults.IsFragmentTolerancePpm);
            AssertEx.IsNull(defaults.AdditionalSettingsXml);
            AssertEx.AreEqual(PRESET_NAME, preset.Name);
            AssertEx.IsFalse(preset.IsHighResolution);
            AssertEx.AreEqual(0.5, preset.FragmentTolerance);
            AssertEx.IsFalse(preset.IsFragmentTolerancePpm);
            AssertEx.AreEqual(0.05, preset.RunFdr);
            AssertEx.AreEqual(0.02, preset.ExperimentFdr);
            AssertEx.AreEqual(0.1, preset.ProteinFdr);
            var settings = new OspreyAdditionalSettings(properties);
            AssertEx.AreEqual(0, settings.ApplyXml(preset.AdditionalSettingsXml).Count);
            AssertEx.AreEqual(true, settings.Settings[FLAG].Value);
            AssertEx.IsTrue(settings.Settings[GRADIENT].IsDefault);
        }

        private static void TestList()
        {
            var list = new OspreySettingsPresetList();
            list.AddDefaults();
            AssertEx.AreEqual(0, list.Count);

            var first = new OspreySettingsPreset(PRESET_NAME, true, 10, true, 0.01, 0.01, 0.01, null);
            list.Add(first);
            list.Add(new OspreySettingsPreset(OTHER_NAME, false, 0.5, false, 0.01, 0.01, 0.01, null));
            AssertEx.IsTrue(list.ContainsKey(PRESET_NAME));
            AssertEx.IsTrue(list.ContainsKey(OTHER_NAME));

            // Replacing by name, as saving under an existing name does: the new values, the same count
            var replacement = new OspreySettingsPreset(PRESET_NAME, true, 20, true, 0.05, 0.01, 0.01, null);
            list.RemoveKey(PRESET_NAME);
            list.Add(replacement);
            AssertEx.AreEqual(2, list.Count);
            AssertEx.IsTrue(list.TryGetValue(PRESET_NAME, out var found));
            AssertEx.AreEqual(replacement, found);
            AssertEx.IsFalse(first.Equals(found));

            // The list persists in the user's settings, and travels as a file, as XML
            var copy = AssertEx.RoundTrip(list);
            AssertEx.AreEqual(2, copy.Count);
            AssertEx.IsTrue(list.SequenceEqual(copy));
        }
    }
}
