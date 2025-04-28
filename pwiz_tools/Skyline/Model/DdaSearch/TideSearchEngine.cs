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
using MSAmanda.Utils;
using NHibernate.SqlCommand;
using Enzyme = pwiz.Skyline.Model.DocSettings.Enzyme;
using System.Xml.Linq;

namespace pwiz.Skyline.Model.DdaSearch
{
    public class TideSearchEngine : AbstractDdaSearchEngine, IProgressMonitor
    {
        private List<string> TIDE_SETTINGS = new List<string>();

        // Percolator settings
        private const string PERCOLATOR_TEST_QVALUE_CUTOFF = "test-fdr";
        private const string PERCOLATOR_TRAIN_QVALUE_CUTOFF = "train-fdr";
        private readonly string[] PERCOLATOR_SETTINGS = { PERCOLATOR_TEST_QVALUE_CUTOFF, PERCOLATOR_TRAIN_QVALUE_CUTOFF };

        private const string KEEP_INTERMEDIATE_FILES = "keep-intermediate-files";

        public TideSearchEngine(double percolatorQvalueCutoff)
        {
            AdditionalSettings = new Dictionary<string, Setting>
            {
                {PERCOLATOR_TEST_QVALUE_CUTOFF, new Setting(PERCOLATOR_TEST_QVALUE_CUTOFF, percolatorQvalueCutoff, 0, 1)},
                {PERCOLATOR_TRAIN_QVALUE_CUTOFF, new Setting(PERCOLATOR_TRAIN_QVALUE_CUTOFF, 0.01, 0, 1)},
                {KEEP_INTERMEDIATE_FILES, new Setting(KEEP_INTERMEDIATE_FILES, true)},
            };

            // Tide-search settings
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("max-precursor-charge", 5, 1, 9));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("min-precursor-charge", 1, 1, 9));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("deisotope", 0.0, 0.0));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("isotope-error", ""));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("min-peaks", 20, 1));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("mz-bin-offset", 0.4, 0, 1));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("fragment-tolerance", 0.02, 0.00001, 2));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("override-charges", false));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("remove-precursor-peak", false));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("remove-precursor-tolerance", 1.5, 0));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("scan-number", ""));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("score-function", "xcorr", "xcorr|combined-p-values".Split('|')));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("skip-preprocessing", false));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("spectrum-max-mz", 1e+09, 0));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("spectrum-min-mz", 0.0, 0));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("use-flanking-peaks", false));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("use-neutral-loss-peaks", true));
            
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("precursor-window", 50));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("precursor-window-type", "ppm", "mass|mz|ppm".Split('|')));   // No support for 'mass' unit

            // Add param-medic options.
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("pm-charges", "0,2,3,4"));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("pm-max-frag-mz", 1800, 0));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("pm-max-precursor-delta-ppm", 50, 0));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("pm-max-precursor-mz", 1800, 0));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("pm-max-scan-separation", 1000, 1));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("pm-min-common-frag-peaks", 20, 1));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("pm-min-frag-mz", 150, 1));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("pm-min-peak-pairs", 200, 1));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("pm-min-precursor-mz", 400, 1));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("pm-min-scan-frag-peaks", 40, 1));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("pm-pair-top-n-frag-peaks", 5, 1));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("pm-top-n-frag-peaks", 30, 1));

            // Input and output 
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("fileroot", ""));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("mass-precision", 4, 0));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("mzid-output", false));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("mztab-output", false));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("precision", 8, 0));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("print-search-progress", 10000));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("spectrum-parser", "pwiz", "pwiz|mstoolkit".Split('|')));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("sqt-output", false));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("store-index", ""));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("store-spectra", ""));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("top-match", 5, 1));
//            AddAdditionalSetting(TIDE_SETTINGS, new Setting("txt-output", true));   // Must be true
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("use-z-line", true));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("verbosity", 30));

            // Tide-index settings
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("memory-limit", 4, 1));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("auto-modifications-spectra", ""));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("clip-nterm-methionine", false));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("isotopic-mass", "mono", "mono|average".Split('|')));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("max-length", 50, 1));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("max-mass", 7200, 1));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("min-length", 6, 1));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("min-mass", 200, 1));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("cterm-peptide-mods-spec", ""));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("cterm-protein-mods-spec", ""));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("min-mods", 0, 0));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("mod-precision", 4, 0));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("mods-spec", ""));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("nterm-peptide-mods-spec", ""));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("nterm-protein-mods-spec", ""));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("auto-modifications", false));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("allow-dups", false));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("decoy-format", "shuffle", "none|shuffle|peptide-reverse".Split('|')));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("num-decoys-per-target", 1, 0));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("seed", 1, 1));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("digestion", "full-digest", "full-digest|partial-digest|non-specific-digest".Split('|')));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("mass-precision", 4, 0));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("peptide-list", false));
            AddAdditionalSetting(TIDE_SETTINGS, new Setting("temp-dir", ""));

            //// ReSharper restore LocalizableElement
        }

        private void AddAdditionalSetting(List<string> settingNameList, Setting setting)
        {
            settingNameList.Add(setting.Name);
            AdditionalSettings[setting.Name] = setting;
        }

        private static readonly string[] FRAGMENTATION_METHODS =
        {
            @"b,y",  // Tide-search generates only b- and y-ions. No options to change these
        };

        static string CRUX_FILENAME = @"crux-4.2";
        static Uri CRUX_URL = new Uri($@"https://noble.gs.washington.edu/crux-downloads/{CRUX_FILENAME}/{CRUX_FILENAME}.Windows.AMD64.zip");
        public static string CruxDirectory => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), CRUX_FILENAME);
        public static string CruxBinary => Path.Combine(CruxDirectory, $@"{CRUX_FILENAME}.Windows.AMD64", @"bin", @"crux");

        public static FileDownloadInfo[] FilesToDownload => JavaDownloadInfo.FilesToDownload.Concat(new[] {
            new FileDownloadInfo { Filename = CRUX_FILENAME, DownloadUrl = CRUX_URL, InstallPath = CruxDirectory, OverwriteExisting = true, Unzip = true }
        }).ToArray();

        private MzTolerance _precursorMzTolerance;
        private MzTolerance _fragmentMzTolerance;
        private SortedSet<string> _fragmentIons;
        private Enzyme _enzyme;
        private int _ntt, _maxMissedCleavages;
        private int _maxVariableMods = 2;
        private List<CruxModification> _variableMods;
        private string _modParams;
        private string _nTermModParams;
        private string _cTermModParams;
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
                return new[]
                {
                    new MzToleranceUnits(@"mass", MzTolerance.Units.mz),    // No support for 'Units.mass' 
                    new MzToleranceUnits(@"mz", MzTolerance.Units.mz),
                    new MzToleranceUnits(@"ppm", MzTolerance.Units.ppm)
                };
            }
        }

        public override MzToleranceUnits[] FragmentIonToleranceUnitTypes => new[] { new MzToleranceUnits(@"m/z", MzTolerance.Units.mz) };
        public override string EngineName => @"Tide-search";
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

                SetTideParam(paramsFileText, @"num_threads", 0);
                SetTideParam(paramsFileText, @"concat", "True");
                //AddAdditionalSetting(TIDE_SETTINGS, new Setting("concat", false));

                SetTideParam(paramsFileText, @"decoy_prefix", _decoyPrefix);
                SetTideParam(paramsFileText, @"pepxml-output", "T");
                SetTideParam(paramsFileText, @"pin-output", "T");
                SetTideParam(paramsFileText, @"txt-output", "T");
                SetTideParam(paramsFileText, @"precursor-window", _precursorMzTolerance.Value.ToString(CultureInfo.InvariantCulture));
                SetTideParam(paramsFileText, @"precursor-window-type", AdditionalSettings[@"precursor-window-type"].ValueToString(CultureInfo.InvariantCulture));
                SetTideParam(paramsFileText, @"mz-bin-width", _fragmentMzTolerance.Value.ToString(CultureInfo.InvariantCulture));
                SetTideParam(paramsFileText, @"missed-cleavages", _maxMissedCleavages);
                SetTideParam(paramsFileText, @"max-mods", _maxVariableMods);
                SetTideParam(paramsFileText, @"mods-spec", _modParams);
                SetTideParam(paramsFileText, @"nterm-peptide-mods-spec", _nTermModParams);
                SetTideParam(paramsFileText, @"cterm-peptide-mods-spec", _cTermModParams);

                foreach (var settingName in TIDE_SETTINGS)
                {
                    if (settingName == "digestion")
                        continue;
                    SetTideParam(paramsFileText, settingName, AdditionalSettings[settingName].ValueToString(CultureInfo.InvariantCulture));
                }

                if (_enzyme.IsSemiCleaving == true)
                {
                    SetTideParam(paramsFileText, @"digestion", "partial-digest");
                } else
                {
                    SetTideParam(paramsFileText, "digestion", AdditionalSettings["digestion"].ValueToString(CultureInfo.InvariantCulture));
                }

                switch (_enzyme.Name)
                    {
                        case "Trypsin (semi)":
                            SetTideParam(paramsFileText, @"enzyme", "trypsin");
                            break;
                        case "Trypsin":
                            SetTideParam(paramsFileText, @"enzyme", "trypsin");
                            break;
                        case "Trypsin/P":
                            SetTideParam(paramsFileText, @"enzyme", "trypsin/p");
                            break;
                        case "TrypsinK":
                            SetTideParam(paramsFileText, @"enzyme", "lys-c");
                            break;
                        case "TrypsinR":
                            SetTideParam(paramsFileText, @"enzyme", "arg-c");
                            break;
                        case "Chymotrypsin":
                            SetTideParam(paramsFileText, @"enzyme", "chymotrypsin");
                            break;
                        case "ArgC":
                            SetTideParam(paramsFileText, @"enzyme", "arg-c");
                            break;
                        case "AspN":
                            SetTideParam(paramsFileText, @"enzyme", "chymotrypsin");
                            break;
                        case "Clostripain":
                            SetTideParam(paramsFileText, @"enzyme", "clostripain");
                            break;
                        case "CNBr":
                            SetTideParam(paramsFileText, @"enzyme", "custom-enzyme");
                            SetTideParam(paramsFileText, @"custom-enzyme", "[M]|{P}");
                            break;
                        case "Elastase":  // Skyline's digestion rule is different from the one of tide-search
                            SetTideParam(paramsFileText, @"enzyme", "custom-enzyme");
                            SetTideParam(paramsFileText, @"custom-enzyme", "[GVLIA]|{P}");
                            break;
                        case "Formic Acid":
                            SetTideParam(paramsFileText, @"enzyme", "custom-enzyme");
                            SetTideParam(paramsFileText, @"custom-enzyme", "[D]|{P}");
                            break;
                        case "GluC":
                            SetTideParam(paramsFileText, @"enzyme", "glu-c");
                            break;
                        case "GluC bicarb":
                            SetTideParam(paramsFileText, @"enzyme", "custom-enzyme");
                            SetTideParam(paramsFileText, @"custom-enzyme", "[E]|{P}");
                            break;
                        case "Iodosobenzoate":
                            SetTideParam(paramsFileText, @"enzyme", "iodosobenzoate");
                            break;
                        case "LysC":
                            SetTideParam(paramsFileText, @"enzyme", "lys-c");
                            break;
                        case "LysC/P":
                            SetTideParam(paramsFileText, @"enzyme", "custom-enzyme");
                            SetTideParam(paramsFileText, @"custom-enzyme", "[K]|[]");
                            break;
                        case "LysN":
                            SetTideParam(paramsFileText, @"enzyme", "lys-n");
                            break;
                        case "LysN promisc":
                            SetTideParam(paramsFileText, @"enzyme", "custom-enzyme");
                            SetTideParam(paramsFileText, @"custom-enzyme", "[]|[KASR]");
                            break;
                        case "PepsinA":
                            SetTideParam(paramsFileText, @"enzyme", "custom-enzyme");
                            SetTideParam(paramsFileText, @"custom-enzyme", "[FL]|[]");
                            break;
                        case "Protein endopeptidase":
                            SetTideParam(paramsFileText, @"enzyme", "custom-enzyme");
                            SetTideParam(paramsFileText, @"custom-enzyme", "[P]|[]");
                            break;
                        case "Staph protease":
                            SetTideParam(paramsFileText, @"enzyme", "staph-protease");
                            break;
                        case "Trypsin-CNBr":
                            SetTideParam(paramsFileText, @"enzyme", "custom-enzyme");
                            SetTideParam(paramsFileText, @"custom-enzyme", "[KRM]|[P]");
                            break;
                        case "Trypsin-GluC":
                            SetTideParam(paramsFileText, @"enzyme", "custom-enzyme");
                            SetTideParam(paramsFileText, @"custom-enzyme", "[DEKR]|[P]");
                            break;
                        default:
                            // Handle user defined digestion rule.
                            // Tide does not allow digestion from both terminal.
                            // It must be either C-terminal xor N-terminal, but not both.
                            SetTideParam(paramsFileText, @"enzyme", "custom-enzyme");
                            string cleavageRule = string.Empty;
                            if (_enzyme.CleavageC != null)
                                cleavageRule += $@"[{_enzyme.CleavageC}]";
                            if (_enzyme.RestrictC != null)
                                cleavageRule += $@"{{{_enzyme.RestrictC}}}";
                            cleavageRule += "|";
                            if (_enzyme.CleavageN != null)
                                cleavageRule += $@"[{_enzyme.CleavageN}]";
                            if (_enzyme.RestrictN != null)
                                cleavageRule += $@"{{{_enzyme.RestrictN}}}";
                            SetTideParam(paramsFileText, @"custom-enzyme", $@"{cleavageRule}");
                        break;
                    }

                string defaultOutputDirectory = Path.GetDirectoryName(SpectrumFileNames[0].GetFilePath()) ?? Path.Combine(Environment.CurrentDirectory, "crux-output");

                string paramsFile = KeepIntermediateFiles ? Path.Combine(defaultOutputDirectory, @"tide.params") : Path.GetTempFileName();
                _intermediateFiles.Add(paramsFile);
                File.WriteAllText(paramsFile, paramsFileText.ToString());

                // Run Tide
                var pr = new ProcessRunner();
                var psi = new ProcessStartInfo(CruxBinary, $@"tide-search --overwrite T --output-dir ""{defaultOutputDirectory}"" --parameter-file ""{paramsFile}""")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                foreach (var filename in SpectrumFileNames)
                    psi.Arguments += $@" ""{filename}""";
                psi.Arguments += $@" ""{_fastaFilepath}""";
                pr.Run(psi, string.Empty, this, ref _progressStatus, ProcessPriorityClass.BelowNormal, true);

                string cruxOutputDir = defaultOutputDirectory;
                // I do not understand why do we need the next 4 lines --- AKF
                //string cruxParamsFile = KeepIntermediateFiles ? Path.Combine(defaultOutputDirectory, @"tide.params") : Path.GetTempFileName();
                //_intermediateFiles.Add(cruxParamsFile);
                //var cruxParamsFileText = GetCruxParamsText();
                //File.WriteAllText(cruxParamsFile, cruxParamsFileText);

                // Run Crux Percolator
                psi.FileName = CruxBinary;
                psi.Arguments = $@"percolator --only-psms T --output-dir ""{cruxOutputDir}"" --overwrite T --decoy-prefix ""{_decoyPrefix}"" --parameter-file ""{paramsFile}""";

                foreach (var settingName in PERCOLATOR_SETTINGS)
                    psi.Arguments += $@" --{AdditionalSettings[settingName].ToString(false, CultureInfo.InvariantCulture)}";

                // Tide search produces one output file, even when multiple imput spectrum files were specified.. 

                string fileroot = AdditionalSettings["fileroot"].ValueToString(CultureInfo.InvariantCulture);

                string tideOutputFile = Path.Combine(cruxOutputDir, (fileroot.IsNullOrEmpty() ? "" : ".") + "tide-search");

                string TidePepXmlFilepath = tideOutputFile + @".pep.xml" ;
                string cruxInputFilepath = tideOutputFile + @".pin";
                string cruxFixedInputFilepath = tideOutputFile + @".fixed.pin";
                _intermediateFiles.Add(cruxInputFilepath);
                FixTidePin(cruxInputFilepath, cruxFixedInputFilepath, TidePepXmlFilepath);
                psi.Arguments += $@" ""{cruxFixedInputFilepath}""";
                
                pr.Run(psi, string.Empty, this, ref _progressStatus, ProcessPriorityClass.BelowNormal, true);

                var qvalueByPsmId = new Dictionary<string, double>();
                // Read PSMs from text files and update original pepXMLs with Percolator scores
                string percolatorTargetPsmsTsv = Path.Combine(cruxOutputDir, @"percolator.target.psms.txt");
                string percolatorDecoyPsmsTsv = Path.Combine(cruxOutputDir, @"percolator.decoy.psms.txt");
                GetPercolatorScores(percolatorTargetPsmsTsv, qvalueByPsmId);
                GetPercolatorScores(percolatorDecoyPsmsTsv, qvalueByPsmId);

                // We have only one percolator output file
                string finalOutputFilepath = Path.Combine(cruxOutputDir, (fileroot.IsNullOrEmpty() ? "" : ".") + @"percolator.pep.xml");
                FixPercolatorPepXml(TidePepXmlFilepath, finalOutputFilepath, qvalueByPsmId);

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

        private void SetTideParam(StringBuilder config, string name, object value)
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
/*   I do not understand why do we need this for Percolator --- AKF
        private string GetCruxParamsText()
        {
            var cruxParamsFileText = new StringBuilder();
            foreach (var line in _modParams.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
            {
                cruxParamsFileText.AppendLine(line);
            }


            int iMod = 0;
            foreach (var m in _variableMods)
            {
                ++iMod;
                cruxParamsFileText.AppendLine(string.Format(@"kfa--variable_mod{0:D2} = {1} {2} {3} {4} -1 {5} 0", iMod,
                    m.Mz, m.GetCruxResidues(), iMod, _maxVariableMods, m.GetCruxTerminusOrdinal()));
            }

            return cruxParamsFileText.ToString();
        }
*/
        private void GetPercolatorScores(string percolatorTsvFilepath, Dictionary<string, double> qvalueByPsmId)
        {
            var percolatorTargetPsmsReader = new DsvFileReader(percolatorTsvFilepath, TextUtil.SEPARATOR_TSV);
            int psmIdColumn = percolatorTargetPsmsReader.GetFieldIndex(@"PSMId");
            int qvalueColumn = percolatorTargetPsmsReader.GetFieldIndex(@"q-value");
            // Tide-search does not include directory name to the PSMId.
            //int psmIdStartIndex = Path.GetDirectoryName(Path.GetDirectoryName(percolatorTsvFilepath))?.Length + 1 ?? 0; // trim off the directory name
            while (percolatorTargetPsmsReader.ReadLine() != null)
            {
                var psmId = percolatorTargetPsmsReader.GetFieldByIndex(psmIdColumn);
                //psmId = psmId.Substring(psmIdStartIndex); // Tide-search does not include directory name to the PSMId.
                var qvalue = Convert.ToDouble(percolatorTargetPsmsReader.GetFieldByIndex(qvalueColumn), CultureInfo.InvariantCulture);
                qvalueByPsmId[psmId] = qvalue;
            }
        }

        // Add Percolator score to Tide pepXML
        private void FixPercolatorPepXml(string cruxOutputFilepath, string finalOutputFilepath, Dictionary<string, double> qvalueByPsmId)
        {
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
                        //   DdaSearchTest/Tide.run_2.04610.04610.3
                        // to:
                        //   DdaSearchTest/Tide.run_2_4610_3
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

        // Fix (TODO: remove these hacks when it's fixed in Crux and/or Tide):
        // - bug in Crux pepXML writer where it doesn't ignore the N-terminal mod annotation (n[123]); the writer doesn't handle terminal mods anyway, so just remove the n and move the mod over to be an AA mod
        private void FixTidePin(string cruxInputFilepath, string cruxFixedInputFilepath, string TidePepXmlFilepath)
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
            
        public override void SetModifications(IEnumerable<StaticMod> fixedAndVariableModifs, int maxVariableMods_)
        {
            _maxVariableMods = maxVariableMods_;
            _variableMods = new List<CruxModification>();

            // maximum of 16 variable mods - amino acid codes, * for any amino acid, [ and ] specifies protein termini, n and c specifies peptide termini
            // TODO: alert when there are more than 16 variable mods

            var modParamLines = new List<string>();
            var nTermModParamLines = new List<string>();
            var cTermModParamLines = new List<string>();

            var staticModsByAA = new Dictionary<char, double>();
            int modCounter = 0;
            string tideAAs;

            foreach (var mod in fixedAndVariableModifs)
            {
                tideAAs = "";
                if (mod.AAs != null)
                     tideAAs = mod.AAs.Replace(" ", "").Replace(",", "");

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
                    // mod is  a variable mod
                    if (mod.IsVariable  )
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
                        string massSign = mass > 0 ? "+" : "";
                        string res = residues;
                        if (residues == "n^" || residues == "c^")
                            res = "X";
                        switch (position)
                        {
                            case "n":
                                nTermModParamLines.Add($@"{maxVariableMods_}{res}{massSign}{mass.ToString(CultureInfo.InvariantCulture)}");
                                break;
                            case "c":
                                cTermModParamLines.Add($@"{maxVariableMods_}{res}{massSign}{mass.ToString(CultureInfo.InvariantCulture)}");
                                break;
                            default:
                                modParamLines.Add($@"{maxVariableMods_}{res}{massSign}{mass.ToString(CultureInfo.InvariantCulture)}");
                                break;
                        }

                        _variableMods.Add(new CruxModification(mod, mass, residues));
                    }
                    else
                    {
                        // accumulate masses from static mods on each AA
                        if (mod.AAs != null)
                            foreach (char aa in mod.AAs)
                            {
                                if (staticModsByAA.ContainsKey(aa))
                                    staticModsByAA[aa] += mass;
                                else
                                    staticModsByAA[aa] = mass;
                            }
                        else  //Terminal static mods
                        {
                            string massSign = mass > 0 ? "+" : "";
                            string res = "X";
                            switch (position)
                            {
                                case "n":
                                    nTermModParamLines.Add($@"{res}{massSign}{mass.ToString(CultureInfo.InvariantCulture)}");
                                    break;
                                case "c":
                                    cTermModParamLines.Add($@"{res}{massSign}{mass.ToString(CultureInfo.InvariantCulture)}");
                                    break;
                                default:
                                    modParamLines.Add($@"{res}{massSign}{mass.ToString(CultureInfo.InvariantCulture)}");
                                    break;
                            }

                        }
                    }
                };

                if (mod.LabelAtoms == LabelAtoms.None)
                {
                    double mass = mod.MonoisotopicMass ?? SequenceMassCalc.FormulaMass(BioMassCalc.MONOISOTOPIC, mod.ParsedMolecule, SequenceMassCalc.MassPrecision).Value;

                    string residues = string.Empty;
                    if (position.IsNullOrEmpty() || mod.AAs == null)
                        if (mod.AAs == null)
                            residues = mod.AAs ?? (position.IsNullOrEmpty() ? @"*" : $@"{position}^");
                        else
                            residues = tideAAs;
                    else
                        foreach (char aa in tideAAs)
                                residues += $@"{position}{aa}";

                    addMod(mass, residues);
                }
                else
                {
                    foreach(char aa in tideAAs)
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

            if (!staticModsByAA.ContainsKey('C'))
                modParamLines.Add(@"C+0.0"); // disable default cysteine static mod

            foreach (var kvp in staticModsByAA)
                if (AminoAcidFormulas.FullNames.TryGetValue(kvp.Key, out var fullName))
                {
                    string massSign = kvp.Value > 0 ? "+" : "";
                    modParamLines.Add($@"{kvp.Key}{massSign}{kvp.Value.ToString(CultureInfo.InvariantCulture)}");
                }

            modParamLines.Sort();
            nTermModParamLines.Sort();
            cTermModParamLines.Sort();
            _modParams = string.Join(",", modParamLines);
            _nTermModParams = string.Join(",", nTermModParamLines);
            _cTermModParams = string.Join(",", cTermModParamLines);
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
            // not used by Tide
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
        private string GetTideSearchResultFilepath(MsDataFileUri searchFilepath, string extension = ".pep.xml")
        {
            if (SpectrumFileNames.Length > 1)
            {
                return Path.Combine(Path.GetDirectoryName(searchFilepath.GetFilePath()) ?? "",
                    @"Tide-search." + Path.ChangeExtension(searchFilepath.GetFileName(), extension));
            }
            else
            {
                return Path.Combine(Path.GetDirectoryName(searchFilepath.GetFilePath()) ?? "",
                    Path.ChangeExtension(@"tide", extension));
            }
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
 