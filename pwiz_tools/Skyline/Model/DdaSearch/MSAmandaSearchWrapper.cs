/*
 * Original author: Viktoria Dorfer <viktoria.dorfer .at. fh-hagenberg.at>,
 *                  Bioinformatics Research Group, University of Applied Sciences Upper Austria
 *
 * Copyright 2020 University of Applied Sciences Upper Austria
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
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using pwiz.BiblioSpec;
using pwiz.Common.Chemistry;
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
    /// Out-of-process wrapper around the MSAmanda standalone command-line tool.
    /// Downloads MSAmanda.exe from GitHub on first use, generates a settings.xml
    /// per input file, shells out, and returns the produced .mzid.gz path.
    /// The in-process integration against FHOOE_IMP.MSAmanda.* .NET Framework
    /// DLLs is intentionally gone - this wrapper is portable to net8.
    /// </summary>
    public class MSAmandaSearchWrapper : AbstractDdaSearchEngine, IProgressMonitor
    {
        // Version pinned by cache-path only; upstream publishes just a rolling "latest.zip"
        // under release/sa/latest/win/. We detect+embed the version in the install dir
        // (so an upstream bump falls into a new cache dir and re-downloads).
        public const string MSAMANDA_VERSION = @"3.0.22.864";
        public const string MSAMANDA_FILENAME = @"MSAmanda-" + MSAMANDA_VERSION;
        private const string MSAMANDA_EXE = @"MSAmanda.exe";

        private static readonly Uri MSAMANDA_URL = new Uri(
            @"https://github.com/hgb-bin-proteomics/MSAmanda/raw/master/release/sa/latest/win/latest.zip");

        public static string MSAmandaDirectory => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), MSAMANDA_FILENAME);
        public static string MSAmandaBinary => Settings.Default.SearchToolList.GetToolPathOrDefault(SearchToolType.MSAmanda,
            Path.Combine(MSAmandaDirectory, MSAMANDA_EXE));
        public static string MSAmandaArgs => Settings.Default.SearchToolList.GetToolArgsOrDefault(SearchToolType.MSAmanda, "");

        public static FileDownloadInfo[] FilesToDownload => new[]
        {
            new FileDownloadInfo
            {
                Filename = MSAMANDA_FILENAME, DownloadUrl = MSAMANDA_URL, InstallPath = MSAmandaDirectory,
                OverwriteExisting = true, Unzip = true,
                ToolType = SearchToolType.MSAmanda, ToolPath = MSAmandaBinary, ToolExtraArgs = MSAmandaArgs
            }
        };

        // MSAmanda's fragment-ion "Instrument" strings. The set matches the
        // possible values documented in the bundled settings.xml. This replaces
        // the runtime-parsed Instruments.xml the old in-process wrapper used.
        private static readonly string[] FRAGMENT_ION_SETS =
        {
            @"b, y",
            @"a, b, y",
            @"b, y, IMM",
            @"b, y, H2O, NH3",
            @"c, z+1",
            @"c, z"
        };

        // Additional-settings knobs (surface a few useful MSAmanda toggles)
        private const string MAX_LOADED_PROTEINS_AT_ONCE = "MaxLoadedProteinsAtOnce";
        private const string MAX_LOADED_SPECTRA_AT_ONCE = "MaxLoadedSpectraAtOnce";
        private const string CONSIDERED_CHARGES = "ConsideredCharges";
        private const string MAX_NO_DYN_MODIFS = "MaxNoDynModifs";
        private const string MAX_RANK = "MaxRank";
        private const string KEEP_INTERMEDIATE_FILES = "keep-intermediate-files";

        private const string _cutoffScoreName = ScoreType.PERCOLATOR_QVALUE;

        // Captured configuration
        private MzTolerance _precursorTol = new MzTolerance(5, MzTolerance.Units.ppm);
        private MzTolerance _fragmentTol = new MzTolerance(0.02, MzTolerance.Units.mz);
        private string _fragmentIons = @"b, y";
        private Enzyme _enzyme;
        private int _maxMissedCleavages = 2;
        private int _maxVariableMods = 3;
        private readonly List<StaticMod> _fixedMods = new List<StaticMod>();
        private readonly List<StaticMod> _variableMods = new List<StaticMod>();

        // Run-time state
        private CancellationTokenSource _cancelToken;
        private IProgressStatus _progressStatus;
        private bool _success;
        private List<string> _intermediateFiles;

        public int CurrentFile { get; private set; }
        public int TotalFiles => SpectrumFileNames?.Length ?? 0;

        public override event NotificationEventHandler SearchProgressChanged;

        public MSAmandaSearchWrapper()
        {
            AdditionalSettings = new Dictionary<string, Setting>
            {
                {MAX_LOADED_PROTEINS_AT_ONCE, new Setting(MAX_LOADED_PROTEINS_AT_ONCE, 100000, 1000, 1000000000)},
                {MAX_LOADED_SPECTRA_AT_ONCE, new Setting(MAX_LOADED_SPECTRA_AT_ONCE, 10000, 1000, 1000000000)},
                {CONSIDERED_CHARGES, new Setting(CONSIDERED_CHARGES, @"2+,3+,4+")},
                {MAX_NO_DYN_MODIFS, new Setting(MAX_NO_DYN_MODIFS, 4, 0, 10)},
                {MAX_RANK, new Setting(MAX_RANK, 5, 1, 999)},
                {KEEP_INTERMEDIATE_FILES, new Setting(KEEP_INTERMEDIATE_FILES, false)}
            };
        }

        private bool KeepIntermediateFiles => (bool) AdditionalSettings[KEEP_INTERMEDIATE_FILES].Value;

        public override string[] FragmentIons => FRAGMENT_ION_SETS;
        public override string[] Ms2Analyzers => new[] { @"Default" };
        public override string EngineName => @"MS Amanda";
        public override string CutoffScoreName => _cutoffScoreName;
        public override string CutoffScoreLabel => PropertyNames.CutoffScore_PERCOLATOR_QVALUE;
        public override double DefaultCutoffScore { get; } = new ScoreType(_cutoffScoreName, ScoreType.PROBABILITY_INCORRECT).DefaultValue;
        public override Bitmap SearchEngineLogo => Resources.MSAmandaLogo;
        public override string SearchEngineBlurb => string.Empty;

        public override void SetPrecursorMassTolerance(MzTolerance tol) => _precursorTol = tol;
        public override void SetFragmentIonMassTolerance(MzTolerance tol) => _fragmentTol = tol;
        public override void SetFragmentIons(string ions) => _fragmentIons = ions ?? @"b, y";
        public override void SetMs2Analyzer(string analyzer) { /* not used by MSAmanda */ }
        public override void SetCutoffScore(double cutoffScore) { /* MSAmanda doesn't feed this into percolator */ }

        public override void SetEnzyme(Enzyme enzyme, int maxMissedCleavages)
        {
            _enzyme = enzyme;
            _maxMissedCleavages = maxMissedCleavages;
        }

        public override void SetModifications(IEnumerable<StaticMod> modifications, int maxVariableMods_)
        {
            _fixedMods.Clear();
            _variableMods.Clear();
            _maxVariableMods = maxVariableMods_;
            foreach (var mod in modifications)
            {
                if (mod.IsVariable || mod.LabelAtoms != LabelAtoms.None)
                    _variableMods.Add(mod);
                else
                    _fixedMods.Add(mod);
            }
        }

        public override string GetSearchResultFilepath(MsDataFileUri searchFilepath)
        {
            return Path.ChangeExtension(searchFilepath.GetFilePath(), @".mzid.gz");
        }

        public override bool GetSearchFileNeedsConversion(MsDataFileUri searchFilepath, out AbstractDdaConverter.MsdataFileFormat requiredFormat)
        {
            requiredFormat = AbstractDdaConverter.MsdataFileFormat.mzML;
            return false;
        }

        public override bool Run(CancellationTokenSource cancelToken, IProgressStatus status)
        {
            _cancelToken = cancelToken;
            _progressStatus = status;
            _success = true;
            _intermediateFiles = new List<string>();
            CurrentFile = 0;

            try
            {
                foreach (var rawFileName in SpectrumFileNames)
                {
                    _cancelToken.Token.ThrowIfCancellationRequested();

                    string outputMzidGz = GetSearchResultFilepath(rawFileName);
                    FileEx.SafeDelete(outputMzidGz);

                    string spectrumPath = rawFileName.GetFilePath();
                    string outputMzid = Path.ChangeExtension(spectrumPath, @".mzid");
                    string settingsFile = KeepIntermediateFiles
                        ? Path.ChangeExtension(spectrumPath, @".msamanda.settings.xml")
                        : Path.GetTempFileName();
                    _intermediateFiles.Add(settingsFile);
                    _intermediateFiles.Add(outputMzid);

                    File.WriteAllText(settingsFile, BuildSettingsXml());

                    var pr = new ProcessRunner();
                    var psi = new ProcessStartInfo(MSAmandaBinary,
                        $@"{MSAmandaArgs} -s ""{PathEx.GetNonUnicodePath(spectrumPath)}"" " +
                        $@"-d ""{PathEx.GetNonUnicodePath(FastaFileNames[0])}"" " +
                        $@"-e ""{PathEx.GetNonUnicodePath(settingsFile)}"" " +
                        $@"-f 2 " +
                        $@"-o ""{PathEx.GetNonUnicodePath(outputMzid)}""")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = false
                    };

                    // ReSharper disable once LocalizableElement
                    _progressStatus = _progressStatus.ChangeMessage($"Running MS Amanda:\r\n\"{psi.FileName}\" {psi.Arguments}");
                    if (UpdateProgressResponse.cancel == UpdateProgress(_progressStatus))
                        return false;

                    pr.Run(psi, string.Empty, this, ref _progressStatus, ProcessPriorityClass.BelowNormal, true);

                    if (_cancelToken.IsCancellationRequested)
                        break;

                    // gzip the .mzid so downstream (BiblioSpec) consumers see the
                    // legacy .mzid.gz artifact this wrapper has always produced.
                    if (File.Exists(outputMzid))
                        GzipFile(outputMzid, outputMzidGz);
                    else
                        throw new IOException(string.Format(
                            DdaSearchResources.DdaSearch_Search_failed__0,
                            $@"MSAmanda did not produce expected output {outputMzid}"));

                    CurrentFile++;
                    _progressStatus = _progressStatus.NextSegment();
                }
            }
            catch (OperationCanceledException)
            {
                _progressStatus = _progressStatus.ChangeMessage(DdaSearchResources.DdaSearch_Search_is_canceled);
                _success = false;
            }
            catch (Exception ex)
            {
                _progressStatus = _progressStatus.ChangeErrorException(ex)
                    .ChangeMessage(string.Format(DdaSearchResources.DdaSearch_Search_failed__0, ex.Message));
                _success = false;
            }
            finally
            {
                DeleteIntermediateFiles();
            }

            if (IsCanceled && !_progressStatus.IsCanceled)
            {
                _progressStatus = _progressStatus.Cancel().ChangeMessage(Resources.DDASearchControl_SearchProgress_Search_canceled);
                _success = false;
            }

            if (_success)
                _progressStatus = _progressStatus.Complete().ChangeMessage(Resources.DDASearchControl_SearchProgress_Search_done);
            UpdateProgress(_progressStatus);

            return _success;
        }

        private void DeleteIntermediateFiles()
        {
            if (_intermediateFiles == null || KeepIntermediateFiles)
                return;
            foreach (var path in _intermediateFiles)
                FileEx.SafeDelete(path, true);
        }

        private static void GzipFile(string inputPath, string outputPath)
        {
            using (var inStream = File.OpenRead(inputPath))
            using (var outStream = File.Create(outputPath))
            using (var gz = new GZipStream(outStream, CompressionLevel.Optimal))
                inStream.CopyTo(gz);
        }

        private string BuildSettingsXml()
        {
            var xml = new StringBuilder();
            xml.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
            xml.AppendLine(@"<Settings xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">");
            xml.AppendLine(@"  <SearchSettings>");
            xml.Append(BuildEnzymeXml());
            xml.Append(Invariant($@"    <MissedCleavages>{_maxMissedCleavages}</MissedCleavages>{Environment.NewLine}"));
            xml.AppendLine(@"    <Modifications>");
            foreach (var mod in _fixedMods)
                foreach (var line in ModificationLines(mod, isFixed: true))
                    xml.AppendLine(line);
            foreach (var mod in _variableMods)
                foreach (var line in ModificationLines(mod, isFixed: false))
                    xml.AppendLine(line);
            xml.AppendLine(@"    </Modifications>");
            xml.Append(Invariant($@"    <Instrument>{SecurityElementEscape(_fragmentIons)}</Instrument>{Environment.NewLine}"));
            xml.Append(Invariant($@"    <MS1Tol Unit=""{TolUnit(_precursorTol)}"">{_precursorTol.Value.ToString(CultureInfo.InvariantCulture)}</MS1Tol>{Environment.NewLine}"));
            xml.Append(Invariant($@"    <MS2Tol Unit=""{TolUnit(_fragmentTol)}"">{_fragmentTol.Value.ToString(CultureInfo.InvariantCulture)}</MS2Tol>{Environment.NewLine}"));
            xml.Append(Invariant($@"    <MaxRank>{AdditionalSettings[MAX_RANK].Value}</MaxRank>{Environment.NewLine}"));
            xml.AppendLine(@"    <GenerateDecoy>true</GenerateDecoy>");
            xml.AppendLine(@"    <PerformDeisotoping>true</PerformDeisotoping>");
            xml.Append(Invariant($@"    <MaxNoDynModifs>{AdditionalSettings[MAX_NO_DYN_MODIFS].Value}</MaxNoDynModifs>{Environment.NewLine}"));
            xml.AppendLine(@"    <MinimumPepLength>6</MinimumPepLength>");
            xml.AppendLine(@"    <MaximumPepLength>30</MaximumPepLength>");
            xml.AppendLine(@"  </SearchSettings>");
            xml.AppendLine(@"  <BasicSettings>");
            xml.AppendLine(@"    <Monoisotopic>true</Monoisotopic>");
            xml.Append(Invariant($@"    <ConsideredCharges>{SecurityElementEscape((string) AdditionalSettings[CONSIDERED_CHARGES].Value)}</ConsideredCharges>{Environment.NewLine}"));
            xml.AppendLine(@"    <CombineConsideredCharges>true</CombineConsideredCharges>");
            xml.Append(Invariant($@"    <LoadedProteinsAtOnce>{AdditionalSettings[MAX_LOADED_PROTEINS_AT_ONCE].Value}</LoadedProteinsAtOnce>{Environment.NewLine}"));
            xml.Append(Invariant($@"    <LoadedSpectraAtOnce>{AdditionalSettings[MAX_LOADED_SPECTRA_AT_ONCE].Value}</LoadedSpectraAtOnce>{Environment.NewLine}"));
            xml.AppendLine(@"    <DataFolder>DEFAULT</DataFolder>");
            xml.AppendLine(@"    <EnzymesFile>enzymes.xml</EnzymesFile>");
            xml.AppendLine(@"    <ModificationsFile>modifications.xml</ModificationsFile>");
            xml.AppendLine(@"  </BasicSettings>");
            xml.AppendLine(@"  <PercolatorSettings>");
            xml.AppendLine(@"    <GeneratePInFile>false</GeneratePInFile>");
            xml.AppendLine(@"    <RunPercolator>true</RunPercolator>");
            xml.AppendLine(@"  </PercolatorSettings>");
            xml.AppendLine(@"</Settings>");
            return xml.ToString();
        }

        private string BuildEnzymeXml()
        {
            if (_enzyme == null)
                return @"    <Enzyme Name=""Trypsin"" Specificity=""FULL"" />" + Environment.NewLine;

            string spec = _enzyme.IsSemiCleaving ? @"SEMI" : @"FULL";
            string cleavage = _enzyme.IsNTerm ? _enzyme.CleavageN : _enzyme.CleavageC;
            string restrict = _enzyme.IsNTerm ? _enzyme.RestrictN : _enzyme.RestrictC;
            string offset = _enzyme.IsNTerm ? @"before" : @"after";

            var sb = new StringBuilder();
            sb.Append(Invariant($@"    <Enzyme Name=""{SecurityElementEscape(_enzyme.Name)}"" Specificity=""{spec}"">{Environment.NewLine}"));
            sb.Append(Invariant($@"      <Cleavage CleavageSites=""{SecurityElementEscape(cleavage ?? string.Empty)}"" "));
            if (_enzyme.IsNTerm)
                sb.Append(Invariant($@"PostfixInhibitors=""{SecurityElementEscape(restrict ?? string.Empty)}"" "));
            else
                sb.Append(Invariant($@"PrefixInhibitors=""{SecurityElementEscape(restrict ?? string.Empty)}"" "));
            sb.Append(Invariant($@"Offset=""{offset}"" />{Environment.NewLine}"));
            sb.AppendLine(@"    </Enzyme>");
            return sb.ToString();
        }

        // MSAmanda takes either Unimod-named mods (e.g. "Oxidation(M)") or
        // DeltaMass-attributed mods. Prefer the Unimod name when Skyline has one,
        // otherwise fall back to the monoisotopic delta.
        private IEnumerable<string> ModificationLines(StaticMod mod, bool isFixed)
        {
            string name = mod.Name ?? mod.ShortName ?? @"Mod";
            string bareName = name.Split(' ')[0];
            // StaticMod.AAs holds the raw comma-separated residues (e.g. "K, R");
            // AminoAcids is the derived IEnumerable<char>. MSAmanda's params file
            // wants a bare residue array.
            char[] aas = mod.AAs?.Replace(@" ", "").Replace(@",", "").ToCharArray() ?? new char[0];
            string ntermAttr = mod.Terminus == ModTerminus.N ? @" Nterm=""true"" MaxOccurrences=""1""" : string.Empty;
            string ctermAttr = mod.Terminus == ModTerminus.C ? @" Cterm=""true"" MaxOccurrences=""1""" : string.Empty;
            string fixedAttr = isFixed ? @" Fix=""true""" : string.Empty;

            if (aas.Length == 0)
            {
                yield return Invariant(
                    $@"      <Modification{fixedAttr}{ntermAttr}{ctermAttr}{DeltaMassAttr(mod)}>{SecurityElementEscape(bareName)}</Modification>");
                yield break;
            }
            foreach (var aa in aas)
            {
                string body = Invariant($@"{SecurityElementEscape(bareName)}({aa})");
                yield return Invariant(
                    $@"      <Modification{fixedAttr}{ntermAttr}{ctermAttr}{DeltaMassAttr(mod)}>{body}</Modification>");
            }
        }

        private static string DeltaMassAttr(StaticMod mod)
        {
            // Only emit a DeltaMass override when Skyline has a mono mass and the
            // mod isn't from the Unimod dictionary MSAmanda's modifications.xml
            // already knows about. Cheap heuristic: if UnimodId is set, trust the name.
            if (mod.UnimodId.HasValue && mod.UnimodId.Value > 0)
                return string.Empty;
            if (!mod.MonoisotopicMass.HasValue)
                return string.Empty;
            return Invariant($@" DeltaMass=""{mod.MonoisotopicMass.Value.ToString(CultureInfo.InvariantCulture)}""");
        }

        private static string TolUnit(MzTolerance tol)
        {
            return tol.Unit == MzTolerance.Units.ppm ? @"ppm" : @"Da";
        }

        private static string Invariant(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

        private static string SecurityElementEscape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s ?? string.Empty;
            return s.Replace(@"&", @"&amp;").Replace(@"<", @"&lt;").Replace(@">", @"&gt;")
                    .Replace("\"", @"&quot;").Replace(@"'", @"&apos;");
        }

        #region IProgressMonitor

        public bool IsCanceled => _cancelToken != null && _cancelToken.IsCancellationRequested;

        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            _progressStatus = status;
            SearchProgressChanged?.Invoke(this, status);
            return IsCanceled ? UpdateProgressResponse.cancel : UpdateProgressResponse.normal;
        }

        public bool HasUI => false;

        #endregion
    }
}
