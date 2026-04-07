/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>,
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.DdaSearch
{
    public static class SearchSettingsParamsFileParser
    {
        // MSFragger enzyme name -> Skyline enzyme name mapping
        private static readonly Dictionary<string, string> MSFRAGGER_ENZYME_MAP =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { @"stricttrypsin", @"Trypsin" },
                { @"trypsin", @"Trypsin/P" },
                { @"lys-c", @"Lys_C" },
                { @"lysc", @"Lys_C" },
                { @"argc", @"Arg_C" },
                { @"aspn", @"Asp_N" },
                { @"chymotrypsin", @"Chymotrypsin" },
                { @"gluc", @"Glu_C" },
                { @"cnbr", @"CNBr" },
            };

        public static bool SupportsImport(SearchEngine engine)
        {
            return engine == SearchEngine.Comet || engine == SearchEngine.MSFragger;
        }

        public static SearchSettingsPreset ImportFromFile(string filePath, string presetName)
        {
            var parseResult = ParseParamsFile(filePath);
            var engine = DetectEngine(parseResult.Settings, parseResult.EnzymeInfoLines);

            switch (engine)
            {
                case SearchEngine.Comet:
                    return ParseCometParams(presetName, parseResult.Settings, parseResult.EnzymeInfoLines);
                case SearchEngine.MSFragger:
                    return ParseMsFraggerParams(presetName, parseResult.Settings);
                default:
                    throw new InvalidDataException(
                        string.Format(@"Unable to detect search engine from params file: {0}", filePath));
            }
        }

        private static SearchEngine DetectEngine(Dictionary<string, string> settings, List<string> enzymeInfoLines)
        {
            if (enzymeInfoLines.Count > 0)
                return SearchEngine.Comet;
            if (settings.ContainsKey(@"search_enzyme_name_1") ||
                settings.ContainsKey(@"fragment_mass_units"))
                return SearchEngine.MSFragger;
            if (settings.ContainsKey(@"search_enzyme_number") ||
                settings.ContainsKey(@"peptide_mass_units") ||
                settings.ContainsKey(@"fragment_bin_tol"))
                return SearchEngine.Comet;
            return SearchEngine.Comet;
        }

        private static SearchSettingsPreset ParseCometParams(string presetName,
            Dictionary<string, string> settings, List<string> enzymeInfoLines)
        {
            // Precursor tolerance
            double precursorTolValue = GetDouble(settings, @"peptide_mass_tolerance", 3.0);
            int precursorUnitsCode = GetInt(settings, @"peptide_mass_units", 0);
            MzTolerance.Units precursorUnit;
            switch (precursorUnitsCode)
            {
                case 2:
                    precursorUnit = MzTolerance.Units.ppm;
                    break;
                case 1: // mmu -> convert to Da
                    precursorTolValue /= 1000.0;
                    precursorUnit = MzTolerance.Units.mz;
                    break;
                default: // 0 = amu/Da
                    precursorUnit = MzTolerance.Units.mz;
                    break;
            }

            // Fragment tolerance: Comet uses fragment_bin_tol and fragment_bin_offset
            // These map to the MS2 Analyzer (High/Low resolution) rather than a numeric tolerance
            double fragmentBinTol = GetDouble(settings, @"fragment_bin_tol", CometSearchEngine.FRAGMENT_BIN_TOL_LOW_RES);
            bool highRes = fragmentBinTol < 0.1; // < 0.1 Da is high resolution
            string ms2Analyzer = highRes
                ? DdaSearchResources.CometSearchEngine_Ms2Analyzer_High_resolution
                : DdaSearchResources.CometSearchEngine_Ms2Analyzer_Low_resolution;

            // Enzyme from COMET_ENZYME_INFO section
            int enzymeNumber = GetInt(settings, @"search_enzyme_number", 1);
            string enzymeName = LookupCometEnzyme(enzymeNumber, enzymeInfoLines);

            // Missed cleavages and variable mods
            int missedCleavages = GetInt(settings, @"allowed_missed_cleavage", 2);
            int maxVarMods = GetInt(settings, @"max_variable_mods_in_peptide", 5);

            // Parse modifications
            var variableMods = ParseCometVariableMods(settings);
            var staticMods = ParseStaticMods(settings);
            var allMods = new List<StaticMod>();
            allMods.AddRange(staticMods);
            allMods.AddRange(variableMods);

            // Collect additional settings that match Comet's registered settings
            var additionalSettings = CollectCometAdditionalSettings(settings);

            return new SearchSettingsPreset(
                presetName,
                SearchEngine.Comet,
                new MzTolerance(precursorTolValue, precursorUnit),
                new MzTolerance(),
                maxVarMods,
                fragmentIons: null,
                ms2Analyzer: ms2Analyzer,
                cutoffScore: 0.01,
                additionalSettings: additionalSettings,
                enzymeName: enzymeName,
                maxMissedCleavages: missedCleavages,
                structuralModifications: allMods,
                hasExplicitModifications: allMods.Count > 0);
        }

        private static SearchSettingsPreset ParseMsFraggerParams(string presetName,
            Dictionary<string, string> settings)
        {
            // Precursor tolerance - prefer precursor_true_tolerance
            double precursorTolValue;
            MzTolerance.Units precursorUnit;
            if (settings.ContainsKey(@"precursor_true_tolerance"))
            {
                precursorTolValue = GetDouble(settings, @"precursor_true_tolerance", 20.0);
                int unitsCode = GetInt(settings, @"precursor_true_units", 1);
                precursorUnit = unitsCode == 1 ? MzTolerance.Units.ppm : MzTolerance.Units.mz;
            }
            else
            {
                precursorTolValue = Math.Abs(GetDouble(settings, @"precursor_mass_upper", 20.0));
                int unitsCode = GetInt(settings, @"precursor_mass_units", 1);
                precursorUnit = unitsCode == 1 ? MzTolerance.Units.ppm : MzTolerance.Units.mz;
            }

            // Fragment tolerance
            double fragmentTolValue = GetDouble(settings, @"fragment_mass_tolerance", 20.0);
            int fragmentUnitsCode = GetInt(settings, @"fragment_mass_units", 1);
            var fragmentUnit = fragmentUnitsCode == 1 ? MzTolerance.Units.ppm : MzTolerance.Units.mz;

            // Enzyme
            string rawEnzymeName = GetString(settings, @"search_enzyme_name_1");
            string enzymeName = NormalizeMsFraggerEnzymeName(rawEnzymeName);

            // Missed cleavages and variable mods
            int missedCleavages = GetInt(settings, @"allowed_missed_cleavage_1", 2);
            int maxVarMods = GetInt(settings, @"max_variable_mods_per_peptide", 3);

            // Fragment ions
            string fragmentIons = GetString(settings, @"fragment_ion_series");

            // Parse modifications
            var variableMods = ParseMsFraggerVariableMods(settings);
            var staticMods = ParseStaticMods(settings);
            var allMods = new List<StaticMod>();
            allMods.AddRange(staticMods);
            allMods.AddRange(variableMods);

            // Collect additional settings
            var additionalSettings = CollectMsFraggerAdditionalSettings(settings);

            return new SearchSettingsPreset(
                presetName,
                SearchEngine.MSFragger,
                new MzTolerance(precursorTolValue, precursorUnit),
                new MzTolerance(fragmentTolValue, fragmentUnit),
                maxVarMods,
                fragmentIons: fragmentIons,
                ms2Analyzer: null,
                cutoffScore: 0.01,
                additionalSettings: additionalSettings,
                enzymeName: enzymeName,
                maxMissedCleavages: missedCleavages,
                structuralModifications: allMods,
                hasExplicitModifications: allMods.Count > 0);
        }

        #region File parsing

        private class ParamsFileParseResult
        {
            public Dictionary<string, string> Settings { get; set; }
            public List<string> EnzymeInfoLines { get; set; }
        }

        private static ParamsFileParseResult ParseParamsFile(string filePath)
        {
            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var enzymeInfoLines = new List<string>();
            bool inEnzymeInfo = false;

            foreach (var rawLine in File.ReadAllLines(filePath))
            {
                var line = rawLine.Trim();

                if (string.IsNullOrEmpty(line))
                    continue;

                // Check for COMET_ENZYME_INFO section marker
                if (line.StartsWith(@"[COMET_ENZYME_INFO]", StringComparison.OrdinalIgnoreCase))
                {
                    inEnzymeInfo = true;
                    continue;
                }

                if (inEnzymeInfo)
                {
                    // Enzyme info lines are not key=value; they're like "1. Trypsin 1 KR P"
                    if (!line.StartsWith(@"#"))
                        enzymeInfoLines.Add(line);
                    continue;
                }

                // Skip comment lines
                if (line.StartsWith(@"#"))
                    continue;

                // Split on first '='
                int eqIndex = line.IndexOf('=');
                if (eqIndex < 0)
                    continue;

                string key = line.Substring(0, eqIndex).Trim();
                string value = line.Substring(eqIndex + 1).Trim();

                // Strip inline comments - find '#' that's not inside the value
                // Be careful: some values legitimately use '#' but this is rare in params files
                int commentIndex = value.IndexOf('#');
                if (commentIndex >= 0)
                    value = value.Substring(0, commentIndex).Trim();

                if (!string.IsNullOrEmpty(key))
                    settings[key] = value;
            }

            return new ParamsFileParseResult { Settings = settings, EnzymeInfoLines = enzymeInfoLines };
        }

        #endregion

        #region Enzyme handling

        private static string LookupCometEnzyme(int enzymeNumber, List<string> enzymeInfoLines)
        {
            // Format: "N. enzyme_name sense cleavage restrict"
            string prefix = enzymeNumber + @".";
            foreach (var line in enzymeInfoLines)
            {
                var trimmed = line.TrimStart();
                if (!trimmed.StartsWith(prefix))
                    continue;
                // Parse: "1.  Trypsin                1      KR          P"
                var afterNumber = trimmed.Substring(prefix.Length).TrimStart();
                // The enzyme name is the first whitespace-delimited token
                var parts = afterNumber.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                    return parts[0] == @"No_enzyme" ? null : parts[0];
            }
            return @"Trypsin"; // default fallback
        }

        private static string NormalizeMsFraggerEnzymeName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName) || rawName == @"null")
                return null;
            return MSFRAGGER_ENZYME_MAP.TryGetValue(rawName, out var skylineName) ? skylineName : rawName;
        }

        #endregion

        #region Modification parsing

        private static List<StaticMod> ParseCometVariableMods(Dictionary<string, string> settings)
        {
            var mods = new List<StaticMod>();

            // Comet format: variable_modNN = mass residues binary maxmods [distance terminus force]
            for (int i = 1; i <= 9; i++)
            {
                string key = @"variable_mod" + i;
                if (!settings.TryGetValue(key, out var value))
                {
                    // Also try zero-padded: variable_mod01
                    key = string.Format(@"variable_mod{0:D2}", i);
                    if (!settings.TryGetValue(key, out value))
                        continue;
                }

                var parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    continue;

                double mass = ParseDouble(parts[0]);
                if (Math.Abs(mass) < 0.001)
                    continue; // Skip zero-mass mods

                string residues = parts[1];
                ParseResiduesAndTerminus(residues, out string cleanAAs, out ModTerminus? terminus);

                var mod = MatchOrCreateMod(mass, cleanAAs, true, terminus);
                if (mod != null)
                    mods.Add(mod);
            }

            return mods;
        }

        private static List<StaticMod> ParseMsFraggerVariableMods(Dictionary<string, string> settings)
        {
            var mods = new List<StaticMod>();

            // MSFragger format: variable_mod_NN = mass residues maxmods
            for (int i = 1; i <= 16; i++)
            {
                string key = string.Format(@"variable_mod_{0:D2}", i);
                if (!settings.TryGetValue(key, out var value))
                    continue;

                var parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    continue;

                double mass = ParseDouble(parts[0]);
                if (Math.Abs(mass) < 0.001)
                    continue;

                string residues = parts[1];
                ParseResiduesAndTerminus(residues, out string cleanAAs, out ModTerminus? terminus);

                var mod = MatchOrCreateMod(mass, cleanAAs, true, terminus);
                if (mod != null)
                    mods.Add(mod);
            }

            return mods;
        }

        private static List<StaticMod> ParseStaticMods(Dictionary<string, string> settings)
        {
            var massByAA = new Dictionary<char, double>();

            // Parse per-amino-acid static mods: add_X_name = mass
            foreach (var kvp in settings)
            {
                if (!kvp.Key.StartsWith(@"add_", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Extract the amino acid letter (second segment after "add_")
                var keyParts = kvp.Key.Split('_');
                if (keyParts.Length < 3)
                    continue;

                string aaOrTerminus = keyParts[1];

                // Skip terminal mods here (handled separately below)
                if (aaOrTerminus.Equals(@"Cterm", StringComparison.OrdinalIgnoreCase) ||
                    aaOrTerminus.Equals(@"Nterm", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (aaOrTerminus.Length != 1 || !char.IsLetter(aaOrTerminus[0]))
                    continue;

                double mass = ParseDouble(kvp.Value);
                if (Math.Abs(mass) < 0.001)
                    continue;

                char aa = char.ToUpper(aaOrTerminus[0]);
                massByAA[aa] = mass;
            }

            // Group amino acids with the same mass (within tolerance) into single mods
            var mods = new List<StaticMod>();
            var grouped = massByAA.GroupBy(
                kvp => Math.Round(kvp.Value, 3),
                kvp => kvp.Key);

            foreach (var group in grouped)
            {
                string aas = new string(group.OrderBy(c => c).ToArray());
                double mass = massByAA[group.First()];
                var mod = MatchOrCreateMod(mass, aas, false, null);
                if (mod != null)
                    mods.Add(mod);
            }

            // Handle terminal static mods
            ParseTerminalStaticMod(settings, @"add_Nterm_peptide", ModTerminus.N, mods);
            ParseTerminalStaticMod(settings, @"add_Cterm_peptide", ModTerminus.C, mods);
            ParseTerminalStaticMod(settings, @"add_Nterm_protein", ModTerminus.N, mods);
            ParseTerminalStaticMod(settings, @"add_Cterm_protein", ModTerminus.C, mods);

            return mods;
        }

        private static void ParseTerminalStaticMod(Dictionary<string, string> settings,
            string key, ModTerminus terminus, List<StaticMod> mods)
        {
            if (!settings.TryGetValue(key, out var value))
                return;
            double mass = ParseDouble(value);
            if (Math.Abs(mass) < 0.001)
                return;
            var mod = MatchOrCreateMod(mass, null, false, terminus);
            if (mod != null)
                mods.Add(mod);
        }

        private static void ParseResiduesAndTerminus(string residues, out string cleanAAs, out ModTerminus? terminus)
        {
            terminus = null;
            var aaChars = new List<char>();

            foreach (char c in residues)
            {
                switch (c)
                {
                    case 'n':
                    case '[':
                        terminus = ModTerminus.N;
                        break;
                    case 'c':
                    case ']':
                        terminus = ModTerminus.C;
                        break;
                    case '^':
                    case '*':
                    case 'X':
                        // Any amino acid - leave AAs empty
                        break;
                    default:
                        if (char.IsUpper(c))
                            aaChars.Add(c);
                        break;
                }
            }

            cleanAAs = aaChars.Count > 0 ? new string(aaChars.ToArray()) : null;
        }

        private static StaticMod MatchOrCreateMod(double mass, string aas, bool isVariable, ModTerminus? terminus)
        {
            // Try UniMod matching for each amino acid
            if (!string.IsNullOrEmpty(aas))
            {
                var matched = UniMod.MatchModificationMass(mass, aas[0], 4, true, terminus, true);
                if (matched != null)
                    return matched;
            }

            // Create a custom mod
            string name = string.Format(@"[{0:F4}]", mass);
            if (!string.IsNullOrEmpty(aas))
                name += string.Format(@" ({0})", aas);
            else if (terminus.HasValue)
                name += string.Format(@" ({0}-term)", terminus == ModTerminus.N ? @"N" : @"C");

            return new StaticMod(name, aas, terminus, isVariable,
                null, LabelAtoms.None, RelativeRT.Matching, mass, mass, null);
        }

        #endregion

        #region Additional settings collection

        // Keys managed as first-class preset fields (not additional settings)
        private static readonly HashSet<string> COMET_FIRST_CLASS_KEYS =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                @"peptide_mass_tolerance", @"peptide_mass_units",
                @"search_enzyme_number", @"allowed_missed_cleavage",
                @"max_variable_mods_in_peptide",
                @"database_name", @"decoy_search",
                @"num_enzyme_termini",
            };

        // Comet additional settings keys (from CometSearchEngine constructor)
        private static readonly HashSet<string> COMET_ADDITIONAL_KEYS =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                @"activation_method", @"add_Nterm_peptide", @"add_Nterm_protein",
                @"auto_fragment_bin_tol", @"auto_peptide_mass_tolerance",
                @"clear_mz_range", @"clip_nterm_methionine", @"digest_mass_range",
                @"equal_I_and_L", @"fragment_bin_offset", @"fragment_bin_tol", @"isotope_error",
                @"mass_offsets", @"mass_type_fragment", @"mass_type_parent",
                @"max_duplicate_proteins", @"max_fragment_charge", @"max_precursor_charge",
                @"minimum_intensity", @"minimum_peaks", @"ms_level",
                @"nucleotide_reading_frame", @"num_output_lines", @"num_results",
                @"override_charge", @"peptide_length_range", @"precursor_NL_ions",
                @"precursor_charge", @"precursor_tolerance_type",
                @"remove_precursor_peak", @"remove_precursor_tolerance",
                @"require_variable_mod", @"scan_range", @"spectrum_batch_size",
                @"text_file_extension", @"theoretical_fragment_ions", @"use_NL_ions",
            };

        private static Dictionary<string, string> CollectCometAdditionalSettings(
            Dictionary<string, string> settings)
        {
            var result = new Dictionary<string, string>();
            foreach (var kvp in settings)
            {
                if (COMET_FIRST_CLASS_KEYS.Contains(kvp.Key))
                    continue;
                if (COMET_ADDITIONAL_KEYS.Contains(kvp.Key))
                    result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        private static readonly HashSet<string> MSFRAGGER_FIRST_CLASS_KEYS =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                @"precursor_mass_lower", @"precursor_mass_upper", @"precursor_mass_units",
                @"precursor_true_tolerance", @"precursor_true_units",
                @"fragment_mass_tolerance", @"fragment_mass_units",
                @"search_enzyme_name_1", @"search_enzyme_cut_1",
                @"search_enzyme_nocut_1", @"search_enzyme_sense_1",
                @"allowed_missed_cleavage_1",
                @"search_enzyme_name_2", @"search_enzyme_cut_2",
                @"search_enzyme_nocut_2", @"search_enzyme_sense_2",
                @"allowed_missed_cleavage_2",
                @"num_enzyme_termini",
                @"max_variable_mods_per_peptide",
                @"fragment_ion_series",
                @"database_name", @"num_threads", @"decoy_prefix",
            };

        // MSFragger additional settings keys (from MsFraggerSearchEngine constructor)
        private static readonly HashSet<string> MSFRAGGER_ADDITIONAL_KEYS =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                @"data_type", @"check_spectral_files", @"calibrate_mass",
                @"use_all_mods_in_first_search", @"deisotope", @"deneutralloss",
                @"isotope_error", @"mass_offsets", @"mass_offsets_detailed",
                @"use_detailed_offsets", @"precursor_mass_mode",
                @"remove_precursor_peak", @"remove_precursor_range",
                @"intensity_transform", @"activation_types", @"analyzer_types",
                @"group_variable", @"require_precursor", @"reuse_dia_fragment_peaks",
                @"mass_diff_to_variable_mod", @"localize_delta_mass",
                @"delta_mass_exclude_ranges",
                @"labile_search_mode", @"restrict_deltamass_to",
                @"diagnostic_intensity_filter", @"Y_type_masses",
                @"diagnostic_fragments", @"remainder_fragment_masses",
                @"clip_nTerm_M", @"allow_multiple_variable_mods_on_residue",
                @"max_variable_mods_combinations",
                @"output_report_topN", @"output_max_expect", @"report_alternative_proteins",
                @"precursor_charge", @"override_charge",
                @"digest_min_length", @"digest_max_length", @"digest_mass_range",
                @"max_fragment_charge",
                @"track_zero_topN", @"zero_bin_accept_expect", @"zero_bin_mult_expect",
                @"minimum_peaks", @"use_topN_peaks",
                @"min_fragments_modelling", @"min_matched_fragments",
                @"min_sequence_matches", @"minimum_ratio", @"clear_mz_range",
            };

        private static Dictionary<string, string> CollectMsFraggerAdditionalSettings(
            Dictionary<string, string> settings)
        {
            var result = new Dictionary<string, string>();
            foreach (var kvp in settings)
            {
                if (MSFRAGGER_FIRST_CLASS_KEYS.Contains(kvp.Key))
                    continue;
                if (MSFRAGGER_ADDITIONAL_KEYS.Contains(kvp.Key))
                    result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        #endregion

        #region Helpers

        private static double GetDouble(Dictionary<string, string> settings, string key, double defaultValue)
        {
            return settings.TryGetValue(key, out var value)
                ? ParseDouble(value, defaultValue)
                : defaultValue;
        }

        private static int GetInt(Dictionary<string, string> settings, string key, int defaultValue)
        {
            return settings.TryGetValue(key, out var value) && int.TryParse(value, out int result)
                ? result
                : defaultValue;
        }

        private static string GetString(Dictionary<string, string> settings, string key)
        {
            return settings.TryGetValue(key, out var value) ? value : null;
        }

        private static double ParseDouble(string value, double defaultValue = 0)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
                ? result
                : defaultValue;
        }

        #endregion
    }
}
