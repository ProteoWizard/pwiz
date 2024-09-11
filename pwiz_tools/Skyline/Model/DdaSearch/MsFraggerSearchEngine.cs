/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DdaSearch
{
    public class MsFraggerSearchEngine : AbstractDdaSearchEngine, IProgressMonitor
    {
        // MSFragger settings
        private const string CHECK_SPECTRAL_FILES = "check_spectral_files";
        private const string CALIBRATE_MASS = "calibrate_mass";
        private List<string> MSFRAGGER_SETTINGS = new List<string> { CHECK_SPECTRAL_FILES, CALIBRATE_MASS };

        // Percolator settings
        private const string PERCOLATOR_TEST_QVALUE_CUTOFF = "test-fdr";
        private const string PERCOLATOR_TRAIN_QVALUE_CUTOFF = "train-fdr";
        private readonly string[] PERCOLATOR_SETTINGS = { PERCOLATOR_TEST_QVALUE_CUTOFF, PERCOLATOR_TRAIN_QVALUE_CUTOFF };

        private const string KEEP_INTERMEDIATE_FILES = "keep-intermediate-files";

        private bool DataIsDIA { get; }

        public MsFraggerSearchEngine(double percolatorQvalueCutoff, bool dataIsDIA)
        {
            DataIsDIA = dataIsDIA;

            AdditionalSettings = new Dictionary<string, Setting>
            {
                {CHECK_SPECTRAL_FILES, new Setting(CHECK_SPECTRAL_FILES, 1, 0, 1)},
                {CALIBRATE_MASS, new Setting(CALIBRATE_MASS, 0, 0, 2)},
                {PERCOLATOR_TEST_QVALUE_CUTOFF, new Setting(PERCOLATOR_TEST_QVALUE_CUTOFF, percolatorQvalueCutoff, 0, 1)},
                {PERCOLATOR_TRAIN_QVALUE_CUTOFF, new Setting(PERCOLATOR_TRAIN_QVALUE_CUTOFF, 0.01, 0, 1)},
                {KEEP_INTERMEDIATE_FILES, new Setting(KEEP_INTERMEDIATE_FILES, false)},
            };

            // ReSharper disable LocalizableElement
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting(@"data_type", DataIsDIA ? 1 : 0, 0, 3)); // Data type (0 for DDA, 1 for DIA, 2 for gas-phase fractionation DIA, 3 for DDA+).

            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("precursor_true_tolerance", 20.0)); //  True precursor mass tolerance (window is +/- this value).
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("precursor_true_units", 1, 0, 1)); //  True precursor mass tolerance units (0 for Da, 1 for ppm).
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("use_all_mods_in_first_search", 0, 0, 1)); //  Use all variable modifications in first search (0 for No, 1 for Yes).
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("deisotope", 1, 0, 2)); //  Perform deisotoping or not (0=no, 1=yes and assume singleton peaks single charged, 2=yes and assume singleton peaks single or double charged).
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("deneutralloss", 1, 0, 1)); //  Perform deneutrallossing or not (0=no, 1=yes).
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("isotope_error", DataIsDIA ? "0" : "0/1/2/3")); //  Also search for MS/MS events triggered on specified isotopic peaks.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("mass_offsets", 0.0)); //  Creates multiple precursor tolerance windows with specified mass offsets.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("mass_offsets_detailed", "")); //  Optional detailed mass offset list. Overrides mass_offsets if use_detailed_offsets = 1.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("use_detailed_offsets", 0, 0, 1)); //  Whether to use the regular (0) or detailed (1) mass offset list.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("precursor_mass_mode", DataIsDIA ? "isolated" : "selected", new []{"isolated", "selected", "corrected"})); //  One of isolated/selected/corrected.

            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("remove_precursor_peak", 1, 0, 1)); //   Remove precursor peaks from tandem mass spectra. 0 = not remove; 1 = remove the peak with precursor charge; 2 = remove the peaks with all charge states (only for DDA mode).
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("remove_precursor_range", "-1.500000,1.500000")); //  m/z range in removing precursor peaks. Only for DDA mode. Unit: Th.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("intensity_transform", DataIsDIA ? 1 : 0, 0, 1)); //  Transform peaks intensities with sqrt root. 0 = not transform; 1 = transform using sqrt root.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("activation_types", "all", new []{"all", "HCD", "CID", "ETD", "ECD"})); //  Filter to only search scans of provided activation type(s). Allowed: All, HCD, CID, ETD, ECD.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("analyzer_types", "all", new[]{"all", "ITMS", "FTMS"})); //  Filter to only include scans matching the provided analyzer type(s) in search. Only support the mzML and raw format. Allowed types: all, FTMS, ITMS.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("group_variable", 0, 0, 2)); //  Specify the variable used to decide the PSM group in the group FDR estimation. 0 = no group FDR; 1 = num_enzyme_termini; 2 = PE from protein header.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("require_precursor", DataIsDIA ? 0 : 1, 0, 1)); //  If required, PSMs with no precursor peaks will be discarded. For DIA data type only. 0 = no, 1 = yes.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("reuse_dia_fragment_peaks", DataIsDIA ? 1 : 0, 0, 1)); //  Allow the same peak matches to multiple peptides. For DIA data type only. 0 = no, 1 = yes.

            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("mass_diff_to_variable_mod", 0, 0, 2)); //  Put mass diff as a variable modification. 0 for no; 1 for yes and remove delta mass; 2 for yes and keep delta mass.

            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("localize_delta_mass", 0, 0, 1)); //  Include fragment ions mass-shifted by unknown modifications (recommended for open and mass offset searches) (0 for OFF, 1 for ON).
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("delta_mass_exclude_ranges", "(-1.5,3.5)")); //  Exclude mass range for shifted ions searching.
            //AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("fragment_ion_series", "")); //  Ion series used in search, specify any of a,b,c,x,y,z,Y,b-18,y-18 (comma separated).
            //AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("ion_series_definitions", "")); //  User defined ion series. Example: ""b* N -17.026548;b0 N -18.010565"".

            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("labile_search_mode", "off", new []{"off", "nglycan", "labile"})); //  type of search (nglycan, labile, or off). Off means non-labile/typical search.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("restrict_deltamass_to", "all")); //  Specify amino acids on which delta masses (mass offsets or search modifications) can occur. Allowed values are single letter codes (e.g. ACD) and '-', must be capitalized. Use 'all' to allow any amino acid.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("diagnostic_intensity_filter", 0.0, 0.0)); //  [nglycan/labile search_mode only]. Minimum relative intensity for SUM of all detected oxonium ions to achieve for spectrum to contain diagnostic fragment evidence. Calculated relative to spectrum base peak. 0 <= value.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("Y_type_masses", "")); //   [nglycan/labile search_mode only]. Specify fragments of labile mods that are commonly retained on intact peptides (e.g. Y ions for glycans). Only used if 'Y' is included in fragment_ion_series.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("diagnostic_fragments", "")); //  [nglycan/labile search_mode only]. Specify diagnostic fragments of labile mods that appear in the low m/z region. Only used if diagnostic_intensity_filter > 0.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("remainder_fragment_masses", "")); //  [labile search_mode only] List of possible remainder fragment ions to consider. Remainder masses are partial modification masses left on b/y ions after fragmentation.

            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("clip_nTerm_M", 1, 0, 1)); //  Specifies the trimming of a protein N-terminal methionine as a variable modification (0 or 1).

            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("allow_multiple_variable_mods_on_residue", 0, 0, 1));
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("max_variable_mods_combinations", 5000, 0, 65534)); //  Maximum number of modified forms allowed for each peptide (up to 65534).
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("output_report_topN", 1, 1)); //  Reports top N PSMs per input spectrum.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("output_max_expect", 50.0, 0)); //  Suppresses reporting of PSM if top hit has expectation value greater than this threshold.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("report_alternative_proteins", 1, 0, 1)); //  Report alternative proteins for peptides that are found in multiple proteins (0 for no, 1 for yes).

            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("precursor_charge", "1 4")); //  Assumed range of potential precursor charge states. Only relevant when override_charge is set to 1.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("override_charge", 0, 0, 1)); //  Ignores precursor charge and uses charge state specified in precursor_charge range (0 or 1).

            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("digest_min_length", 7, 1)); //  Minimum length of peptides to be generated during in-silico digestion.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("digest_max_length", 50, 1)); //  Maximum length of peptides to be generated during in-silico digestion.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("digest_mass_range", "500.0 5000.0")); //  Mass range of peptides to be generated during in-silico digestion in Daltons.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("max_fragment_charge", 2, 1, 4)); //  Maximum charge state for theoretical fragments to match (1-4).

            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("track_zero_topN", 0, 0)); //  Track top N unmodified peptide results separately from main results internally for boosting features.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("zero_bin_accept_expect", 0.0, 0.0)); //  Ranks a zero-bin hit above all non-zero-bin hit if it has expectation less than this value.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("zero_bin_mult_expect", 1.0, 0.0)); //  Multiplies expect value of PSMs in the zero-bin during  results ordering (set to less than 1 for boosting).

            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("minimum_peaks", 15, 1)); //  Minimum number of peaks in experimental spectrum for matching.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("use_topN_peaks", DataIsDIA ? 300 : 150, 1)); //  Pre-process experimental spectrum to only use top N peaks.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("min_fragments_modelling", 2, 1)); //  Minimum number of matched peaks in PSM for inclusion in statistical modeling.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("min_matched_fragments", 4, 1)); //  Minimum number of matched peaks for PSM to be reported.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("min_sequence_matches", 2, 1)); //  [nglycan/labile search_mode only] Minimum number of sequence-specific (not Y) ions to record a match.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("minimum_ratio", DataIsDIA ? 0 : 0.01, 0.0)); //  Filters out all peaks in experimental spectrum less intense than this multiple of the base peak intensity.
            AddAdditionalSetting(MSFRAGGER_SETTINGS, new Setting("clear_mz_range", "0.0 0.0")); //  Removes peaks in this m/z range prior to matching.
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
        };

        public static string MSFRAGGER_VERSION = @"4.1";
        public static string MSFRAGGER_FILENAME = @"MSFragger-4.1";
        public static string MsFraggerDirectory => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), MSFRAGGER_FILENAME);
        public static string MsFraggerBinary => Path.Combine(MsFraggerDirectory, MSFRAGGER_FILENAME, MSFRAGGER_FILENAME + @".jar");
        public static FileDownloadInfo MsFraggerDownloadInfo => new FileDownloadInfo { Filename = MSFRAGGER_FILENAME, InstallPath = MsFraggerDirectory, OverwriteExisting = true, Unzip = true };

        static string CRUX_FILENAME = @"crux-4.2";
        static Uri CRUX_URL = new Uri($@"https://noble.gs.washington.edu/crux-downloads/{CRUX_FILENAME}/{CRUX_FILENAME}.Windows.AMD64.zip");
        public static string CruxDirectory => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), CRUX_FILENAME);
        public static string CruxBinary => Path.Combine(CruxDirectory, $@"{CRUX_FILENAME}.Windows.AMD64", @"bin", @"crux");

        public static FileDownloadInfo[] FilesToDownload => JavaDownloadInfo.FilesToDownload.Concat(new[] {
            MsFraggerDownloadInfo,
            new FileDownloadInfo { Filename = CRUX_FILENAME, DownloadUrl = CRUX_URL, InstallPath = CruxDirectory, OverwriteExisting = true, Unzip = true }
        }).ToArray();

        private MzTolerance _precursorMzTolerance;
        private MzTolerance _fragmentMzTolerance;
        private string _fragmentIons;
        private Enzyme _enzyme;
        private int _ntt, _maxMissedCleavages;
        private int _maxVariableMods = 2;
        private List<CruxModification> _variableMods;
        private string _modParams;
        private int _maxCharge = 7;
        private string _fastaFilepath;
        private string _decoyPrefix;
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
        public override string EngineName => @"MSFragger";
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

            void RunInner()
            {
                try
                {
                    _intermediateFiles = new List<string>();
                    _fastaFilepath = FastaFileNames[0];
                    EnsureFastaHasDecoys();

                    var paramsFileText = new StringBuilder(defaultClosedConfig);
                    
                    SetMsFraggerParam(paramsFileText, @"num_threads", 0);
                    SetMsFraggerParam(paramsFileText, @"database_name", _fastaFilepath);
                    SetMsFraggerParam(paramsFileText, @"decoy_prefix", _decoyPrefix);
                    SetMsFraggerParam(paramsFileText, @"precursor_mass_lower", (-_precursorMzTolerance.Value).ToString(CultureInfo.InvariantCulture));
                    SetMsFraggerParam(paramsFileText, @"precursor_mass_upper", _precursorMzTolerance.Value.ToString(CultureInfo.InvariantCulture));
                    SetMsFraggerParam(paramsFileText, @"precursor_mass_units", (int)_precursorMzTolerance.Unit);
                    SetMsFraggerParam(paramsFileText, @"fragment_mass_tolerance", _fragmentMzTolerance.Value.ToString(CultureInfo.InvariantCulture));
                    SetMsFraggerParam(paramsFileText, @"fragment_mass_units", (int)_fragmentMzTolerance.Unit);
                    SetMsFraggerParam(paramsFileText, @"fragment_ion_series", _fragmentIons);
                    SetMsFraggerParam(paramsFileText, @"num_enzyme_termini", _ntt);
                    SetMsFraggerParam(paramsFileText, @"allowed_missed_cleavage_1", _maxMissedCleavages);
                    SetMsFraggerParam(paramsFileText, @"search_enzyme_name_1", _enzyme.Name ?? @"unnamed");
                    SetMsFraggerParam(paramsFileText, @"search_enzyme_cut_1", _enzyme.CleavageC ?? _enzyme.CleavageN);
                    SetMsFraggerParam(paramsFileText, @"search_enzyme_nocut_1", _enzyme.RestrictC ?? _enzyme.RestrictN);
                    SetMsFraggerParam(paramsFileText, @"search_enzyme_sense_1", _enzyme.IsCTerm ? @"C" : @"N");
                    SetMsFraggerParam(paramsFileText, @"max_variable_mods_per_peptide", _maxVariableMods);
                    foreach (var settingName in MSFRAGGER_SETTINGS)
                        SetMsFraggerParam(paramsFileText, settingName, AdditionalSettings[settingName].ValueToString(CultureInfo.InvariantCulture));
                    paramsFileText.Append(_modParams);

                    string defaultOutputDirectory = Path.GetDirectoryName(SpectrumFileNames[0].GetFilePath()) ?? Environment.CurrentDirectory;

                    string paramsFile = KeepIntermediateFiles ? Path.Combine(defaultOutputDirectory, @"msfragger.params") : Path.GetTempFileName();
                    _intermediateFiles.Add(paramsFile);
                    File.WriteAllText(paramsFile, paramsFileText.ToString());

                    long javaMaxHeapMB = Math.Min(24 * 1024L * 1024 * 1024, MemoryInfo.TotalBytes / 2) / 1024 / 1024;

                    // Run MSFragger
                    var pr = new ProcessRunner();
                    var psi = new ProcessStartInfo(JavaDownloadInfo.JavaBinary,
                            $@"-Xmx{javaMaxHeapMB}M -jar """ + MsFraggerBinary + $@""" ""{paramsFile}""")// ""{spectrumFilename}""")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    foreach (var filename in SpectrumFileNames)
                        psi.Arguments += $@" ""{filename}""";
                    // ReSharper disable once LocalizableElement
                    _progressStatus = _progressStatus.ChangeMessage($"Running MSFragger:\r\n\"{psi.FileName}\" {psi.Arguments}");
                    if (UpdateProgressResponse.cancel == UpdateProgress(_progressStatus))
                        return;
                    pr.Run(psi, string.Empty, this, ref _progressStatus, ProcessPriorityClass.BelowNormal, true);

                    string cruxParamsFile = KeepIntermediateFiles ? Path.Combine(defaultOutputDirectory, @"crux.params") : Path.GetTempFileName();
                    _intermediateFiles.Add(cruxParamsFile);
                    var cruxParamsFileText = GetCruxParamsText();
                    File.WriteAllText(cruxParamsFile, cruxParamsFileText);

                    // Run Crux Percolator
                    string cruxOutputDir = Path.Combine(defaultOutputDirectory, "crux-output");
                    _intermediateFiles.Add(cruxOutputDir);
                    psi.FileName = CruxBinary;
                    psi.Arguments = $@"percolator --only-psms T --output-dir ""{cruxOutputDir}"" --overwrite T --decoy-prefix ""{_decoyPrefix}"" --parameter-file ""{cruxParamsFile}""";

                    foreach (var settingName in PERCOLATOR_SETTINGS)
                        psi.Arguments += $@" --{AdditionalSettings[settingName].ToString(false, CultureInfo.InvariantCulture)}";

                    foreach (var spectrumFilename in SpectrumFileNames)
                    {
                        string msfraggerPepXmlFilepath = Path.Combine(Path.GetDirectoryName(spectrumFilename.GetFilePath()) ?? "",
                            spectrumFilename.GetFileNameWithoutExtension() + (DataIsDIA ? @"_rank1.pepXML" : @".pepXML"));
                        string cruxInputFilepath = Path.ChangeExtension(spectrumFilename.GetFilePath(), ".pin");
                        string cruxFixedInputFilepath = Path.ChangeExtension(spectrumFilename.GetFilePath(), "fixed.pin");
                        _intermediateFiles.Add(cruxInputFilepath);
                        _intermediateFiles.Add(cruxFixedInputFilepath);
                        FixMSFraggerPin(cruxInputFilepath, cruxFixedInputFilepath, msfraggerPepXmlFilepath, this);
                        psi.Arguments += $@" ""{cruxFixedInputFilepath}""";

                        if (spectrumFilename.GetExtension().ToLowerInvariant() == DataSourceUtil.EXT_THERMO_RAW)
                        {
                            string unwantedMzMl = Path.Combine(Path.GetDirectoryName(spectrumFilename.GetFilePath()) ?? "",
                                spectrumFilename.GetFileNameWithoutExtension() + @"_uncalibrated.mzML");
                            File.Delete(unwantedMzMl);
                        }
                    }

                    // ReSharper disable once LocalizableElement
                    _progressStatus = _progressStatus.ChangeMessage($"Running Crux Percolator:\r\n\"{psi.FileName}\" {psi.Arguments}");
                    if (UpdateProgressResponse.cancel == UpdateProgress(_progressStatus))
                        return;
                    pr.Run(psi, string.Empty, this, ref _progressStatus, ProcessPriorityClass.BelowNormal, true);

                    var qvalueByPsmId = new Dictionary<string, double>();
                    // Read PSMs from text files and update original pepXMLs with Percolator scores
                    string percolatorTargetPsmsTsv = Path.Combine(cruxOutputDir, @"percolator.target.psms.txt");
                    string percolatorDecoyPsmsTsv = Path.Combine(cruxOutputDir, @"percolator.decoy.psms.txt");
                    GetPercolatorScores(percolatorTargetPsmsTsv, qvalueByPsmId);
                    GetPercolatorScores(percolatorDecoyPsmsTsv, qvalueByPsmId);

                    foreach (var spectrumFilename in SpectrumFileNames)
                    {
                        string msfraggerPepXmlFilepath = Path.Combine(Path.GetDirectoryName(spectrumFilename.GetFilePath()) ?? "",
                            spectrumFilename.GetFileNameWithoutExtension() + (DataIsDIA ? @"_rank1.pepXML" : @".pepXML"));
                        string finalOutputFilepath = GetSearchResultFilepath(spectrumFilename);
                        _intermediateFiles.Add(msfraggerPepXmlFilepath);
                        FixPercolatorPepXml(msfraggerPepXmlFilepath, finalOutputFilepath, spectrumFilename, qvalueByPsmId, this);
                    }

                    DeleteIntermediateFiles();

                    _progressStatus = _progressStatus.NextSegment();
                }
                catch (Exception ex)
                {
                    _progressStatus = _progressStatus.ChangeErrorException(ex).ChangeMessage(string.Format(DdaSearchResources.DdaSearch_Search_failed__0, ex.Message));
                    _success = false;
                }
            }

            RunInner();

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

        private void SetMsFraggerParam(StringBuilder config, string name, object value)
        {
            string nameWithEquals = '\n' + name + @" = ";
            int oldLength = config.Length;
            config.Replace(nameWithEquals, nameWithEquals + value);
            if (Program.FunctionalTest && !MSFRAGGER_SETTINGS.Contains(name))
                Assume.AreNotEqual(config.Length, oldLength);
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
            var percolatorTargetPsmsReader = new DsvFileReader(percolatorTsvFilepath, TextUtil.SEPARATOR_TSV);
            int psmIdColumn = percolatorTargetPsmsReader.GetFieldIndex(@"PSMId");
            int qvalueColumn = percolatorTargetPsmsReader.GetFieldIndex(@"q-value");
            while (percolatorTargetPsmsReader.ReadLine() != null)
            {
                var psmId = percolatorTargetPsmsReader.GetFieldByIndex(psmIdColumn);
                psmId = psmId.Substring(0, psmId.Length - 2);
                var qvalue = Convert.ToDouble(percolatorTargetPsmsReader.GetFieldByIndex(qvalueColumn), CultureInfo.InvariantCulture);
                qvalueByPsmId[psmId] = qvalue;
            }
        }

        // Add Percolator score to MSFragger pepXML
        private void FixPercolatorPepXml(string cruxOutputFilepath, string finalOutputFilepath, MsDataFileUri spectrumFilename, Dictionary<string, double> qvalueByPsmId, IProgressMonitor monitor)
        {
            bool isBrukerSource = DataSourceUtil.GetSourceType(spectrumFilename.GetFilePath()) == DataSourceUtil.TYPE_BRUKER;
            var lastPsmIdRegex = new Regex(@".* spectrum=""([^""]+?)"" .*",RegexOptions.Compiled);

            using (var pepXmlFile = new StreamReader(cruxOutputFilepath))
            using (var fixedPepXmlFile = new StreamWriter(finalOutputFilepath))
            {
                string line;
                string lastPsmId = "";
                while ((line = pepXmlFile.ReadLine()) != null)
                {
                    if (line.Contains(@"<spectrum_query"))
                        lastPsmId = lastPsmIdRegex.Replace(line, "$1");
                    else if (line.Contains(@"<search_score name=""hyperscore"""))
                    {
                        if (qvalueByPsmId.ContainsKey(lastPsmId))
                            fixedPepXmlFile.WriteLine(@"<search_score name=""percolator_qvalue"" value=""{0}"" />", qvalueByPsmId[lastPsmId].ToString(CultureInfo.InvariantCulture));
                        // MCC: This happens when percolator's text tables drops a PSM that is in pepXML; I'm not sure why it happens though.
                        //else
                        //    Console.WriteLine($"{lastPsmId} not found in percolator scores.");
                    }
                    else if (line.Contains(@"</search_summary>"))
                    {
                        fixedPepXmlFile.WriteLine(@"<parameter name=""post-processor"" value=""percolator"" />");
                    }
                    fixedPepXmlFile.WriteLine(line);

                    if (monitor.IsCanceled)
                        return;
                }
            }
        }

        // Fix (TODO: remove these hacks when it's fixed in Crux and/or MSFragger):
        // - bug in MSFragger PIN output (or bug in Crux Percolator PIN input): it doesn't like the underscore after charge_
        // - bug in Crux pepXML writer where it doesn't ignore the N-terminal mod annotation (n[123]); the writer doesn't handle terminal mods anyway, so just remove the n and move the mod over to be an AA mod
        // - change in MSFragger 3.4 PIN output: it no longer has charge features which Crux Percolator requires for putting charge in pepXML
        private void FixMSFraggerPin(string cruxInputFilepath, string cruxFixedInputFilepath, string msfraggerPepxmlFilepath, IProgressMonitor monitor)
        {
            var nativeIdRegex = new Regex(".* spectrumNativeID=\"controllerType=0 controllerNumber=1 scan=(\\d+)\"", RegexOptions.Compiled);
            var startScanRegex = new Regex(".* start_scan=\"(\\d+)\" .*", RegexOptions.Compiled);
            var scanNumbers = new List<int>();
            using (var pepXmlFile = new StreamReader(msfraggerPepxmlFilepath))
            {
                string line;
                while ((line = pepXmlFile.ReadLine()) != null)
                {
                    if (line.Contains(@"<spectrum_query"))
                    {
                        if (!int.TryParse(nativeIdRegex.Replace(line, "$1"), out int scanNumber))
                            scanNumber = int.Parse(startScanRegex.Replace(line, "$1"));

                        scanNumbers.Add(scanNumber);
                    }

                    if (monitor.IsCanceled)
                        return;
                }
            }

            int scanIndex = 0;
            bool headerFixed = false;
            bool addChargeFeatures = false;
            using (var pinFile = new StreamReader(cruxInputFilepath))
            using (var pinFixedFile = new StreamWriter(cruxFixedInputFilepath))
            {
                string line;
                while ((line = pinFile.ReadLine()) != null)
                {
                    if (!headerFixed)
                    {
                        if (line.Contains(@"charge_"))
                        {
                            line = line.Replace(@"charge_", @"Charge");
                            line = line.Replace(@"_or_more", ""); // MSFragger 3.8
                        }
                        else
                        {
                            addChargeFeatures = true;
                            var chargeColumnNames = new string[_maxCharge];
                            for (int i = 1; i <= _maxCharge; ++i)
                                chargeColumnNames[i - 1] = $@"Charge{i}";
                            // ReSharper disable LocalizableElement
                            string chargeColumns = string.Join("\t", chargeColumnNames);
                            line = line.Replace("nmc\tPeptide", "nmc\t" + chargeColumns + "\tPeptide");
                            // ReSharper restore LocalizableElement
                        }

                        headerFixed = true;
                    }
                    else
                    {
                        if (scanIndex >= scanNumbers.Count)
                            throw new InvalidDataException(@"while fixing scan numbers in PIN file, ran out of correct scan numbers from pepXML");

                        int fixedScanNumber = scanNumbers[scanIndex++];
                        line = Regex.Replace(line, "^([^.]*)\\.\\d+\\.\\d+\\.(\\d+_\\d+\t\\d)\t\\d+", $"$1.{fixedScanNumber}.{fixedScanNumber}.$2\t{fixedScanNumber}");

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
                            if (!double.TryParse(m.Groups[1].Value, out double modMass1))
                                throw new InvalidDataException(@"could not parse mod mass from " + m.Groups[1].Value);
                            if (!double.TryParse(m.Groups[2].Value, out double modMass2))
                                throw new InvalidDataException(@"could not parse mod mass from " + m.Groups[2].Value);
                            line = Regex.Replace(line, "\\[([^]]+\\]\\[[^]]+)\\]", $"[{modMass1 + modMass2:F4}]");
                        }

                        if (addChargeFeatures)
                        {
                            if (!int.TryParse(Regex.Replace(line, @"^.*\.\d+\.\d+\.(\d+)_.*", "$1"), out int chargeState))
                                throw new InvalidDataException(@"cannot determine charge state from line in PIN file: " + line);
                            var chargeColumnChars = new char[_maxCharge];
                            for (int i = 1; i <= _maxCharge; ++i)
                                chargeColumnChars[i - 1] = (i == chargeState ? '1' : '0');
                            string chargeColumns = string.Join('\t'.ToString(), chargeColumnChars);
                            line = Regex.Replace(line, "(\\d\t)([A-Z-]\\.)", $"$1\t{chargeColumns}\t$2");
                        }
                    }

                    pinFixedFile.WriteLine(line);
                }
            }
        }

        private void EnsureFastaHasDecoys()
        {
            _fastaFilepath ??= string.Empty;    // For ReSharper
            Assume.IsFalse(string.IsNullOrEmpty(_fastaFilepath));

            _progressStatus = _progressStatus.ChangeMessage(string.Format(DdaSearchResources.EnsureFastaHasDecoys_Detecting_decoy_prefix_in__0__, Path.GetFileName(_fastaFilepath)));
            UpdateProgress(_progressStatus);

            // ReSharper disable LocalizableElement 
            IEnumerable<string> commonAffixes = new [] { "decoy", "dec", "reverse", "rev", "__id_decoy", "xxx", "shuffled", "shuffle", "pseudo", "random" };
            commonAffixes = commonAffixes.Concat(commonAffixes.Select(o => o.ToUpperInvariant())).ToArray();
            // ReSharper restore LocalizableElement

            var prefixCounts = commonAffixes.ToDictionary(o => o, k => 0);
            var suffixCounts = commonAffixes.ToDictionary(o => o, k => 0);
            int entryCount = 0;

            using (var fastaReader = new StreamReader(_fastaFilepath))
            {
                var fastaEntries = FastaData.ParseFastaFile(fastaReader, true);
                foreach (var entry in fastaEntries)
                {
                    ++entryCount;
                    foreach (var affix in commonAffixes)
                    {
                        if (entry.Name.StartsWith(affix))
                            ++prefixCounts[affix];
                        else if (entry.Name.EndsWith(affix))
                            ++suffixCounts[affix];
                    }
                }
            }

            _decoyPrefix = string.Empty;

            var decoyPrefixDetectionMessages = new StringBuilder();
            if (prefixCounts.Any(kvp => kvp.Value > 0))
            {
                var prefixPercentages = prefixCounts.Where(kvp => kvp.Value > 0)
                    .ToDictionary(kvp => kvp.Key, kvp => (double)kvp.Value / entryCount)
                    .OrderByDescending(kvp => kvp.Value)
                    .ToList();
                decoyPrefixDetectionMessages.AppendLine(DdaSearchResources.EnsureFastaHasDecoys_Some_common_prefixes_were_found_);
                foreach (var kvp in prefixPercentages)
                    decoyPrefixDetectionMessages.AppendFormat($@"{kvp.Key}: {kvp.Value:P1}{Environment.NewLine}");
                decoyPrefixDetectionMessages.AppendLine();

                if (prefixPercentages.First().Value > 0.4)
                {
                    _decoyPrefix = prefixPercentages.Select(kvp => kvp.Key).First();
                    decoyPrefixDetectionMessages.AppendLine(string.Format(DdaSearchResources.EnsureFastaHasDecoys_Using__0__as_the_most_likely_decoy_prefix_, _decoyPrefix));
                }
                else
                    decoyPrefixDetectionMessages.AppendLine(DdaSearchResources.EnsureFastaHasDecoys_No_prefixes_were_frequent_enough_to_be_a_decoy_prefix__present_in_at_least_40__of_entries__);
            }
            else
            {
                decoyPrefixDetectionMessages.AppendLine(DdaSearchResources.EnsureFastaHasDecoys_No_common_prefixes_were_found_);
                decoyPrefixDetectionMessages.AppendLine();

                if (suffixCounts.Any(kvp => kvp.Value > 0))
                {
                    var suffixPercentages = suffixCounts.Where(kvp => kvp.Value > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => (double)kvp.Value / entryCount)
                        .OrderByDescending(kvp => kvp.Value)
                        .ToList();
                    decoyPrefixDetectionMessages.AppendLine(DdaSearchResources.EnsureFastaHasDecoys_Some_common_suffixes_were_found_but_these_are_not_supported_);
                    foreach (var kvp in suffixPercentages)
                        decoyPrefixDetectionMessages.AppendFormat($@"{kvp.Key}: {kvp.Value:P1}{Environment.NewLine}");
                    decoyPrefixDetectionMessages.AppendLine();

                    if (suffixPercentages.First().Value > 0.4 && suffixPercentages.First().Value < 0.6)
                    {
                        decoyPrefixDetectionMessages.AppendLine(string.Format(DdaSearchResources.EnsureFastaHasDecoys_The_suffix__0__was_likely_intended_as_a_decoy_suffix__but_Skyline_s_DDA_search_tools_do_not_support_decoy_suffixes, suffixPercentages.First().Key));
                    }
                }
                else
                    decoyPrefixDetectionMessages.AppendLine(DdaSearchResources.EnsureFastaHasDecoys_No_common_suffixes_were_found_);
            }
            _progressStatus = _progressStatus.ChangeMessage(decoyPrefixDetectionMessages.ToString());
            UpdateProgress(_progressStatus);

            if (_decoyPrefix.IsNullOrEmpty())
            {
                string decoyFastaFilepath = Path.Combine(Path.GetDirectoryName(_fastaFilepath) ?? "", @"decoy_" + Path.GetFileName(_fastaFilepath));
                if (File.Exists(decoyFastaFilepath))
                {
                    _progressStatus = _progressStatus.ChangeMessage(string.Format(DdaSearchResources.EnsureFastaHasDecoys_No_decoy_prefix_detected__but_an_existing_decoy_database_seems_to_exist_at__0__, Path.GetFileName(decoyFastaFilepath)));
                    UpdateProgress(_progressStatus);
                    _fastaFilepath = decoyFastaFilepath;
                    EnsureFastaHasDecoys();
                    return;
                }

                _decoyPrefix = @"DECOY_";
                _progressStatus = _progressStatus.ChangeMessage(string.Format(DdaSearchResources.EnsureFastaHasDecoys_No_decoy_prefix_detected__A_new_FASTA_will_be_generated_using_reverse_sequences_as_decoys__with_prefix___0____, _decoyPrefix));
                UpdateProgress(_progressStatus);

                File.Copy(_fastaFilepath, decoyFastaFilepath);

                using (var fastaReader = new StreamReader(_fastaFilepath, Encoding.ASCII))
                using (var fastaWriter = new StreamWriter(decoyFastaFilepath, true, Encoding.ASCII))
                {
                    var fastaEntries = FastaData.ParseFastaFile(fastaReader);
                    foreach (var entry in fastaEntries)
                    {
                        fastaWriter.WriteLine($@">{_decoyPrefix}{entry.Name}");
                        foreach(var aa in entry.Sequence.Reverse())
                            fastaWriter.Write(aa);
                        fastaWriter.WriteLine();
                    }
                }
                _progressStatus = _progressStatus.ChangeMessage(string.Format(DdaSearchResources.EnsureFastaHasDecoys_Using_decoy_database_at__0___1_, Path.GetFileName(decoyFastaFilepath), Environment.NewLine));
                UpdateProgress(_progressStatus);
                _fastaFilepath = decoyFastaFilepath;
            }
        }

        private string defaultClosedConfig => @"
