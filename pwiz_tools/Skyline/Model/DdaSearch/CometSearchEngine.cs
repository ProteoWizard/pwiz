/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2024 University of Washington - Seattle, WA
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

using pwiz.BiblioSpec;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using pwiz.CommonMsData;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DdaSearch
{
    public class CometSearchEngine : AbstractDdaSearchEngine, IProgressMonitor
    {
        private List<string> COMET_SETTINGS = new List<string>();

        // Percolator settings
        private const string PERCOLATOR_TEST_QVALUE_CUTOFF = "test-fdr";
        private const string PERCOLATOR_TRAIN_QVALUE_CUTOFF = "train-fdr";
        private readonly string[] PERCOLATOR_SETTINGS = { PERCOLATOR_TEST_QVALUE_CUTOFF, PERCOLATOR_TRAIN_QVALUE_CUTOFF };

        private const string KEEP_INTERMEDIATE_FILES = "keep-intermediate-files";

        public CometSearchEngine(double percolatorQvalueCutoff)
        {
            AdditionalSettings = new Dictionary<string, Setting>
            {
                {PERCOLATOR_TEST_QVALUE_CUTOFF, new Setting(PERCOLATOR_TEST_QVALUE_CUTOFF, percolatorQvalueCutoff, 0, 1)},
                {PERCOLATOR_TRAIN_QVALUE_CUTOFF, new Setting(PERCOLATOR_TRAIN_QVALUE_CUTOFF, 0.01, 0, 1)},
                {KEEP_INTERMEDIATE_FILES, new Setting(KEEP_INTERMEDIATE_FILES, false)},
            };

            // ReSharper disable LocalizableElement
            AddAdditionalSetting(COMET_SETTINGS, new Setting("activation_method", "ALL", "ALL|CID|ECD|ETD+SA|ETD|PQD|HCD|IRMPD".Split('|')));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("add_Nterm_peptide", 0.0, 0.0));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("add_Nterm_protein", 0.0, 0.0));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("auto_fragment_bin_tol", "false", "false|warn|fail".Split('|')));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("auto_peptide_mass_tolerance", "false", "false|warn|fail".Split('|')));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("clear_mz_range", "0.0 0.0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("clip_nterm_methionine", 0, 0, 1));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("digest_mass_range", "600.0 5000.0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("equal_I_and_L", 1, 0, 1));
            //AddAdditionalSetting(COMET_SETTINGS, new Setting("explicit_deltacn", "0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("fragment_bin_offset", 0.4, 0.0));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("isotope_error", 0, 0, 5)); // 0=off, 1=0/1 (C13 error), 2=0/1/2, 3=0/1/2/3, 4=--8/-4/0/4/8 (for +4/+8 labeling), 5=-1/0/1/2/3.
            AddAdditionalSetting(COMET_SETTINGS, new Setting("mass_offsets", ""));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("mass_type_fragment", 1, 0, 1));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("mass_type_parent", 1, 0, 1));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("max_duplicate_proteins", 20, 0));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("max_fragment_charge", 3, 1));
            //AddAdditionalSetting(COMET_SETTINGS, new Setting("max_index_runtime", "0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("max_precursor_charge", 6, 1, 9));
            //AddAdditionalSetting(COMET_SETTINGS, new Setting("max_variable_mods_in_peptide", 5, 0));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("minimum_intensity", 0.0, 0.0));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("minimum_peaks", 10, 0));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("ms_level", 2, 2));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("nucleotide_reading_frame", "0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("num_output_lines", 5, 1));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("num_results", 50, 1));
            //AddAdditionalSetting(COMET_SETTINGS, new Setting("num_threads", "0"));
            //AddAdditionalSetting(COMET_SETTINGS, new Setting("old_mods_encoding", 0, 0, 1));
            /*AddAdditionalSetting(COMET_SETTINGS, new Setting("output_mzidentmlfile", "0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("output_pepxmlfile", "1"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("output_percolatorfile", "0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("output_sqtfile", "0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("output_sqtstream", "0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("output_suffix", ""));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("output_txtfile", "1"));*/
            AddAdditionalSetting(COMET_SETTINGS, new Setting("override_charge", 0, 0, 1));
            //AddAdditionalSetting(COMET_SETTINGS, new Setting("peff_format", 0, 0, 1));
            //AddAdditionalSetting(COMET_SETTINGS, new Setting("peff_obo", ""));
            //AddAdditionalSetting(COMET_SETTINGS, new Setting("peff_verbose_output", 0, 0, 1));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("peptide_length_range", "6 50"));
            //AddAdditionalSetting(COMET_SETTINGS, new Setting("peptide_mass_tolerance", 3.0, 0));
            //AddAdditionalSetting(COMET_SETTINGS, new Setting("peptide_mass_units", 0, 0, 1));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("precursor_NL_ions", ""));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("precursor_charge", "0 0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("precursor_tolerance_type", 1, 0, 1));
            //AddAdditionalSetting(COMET_SETTINGS, new Setting("print_expect_score", "1"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("remove_precursor_peak", 0, 0, 1));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("remove_precursor_tolerance", 1.5, 0.0));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("require_variable_mod", 0, 0, 1));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("scan_range", "0 0"));
            //AddAdditionalSetting(COMET_SETTINGS, new Setting("search_enzyme2_number", "0"));
            //AddAdditionalSetting(COMET_SETTINGS, new Setting("show_fragment_ions", 0, 0, 1));
            //AddAdditionalSetting(COMET_SETTINGS, new Setting("skip_researching", 1, 0, 1));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("spectrum_batch_size", 0, 0));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("text_file_extension", ""));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("theoretical_fragment_ions", 1, 0, 1));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("use_NL_ions", "1", new []{ "0", "1" }));
            /*AddAdditionalSetting(COMET_SETTINGS, new Setting("use_A_ions", 0, 0, 1));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("use_B_ions", 1, 0, 1));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("use_C_ions", 0, 0, 1));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("use_X_ions", 0, 0, 1));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("use_Y_ions", 1, 0, 1));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("use_Z1_ions", 0, 0, 1));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("use_Z_ions", 0, 0, 1));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("variable_mod01", "0.0 null 0 4 -1 0 0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("variable_mod02", "0.0 null 0 4 -1 0 0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("variable_mod03", "0.0 null 0 4 -1 0 0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("variable_mod04", "0.0 null 0 4 -1 0 0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("variable_mod05", "0.0 null 0 4 -1 0 0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("variable_mod06", "0.0 null 0 4 -1 0 0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("variable_mod07", "0.0 null 0 4 -1 0 0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("variable_mod08", "0.0 null 0 4 -1 0 0"));
            AddAdditionalSetting(COMET_SETTINGS, new Setting("variable_mod09", "0.0 null 0 4 -1 0 0"));*/
            // ReSharper restore LocalizableElement
        }

        private void AddAdditionalSetting(List<string> settingNameList, Setting setting)
        {
            settingNameList.Add(setting.Name);
            AdditionalSettings[setting.Name] = setting;
        }

        private static readonly string[] FRAGMENTATION_METHODS =
        {
            @"b,y",
            @"y",
            @"c,z",
            @"c,z1",
        };

        static string CRUX_FILENAME = @"crux-4.3";
        static Uri CRUX_URL = new Uri($@"https://noble.gs.washington.edu/crux-downloads/{CRUX_FILENAME}/{CRUX_FILENAME}.Windows.AMD64.zip");
        public static string CruxDirectory => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), CRUX_FILENAME);
        public static string CruxBinary => Settings.Default.SearchToolList.GetToolPathOrDefault(SearchToolType.CruxComet, Path.Combine(CruxDirectory, $@"{CRUX_FILENAME}.Windows.AMD64", @"bin", @"crux.exe"));
        public static string CometArgs => Settings.Default.SearchToolList.GetToolArgsOrDefault(SearchToolType.CruxComet, "");
        public static string PercolatorArgs => Settings.Default.SearchToolList.GetToolArgsOrDefault(SearchToolType.CruxPercolator, "");

        public static FileDownloadInfo[] FilesToDownload => new[] {
            new FileDownloadInfo
            {
                Filename = CRUX_FILENAME, DownloadUrl = CRUX_URL, InstallPath = CruxDirectory, OverwriteExisting = true, Unzip = true,
                ToolType = SearchToolType.CruxComet, ToolPath = CruxBinary, ToolExtraArgs = CometArgs
            },
            new FileDownloadInfo
            {
                Filename = CRUX_FILENAME, DownloadUrl = CRUX_URL, InstallPath = CruxDirectory, OverwriteExisting = true, Unzip = true,
                ToolType = SearchToolType.CruxPercolator, ToolPath = CruxBinary, ToolExtraArgs = PercolatorArgs
            }
        };

        private MzTolerance _precursorMzTolerance;
        private MzTolerance _fragmentMzTolerance;
        private SortedSet<string> _fragmentIons;
        private Enzyme _enzyme;
        private int _ntt, _maxMissedCleavages;
        private int _maxVariableMods = 2;
        private List<CruxModification> _variableMods;
        private string _modParams;
        private string _fastaFilepath;
        private string _decoyPrefix = @"DECOY_";
        private List<string> _intermediateFiles;
        private const string _cutoffScoreName = ScoreType.PERCOLATOR_QVALUE;

        private CancellationTokenSource _cancelToken;
        private IProgressStatus _progressStatus;
        private bool _success;

        private void DeleteIntermediateFiles()
        {
            if (_intermediateFiles != null && !KeepIntermediateFiles)
            {
                foreach (var path in _intermediateFiles)
                {
                    FileEx.SafeDelete(path, true); // Don't throw if file can't be deleted
                    DirectoryEx.SafeDelete(path); // In case it's actually a directory
                }
            }
        }

        private bool KeepIntermediateFiles => (bool)AdditionalSettings[KEEP_INTERMEDIATE_FILES].Value;

        public override string[] FragmentIons => FRAGMENTATION_METHODS;
        public override string[] Ms2Analyzers => new [] { @"Default" };

        public override MzToleranceUnits[] PrecursorIonToleranceUnitTypes
        {
            get
            {
                string mzName = (int) AdditionalSettings[@"precursor_tolerance_type"].Value == 0 ? @"Da" : @"m/z";
                return new[]
                {
                    new MzToleranceUnits(mzName, MzTolerance.Units.mz),
                    new MzToleranceUnits(@"ppm", MzTolerance.Units.ppm)
                };
            }
        }

        public override MzToleranceUnits[] FragmentIonToleranceUnitTypes => new[] { new MzToleranceUnits(@"m/z", MzTolerance.Units.mz) };
        public override string EngineName => @"Comet";
        public override Bitmap SearchEngineLogo => null;
        public override string CutoffScoreName => _cutoffScoreName;
        public override string CutoffScoreLabel => PropertyNames.CutoffScore_PERCOLATOR_QVALUE;
        public override double DefaultCutoffScore { get; } = new ScoreType(_cutoffScoreName, ScoreType.PROBABILITY_INCORRECT).DefaultValue;

        public override string SearchEngineBlurb => string.Empty;
        public override event NotificationEventHandler SearchProgressChanged;

        public override bool Run(CancellationTokenSource cancelToken, IProgressStatus status)
        {

            _cancelToken = cancelToken;
            _progressStatus = status;
            _success = true;

            try
            {
                _intermediateFiles = new List<string>();
                _fastaFilepath = FastaFileNames[0];

                var paramsFileText = new StringBuilder();

                SetCometParam(paramsFileText, @"num_threads", 0);
                SetCometParam(paramsFileText, @"decoy_prefix", _decoyPrefix);
                SetCometParam(paramsFileText, @"decoy_search", 1);
                SetCometParam(paramsFileText, @"output_pepxmlfile", 1);
                SetCometParam(paramsFileText, @"output_percolatorfile", 1);
                SetCometParam(paramsFileText, @"output_txtfile", 0);
                SetCometParam(paramsFileText, @"peptide_mass_tolerance", _precursorMzTolerance.Value.ToString(CultureInfo.InvariantCulture));
                SetCometParam(paramsFileText, @"peptide_mass_units", _precursorMzTolerance.Unit == MzTolerance.Units.ppm ? 2 : 0);
                SetCometParam(paramsFileText, @"fragment_bin_tol", _fragmentMzTolerance.Value.ToString(CultureInfo.InvariantCulture));
                //SetCometParam(paramsFileText, @"fragment_ion_series", _fragmentIons);
                SetCometParam(paramsFileText, @"num_enzyme_termini", _ntt);
                SetCometParam(paramsFileText, @"allowed_missed_cleavage", _maxMissedCleavages);
                SetCometParam(paramsFileText, @"sample_enzyme_number", 1);
                SetCometParam(paramsFileText, @"search_enzyme_number", 1);
                SetCometParam(paramsFileText, @"max_variable_mods_in_peptide", _maxVariableMods);
                foreach (var settingName in COMET_SETTINGS)
                    SetCometParam(paramsFileText, settingName, AdditionalSettings[settingName].ValueToString(CultureInfo.InvariantCulture));

                // ReSharper disable LocalizableElement
                string[] possibleIonTypes = { "a", "b", "c", "x", "y", "z", "z1" };
                // ReSharper restore LocalizableElement
                foreach (var ionType in possibleIonTypes)
                {
                    if (_fragmentIons.Contains(ionType))
                        SetCometParam(paramsFileText, @"use_" + ionType.ToUpperInvariant() + @"_ions", 1);
                    else
                        SetCometParam(paramsFileText, @"use_" + ionType.ToUpperInvariant() + @"_ions", 0);
                }

                paramsFileText.Append(_modParams);

                paramsFileText.Append(enzymeParameterInfo);
                // 1. Trypsin             1      KR P
                paramsFileText.AppendFormat(@"1. {0} {1} {2} {3}{4}",
                    _enzyme.Name ?? @"unnamed",
                    _enzyme.IsCTerm ? 1 : 0,
                    _enzyme.CleavageC ?? _enzyme.CleavageN,
                    _enzyme.RestrictC ?? _enzyme.RestrictN,
                    Environment.NewLine);

                string defaultOutputDirectory = Path.GetDirectoryName(SpectrumFileNames[0].GetFilePath()) ?? Path.Combine(Environment.CurrentDirectory, "comet-output");
                defaultOutputDirectory = PathEx.GetNonUnicodePath(defaultOutputDirectory);  // Convert unicode path to 8.3 if needed

                string paramsFile = KeepIntermediateFiles ? Path.Combine(defaultOutputDirectory, @"comet.params") : Path.GetTempFileName();
                paramsFile = PathEx.GetNonUnicodePath(paramsFile); // Convert unicode path to 8.3 if needed
                _intermediateFiles.Add(paramsFile);
                File.WriteAllText(paramsFile, paramsFileText.ToString());

                // Run Comet
                var pr = new ProcessRunner();
                var psi = new ProcessStartInfo(CruxBinary, $@"comet {CometArgs} --overwrite T --output-dir ""{defaultOutputDirectory}"" --parameter-file ""{paramsFile}""")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                foreach (var filename in SpectrumFileNames)
                    psi.Arguments += $@" ""{PathEx.GetNonUnicodePath(filename.GetFilePath())}""";
                psi.Arguments += $@" ""{PathEx.GetNonUnicodePath(_fastaFilepath)}""";
                pr.Run(psi, string.Empty, this, ref _progressStatus, ProcessPriorityClass.BelowNormal, true);

                string cruxOutputDir = defaultOutputDirectory;
                string cruxParamsFile = PathEx.GetNonUnicodePath(KeepIntermediateFiles ? Path.Combine(defaultOutputDirectory, @"crux.params") : Path.GetTempFileName());
                _intermediateFiles.Add(cruxParamsFile);
                var cruxParamsFileText = GetCruxParamsText();
                File.WriteAllText(cruxParamsFile, cruxParamsFileText);

                // Run Crux Percolator
                psi.FileName = CruxBinary;
                psi.Arguments = $@"percolator {PercolatorArgs} --only-psms T --output-dir ""{cruxOutputDir}"" --overwrite T --decoy-prefix ""{_decoyPrefix}"" --parameter-file ""{cruxParamsFile}""";

                foreach (var settingName in PERCOLATOR_SETTINGS)
                    psi.Arguments += $@" --{AdditionalSettings[settingName].ToString(false, CultureInfo.InvariantCulture)}";

                foreach (var spectrumFilename in SpectrumFileNames)
                {
                    string cometPepXmlFilepath = GetCometSearchResultFilepath(spectrumFilename);
                    string cruxInputFilepath = GetCometSearchResultFilepath(spectrumFilename, @".pin");
                    string cruxFixedInputFilepath = GetCometSearchResultFilepath(spectrumFilename, @".fixed.pin");
                    _intermediateFiles.Add(cruxInputFilepath);
                    FixCometPin(cruxInputFilepath, cruxFixedInputFilepath, cometPepXmlFilepath);
                    psi.Arguments += $@" ""{cruxFixedInputFilepath}""";

                }

                pr.Run(psi, string.Empty, this, ref _progressStatus, ProcessPriorityClass.BelowNormal, true);

                var qvalueByPsmId = new Dictionary<string, double>();
                // Read PSMs from text files and update original pepXMLs with Percolator scores
                string percolatorTargetPsmsTsv = Path.Combine(cruxOutputDir, @"percolator.target.psms.txt");
                string percolatorDecoyPsmsTsv = Path.Combine(cruxOutputDir, @"percolator.decoy.psms.txt");
                GetPercolatorScores(percolatorTargetPsmsTsv, qvalueByPsmId);
                GetPercolatorScores(percolatorDecoyPsmsTsv, qvalueByPsmId);

                foreach (var spectrumFilename in SpectrumFileNames)
                {
                    string cometPepXmlFilepath = GetCometSearchResultFilepath(spectrumFilename);
                    string finalOutputFilepath = GetSearchResultFilepath(spectrumFilename);
                    FixPercolatorPepXml(cometPepXmlFilepath, finalOutputFilepath, spectrumFilename, qvalueByPsmId);
                }

                DeleteIntermediateFiles();

                _progressStatus = _progressStatus.NextSegment();
            }
            catch (Exception ex)
            {
                _progressStatus = _progressStatus.ChangeErrorException(ex).ChangeMessage(string.Format(DdaSearchResources.DdaSearch_Search_failed__0, ex.Message));
                _success = false;
            }

            if (IsCanceled && !_progressStatus.IsCanceled)
            {
                _progressStatus = _progressStatus.Cancel().ChangeMessage(Resources.DDASearchControl_SearchProgress_Search_canceled);
                _success = false;
            }

            if (!_success)
            {
                DeleteIntermediateFiles();
                _cancelToken.Cancel();
                //break;
            }

            if (_success)
                _progressStatus = _progressStatus.Complete().ChangeMessage(Resources.DDASearchControl_SearchProgress_Search_done);
            UpdateProgress(_progressStatus);

            return _success;
        }

        private void SetCometParam(StringBuilder config, string name, object value)
        {
            string nameWithEquals = name + @"=";
            config.Append(nameWithEquals);
            config.AppendLine(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private class CruxModification
        {
            public CruxModification(StaticMod mod, double mz, string residues)
            {
                Mod = mod;
                Mz = mz;
                Residues = residues;
            }

            public StaticMod Mod { get; }
            public double Mz { get; }
            public string Residues { get; }

            public int GetCruxTerminusOrdinal()
            {
                switch (Mod.Terminus)
                {
                    case ModTerminus.C: return 3;
                    case ModTerminus.N: return 2;
                    case null: return 0;
                    default: throw new ArgumentOutOfRangeException();
                }
            }

            public string GetCruxResidues()
            {
                switch (Mod.Terminus)
                {
                    case ModTerminus.C: return @"null";
                    case ModTerminus.N: return @"null";
                    case null: return Residues;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }

        private string GetCruxParamsText()
        {
            var cruxParamsFileText = new StringBuilder();
            foreach (var line in _modParams.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
            {
                if (line.Contains(@"variable_mod"))
                    continue;
                string cruxLine = Regex.Replace(line, "add_([A-Z])_(\\S+)\\s*=\\s*(.*)", $"add_$1_$2 = $3{Environment.NewLine}$1 = $3");
                cruxParamsFileText.AppendLine(cruxLine);
            }

            //# Up to 9 variable modifications are supported. Each modification is specified
            //# using seven entries: <mass> <residues> <type> <max> <distance> <terminus>
            //# <force>." Type is 0 for static mods and non-zero for variable mods. Note that
            //# that if you set the same type value on multiple modification entries, Comet
            //# will treat those variable modifications as a binary set. This means that all
            //# modifiable residues in the binary set must be unmodified or modified. Multiple
            //# binary sets can be specified by setting a different binary modification value.
            //# Max is an integer specifying the maximum number of modified residues possible
            //# in a peptide for this modification entry. Distance specifies the distance the
            //# modification is applied to from the respective terminus: -1 = no distance
            //# constraint; 0 = only applies to terminal residue; N = only applies to terminal
            //# residue through next N residues. Terminus specifies which terminus the
            //# distance constraint is applied to: 0 = protein N-terminus; 1 = protein
            //# C-terminus; 2 = peptide N-terminus; 3 = peptide C-terminus.Force specifies
            //# whether peptides must contain this modification: 0 = not forced to be present;
            //# 1 = modification is required.

            int iMod = 0;
            foreach (var m in _variableMods)
            {
                ++iMod;
                cruxParamsFileText.AppendLine(string.Format(@"variable_mod{0:D2} = {1} {2} {3} {4} -1 {5} 0", iMod,
                    m.Mz, m.GetCruxResidues(), iMod, _maxVariableMods, m.GetCruxTerminusOrdinal()));
            }

            return cruxParamsFileText.ToString();
        }

        private void GetPercolatorScores(string percolatorTsvFilepath, Dictionary<string, double> qvalueByPsmId)
        {
            using var percolatorTargetPsmsReader = new DsvFileReader(percolatorTsvFilepath, TextUtil.SEPARATOR_TSV);
            int psmIdColumn = percolatorTargetPsmsReader.GetFieldIndex(@"PSMId");
            int qvalueColumn = percolatorTargetPsmsReader.GetFieldIndex(@"q-value");
            int psmIdStartIndex = Path.GetDirectoryName(Path.GetDirectoryName(percolatorTsvFilepath))?.Length + 1 ?? 0; // trim off the directory name
            while (percolatorTargetPsmsReader.ReadLine() != null)
            {
                var psmId = percolatorTargetPsmsReader.GetFieldByIndex(psmIdColumn);
                psmId = psmId.Substring(psmIdStartIndex);
                var qvalue = Convert.ToDouble(percolatorTargetPsmsReader.GetFieldByIndex(qvalueColumn), CultureInfo.InvariantCulture);
                qvalueByPsmId[psmId] = qvalue;
            }
        }

        // Add Percolator score to Comet pepXML
        private void FixPercolatorPepXml(string cruxOutputFilepath, string finalOutputFilepath, MsDataFileUri spectrumFilename, Dictionary<string, double> qvalueByPsmId)
        {
            bool isBrukerSource = DataSourceUtil.GetSourceType(spectrumFilename.GetFilePath()) == DataSourceUtil.TYPE_BRUKER;

            using (var pepXmlFile = new StreamReader(cruxOutputFilepath))
            using (var fixedPepXmlFile = new StreamWriter(finalOutputFilepath))
            {
                string line;
                string lastPsmId = "";
                string lastRank = "";
                while ((line = pepXmlFile.ReadLine()) != null)
                {
                    if (line.Contains(@"<spectrum_query"))
                    {
                        // We need to convert:
                        //   DdaSearchTest/comet.run_2.04610.04610.3
                        // to:
                        //   DdaSearchTest/comet.run_2_4610_3
                        lastPsmId = Regex.Replace(line, @".* spectrum=""(.+?)\.0*(\d+)\.\d+\.(\d+)"" .*", "$1_$2_$3");
                    }
                    else if (line.Contains(@"<search_hit"))
                    {
                        lastRank = Regex.Replace(line, @".* hit_rank=""(\d+)"" .*", "$1");
                    }
                    else if (line.Contains(@"<search_score name=""expect"""))
                    {
                        string psmIdAndRank = $@"{lastPsmId}_{lastRank}";
                        if (qvalueByPsmId.ContainsKey(psmIdAndRank))
                        {
                            fixedPepXmlFile.WriteLine(line);
                            fixedPepXmlFile.WriteLine(@"    <search_score name=""percolator_qvalue"" value=""{0}"" />", qvalueByPsmId[psmIdAndRank].ToString(CultureInfo.InvariantCulture));
                            continue;
                        }
                        // MCC: This happens when percolator's text tables drops a PSM that is in pepXML; I'm not sure why it happens though.
                        //else
                        //    Console.WriteLine($"{lastPsmId} not found in percolator scores.");
                    }
                    else if (line.Contains(@"</search_summary>"))
                    {
                        fixedPepXmlFile.WriteLine(@"<parameter name=""post-processor"" value=""percolator"" />");
                    }
                    fixedPepXmlFile.WriteLine(line);
                }
            }
        }

        // Fix (TODO: remove these hacks when it's fixed in Crux and/or Comet):
        // - bug in Crux pepXML writer where it doesn't ignore the N-terminal mod annotation (n[123]); the writer doesn't handle terminal mods anyway, so just remove the n and move the mod over to be an AA mod
        private void FixCometPin(string cruxInputFilepath, string cruxFixedInputFilepath, string cometPepXmlFilepath)
        {
            using (var pinFile = new StreamReader(cruxInputFilepath))
            using (var pinFixedFile = new StreamWriter(cruxFixedInputFilepath))
            {
                string line;
                while ((line = pinFile.ReadLine()) != null)
                {
                    // move N-terminal mod to after first AA
                    line = Regex.Replace(line, "n(\\[[^]]+\\])([A-Z])", "$2$1");

                    // remove C-terminal mod indicator to avoid "'c' is not an amino acid"
                    line = line.Replace(@"c[", @"[");

                    // handle case of mod on terminal and on terminal AA
                    if (line.Contains(@"]["))
                    {
                        var m = Regex.Match(line, "\\[([^]]+)\\]\\[([^]]+)\\]");
                        if (!m.Success)
                            throw new InvalidDataException(@"found back to back brackets but could not parse them with regex: " + line);
                        if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double modMass1))
                            throw new InvalidDataException(@"could not parse mod mass from " + m.Groups[1].Value);
                        if (!double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double modMass2))
                            throw new InvalidDataException(@"could not parse mod mass from " + m.Groups[2].Value);
                        line = Regex.Replace(line, "\\[([^]]+\\]\\[[^]]+)\\]", $"[{modMass1 + modMass2:F4}]");
                    }

                    pinFixedFile.WriteLine(line);
                }
            }
        }

        private string enzymeParameterInfo => @"
[COMET_ENZYME_INFO]
0.  No_enzyme				0       -           -
";
/*1.  Trypsin                       1      KR           P
2.  Trypsin/P                        1      KR           -
3.  Lys_C                      1      K            P
4.  Lys_N                      0      K            -
5.  Arg_C                      1      R            P
6.  Asp_N                      0      D            -
7.  CNBr                       1      M            -
8.  Glu_C                      1      DE           P
9.  PepsinA                    1      FL           P
10. Chymotrypsin               1      FWYL         P
11. No_cut               1      @         @
";*/
            
        public override void SetModifications(IEnumerable<StaticMod> fixedAndVariableModifs, int maxVariableMods_)
        {
            _maxVariableMods = maxVariableMods_;
            _variableMods = new List<CruxModification>();

            // maximum of 16 variable mods - amino acid codes, * for any amino acid, [ and ] specifies protein termini, n and c specifies peptide termini
            // TODO: alert when there are more than 16 variable mods

            var modParamLines = new List<string>();
            var staticModsByAA = new Dictionary<char, double>();
            int modCounter = 0;

            foreach (var mod in fixedAndVariableModifs)
            {
                // can't use mod with no formula or mass; CONSIDER throwing exception
                if (mod.LabelAtoms == LabelAtoms.None && ParsedMolecule.IsNullOrEmpty(mod.ParsedMolecule) && mod.MonoisotopicMass == null ||
                    mod.LabelAtoms != LabelAtoms.None && mod.AAs.IsNullOrEmpty())
                    continue;

                string position = string.Empty;
                switch (mod.Terminus)
                {
                    case ModTerminus.N:
                        position = @"n";
                        break;
                    case ModTerminus.C:
                        position = @"c";
                        break;
                }

                Action<double, string> addMod = (mass, residues) =>
                {
                    /*  # Up to 9 variable modifications are supported. Each modification is specified
                        # using seven entries: "<mass> <residues> <type> <max> <distance> <terminus> <force>."
                        # Type is 0 for static mods and non-zero for variable mods. Note that
                        # that if you set the same type value on multiple modification entries, Comet
                        # will treat those variable modifications as a binary set. This means that all
                        # modifiable residues in the binary set must be unmodified or modified. Multiple
                        # binary sets can be specified by setting a different binary modification value.
                        # Max is an integer specifying the maximum number of modified residues possible
                        # in a peptide for this modification entry. Distance specifies the distance the
                        # modification is applied to from the respective terminus: -1 = no distance
                        # contraint; 0 = only applies to terminal residue; N = only applies to terminal
                        # residue through next N residues. Terminus specifies which terminus the
                        # distance constraint is applied to: 0 = protein N-terminus; 1 = protein
                        # C-terminus; 2 = peptide N-terminus; 3 = peptide C-terminus.Force specifies
                        # whether peptides must contain this modification: 0 = not forced to be present;
                        # 1 = modification is required.
                    */
                    // Comet static mods must have an AA and cannot be negative or terminal-specific, so in those cases, treat it as a variable mod
                    if (mod.IsVariable || mod.AAs == null || !position.IsNullOrEmpty() || mass < 0)
                    {
                        ++modCounter;
                        const int force = 0;
                        int distance = mod.Terminus == null ? -1 : 0;
                        int terminus = mod.Terminus switch
                        {
                            null => 0,
                            ModTerminus.N => 2,
                            ModTerminus.C => 3,
                            _ => throw new ArgumentException(nameof(mod.Terminus))
                        };
                        modParamLines.Add($@"variable_mod{modCounter:D2} = {mass.ToString(CultureInfo.InvariantCulture)} {residues} {modCounter} {maxVariableMods_} {distance} {terminus} {force}");
                        _variableMods.Add(new CruxModification(mod, mass, residues));
                    }
                    else
                    {
                        // accumulate masses from static mods on each AA
                        foreach (char aa in mod.AAs)
                        {
                            if (staticModsByAA.ContainsKey(aa))
                                staticModsByAA[aa] += mass;
                            else
                                staticModsByAA[aa] = mass;
                        }
                    }
                };

                if (mod.LabelAtoms == LabelAtoms.None)
                {
                    double mass = mod.MonoisotopicMass ?? SequenceMassCalc.FormulaMass(BioMassCalc.MONOISOTOPIC, mod.ParsedMolecule, SequenceMassCalc.MassPrecision).Value;

                    string residues = string.Empty;
                    if (position.IsNullOrEmpty() || mod.AAs == null)
                        residues = mod.AAs ?? (position.IsNullOrEmpty() ? @"*" : $@"{position}^");
                    else
                        foreach (char aa in mod.AAs)
                            residues += $@"{position}{aa}";

                    addMod(mass, residues);
                }
                else
                {
                    foreach(char aa in mod.AAs)
                    {
                        string residue = string.Empty;
                        if (position.IsNullOrEmpty())
                            residue = aa.ToString();
                        else
                            residue = $@"{position}{aa}";

                        double mass = SequenceMassCalc.FormulaMass(BioMassCalc.MONOISOTOPIC, SequenceMassCalc.GetHeavyFormula(aa, mod.LabelAtoms), SequenceMassCalc.MassPrecision).Value;
                        addMod(mass, residue);
                    }
                }
            }


            /*add_G_glycine = 0.000000
              add_A_alanine = 0.000000
              add_S_serine = 0.000000
            */
            if (!staticModsByAA.ContainsKey('C'))
                modParamLines.Add(@"add_C_cysteine = 0"); // disable default cysteine static mod

            foreach (var kvp in staticModsByAA)
                if (AminoAcidFormulas.FullNames.TryGetValue(kvp.Key, out var fullName))
                    modParamLines.Add($@"add_{kvp.Key}_{fullName.ToLowerInvariant().Replace(' ', '_')} = {kvp.Value.ToString(CultureInfo.InvariantCulture)}");

            modParamLines.Sort();
            _modParams = string.Join(Environment.NewLine, modParamLines);
        }

        public override void SetEnzyme(Enzyme enz, int mmc)
        {
            _enzyme = enz;
            _ntt = enz.IsSemiCleaving ? 1 : 2;
            _maxMissedCleavages = mmc;
        }

        public override void SetFragmentIonMassTolerance(MzTolerance mzTolerance)
        {
            _fragmentMzTolerance = mzTolerance;
        }

        public override void SetFragmentIons(string ions)
        {
            _fragmentIons = new SortedSet<string>(ions.Split(','));
        }

        public override void SetMs2Analyzer(string ms2Analyzer)
        {
            // not used by Comet
        }

        public override void SetPrecursorMassTolerance(MzTolerance mzTolerance)
        {
            _precursorMzTolerance = mzTolerance;
        }

        public override void SetCutoffScore(double cutoffScore)
        {
            AdditionalSettings[PERCOLATOR_TEST_QVALUE_CUTOFF] =
                new Setting(PERCOLATOR_TEST_QVALUE_CUTOFF, cutoffScore, 0, 1);
        }

        public override string GetSearchResultFilepath(MsDataFileUri searchFilepath)
        {
            const string extensionToReplace = @".percolator-pepXML";
            return Path.ChangeExtension(searchFilepath.GetFilePath(), extensionToReplace).Replace(extensionToReplace, @"-percolator.pepXML");
        }
        private string GetCometSearchResultFilepath(MsDataFileUri searchFilepath, string extension = ".pep.xml")
        {
            string result;
            if (SpectrumFileNames.Length > 1)
            {
                result = Path.Combine(Path.GetDirectoryName(searchFilepath.GetFilePath()) ?? "",
                    @"comet." + Path.ChangeExtension(searchFilepath.GetFileName(), extension));
            }
            else
            {
                result = Path.Combine(Path.GetDirectoryName(searchFilepath.GetFilePath()) ?? "",
                    Path.ChangeExtension(@"comet", extension));
            }

            return PathEx.GetNonUnicodePath(result); // Convert to 8.3 format if needed
        }

        private string[] SupportedExtensions = { @".mzml", @".mzxml", @".raw" };

        public override bool GetSearchFileNeedsConversion(MsDataFileUri searchFilepath, out AbstractDdaConverter.MsdataFileFormat requiredFormat)
        {
            requiredFormat = AbstractDdaConverter.MsdataFileFormat.mzML;
            if (!SupportedExtensions.Contains(e => e == searchFilepath.GetExtension().ToLowerInvariant()))
                return true;
            return false;
        }

        public bool IsCanceled => _cancelToken?.IsCancellationRequested ?? false;
        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            // trim INFO: prefix
            status = status.ChangeMessage(status.Message.Replace(@"INFO: ", @""));

            SearchProgressChanged?.Invoke(this, status);
            return IsCanceled ? UpdateProgressResponse.cancel : UpdateProgressResponse.normal;
        }

        public bool HasUI => false;

        public override void Dispose()
        {
            if (IsCanceled)
            {
                DeleteIntermediateFiles(); // In case cancel came at an awkward time
            }
        }
    }
}
 