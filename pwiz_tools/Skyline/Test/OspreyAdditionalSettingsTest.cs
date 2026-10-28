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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Osprey's additional settings, from the properties <c>Osprey --help properties</c> declares
    /// (here a sample in its shape) to the command line: which become settings, what the grid
    /// shows for each scope, what a value must be, how the values reach the command line, and how
    /// the last-used values persist. The real export is checked against the installed Osprey by
    /// the functional tests, and its shape by Osprey's own TestPropertiesExport.
    /// </summary>
    [TestClass]
    public class OspreyAdditionalSettingsTest : AbstractUnitTest
    {
        private const string SAMPLE_JSON = @"{
  ""tool"": ""Osprey"", ""version"": ""26.1.1.300"", ""culture"": ""en-US"",
  ""properties"": [
    { ""name"": ""cache-dir"", ""label"": ""Spectra cache folder"", ""description"": ""Where Osprey keeps the files it builds once"", ""group"": ""General I/O"", ""scope"": ""search"", ""type"": ""path"" },
    { ""name"": ""run-fdr"", ""label"": ""Run FDR"", ""description"": ""The run-level FDR threshold"", ""group"": ""FDR"", ""scope"": ""search"", ""type"": ""number"", ""default"": ""0.01"", ""min"": 0, ""max"": 1 },
    { ""name"": ""reconciliation-compaction-fdr"", ""label"": ""Reconciliation compaction FDR"", ""description"": ""Peptide q-value threshold"", ""group"": ""FDR"", ""scope"": ""search"", ""type"": ""number"", ""default"": ""0.01"", ""min"": 0, ""max"": 1 },
    { ""name"": ""fdr-level"", ""label"": ""Experiment FDR level"", ""description"": ""Which experiment-level q-value"", ""group"": ""FDR"", ""scope"": ""search"", ""type"": ""list"", ""values"": [""precursor"", ""peptide"", ""both""], ""default"": ""precursor"" },
    { ""name"": ""parallel-files"", ""label"": ""Parallel files"", ""description"": ""Input files run concurrently"", ""group"": ""Performance"", ""scope"": ""search"", ""type"": ""integer"", ""min"": 0, ""optional"": true },
    { ""name"": ""verbose"", ""label"": ""Verbose output"", ""description"": ""Show more detail"", ""group"": ""Logging"", ""scope"": ""search"", ""type"": ""flag"" },
    { ""name"": ""enzyme"", ""label"": ""Enzyme"", ""description"": ""The enzyme, by its index or name"", ""group"": ""Library Digest"", ""scope"": ""digest"", ""type"": ""text"", ""default"": ""1"" },
    { ""name"": ""clip-n-m"", ""label"": ""Clip N-terminal methionine"", ""description"": ""Also digest each protein"", ""group"": ""Library Digest"", ""scope"": ""digest"", ""type"": ""flag"" },
    { ""name"": ""ms-instrument"", ""label"": ""Instrument"", ""description"": ""The instrument the MS2 model predicts for"", ""group"": ""Library Prediction"", ""scope"": ""library"", ""type"": ""text"", ""default"": ""Eclipse"" },
    { ""name"": ""rt-max"", ""label"": ""Gradient length (minutes)"", ""description"": ""The gradient length"", ""group"": ""Library Prediction"", ""scope"": ""library"", ""type"": ""number"", ""min"": 0 },
    { ""name"": ""activation"", ""label"": ""Activation"", ""description"": ""The activation predicted for"", ""group"": ""Library Prediction"", ""scope"": ""library"", ""type"": ""list"", ""values"": [""beam-type CID"", ""resonance CID""] },
    { ""name"": ""seed"", ""label"": ""Random seed"", ""description"": ""The random seed of training"", ""group"": ""Library Tuning"", ""scope"": ""tuning"", ""type"": ""integer"", ""default"": ""2024"", ""min"": 0 }
  ]
}";

        [TestMethod]
        public void TestOspreyAdditionalSettings()
        {
            var properties = OspreyProperties.Parse(SAMPLE_JSON);
            TestParse(properties);
            TestSettings(properties);
            TestDescriptors(properties);
            TestValidation(properties);
            TestArguments(properties);
            TestCultures(properties);
            TestPersistence(properties);
            TestScopes();
            TestBadExport();
        }

        private static void TestParse(OspreyProperties properties)
        {
            AssertEx.AreEqual(@"26.1.1.300", properties.Version);
            AssertEx.AreEqual(@"en-US", properties.Culture);
            AssertEx.AreEqual(12, properties.Properties.Count);
            var fdrLevel = properties.Find(@"fdr-level");
            AssertEx.AreEqual(OspreyPropertyType.list, fdrLevel.Type);
            AssertEx.AreEqual(OspreyPropertyScope.search, fdrLevel.Scope);
            AssertEx.AreEqual(@"--fdr-level", fdrLevel.ArgumentText);
            AssertEx.AreEqual(3, fdrLevel.Values.Count);
            AssertEx.AreEqual(@"precursor", fdrLevel.Default);
            var parallel = properties.Find(@"parallel-files");
            AssertEx.IsTrue(parallel.Optional);
            AssertEx.IsFalse(parallel.HasDefault);
            AssertEx.AreEqual(0.0, parallel.Min);
            AssertEx.IsNull(parallel.Max);
            AssertEx.AreEqual(OspreyPropertyScope.tuning, properties.Find(@"seed").Scope);
            // The help text is the usage text, and how to give an optional value; never the argument
            AssertEx.Contains(parallel.HelpText, parallel.Description, OspreyProperty.AUTO_VALUE);
            AssertEx.IsFalse(parallel.HelpText.Contains(parallel.ArgumentText));
            AssertEx.IsFalse(fdrLevel.HelpText.Contains(OspreyProperty.AUTO_VALUE));
        }

        /// <summary>The form-bound arguments are no settings; the rest are, typed by their properties.</summary>
        private static void TestSettings(OspreyProperties properties)
        {
            var settings = new OspreyAdditionalSettings(properties);
            AssertEx.IsFalse(settings.Settings.ContainsKey(@"run-fdr"));
            AssertEx.IsFalse(settings.Settings.ContainsKey(@"enzyme"));
            AssertEx.AreEqual(10, settings.Settings.Count);
            AssertEx.AreEqual(false, settings.Settings[@"verbose"].Value);
            AssertEx.AreEqual(@"precursor", settings.Settings[@"fdr-level"].Value);
            AssertEx.AreEqual(@"0.01", settings.Settings[@"reconciliation-compaction-fdr"].Value);
            AssertEx.AreEqual(string.Empty, settings.Settings[@"parallel-files"].Value);
            AssertEx.AreEqual(string.Empty, settings.Settings[@"rt-max"].Value);
            // A list without a default offers a blank first, which leaves the argument out
            var activation = settings.Settings[@"activation"];
            AssertEx.AreEqual(string.Empty, activation.Value);
            AssertEx.AreEqual(string.Empty, activation.ValidValues.First());
            AssertEx.AreEqual(3, activation.ValidValues.Count());
            AssertEx.IsTrue(settings.Settings.Values.All(s => s.IsDefault));
            AssertEx.IsFalse(settings.ChangedSettings.Any());
        }

        /// <summary>
        /// The property grid's view of the settings: one descriptor per setting of the scopes, in
        /// help order, named by the argument and shown under the label in the help section's
        /// category with the tooltip as the description; a flag a bool, a list a choice of its
        /// values; a refused value thrown from SetValue; a value that is not the default bold and
        /// resettable.
        /// </summary>
        private static void TestDescriptors(OspreyProperties properties)
        {
            var settings = new OspreyAdditionalSettings(properties);
            var searchOnly = new OspreySettingsObject(settings, new[] { OspreyPropertyScope.search });
            var descriptors = searchOnly.Descriptors.ToArray();
            AssertEx.AreEqualDeep(properties.Properties.Where(p => p.Scope == OspreyPropertyScope.search && settings.Settings.ContainsKey(p.Name)).Select(p => p.Label).ToArray(),
                descriptors.Select(d => d.DisplayName).ToArray());
            AssertEx.AreEqual(descriptors.Length, searchOnly.GetProperties().Count);
            AssertEx.IsTrue(ReferenceEquals(searchOnly, searchOnly.GetPropertyOwner(descriptors[0])));
            AssertEx.IsNull(searchOnly.FindByLabel(@"Instrument"));
            AssertEx.AreEqual(settings.Settings.Count, new OspreySettingsObject(settings, OspreyAdditionalSettings.ALL_SCOPES).Descriptors.Count());

            var verbose = searchOnly.FindByLabel(@"Verbose output");
            Assert.IsNotNull(verbose);
            var verboseProperty = properties.Find(@"verbose");
            AssertEx.AreEqual(verboseProperty.Name, verbose.Name);
            AssertEx.AreEqual(verboseProperty.Group, verbose.Category);
            AssertEx.AreEqual(verboseProperty.HelpText, verbose.Description);
            AssertEx.AreEqual(typeof(bool), verbose.PropertyType);
            AssertEx.IsFalse(verbose.IsReadOnly);
            AssertEx.IsTrue(ReferenceEquals(settings.Settings[@"verbose"], verbose.Setting));
            AssertEx.AreEqual(false, verbose.GetValue(searchOnly));
            AssertEx.IsFalse(verbose.ShouldSerializeValue(searchOnly));
            AssertEx.IsFalse(verbose.CanResetValue(searchOnly));
            verbose.SetValue(searchOnly, true);
            AssertEx.AreEqual(true, settings.Settings[@"verbose"].Value);
            AssertEx.IsTrue(verbose.ShouldSerializeValue(searchOnly));
            AssertEx.IsTrue(verbose.CanResetValue(searchOnly));
            verbose.ResetValue(searchOnly);
            AssertEx.AreEqual(false, settings.Settings[@"verbose"].Value);
            // A flag set from text, as a test sets it
            verbose.SetValue(searchOnly, true.ToString());
            AssertEx.AreEqual(true, settings.Settings[@"verbose"].Value);

            // A list offers its values and nothing else
            var fdrLevel = searchOnly.FindByLabel(@"Experiment FDR level");
            Assert.IsNotNull(fdrLevel);
            AssertEx.AreEqual(typeof(string), fdrLevel.PropertyType);
            AssertEx.IsTrue(fdrLevel.Converter.GetStandardValuesSupported());
            AssertEx.IsTrue(fdrLevel.Converter.GetStandardValuesExclusive());
            AssertEx.AreEqualDeep(properties.Find(@"fdr-level").Values.ToArray(),
                fdrLevel.Converter.GetStandardValues().Cast<string>().ToArray());
            fdrLevel.SetValue(searchOnly, @"peptide");
            AssertEx.AreEqual(@"peptide", fdrLevel.GetValue(searchOnly));
            AssertEx.ThrowsException<ArgumentException>(() => fdrLevel.SetValue(searchOnly, @"protein"),
                x => AssertEx.Contains(x.Message, fdrLevel.DisplayName));
            AssertEx.AreEqual(@"peptide", settings.Settings[@"fdr-level"].Value);
            AssertEx.IsFalse(searchOnly.FindByLabel(@"Parallel files").Converter.GetStandardValuesSupported());

            // A number is kept in the invariant culture, and refused out of range with the label
            var compaction = searchOnly.FindByLabel(@"Reconciliation compaction FDR");
            Assert.IsNotNull(compaction);
            compaction.SetValue(searchOnly, @"0.05");
            AssertEx.AreEqual(@"0.05", settings.Settings[@"reconciliation-compaction-fdr"].Value);
            AssertEx.ThrowsException<ArgumentOutOfRangeException>(() => compaction.SetValue(searchOnly, @"2"),
                x => AssertEx.Contains(x.Message, compaction.DisplayName));
            AssertEx.AreEqual(@"0.05", settings.Settings[@"reconciliation-compaction-fdr"].Value);
            compaction.ResetValue(searchOnly);
            AssertEx.AreEqual(@"0.01", settings.Settings[@"reconciliation-compaction-fdr"].Value);
        }

        private static void TestValidation(OspreyProperties properties)
        {
            var settings = new OspreyAdditionalSettings(properties);
            // A number in range, a listed value, a whole number, a blank or auto for the optional count
            ValidateOk(settings, @"reconciliation-compaction-fdr", @"0.05");
            ValidateOk(settings, @"reconciliation-compaction-fdr", @"1");
            ValidateOk(settings, @"fdr-level", @"peptide");
            ValidateOk(settings, @"fdr-level", @"Peptide");
            // A listed value in another case is kept in its listed spelling
            settings.SetValue(settings.Settings[@"fdr-level"], @"PEPTIDE");
            AssertEx.AreEqual(@"peptide", settings.Settings[@"fdr-level"].Value);
            settings.SetValue(settings.Settings[@"fdr-level"], @"precursor");
            ValidateOk(settings, @"parallel-files", string.Empty);
            ValidateOk(settings, @"parallel-files", OspreyProperty.AUTO_VALUE);
            ValidateOk(settings, @"parallel-files", @"4");
            ValidateOk(settings, @"rt-max", string.Empty);
            ValidateOk(settings, @"rt-max", @"90.5");
            ValidateOk(settings, @"activation", string.Empty);
            ValidateOk(settings, @"seed", @"7");
            ValidateOk(settings, @"cache-dir", @"D:\cache");
            // Not a number, out of range, not listed, not whole, negative
            ValidateFails<ArgumentException>(settings, @"reconciliation-compaction-fdr", @"abc");
            ValidateFails<ArgumentOutOfRangeException>(settings, @"reconciliation-compaction-fdr", @"2");
            ValidateFails<ArgumentOutOfRangeException>(settings, @"reconciliation-compaction-fdr", @"-0.1");
            ValidateFails<ArgumentException>(settings, @"fdr-level", @"protein");
            ValidateFails<ArgumentException>(settings, @"parallel-files", @"1.5");
            ValidateFails<ArgumentException>(settings, @"parallel-files", @"many");
            ValidateFails<ArgumentOutOfRangeException>(settings, @"parallel-files", @"-1");
            ValidateFails<ArgumentException>(settings, @"seed", @"abc");
            ValidateOk(settings, @"seed", string.Empty);
            ValidateOk(settings, @"ms-instrument", string.Empty);
            ValidateFails<ArgumentOutOfRangeException>(settings, @"rt-max", @"-5");
            // The refusal names the property as the grid shows it
            AssertEx.ThrowsException<ArgumentException>(() => settings.Validate(@"abc", settings.Settings[@"seed"]),
                x => AssertEx.Contains(x.Message, @"Random seed"));
            AssertEx.ThrowsException<ArgumentException>(() => settings.Validate(@"many", settings.Settings[@"parallel-files"]),
                x => AssertEx.Contains(x.Message, @"Parallel files", OspreyProperty.AUTO_VALUE));
        }

        private static void ValidateOk(OspreyAdditionalSettings settings, string name, string text)
        {
            settings.Validate(text, settings.Settings[name]);
        }

        private static void ValidateFails<TException>(OspreyAdditionalSettings settings, string name, string text) where TException : Exception
        {
            AssertEx.ThrowsException<TException>(() => settings.Validate(text, settings.Settings[name]));
        }

        /// <summary>
        /// The command line carries the settings that are not defaults and apply to the run's
        /// scopes: a flag as a bare switch, auto as the bare argument, a path quoted, in the
        /// properties' order, after the pages' arguments.
        /// </summary>
        private static void TestArguments(OspreyProperties properties)
        {
            var settings = new OspreyAdditionalSettings(properties);
            settings.Settings[@"verbose"].Value = @"True";
            settings.Settings[@"fdr-level"].Value = @"peptide";
            settings.Settings[@"reconciliation-compaction-fdr"].Value = @"0.05";
            settings.Settings[@"parallel-files"].Value = OspreyProperty.AUTO_VALUE;
            settings.Settings[@"cache-dir"].Value = @"D:\my cache";
            settings.Settings[@"ms-instrument"].Value = @"Lumos";
            settings.Settings[@"rt-max"].Value = @"120";
            settings.Settings[@"seed"].Value = @"7";
            settings.Settings[@"clip-n-m"].Value = @"True";
            AssertEx.AreEqual(9, settings.ChangedSettings.Count());

            AssertEx.AreEqual(@"--cache-dir ""D:\my cache"" --reconciliation-compaction-fdr 0.05 --fdr-level peptide --parallel-files --verbose",
                Arguments(settings, OspreyPropertyScope.search));
            AssertEx.AreEqual(@"--ms-instrument Lumos --rt-max 120", Arguments(settings, OspreyPropertyScope.library));
            AssertEx.AreEqual(@"--clip-n-m", Arguments(settings, OspreyPropertyScope.digest));
            AssertEx.AreEqual(@"--seed 7", Arguments(settings, OspreyPropertyScope.tuning));
            // A count given to the optional argument, and a setting back at its default, which is left out
            settings.Settings[@"parallel-files"].Value = @"3";
            settings.Settings[@"verbose"].Value = @"False";
            AssertEx.Contains(Arguments(settings, OspreyPropertyScope.search), @"--parallel-files 3");
            AssertEx.IsFalse(Arguments(settings, OspreyPropertyScope.search).Contains(@"--verbose"));
            // A setting with a default cleared to blank is left out, not given without a value
            settings.Settings[@"ms-instrument"].Value = string.Empty;
            settings.Settings[@"seed"].Value = string.Empty;
            AssertEx.AreEqual(@"--rt-max 120", Arguments(settings, OspreyPropertyScope.library));
            AssertEx.AreEqual(string.Empty, Arguments(settings, OspreyPropertyScope.tuning));
            settings.Settings[@"ms-instrument"].Value = @"Lumos";
            settings.Settings[@"seed"].Value = @"7";

            // Through the two command lines: after the pages' arguments and before the culture,
            // for the scopes the configuration has; the prediction's command line takes the
            // library scope alone.
            var config = new OspreyConfig
            {
                DataFiles = new List<string> { @"a.mzML" },
                LibraryPath = @"lib.blib",
                OutputPath = @"out.blib",
                CultureName = @"en",
                AdditionalSettings = settings,
            };
            string commandLine = OspreyHelpers.BuildCommandLine(config);
            AssertEx.Contains(commandLine, @"--parallel-files 3 --culture en");
            AssertEx.IsFalse(commandLine.Contains(@"--ms-instrument"));
            config.LibraryPath = null;
            config.FastaPath = @"proteins.fasta";
            commandLine = OspreyHelpers.BuildCommandLine(config);
            AssertEx.Contains(commandLine, @"--parallel-files 3 --clip-n-m --ms-instrument Lumos --rt-max 120 --culture en");
            AssertEx.IsFalse(commandLine.Contains(@"--seed"));
        }

        private static string Arguments(OspreyAdditionalSettings settings, params OspreyPropertyScope[] scopes)
        {
            var sb = new System.Text.StringBuilder();
            settings.AppendArguments(sb, scopes);
            return sb.ToString().TrimEnd();
        }

        /// <summary>A number typed with the UI culture's separator is accepted, and reaches the command line in the invariant culture.</summary>
        private static void TestCultures(OspreyProperties properties)
        {
            var settings = new OspreyAdditionalSettings(properties);
            var culture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(@"fr-FR");
                ValidateOk(settings, @"reconciliation-compaction-fdr", @"0,05");
                ValidateOk(settings, @"reconciliation-compaction-fdr", @"0.05");
                // Set as the grid sets it: the number is kept in the invariant culture, so it persists for any culture
                settings.SetValue(settings.Settings[@"reconciliation-compaction-fdr"], @"0,05");
                AssertEx.AreEqual(@"0.05", settings.Settings[@"reconciliation-compaction-fdr"].Value);
                AssertEx.AreEqual(@"--reconciliation-compaction-fdr 0.05", Arguments(settings, OspreyPropertyScope.search));
                AssertEx.Contains(settings.ToXml(), @"value=" + "\"0.05\"");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = culture;
            }
        }

        /// <summary>
        /// The values that are not defaults persist as the DDA presets' XML and come back; a
        /// persisted form-bound argument, or a value the argument does not take, is refused by
        /// name, and an argument this Osprey does not declare is ignored.
        /// </summary>
        private static void TestPersistence(OspreyProperties properties)
        {
            var settings = new OspreyAdditionalSettings(properties);
            AssertEx.IsNull(settings.ToXml());
            settings.Settings[@"verbose"].Value = @"True";
            settings.Settings[@"seed"].Value = @"7";
            settings.Settings[@"cache-dir"].Value = @"D:\my cache";
            string xml = settings.ToXml();
            AssertEx.Contains(xml, @"name=""verbose"" value=""True""", @"name=""seed"" value=""7""", @"name=""cache-dir"" value=""D:\my cache""");
            AssertEx.IsFalse(xml.Contains(@"fdr-level"));

            var restored = new OspreyAdditionalSettings(properties);
            AssertEx.AreEqual(0, restored.ApplyXml(xml).Count);
            AssertEx.AreEqual(true, restored.Settings[@"verbose"].Value);
            AssertEx.AreEqual(@"7", restored.Settings[@"seed"].Value);
            AssertEx.AreEqual(@"D:\my cache", restored.Settings[@"cache-dir"].Value);
            AssertEx.AreEqual(xml, restored.ToXml());

            var refused = new OspreyAdditionalSettings(properties).ApplyXml(
                @"<AdditionalSettings><Setting name=""run-fdr"" value=""0.1"" /><Setting name=""seed"" value=""abc"" />" +
                @"<Setting name=""no-such-argument"" value=""1"" /><Setting name=""fdr-level"" value=""both"" /></AdditionalSettings>");
            AssertEx.AreEqual(2, refused.Count);
            AssertEx.IsTrue(refused.Contains(@"run-fdr"));
            AssertEx.IsTrue(refused.Contains(@"seed"));

            // The last used: saved and applied through the user settings
            string saved = Settings.Default.OspreyAdditionalSettings;
            try
            {
                settings.SaveLastUsed();
                AssertEx.AreEqual(xml, Settings.Default.OspreyAdditionalSettings);
                var lastUsed = new OspreyAdditionalSettings(properties);
                AssertEx.AreEqual(0, lastUsed.ApplyLastUsed().Count);
                AssertEx.AreEqual(xml, lastUsed.ToXml());
                new OspreyAdditionalSettings(properties).SaveLastUsed();
                AssertEx.AreEqual(string.Empty, Settings.Default.OspreyAdditionalSettings);
                AssertEx.AreEqual(0, new OspreyAdditionalSettings(properties).ApplyLastUsed().Count);
            }
            finally
            {
                Settings.Default.OspreyAdditionalSettings = saved;
            }
        }

        /// <summary>The scopes of a run, from what the configuration searches, predicts, digests and tunes.</summary>
        private static void TestScopes()
        {
            var config = new OspreyConfig { DataFiles = new List<string> { @"a.mzML" }, LibraryPath = @"lib.blib" };
            AssertScopes(config, OspreyPropertyScope.search);
            config.LibraryPath = null;
            config.FastaPath = @"proteins.fasta";
            AssertScopes(config, OspreyPropertyScope.search, OspreyPropertyScope.library, OspreyPropertyScope.digest);
            config.TrainingInputs = new List<string> { @"a.mzML" };
            AssertScopes(config, OspreyPropertyScope.search, OspreyPropertyScope.library, OspreyPropertyScope.digest, OspreyPropertyScope.tuning);
            // Build Library: a precursor table predicted as it is, or with a fine-tune on the FASTA's search of the tuning runs
            var predict = new OspreyConfig { PrecursorTablePath = @"precursors.tsv" };
            AssertScopes(predict, OspreyPropertyScope.library);
            predict.FastaPath = @"proteins.fasta";
            predict.TrainingInputs = new List<string> { @"a.mzML" };
            AssertScopes(predict, OspreyPropertyScope.search, OspreyPropertyScope.library, OspreyPropertyScope.digest, OspreyPropertyScope.tuning);
        }

        private static void AssertScopes(OspreyConfig config, params OspreyPropertyScope[] expected)
        {
            AssertEx.AreEqual(string.Join(@",", expected), string.Join(@",", OspreyAdditionalSettings.ScopesFor(config)));
        }

        /// <summary>An Osprey from before the grid answers --help properties with text that is not JSON, which is reported with its first line.</summary>
        private static void TestBadExport()
        {
            const string notJson = "No help section matching 'properties' found.\r\nUse --help sections to list available sections.";
            AssertEx.ThrowsException<System.IO.IOException>(() => OspreyProperties.Parse(notJson, @"C:\Osprey\Osprey.exe"),
                x => AssertEx.AreEqual(string.Format(ModelResources.OspreyProperties_Parse_Osprey_at__0__did_not_report_its_settings___1_,
                    @"C:\Osprey\Osprey.exe", @"No help section matching 'properties' found."), x.Message));
            // An export with no properties is an empty grid, not an error
            AssertEx.AreEqual(0, OspreyProperties.Parse(@"{ ""tool"": ""Osprey"", ""version"": ""1"", ""culture"": ""en"" }").Properties.Count);
        }
    }
}