database_name = 			# Path to the protein database file in FASTA format.
num_threads = 			# Number of CPU threads to use.

precursor_mass_lower = 			# Lower bound of the precursor mass window.
precursor_mass_upper = 			# Upper bound of the precursor mass window.
precursor_mass_units = 			# Precursor mass tolerance units (0 for Da, 1 for ppm).
data_type = 			# Data type (0 for DDA, 1 for DIA, 2 for gas-phase fractionation DIA, 3 for DDA+).
precursor_true_tolerance = 			# True precursor mass tolerance (window is +/- this value).
precursor_true_units = 		# True precursor mass tolerance units (0 for Da, 1 for ppm).
fragment_mass_tolerance = 			# Fragment mass tolerance (window is +/- this value).
fragment_mass_units = 			# Fragment mass tolerance units (0 for Da, 1 for ppm).
calibrate_mass = 			# Perform mass calibration (0 for OFF, 1 for ON, 2 for ON and find optimal parameters, 4 for ON and find the optimal fragment mass tolerance).
use_all_mods_in_first_search = 			# Use all variable modifications in first search (0 for No, 1 for Yes).
decoy_prefix = 			# Prefix of the decoy protein entries. Used for parameter optimization only.

deisotope = 			# Perform deisotoping or not (0=no, 1=yes and assume singleton peaks single charged, 2=yes and assume singleton peaks single or double charged).
deneutralloss = 			# Perform deneutrallossing or not (0=no, 1=yes).
isotope_error = 			# Also search for MS/MS events triggered on specified isotopic peaks.
mass_offsets = 			# Creates multiple precursor tolerance windows with specified mass offsets.
mass_offsets_detailed = 			# Optional detailed mass offset list. Overrides mass_offsets if use_detailed_offsets = 1.
use_detailed_offsets = 			# Whether to use the regular (0) or detailed (1) mass offset list.
precursor_mass_mode = 			# One of isolated/selected/corrected.

