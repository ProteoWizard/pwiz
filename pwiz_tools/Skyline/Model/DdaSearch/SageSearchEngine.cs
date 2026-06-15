/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
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
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using pwiz.BiblioSpec;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DdaSearch
{
    /// <summary>
    /// Wraps the Sage (https://github.com/lazear/sage) DDA search engine.
    /// Sage is a self-contained native binary that emits a tab-separated results file
    /// (results.sage.tsv) and a Percolator-input file (results.sage.pin). To stay
    /// consistent with the other Percolator-based engines (Comet/Tide/MSFragger),
    /// this wrapper runs the bundled Crux Percolator on the pin, then writes one
    /// BiblioSpec SSL file per spectrum file (Sage's psm_id joins the two).
    /// </summary>
    public class SageSearchEngine : AbstractDdaSearchEngine, IProgressMonitor
    {
        private List<string> SAGE_SETTINGS = new List<string>();

        // Percolator settings
        private const string PERCOLATOR_TEST_QVALUE_CUTOFF = "test-fdr";
        private const string PERCOLATOR_TRAIN_QVALUE_CUTOFF = "train-fdr";
        private readonly string[] PERCOLATOR_SETTINGS = { PERCOLATOR_TEST_QVALUE_CUTOFF, PERCOLATOR_TRAIN_QVALUE_CUTOFF };

        private const string KEEP_INTERMEDIATE_FILES = "keep-intermediate-files";

        // Sage output filenames (fixed by the Sage binary, written to the output directory)
        private const string SAGE_RESULTS_TSV = "results.sage.tsv";
        private const string SAGE_RESULTS_PIN = "results.sage.pin";
        private const string SAGE_RESULTS_JSON = "results.json";

        private const string STANDARD_AMINO_ACIDS = "ACDEFGHIKLMNPQRSTVWY";

        public SageSearchEngine(double percolatorQvalueCutoff)
        {
            AdditionalSettings = new Dictionary<string, Setting>
            {
                {PERCOLATOR_TEST_QVALUE_CUTOFF, new Setting(PERCOLATOR_TEST_QVALUE_CUTOFF, percolatorQvalueCutoff, 0, 1)},
                {PERCOLATOR_TRAIN_QVALUE_CUTOFF, new Setting(PERCOLATOR_TRAIN_QVALUE_CUTOFF, 0.01, 0, 1)},
                {KEEP_INTERMEDIATE_FILES, new Setting(KEEP_INTERMEDIATE_FILES, false)},
            };

            // ReSharper disable LocalizableElement
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("bucket_size", 32768, 1024, 1048576));
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("min_len", 5, 1, 100));
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("max_len", 50, 1, 200));
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("peptide_min_mass", 500.0, 0.0));
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("peptide_max_mass", 5000.0, 0.0));
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("min_ion_index", 2, 0));
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("min_peaks", 15, 0));
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("max_peaks", 150, 0));
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("min_matched_peaks", 6, 0));
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("report_psms", 1, 1, 10));
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("isotope_error_min", -1, -8, 0));
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("isotope_error_max", 3, 0, 8));
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("precursor_charge_min", 2, 1, 9));
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("precursor_charge_max", 4, 1, 9));
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("deisotope", false));
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("chimera", false));
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("wide_window", false));
            AddAdditionalSetting(SAGE_SETTINGS, new Setting("predict_rt", true));
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
            @"b",
        };

        // Sage native binary (downloaded from GitHub releases)
        static string SAGE_VERSION = @"0.14.7";
        static string SAGE_FILENAME = $@"sage-v{SAGE_VERSION}-x86_64-pc-windows-msvc";
        static Uri SAGE_URL = new Uri($@"https://github.com/lazear/sage/releases/download/v{SAGE_VERSION}/{SAGE_FILENAME}.zip");
        public static string SageDirectory => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), SAGE_FILENAME);
        public static string SageBinary => Settings.Default.SearchToolList.GetToolPathOrDefault(SearchToolType.Sage, Path.Combine(SageDirectory, SAGE_FILENAME, @"sage.exe"));
        public static string SageArgs => Settings.Default.SearchToolList.GetToolArgsOrDefault(SearchToolType.Sage, "");

        // Crux Percolator, used to compute q-values from the Sage pin (same tool/version as Comet/Tide;
        // shared download since the InstallPath and SearchToolType match).
        static string CRUX_FILENAME = @"crux-4.3";
        static Uri CRUX_URL = new Uri($@"https://noble.gs.washington.edu/crux-downloads/{CRUX_FILENAME}/{CRUX_FILENAME}.Windows.AMD64.zip");
        public static string CruxDirectory => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), CRUX_FILENAME);
        public static string CruxBinary => Settings.Default.SearchToolList.GetToolPathOrDefault(SearchToolType.CruxPercolator, Path.Combine(CruxDirectory, $@"{CRUX_FILENAME}.Windows.AMD64", @"bin", @"crux.exe"));
        public static string PercolatorArgs => Settings.Default.SearchToolList.GetToolArgsOrDefault(SearchToolType.CruxPercolator, "");

        public static FileDownloadInfo[] FilesToDownload => new[] {
            new FileDownloadInfo
            {
                Filename = SAGE_FILENAME, DownloadUrl = SAGE_URL, InstallPath = SageDirectory, OverwriteExisting = true, Unzip = true,
                ToolType = SearchToolType.Sage, ToolPath = SageBinary, ToolExtraArgs = SageArgs
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
        private int _maxMissedCleavages;
        private int _maxVariableMods = 2;
        private JObject _staticModsJson;
        private JObject _variableModsJson;
        private string _fastaFilepath;
        private readonly string _decoyTag = @"rev_";
        private List<string> _intermediateFiles;
        private const string _cutoffScoreName = ScoreType.PERCOLATOR_QVALUE;

        private CancellationTokenSource _cancelToken;
        private IProgressStatus _progressStatus;
        private bool _success;

        private bool KeepIntermediateFiles => (bool)AdditionalSettings[KEEP_INTERMEDIATE_FILES].Value;

        public override string[] FragmentIons => FRAGMENTATION_METHODS;
        public override string[] Ms2Analyzers => new[] { @"Default" };

        public override MzToleranceUnits[] PrecursorIonToleranceUnitTypes => new[]
        {
            new MzToleranceUnits(@"ppm", MzTolerance.Units.ppm),
            new MzToleranceUnits(@"Da", MzTolerance.Units.mz)
        };

        public override MzToleranceUnits[] FragmentIonToleranceUnitTypes => new[]
        {
            new MzToleranceUnits(@"ppm", MzTolerance.Units.ppm),
            new MzToleranceUnits(@"Da", MzTolerance.Units.mz)
        };

        public override string EngineName => @"Sage";
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

                string outputDirectory = Path.GetDirectoryName(SpectrumFileNames[0].GetFilePath()) ?? Path.Combine(Environment.CurrentDirectory, @"sage-output");
                outputDirectory = PathEx.GetNonUnicodePath(outputDirectory); // Convert unicode path to 8.3 if needed

                // Write the Sage JSON configuration file
                string configFile = KeepIntermediateFiles ? Path.Combine(outputDirectory, @"sage.json") : Path.GetTempFileName();
                configFile = PathEx.GetNonUnicodePath(configFile);
                _intermediateFiles.Add(configFile);
                File.WriteAllText(configFile, GetConfigJson());

                // Run Sage
                var pr = new ProcessRunner();
                var psi = new ProcessStartInfo(SageBinary, $@"{SageArgs} ""{configFile}"" --output_directory ""{outputDirectory}"" --write-pin --batch-size 1")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                foreach (var filename in SpectrumFileNames)
                    psi.Arguments += $@" ""{PathEx.GetNonUnicodePath(filename.GetFilePath())}""";
                pr.Run(psi, string.Empty, this, ref _progressStatus, ProcessPriorityClass.BelowNormal, true);

                string sageTsvFilepath = Path.Combine(outputDirectory, SAGE_RESULTS_TSV);
                string sagePinFilepath = Path.Combine(outputDirectory, SAGE_RESULTS_PIN);
                _intermediateFiles.Add(sageTsvFilepath);
                _intermediateFiles.Add(sagePinFilepath);
                _intermediateFiles.Add(Path.Combine(outputDirectory, SAGE_RESULTS_JSON));

                // Crux Percolator cannot parse Sage's non-numeric FileName column, so strip it first
                string fixedPinFilepath = PathEx.GetNonUnicodePath(KeepIntermediateFiles
                    ? Path.Combine(outputDirectory, @"results.sage.fixed.pin")
                    : Path.GetTempFileName());
                _intermediateFiles.Add(fixedPinFilepath);
                FixSagePin(sagePinFilepath, fixedPinFilepath);

                // Run Crux Percolator on the cleaned pin
                psi = new ProcessStartInfo(CruxBinary,
                    $@"percolator {PercolatorArgs} --only-psms T --output-dir ""{outputDirectory}"" --overwrite T --decoy-prefix ""{_decoyTag}""")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                foreach (var settingName in PERCOLATOR_SETTINGS)
                    psi.Arguments += $@" --{AdditionalSettings[settingName].ToString(false, CultureInfo.InvariantCulture)}";
                psi.Arguments += $@" ""{fixedPinFilepath}""";
                pr.Run(psi, string.Empty, this, ref _progressStatus, ProcessPriorityClass.BelowNormal, true);

                // Read Percolator q-values, keyed by Sage psm_id (== pin SpecId == Percolator PSMId).
                // Only target PSMs go into the library, so the decoy table is not read (just cleaned up).
                var qvalueByPsmId = new Dictionary<string, double>();
                GetPercolatorScores(Path.Combine(outputDirectory, @"percolator.target.psms.txt"), qvalueByPsmId);
                _intermediateFiles.Add(Path.Combine(outputDirectory, @"percolator.target.psms.txt"));
                _intermediateFiles.Add(Path.Combine(outputDirectory, @"percolator.decoy.psms.txt"));
                _intermediateFiles.Add(Path.Combine(outputDirectory, @"percolator.log.txt"));

                // Write one BiblioSpec SSL per spectrum file from the Sage TSV + Percolator q-values
                WriteSslFiles(sageTsvFilepath, qvalueByPsmId);

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
            }

            if (_success)
                _progressStatus = _progressStatus.Complete().ChangeMessage(Resources.DDASearchControl_SearchProgress_Search_done);
            UpdateProgress(_progressStatus);

            return _success;
        }

        private string GetConfigJson()
        {
            bool cTerminal = _enzyme.IsCTerm;
            string cleaveAt = (cTerminal ? _enzyme.CleavageC : _enzyme.CleavageN) ?? string.Empty;
            string restrict = (cTerminal ? _enzyme.RestrictC : _enzyme.RestrictN) ?? string.Empty;

            var enzyme = new JObject
            {
                [@"missed_cleavages"] = _maxMissedCleavages,
                [@"min_len"] = GetIntSetting(@"min_len"),
                [@"max_len"] = GetIntSetting(@"max_len"),
                [@"cleave_at"] = cleaveAt,
                [@"c_terminal"] = cTerminal,
                [@"semi_enzymatic"] = _enzyme.IsSemiCleaving
            };
            // Sage's "restrict" is an optional SINGLE character. Omit it when there is no restriction;
            // also omit (rather than emit an unparseable multi-char value) for the rare custom enzyme
            // whose restriction list has more than one residue, which Sage cannot represent.
            if (restrict.Length == 1)
                enzyme[@"restrict"] = restrict;

            var database = new JObject
            {
                [@"bucket_size"] = GetIntSetting(@"bucket_size"),
                [@"enzyme"] = enzyme,
                [@"peptide_min_mass"] = GetDoubleSetting(@"peptide_min_mass"),
                [@"peptide_max_mass"] = GetDoubleSetting(@"peptide_max_mass"),
                [@"ion_kinds"] = new JArray(_fragmentIons.Cast<object>().ToArray()),
                [@"min_ion_index"] = GetIntSetting(@"min_ion_index"),
                [@"static_mods"] = _staticModsJson ?? new JObject(),
                [@"variable_mods"] = _variableModsJson ?? new JObject(),
                [@"max_variable_mods"] = _maxVariableMods,
                [@"decoy_tag"] = _decoyTag,
                [@"generate_decoys"] = true,
                [@"fasta"] = _fastaFilepath
            };

            var config = new JObject
            {
                [@"database"] = database,
                [@"precursor_tol"] = GetToleranceJson(_precursorMzTolerance),
                [@"fragment_tol"] = GetToleranceJson(_fragmentMzTolerance),
                [@"isotope_errors"] = new JArray(GetIntSetting(@"isotope_error_min"), GetIntSetting(@"isotope_error_max")),
                [@"precursor_charge"] = new JArray(GetIntSetting(@"precursor_charge_min"), GetIntSetting(@"precursor_charge_max")),
                [@"min_peaks"] = GetIntSetting(@"min_peaks"),
                [@"max_peaks"] = GetIntSetting(@"max_peaks"),
                [@"min_matched_peaks"] = GetIntSetting(@"min_matched_peaks"),
                [@"report_psms"] = GetIntSetting(@"report_psms"),
                [@"deisotope"] = GetBoolSetting(@"deisotope"),
                [@"chimera"] = GetBoolSetting(@"chimera"),
                [@"wide_window"] = GetBoolSetting(@"wide_window"),
                [@"predict_rt"] = GetBoolSetting(@"predict_rt")
                // output_directory and write_pin come from the --output_directory / --write-pin CLI flags
            };

            return config.ToString();
        }

        private int GetIntSetting(string name)
        {
            return (int)AdditionalSettings[name].Value;
        }

        private double GetDoubleSetting(string name)
        {
            return (double)AdditionalSettings[name].Value;
        }

        private bool GetBoolSetting(string name)
        {
            return (bool)AdditionalSettings[name].Value;
        }

        // Sage tolerance is { "ppm"|"da": [low, high] }; low is subtracted, high is added.
        private static JObject GetToleranceJson(MzTolerance tolerance)
        {
            string unitKey = tolerance.Unit == MzTolerance.Units.ppm ? @"ppm" : @"da";
            return new JObject { [unitKey] = new JArray(-tolerance.Value, tolerance.Value) };
        }

        // Remove the non-numeric FileName column that Crux Percolator cannot parse as a feature.
        private void FixSagePin(string pinFilepath, string fixedPinFilepath)
        {
            using (var pinFile = new StreamReader(pinFilepath))
            using (var fixedPinFile = new StreamWriter(fixedPinFilepath))
            {
                string headerLine = pinFile.ReadLine();
                if (headerLine == null)
                    throw new InvalidDataException(string.Format(DdaSearchResources.DdaSearch_SageSearchEngine_Sage_did_not_produce_a_valid_Percolator_input_file___0__, SAGE_RESULTS_PIN));

                var headers = headerLine.Split(TextUtil.SEPARATOR_TSV);
                int fileNameColumn = Array.FindIndex(headers, h => string.Equals(h, @"FileName", StringComparison.OrdinalIgnoreCase));

                fixedPinFile.WriteLine(fileNameColumn < 0 ? headerLine : RemoveColumn(headerLine, fileNameColumn));

                string line;
                while ((line = pinFile.ReadLine()) != null)
                    fixedPinFile.WriteLine(fileNameColumn < 0 ? line : RemoveColumn(line, fileNameColumn));
            }
        }

        // The Proteins column (last) may legitimately contain tabs, so only split off the leading columns.
        private static string RemoveColumn(string line, int columnIndex)
        {
            var fields = line.Split(TextUtil.SEPARATOR_TSV);
            if (columnIndex >= fields.Length)
                return line;
            return string.Join(TextUtil.SEPARATOR_TSV.ToString(), fields.Where((f, i) => i != columnIndex));
        }

        private void GetPercolatorScores(string percolatorTsvFilepath, Dictionary<string, double> qvalueByPsmId)
        {
            using var reader = new DsvFileReader(percolatorTsvFilepath, TextUtil.SEPARATOR_TSV);
            int psmIdColumn = reader.GetFieldIndex(@"PSMId");
            int qvalueColumn = reader.GetFieldIndex(@"q-value");
            while (reader.ReadLine() != null)
            {
                var psmId = reader.GetFieldByIndex(psmIdColumn);
                var qvalue = Convert.ToDouble(reader.GetFieldByIndex(qvalueColumn), CultureInfo.InvariantCulture);
                qvalueByPsmId[psmId] = qvalue;
            }
        }

        private void WriteSslFiles(string sageTsvFilepath, Dictionary<string, double> qvalueByPsmId)
        {
            // Group target PSMs (that survived Percolator) by source spectrum file.
            // The join key is Sage's integer psm_id: it is the TSV "psm_id" column, the pin "SpecId"
            // column, and therefore the Percolator "PSMId" - all the same global PSM counter in Sage's
            // output format (verified against Sage v0.14.7). Unlike Comet, Sage's SpecId carries no path
            // prefix, so the q-values join directly without any trimming.
            // Keyed by the basename of the path AS PASSED TO SAGE (i.e. after GetNonUnicodePath, which may
            // yield an 8.3 short name); the per-file lookup below applies the same transform so they match.
            var rowsByFile = new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);
            int totalRows = 0;
            using (var reader = new DsvFileReader(sageTsvFilepath, TextUtil.SEPARATOR_TSV))
            {
                int psmIdColumn = reader.GetFieldIndex(@"psm_id");
                int filenameColumn = reader.GetFieldIndex(@"filename");
                int scannrColumn = reader.GetFieldIndex(@"scannr");
                int chargeColumn = reader.GetFieldIndex(@"charge");
                int peptideColumn = reader.GetFieldIndex(@"peptide");
                int labelColumn = reader.GetFieldIndex(@"label");
                int rankColumn = reader.GetFieldIndex(@"rank");

                while (reader.ReadLine() != null)
                {
                    if (reader.GetFieldByIndex(labelColumn) != @"1") // targets only
                        continue;
                    if (reader.GetFieldByIndex(rankColumn) != @"1") // best match per spectrum only (report_psms may be > 1)
                        continue;
                    if (!qvalueByPsmId.TryGetValue(reader.GetFieldByIndex(psmIdColumn), out double qvalue))
                        continue;

                    string fileKey = Path.GetFileName(reader.GetFieldByIndex(filenameColumn));
                    if (!rowsByFile.TryGetValue(fileKey, out var rows))
                        rowsByFile[fileKey] = rows = new List<string[]>();
                    rows.Add(new[]
                    {
                        reader.GetFieldByIndex(scannrColumn),
                        reader.GetFieldByIndex(chargeColumn),
                        TransformSagePeptideToSsl(reader.GetFieldByIndex(peptideColumn)),
                        qvalue.ToString(CultureInfo.InvariantCulture)
                    });
                    ++totalRows;
                }
            }

            // If Percolator scored PSMs but none joined to a result row, the library would be silently
            // empty - fail loudly instead, since it likely means the Sage output format changed.
            if (qvalueByPsmId.Count > 0 && totalRows == 0)
                throw new InvalidDataException(
                    DdaSearchResources.DdaSearch_SageSearchEngine_Sage_produced_Percolator_scores__but_no_search_results_could_be_matched_to_them__This_may_indicate_an_unsupported_Sage_version_);

            foreach (var spectrumFilename in SpectrumFileNames)
            {
                string sslFilepath = GetSearchResultFilepath(spectrumFilename);
                // The SSL "file" column must be the real on-disk name (BiblioSpec opens it next to the SSL),
                // but the lookup key must match Sage's echoed filename (the GetNonUnicodePath form).
                string sslFileValue = Path.GetFileName(spectrumFilename.GetFilePath());
                string lookupKey = Path.GetFileName(PathEx.GetNonUnicodePath(spectrumFilename.GetFilePath()));

                using var sslWriter = new StreamWriter(sslFilepath);
                sslWriter.WriteLine(string.Join(TextUtil.SEPARATOR_TSV.ToString(),
                    @"file", @"scan", @"charge", @"sequence", @"score-type", @"score"));
                if (!rowsByFile.TryGetValue(lookupKey, out var rows))
                    continue;
                foreach (var row in rows)
                    sslWriter.WriteLine(string.Join(TextUtil.SEPARATOR_TSV.ToString(),
                        sslFileValue, row[0], row[1], row[2], @"PERCOLATOR QVALUE", row[3]));
            }
        }

        // Sage writes terminal mods with a hyphen separator (e.g. "[+42.01]-PEP" / "PEP-[+11]") that
        // BiblioSpec's SSL parser rejects. Strip ONLY the terminal separators; in-bracket signs like
        // "[-17.0265]" (pyro-Glu) must be preserved.
        private static string TransformSagePeptideToSsl(string sagePeptide)
        {
            return sagePeptide.Replace(@"]-", @"]").Replace(@"-[", @"[");
        }

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

        public override void SetModifications(IEnumerable<StaticMod> fixedAndVariableModifs, int maxVariableMods_)
        {
            _maxVariableMods = maxVariableMods_;
            _staticModsJson = new JObject();
            _variableModsJson = new JObject();

            foreach (var mod in fixedAndVariableModifs)
            {
                // can't use mod with no formula or mass
                if (mod.LabelAtoms == LabelAtoms.None && ParsedMolecule.IsNullOrEmpty(mod.ParsedMolecule) && mod.MonoisotopicMass == null ||
                    mod.LabelAtoms != LabelAtoms.None && mod.AAs.IsNullOrEmpty())
                    continue;

                if (mod.LabelAtoms == LabelAtoms.None)
                {
                    double mass = mod.MonoisotopicMass ?? SequenceMassCalc.FormulaMass(BioMassCalc.MONOISOTOPIC, mod.ParsedMolecule, SequenceMassCalc.MassPrecision).Value;
                    foreach (var key in GetSageModKeys(mod))
                        AddSageMod(mod.IsVariable, key, mass);
                }
                else
                {
                    foreach (char aa in mod.AAs)
                    {
                        double mass = SequenceMassCalc.FormulaMass(BioMassCalc.MONOISOTOPIC, SequenceMassCalc.GetHeavyFormula(aa, mod.LabelAtoms), SequenceMassCalc.MassPrecision).Value;
                        AddSageMod(mod.IsVariable, GetSageTerminusKey(mod) + aa, mass);
                    }
                }
            }
        }

        // Sage mod keys: a residue letter, a bare terminus ("^" N-term, "$" C-term), or a
        // terminus+residue combination ("^X"). Protein-terminal mods are not distinguished here.
        private IEnumerable<string> GetSageModKeys(StaticMod mod)
        {
            string terminusKey = GetSageTerminusKey(mod);
            if (!mod.AAs.IsNullOrEmpty())
            {
                foreach (char aa in mod.AAs)
                    yield return terminusKey + aa;
            }
            else if (!terminusKey.IsNullOrEmpty())
            {
                yield return terminusKey; // a bare peptide terminus (any residue)
            }
            else
            {
                // No AAs and no terminus means the mod applies to every amino acid; Sage has no
                // wildcard key, so expand it to all standard residues.
                foreach (char aa in STANDARD_AMINO_ACIDS)
                    yield return aa.ToString();
            }
        }

        private static string GetSageTerminusKey(StaticMod mod)
        {
            switch (mod.Terminus)
            {
                case ModTerminus.N: return @"^";
                case ModTerminus.C: return @"$";
                default: return string.Empty;
            }
        }

        private void AddSageMod(bool isVariable, string key, double mass)
        {
            if (isVariable)
            {
                if (!(_variableModsJson[key] is JArray masses))
                    _variableModsJson[key] = masses = new JArray();
                masses.Add(mass);
            }
            else
            {
                // Sage static_mods take a single mass per key; accumulate if multiple fixed mods target the same key
                double existing = _staticModsJson[key] != null ? (double)_staticModsJson[key] : 0.0;
                _staticModsJson[key] = existing + mass;
            }
        }

        public override void SetEnzyme(Enzyme enz, int mmc)
        {
            _enzyme = enz;
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
            // not used by Sage
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
            return Path.ChangeExtension(searchFilepath.GetFilePath(), @".ssl");
        }

        private string[] SupportedExtensions = { @".mzml" };

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