remove_precursor_peak = 			#  Remove precursor peaks from tandem mass spectra. 0 = not remove; 1 = remove the peak with precursor charge; 2 = remove the peaks with all charge states (only for DDA mode).
remove_precursor_range = 			# m/z range in removing precursor peaks. Only for DDA mode. Unit: Th.
intensity_transform = 			# Transform peaks intensities with sqrt root. 0 = not transform; 1 = transform using sqrt root.
activation_types = 			# Filter to only search scans of provided activation type(s). Allowed: All, HCD, CID, ETD, ECD.
analyzer_types = 			# Filter to only include scans matching the provided analyzer type(s) in search. Only support the mzML and raw format. Allowed types: all, FTMS, ITMS.
group_variable = 			# Specify the variable used to decide the PSM group in the group FDR estimation. 0 = no group FDR; 1 = num_enzyme_termini; 2 = PE from protein header.
require_precursor = 			# If required, PSMs with no precursor peaks will be discarded. For DIA data type only. 0 = no, 1 = yes.
reuse_dia_fragment_peaks = 			# Allow the same peak matches to multiple peptides. For DIA data type only. 0 = no, 1 = yes.

write_calibrated_mzml = 0			# Write calibrated MS2 scan to a mzML file (0 for No, 1 for Yes).
write_uncalibrated_mgf = 0			# Write uncalibrated MS2 scan to a MGF file (0 for No, 1 for Yes). Only for .raw and .d formats.
write_mzbin_all = 0
mass_diff_to_variable_mod = 			# Put mass diff as a variable modification. 0 for no; 1 for yes and remove delta mass; 2 for yes and keep delta mass.

localize_delta_mass = 			# Include fragment ions mass-shifted by unknown modifications (recommended for open and mass offset searches) (0 for OFF, 1 for ON).
delta_mass_exclude_ranges = 			# Exclude mass range for shifted ions searching.
fragment_ion_series = 			# Ion series used in search, specify any of a,b,c,x,y,z,Y,b-18,y-18 (comma separated).
ion_series_definitions = 			# User defined ion series. Example: ""b* N -17.026548;b0 N -18.010565"".

labile_search_mode = 			# type of search (nglycan, labile, or off). Off means non-labile/typical search.
restrict_deltamass_to = 			# Specify amino acids on which delta masses (mass offsets or search modifications) can occur. Allowed values are single letter codes (e.g. ACD) and '-', must be capitalized. Use 'all' to allow any amino acid.
diagnostic_intensity_filter = 			# [nglycan/labile search_mode only]. Minimum relative intensity for SUM of all detected oxonium ions to achieve for spectrum to contain diagnostic fragment evidence. Calculated relative to spectrum base peak. 0 <= value.
Y_type_masses = 			#  [nglycan/labile search_mode only]. Specify fragments of labile mods that are commonly retained on intact peptides (e.g. Y ions for glycans). Only used if 'Y' is included in fragment_ion_series.
diagnostic_fragments = 			# [nglycan/labile search_mode only]. Specify diagnostic fragments of labile mods that appear in the low m/z region. Only used if diagnostic_intensity_filter > 0.
remainder_fragment_masses = 			# [labile search_mode only] List of possible remainder fragment ions to consider. Remainder masses are partial modification masses left on b/y ions after fragmentation.

search_enzyme_name_1 = 			# Name of the first enzyme.
search_enzyme_cut_1 = 			# First enzyme's cutting amino acid.
search_enzyme_nocut_1 = 			# First enzyme's protecting amino acid.
search_enzyme_sense_1 = 			# First enzyme's cutting terminal.
allowed_missed_cleavage_1 = 			# First enzyme's allowed number of missed cleavages per peptide. Maximum value is 5.

search_enzyme_name_2 = null			# Name of the second enzyme.
search_enzyme_cut_2 = 			# Second enzyme's cutting amino acid.
search_enzyme_nocut_2 = 			# Second enzyme's protecting amino acid.
search_enzyme_sense_2 = C			# Second enzyme's cutting terminal.
allowed_missed_cleavage_2 = 2			# Second enzyme's allowed number of missed cleavages per peptide. Maximum value is 5.

num_enzyme_termini = 			# 0 for non-enzymatic, 1 for semi-enzymatic, and 2 for fully-enzymatic.

clip_nTerm_M = 			# Specifies the trimming of a protein N-terminal methionine as a variable modification (0 or 1).

# maximum of 16 mods - amino acid codes, * for any amino acid, [ and ] specifies protein termini, n and c specifies peptide termini
# variable_mod_01 = 15.9949 M 3
# variable_mod_02 = 42.0106 [^ 1
# variable_mod_03 = 79.96633 STY 3
# variable_mod_04 = -17.0265 nQnC 1
# variable_mod_05 = -18.0106 nE 1
# variable_mod_06 = 4.025107 K 2
# variable_mod_07 = 6.020129 KR 2
# variable_mod_08 = 8.014199 cK 2
# variable_mod_09 = 10.008269 cR 2
# variable_mod_10 = 0.0 site_10 1
# variable_mod_11 = 0.0 site_11 1
# variable_mod_12 = 0.0 site_12 1
# variable_mod_13 = 0.0 site_13 1
# variable_mod_14 = 0.0 site_14 1
# variable_mod_15 = 0.0 site_15 1
# variable_mod_16 = 0.0 site_16 1

allow_multiple_variable_mods_on_residue = 
max_variable_mods_per_peptide = 			# Maximum total number of variable modifications per peptide.
max_variable_mods_combinations = 			# Maximum number of modified forms allowed for each peptide (up to 65534).

output_format = pepXML_pin			# File format of output files (tsv, pin, pepxml, tsv_pin, tsv_pepxml, pepxml_pin, or tsv_pepxml_pin).
output_report_topN = 			# Reports top N PSMs per input spectrum.
output_max_expect = 			# Suppresses reporting of PSM if top hit has expectation value greater than this threshold.
report_alternative_proteins = 			# Report alternative proteins for peptides that are found in multiple proteins (0 for no, 1 for yes).

precursor_charge = 			# Assumed range of potential precursor charge states. Only relevant when override_charge is set to 1.
override_charge = 			# Ignores precursor charge and uses charge state specified in precursor_charge range (0 or 1).

digest_min_length = 			# Minimum length of peptides to be generated during in-silico digestion.
digest_max_length = 			# Maximum length of peptides to be generated during in-silico digestion.
digest_mass_range = 			# Mass range of peptides to be generated during in-silico digestion in Daltons.
max_fragment_charge = 			# Maximum charge state for theoretical fragments to match (1-4).

track_zero_topN = 			# Track top N unmodified peptide results separately from main results internally for boosting features.
zero_bin_accept_expect = 			# Ranks a zero-bin hit above all non-zero-bin hit if it has expectation less than this value.
zero_bin_mult_expect = 			# Multiplies expect value of PSMs in the zero-bin during  results ordering (set to less than 1 for boosting).

check_spectral_files = 			# Checking spectral files before searching.
minimum_peaks = 			# Minimum number of peaks in experimental spectrum for matching.
use_topN_peaks = 			# Pre-process experimental spectrum to only use top N peaks.
min_fragments_modelling = 			# Minimum number of matched peaks in PSM for inclusion in statistical modeling.
min_matched_fragments = 			# Minimum number of matched peaks for PSM to be reported.
min_sequence_matches = 			# [nglycan/labile search_mode only] Minimum number of sequence-specific (not Y) ions to record a match.
minimum_ratio = 			# Filters out all peaks in experimental spectrum less intense than this multiple of the base peak intensity.
clear_mz_range = 			# Removes peaks in this m/z range prior to matching.

#add_Cterm_peptide = 0.0
#add_Nterm_peptide = 0.0
#add_Cterm_protein = 0.0
#add_Nterm_protein = 0.0

#add_G_glycine = 0.0
#add_A_alanine = 0.0
#add_S_serine = 0.0
#add_P_proline = 0.0
#add_V_valine = 0.0
#add_T_threonine = 0.0
#add_C_cysteine = 57.02146
#add_L_leucine = 0.0
#add_I_isoleucine = 0.0
#add_N_asparagine = 0.0
#add_D_aspartic_acid = 0.0
#add_Q_glutamine = 0.0
#add_K_lysine = 0.0
#add_E_glutamic_acid = 0.0
#add_M_methionine = 0.0
#add_H_histidine = 0.0
#add_F_phenylalanine = 0.0
#add_R_arginine = 0.0
#add_Y_tyrosine = 0.0
#add_W_tryptophan = 0.0
#add_B_user_amino_acid = 0.0
#add_J_user_amino_acid = 0.0
#add_O_user_amino_acid = 0.0
#add_U_user_amino_acid = 0.0
#add_X_user_amino_acid = 0.0
#add_Z_user_amino_acid = 0.0
";

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
                    /*variable_mod_01 = 15.994900 M 3
                    # variable_mod_02 = 42.010600 [^ 1
                    # variable_mod_03 = 79.966330 STY 3
                    # variable_mod_04 = -17.026500 nQnC 1
                    # variable_mod_05 = -18.010600 nE 1
                    # variable_mod_06 = 229.162930 n^ 1
                    # variable_mod_07 = 229.162930 S 1
                    */
                    // MSFragger static mods must have an AA and cannot be negative or terminal-specific, so in those cases, treat it as a variable mod
                    if (mod.IsVariable || mod.AAs == null || !position.IsNullOrEmpty() || mass < 0)
                    {
                        ++modCounter;
                        modParamLines.Add($@"variable_mod_{modCounter:D2} = {mass.ToString(CultureInfo.InvariantCulture)} {residues} {maxVariableMods_}");
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
            _fragmentIons = ions;
        }

        public override void SetMs2Analyzer(string ms2Analyzer)
        {
            // not used by MSFragger
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

        private string[] SupportedExtensions = { @".mzml", @".mzxml", @".raw", @".d" };

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
 