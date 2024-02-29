/*
 * Original author: Brian Pratt <bspratt .at. uw.edu >
 *
 * Copyright 2022 University of Washington - Seattle, WA
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

using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Skyline.Util.Extensions;
using System.Text;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Results.Spectra.Alignment;
using pwiz.Skyline.Model.RetentionTimes;
using Enzyme = pwiz.Skyline.Model.DocSettings.Enzyme;

namespace pwiz.Skyline.Model.DdaSearch
{
    public class HardklorSearchEngine : AbstractDdaSearchEngine, IProgressMonitor
    {
        private ImportPeptideSearch _searchSettings;

        private bool _keepIntermediateFiles;
        // Temp files we'll need to clean up at the end the end if !_keepIntermediateFiles
        private SortedDictionary<MsDataFileUri, string> _inputsAndOutputs; // .hk.bs.kro results files
        private string _isotopesFilename;
        private string _paramsFilename;
        private Dictionary<MsDataFileUri, SpectrumSummaryList> _spectrumSummaryLists = new Dictionary<MsDataFileUri, SpectrumSummaryList>();

        private Dictionary<Tuple<MsDataFileUri, MsDataFileUri>, KdeAligner> _alignments =
            new Dictionary<Tuple<MsDataFileUri, MsDataFileUri>, KdeAligner>();
        public static int MaxCharge = 7; // Look for charge states up to and including this
        public override void SetSpectrumFiles(MsDataFileUri[] searchFilenames)
        {
            SpectrumFileNames = searchFilenames;
            _paramsFilename = null;
            _searchSettings.RemainingStepsInSearch = searchFilenames.Length + 2; // One step for Hardklor, and one Bullseye per file, then unify the bullseye results
        }
        public HardklorSearchEngine(ImportPeptideSearch searchSettings)
        {
            _searchSettings = searchSettings;
        }


        private CancellationTokenSource _cancelToken;
        private IProgressStatus _progressStatus;
        private bool _success;

        public override string[] FragmentIons => Array.Empty<string>();
        public override string[] Ms2Analyzers => Array.Empty<string>();
        public override string EngineName => @"Hardklor";
        public override Bitmap SearchEngineLogo => Resources.HardklorLogo;

        public override event NotificationEventHandler SearchProgressChanged;

        public override bool Run(CancellationTokenSource cancelToken, IProgressStatus status)
        {
            using var tmpDir = new TempDir(); // Set TMP to a new directory that we'll destroy on exit
            _cancelToken = cancelToken;
            _progressStatus = status.ChangePercentComplete(0);
            _success = true;
            _isotopesFilename = null;

            var skylineWorkingDirectory = Settings.Default.ActiveDirectory;
            _keepIntermediateFiles = !string.IsNullOrEmpty(skylineWorkingDirectory);

            try
            {
                if (_searchSettings.RemainingStepsInSearch == 2)
                {
                    // Final step - try to unify similar features across the various Bullseye result files
                    _searchSettings.RemainingStepsInSearch--; // More to do after this?
                    FindSimilarFeatures();
                }
                else
                {
                    // Hardklor is not L10N ready, so take care to run its process under InvariantCulture
                    Func<string> RunHardklor = () =>
                    {
                        string exeName;
                        string args;
                        if (string.IsNullOrEmpty(_paramsFilename))
                        {
                            // First pass - run Hardklor
                            var paramsFileText = GenerateHardklorConfigFile(skylineWorkingDirectory);

                            RunNumber = 0;
                            void SetHardklorParamsFileName()
                            {
                                var versionString = RunNumber == 0 ? string.Empty : $@"_{RunNumber:000}";
                                _paramsFilename = string.IsNullOrEmpty(skylineWorkingDirectory)
                                    ? Path.GetTempFileName()
                                    : Path.Combine(skylineWorkingDirectory, $@"Hardklor{versionString}.conf");
                            }

                            for (SetHardklorParamsFileName(); File.Exists(_paramsFilename);)
                            {
                                RunNumber++;
                                SetHardklorParamsFileName(); // Avoid stomping previous runs
                            }
                            File.WriteAllText(_paramsFilename, paramsFileText.ToString());
                            exeName = @"Hardklor";
                            args = $@"""{_paramsFilename}""";
                        }
                        else
                        {
                            // Refine the Hardklor results with Bullseye
                            _searchSettings.RemainingStepsInSearch--; // More to do after this?
                            var pair = _inputsAndOutputs.ElementAt(_searchSettings.RemainingStepsInSearch - 2); // Last 2 steps are Bullseye then cleanup for blibbuild
                            var mzFile = pair.Key;
                            var hkFile = pair.Value;
                            var matchFile = GetBullseyeMatchFilename(hkFile);
                            var noMatchFile = GetBullseyeNoMatchFilename(hkFile);
                            exeName = @"BullseyeSharp";
                            var ppm = GetPPM();
                            args = $@"-c 0 " + // Don't eliminate long elutions
                                   $@"-r {ppm.ToString(CultureInfo.InvariantCulture)} " +
                                   $@"""{hkFile}"" ""{mzFile}"" ""{matchFile}"" ""{noMatchFile}""";
                        }
                        _progressStatus = status.ChangePercentComplete(0);
                        var pr = new ProcessRunner();
                        var psi = new ProcessStartInfo(exeName, args)
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            RedirectStandardInput = false,
                            StandardOutputEncoding = Encoding.UTF8,
                            StandardErrorEncoding = Encoding.UTF8
                        };
                        pr.ShowCommandAndArgs = true; // Show the commandline
                        pr.Run(psi, string.Empty, this, ref _progressStatus, ProcessPriorityClass.BelowNormal);
                        return _paramsFilename;
                    };
                    LocalizationHelper.CallWithCulture(CultureInfo.InvariantCulture, RunHardklor);
                }
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
                _cancelToken.Cancel();
            }

            if (_success)
                _progressStatus = _progressStatus.Complete().ChangeMessage(Resources.DDASearchControl_SearchProgress_Search_done);
            UpdateProgress(_progressStatus);

            if (!_keepIntermediateFiles)
            {
                FileEx.SafeDelete(_paramsFilename, true);
            }

            return _success;
        }

        private double GetPPM()
        {
            var resolution = GetResolution();
            var ppm = resolution == 0 ? 10 : ((1.0 / resolution) * 1E6); // 10 is the Bullseye default
            return ppm;
        }

        private class hkFeatureDetail
        {
            public double mzObserved; // Observed mz
            public string massString; // Original text representation of mono mass
            public string avergineAndOffset; // The value declared in the avergine column e.g. "H104C54N15O16[+4.761216]"
            public double quality; // Define relative quality for sorting as "Best Correlation" column x "Num of Scans" column (i.e. Points across peak * correlation)
            public double rt; // "Best RTime" column
            public double rtAligned; // RT of best scoring occurance when found in more than one file
            public int charge; // Declared z
            public int fileIndex; // Which file it's from
            public int lineIndex; // Which line in that file
            public bool updated; // Does it need to be written back to the file?
        }

        private void FindSimilarFeatures()
        {
            // Final step - try to unify similar features in the various Bullseye result files
            var bfiles = SpectrumFileNames.Select(GetSearchResultFilepath).ToArray();
            if (bfiles.Length > 1)
            {
                
                var ppm = GetPPM();
                bool SimilarMz(double mzJ, double mzI)
                {
                    var mzDiff = (Math.Abs(mzJ - mzI) / mzI) * 1.0E6;
                    return mzDiff <= ppm;
                }

                _progressStatus = _progressStatus.ChangeMessage(string.Empty);
                _progressStatus = _progressStatus.ChangePercentComplete(0).ChangeMessage(DdaSearchResources.HardklorSearchEngine_FindSimilarFeatures_Looking_for_features_occurring_in_multiple_runs);

                PerformAllAlignments();

                // The shape of the isotope distribution is pretty much determined by everything but
                // hydrogen, so create a lookup based on avergine formulas with H removed
                var featuresByCNOS = new Dictionary<string, List<hkFeatureDetail>>();
                var featuresAll = new List<hkFeatureDetail>();
                var contents = new List<string[]>();
                for (var fileIndex = 0; fileIndex < bfiles.Length; fileIndex++)
                {
                    var file = bfiles[fileIndex];
                    var lines = File.ReadAllLines(file);
                    contents.Add(lines);
                    int l = 1;
                    foreach (var line in lines.Skip(1))
                    {
                        var col = line.Split('\t');
                        var averagine_no_H = col[15].Split('[')[0].Split('C')[1]; // e.g. "H120C119N33O35S1[+0.12]" => "119N33O35S1"
                        var rtParsed = double.Parse(col[11], CultureInfo.InvariantCulture);
                        var feature = new hkFeatureDetail()
                        {
                            charge = int.Parse(col[4], CultureInfo.InvariantCulture),
                            massString = col[5],
                            mzObserved = double.Parse(col[6], CultureInfo.InvariantCulture),
                            quality = double.Parse(col[3], CultureInfo.InvariantCulture) * double.Parse(col[12], CultureInfo.InvariantCulture), // Points across peak * correlation, for sorting
                            rt = rtParsed,
                            rtAligned = rtParsed,
                            avergineAndOffset = col[15],
                            fileIndex = fileIndex,
                            lineIndex = l,
                            updated = false
                        };
                        featuresAll.Add(feature);

                        if (!featuresByCNOS.TryGetValue(averagine_no_H, out var featureList))
                        {
                            featuresByCNOS.Add(averagine_no_H, new List<hkFeatureDetail>() { feature });
                        }
                        else
                        {
                            featureList.Add(feature);
                        }

                        l++;
                    }
                }

                // We have a dictionary of all known isotope distributions (that is, the averagine formulas with H)
                // and all known masses and intensities
                var rtToler = this._searchSettings.SettingsHardklor.FeatureRetentionTimeTolerance ?? double.MaxValue;
                foreach (var featureByCNOS in featuresByCNOS)
                {
                    // Starting with the highest score, see if any other entries from similar RT are similar m/z.
                    // If they are, update the mass information to match the more solid ID.
                    var ordered = featureByCNOS.Value.OrderByDescending(v => v.quality).ToArray();
                    for (var i = 0; i < ordered.Length; i++)
                    {
                        var hkFeatureDetailI = ordered[i];
                        if (hkFeatureDetailI.updated)
                        {
                            continue;
                        }

                        var chargeI = hkFeatureDetailI.charge;

                        var mzI = hkFeatureDetailI.mzObserved;
                        if (mzI == 0)
                        {
                            continue;
                        }

                        for (var j = i + 1; j < ordered.Length; j++)
                        {
                            var hkFeatureDetailJ = ordered[j];
                            if (hkFeatureDetailJ.updated)
                            {
                                continue; // Already processed
                            }

                            if (hkFeatureDetailJ.charge != chargeI)
                            {
                                continue;
                            }

                            if (SimilarMz(hkFeatureDetailJ.mzObserved, mzI))
                            {
                                // Isotope envelopes agree, m/z is similar, so it's just a tiny mass shift - does RT agree?
                                var fileI = SpectrumFileNames[hkFeatureDetailI.fileIndex];
                                var fileJ = SpectrumFileNames[hkFeatureDetailJ.fileIndex];
                                var rtI = hkFeatureDetailI.rt;
                                var rtJ = hkFeatureDetailJ.rt;
                                if (_alignments.TryGetValue(Tuple.Create(fileI, fileJ), out var alignment))
                                {
                                    rtI = alignment.GetValue(rtJ);
                                }
                                if (Math.Abs(rtI - rtJ) > rtToler)
                                {
                                    continue; // Not an RT match
                                }
                                hkFeatureDetailJ.massString = hkFeatureDetailI.massString;
                                hkFeatureDetailJ.rtAligned = hkFeatureDetailI.rtAligned;
                                hkFeatureDetailJ.avergineAndOffset = hkFeatureDetailI.avergineAndOffset;
                                hkFeatureDetailJ.updated = true; // No need to compare to others in this list
                            }
                        }
                    }
                }

                // We asked Hardklor to write its mass values at greater than normal precision, let's peel
                //  that back to 4 decimal places, but we have to watch for collisions
                var featuresByRoundedMass = new Dictionary<string, List<hkFeatureDetail>>();
                foreach (var feature in featuresAll)
                {
                    var rounded = double.Parse(feature.massString, CultureInfo.InvariantCulture)
                        .ToString(@"0.0000", CultureInfo.InvariantCulture);
                    if (!featuresByRoundedMass.TryGetValue(rounded, out var featureList))
                    {
                        featuresByRoundedMass.Add(rounded, new List<hkFeatureDetail>() { feature });
                    }
                    else
                    {
                        featureList.Add(feature);
                    }
                }
                foreach (var byRoundedMass in featuresByRoundedMass)
                {
                    if (byRoundedMass.Value.Count == 1)
                    {
                        var feat = byRoundedMass.Value[0];
                        feat.massString = byRoundedMass.Key;
                        feat.updated = true;
                    }
                    else
                    {
                        var features = byRoundedMass.Value;
                        var masses = features
                            .Select(v => double.Parse(v.massString, CultureInfo.InvariantCulture))
                            .ToArray();
                        for (var digits = 4; digits < 9; digits++)
                        {
                            var format = $@"0.{new string('0', digits)}";
                            var revisedMassStrings = masses.Select(v => v.ToString(format, CultureInfo.InvariantCulture)).ToArray();
                            var success = true;
                            // Can use this precision only if there's no conflict in the averagines
                            for (var i = 0; success && i < masses.Length; i++)
                            {
                                for (var j = i + 1; j < masses.Length; j++)
                                {
                                    if (revisedMassStrings[i] == revisedMassStrings[j] &&
                                        !features[i].avergineAndOffset.Equals(features[j].avergineAndOffset))
                                    {
                                        success = false;
                                        break;
                                    }
                                }
                            }

                            if (success)
                            {
                                for (var i = 0; i < features.Count; i++)
                                {
                                    features[i].massString = revisedMassStrings[i];
                                    features[i].updated = true;
                                }
                                break;
                            }
                        }
                    }
                }

                // Now write back any adjustments before we hand off the files to BlibBuild, so it understands 
                // that the IDs we matched are the same things
                foreach (var update in featuresAll.Where(u => u.updated))
                {
                    var col = contents[update.fileIndex][update.lineIndex].Split('\t');
                    col[5] = update.massString;
                    col[15] = update.avergineAndOffset;
                    contents[update.fileIndex][update.lineIndex] = string.Join(TextUtil.SEPARATOR_TSV_STR, col);
                }

                // Now add the column for feature names, where name consists of mass and aligned RT e.g. mass123.45_RT23.45678
                for (var f = 0; f < bfiles.Length; f++)
                {
                    contents[f][0] += $@"{TextUtil.SEPARATOR_TSV_STR}FeatureName"; // This must agree with pwiz_tools\BiblioSpec\src\HardklorReader.cpp
                }
                foreach (var hkFeatureDetail in featuresAll)
                {
                    var featureName =$@"mass{hkFeatureDetail.massString}_RT{hkFeatureDetail.rtAligned.ToString(@"0.0000", CultureInfo.InvariantCulture)}";
                    contents[hkFeatureDetail.fileIndex][hkFeatureDetail.lineIndex] += $@"{TextUtil.SEPARATOR_TSV_STR}{featureName}";
                }

                // And write it all back, preserving a copy of the original
                for (var f = 0; f < bfiles.Length; f++)
                {
                    // Note the original
                    var unaligned = GetBullseyeKronikUnalignedFilename(bfiles[f]);
                    FileEx.SafeDelete(unaligned);
                    File.Copy(bfiles[f], unaligned);
                    // Update for handoff to BiblioSpec
                    File.WriteAllLines(bfiles[f], contents[f]);  // Update the current file
                }
            }
        }


        private static string GetBullseyeKronikFilename(string hkFile)
        {
            return hkFile + @".bs.kro";
        }

        private static string GetBullseyeKronikUnalignedFilename(string kronikFile)
        {
            return kronikFile + @".unaligned";
        }

        private static string GetBullseyeNoMatchFilename(string hkFile)
        {
            return Path.ChangeExtension(hkFile, @".nomatch.ms2");
        }

        private static string GetBullseyeMatchFilename(string hkFile)
        {
            return Path.ChangeExtension(hkFile, @".match.ms2");
        }


        public override void SetEnzyme(Enzyme enz, int mmc)
        {
            // Not applicable to Hardklor
        }

        public override void SetFragmentIonMassTolerance(MzTolerance mzTolerance)
        {
            // Not applicable to Hardklor
        }

        public override void SetFragmentIons(string ions)
        {
            // Not applicable to Hardklor
        }

        public override void SetMs2Analyzer(string ms2Analyzer)
        {
            // not used by Hardklor
        }

        public override void SetPrecursorMassTolerance(MzTolerance mzTolerance)
        {
            // Not applicable to Hardklor
        }

        public override string GetSearchResultFilepath(MsDataFileUri searchFilepath)
        {
            return GetBullseyeKronikFilename(_inputsAndOutputs[searchFilepath]);
        }

        private string[] SupportedExtensions = { @".mzml", @".mzxml" }; // TODO - build Hardklor+MSToolkit to use pwiz so we don't have to convert to mzML
        public override bool GetSearchFileNeedsConversion(MsDataFileUri searchFilepath, out AbstractDdaConverter.MsdataFileFormat requiredFormat)
        {
            requiredFormat = AbstractDdaConverter.MsdataFileFormat.mzML;
            if (!SupportedExtensions.Contains(e => e == searchFilepath.GetExtension().ToLowerInvariant()))
                return true;
            return false;
        }

        public bool IsCanceled => _cancelToken.IsCancellationRequested;
        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            SearchProgressChanged?.Invoke(this, status);
            return _cancelToken.IsCancellationRequested ? UpdateProgressResponse.cancel : UpdateProgressResponse.normal;
        }

        public bool HasUI => false;

        public override void SetModifications(IEnumerable<StaticMod> modifications, int maxVariableMods_)
        {
            // Not applicable for Hardklor
        }

        public override void Dispose()
        {
            if (!_keepIntermediateFiles)
            {
                FileEx.SafeDelete(_paramsFilename, true);
                FileEx.SafeDelete(_isotopesFilename, true);
                if (_inputsAndOutputs != null)
                {
                    foreach (var hkFile in _inputsAndOutputs.Values)
                    {
                        FileEx.SafeDelete(hkFile, true); // The hardklor .hk file
                        var bullseyeKronikFilename = GetBullseyeKronikFilename(hkFile);
                        FileEx.SafeDelete(bullseyeKronikFilename, true); // The Bullseye result file
                        FileEx.SafeDelete(GetBullseyeKronikUnalignedFilename(bullseyeKronikFilename), true); // The Bullseye result file before we aligned it
                        FileEx.SafeDelete(GetBullseyeMatchFilename(hkFile), true);
                        FileEx.SafeDelete(GetBullseyeNoMatchFilename(hkFile), true);
                    }
                }
            }
        }

        [Localizable(false)]
        private void InitializeIsotopes()
        {
            // Make sure Hardklor is working with the same isotope information as Skyline
            _isotopesFilename = Path.GetTempFileName();
            var isotopeValues = new List<string>
            {
                // First few lines are particular to Hardklor
                "X  2", "1  0.9", "2  0.1", string.Empty
            };
            var abundances = IsotopeAbundances.Default; // A map of element -> [ mass,abundance, mass, abundance, ...]
            // These are the elements listed in  CMercury8::DefaultValues() in pwiz_tools\Skyline\Executables\Hardklor\Hardklor\CMercury8.cpp
            foreach (var element in new [] { 
                         // "X", ?? appears in standard file, no Skyline equivalent - see hardcoded values above
                         "H","He","Li","Be","B","C","N","O","F","Ne","Na","Mg","Al","Si","P","S","Cl","Ar",
                         "K","Ca","Sc","Ti","V","Cr","Mn","Fe","Co","Ni","Cu","Zn","Ga","Ge","As","Se","Br","Kr","Rb","Sr","Y","Zr",
                         "Nb","Mo","Tc","Ru","Rh","Pd","Ag","Cd","In","Sn","Sb","Te","I","Xe","Cs","Ba","La","Ce","Pr","Nd","Pm","Sm",
                         "Eu","Gd","Tb","Dy","Ho","Er","Tm","Yb","Lu","Hf","Ta","W","Re","Os","Ir","Pt","Au","Hg","Tl","Pb","Bi","Po",
                         "At","Rn","Fr","Ra","Ac","Th","Pa","U","Np","Pu","Am","Cm","Bk","Cf","Es","Fm","Md","No","Lr",
                         "Hx","Cx","Nx", "Ox","Sx"}) // These are just repeats of H, C, N, O, S in the standard file
            {
                var massDistribution = abundances[element.EndsWith("x")? element.Substring(0,1) : element];
                isotopeValues.Add($"{element}  {massDistribution.Values.Count}");
                for (var i = 0; i < massDistribution.Values.Count; i++)
                {
                    isotopeValues.Add($"{massDistribution.Keys[i]}  {massDistribution.Values[i]} ");
                }
                isotopeValues.Add(string.Empty);
            }

            File.AppendAllLines(_isotopesFilename, isotopeValues);
        }

        private static int RunNumber { get; set; } // Used in filename creation to avoid stomping previous results
        
        private string GenerateHardklorConfigFile(string skylineWorkingDirectory)
        {
            _inputsAndOutputs = new SortedDictionary<MsDataFileUri, string>();
            var workingDirectory = string.IsNullOrEmpty(skylineWorkingDirectory) ? Path.GetTempPath() : skylineWorkingDirectory;
            int? isCentroided = null;

            foreach (var input in SpectrumFileNames)
            {
                string outputHardklorFile;

                void SetHardklorOutputFilename()
                {
                    var version = RunNumber == 0 ? string.Empty : $@"_{RunNumber:000}";
                    outputHardklorFile = $@"{Path.Combine(workingDirectory, input.GetFileName())}{version}.hk";
                }

                for (SetHardklorOutputFilename(); File.Exists(outputHardklorFile);)
                {
                    RunNumber++;
                    SetHardklorOutputFilename(); // Don't stomp existing results
                }
                _inputsAndOutputs.Add(input, outputHardklorFile);
                if (!isCentroided.HasValue)
                {
                    // Hardklor wants to know if the data is centroided, we should
                    // find a clue within the first few hundred lines of mzML.
                    using var reader = new StreamReader(input.GetFilePath());
                    for (var lineNum = 0; lineNum < 500; lineNum++)
                    {
                        var line = reader.ReadLine();
                        if (line == null)
                        {
                            break; // EOF
                        }
                        if (line.Contains(@"MS:1000127") || line.Contains(@"centroid spectrum"))
                        {
                            isCentroided = 1;
                            break;
                        }
                        else if (line.Contains(@"MS:1000128") || line.Contains(@"profile spectrum"))
                        {
                            isCentroided = 0;
                            break;
                        }
                    }
                }
            }

            // Make sure Hardklor is working with the same isotope information as Skyline
            InitializeIsotopes();

            var instrument = _searchSettings.SettingsHardklor.Instrument;
            var resolution = GetResolution();

            return TextUtil.LineSeparate(
                $@"# comments in ALL CAPS are from a discussion with Danielle Faivre about Skyline integration",
                $@"",
                $@"# Please see online documentation for detailed explanations: ",
                $@"# http://proteome.gs.washington.edu/software/hardklor",
                $@"",
                $@"# All parameters are separated from their values by an equals sign ('=')",
                $@"# Anything after a '#' will be ignored for the remainder of the line.",
                $@"# All data files (including paths if necessary) to be analyzed are discussed below.",
                $@"",
                $@"# Parameters used to described the data being input to Hardklor",
                $@"instrument	=	{TransitionFullScan.MassAnalyzerToString(instrument).Replace(@"-", string.Empty)}	#Values are: FTICR, Orbitrap, TOF, QIT #NEED UI",
                $@"resolution	=	{resolution}		#Resolution at 400 m/z #NEED UI",
                $@"centroided	=	{isCentroided??1}			#0=no, 1=yes",
                $@"",
                $@"# Parameters used in preprocessing spectra prior to analysis",
                $@"ms_level			=	1		#1=MS1, 2=MS2, 3=MS3, 0=all",
                $@"scan_range_min		=	0		#ignore any spectra lower than this number, 0=off",
                $@"scan_range_max		=	0		#ignore any spectra higher than this number, 0=off",
                $@"signal_to_noise		=	{_searchSettings.SettingsHardklor.SignalToNoise}		#set signal-to-noise ratio, 0=off #NEED UI",
                $@"sn_window			=	250.0	#size in m/z for computing localized noise level in a spectrum.",
                $@"static_sn			=	0		#0=off, 1=on. Apply lowest localized noise level to entire spectrum.",
                $@"boxcar_averaging	=	0		#0=off, or specify number of scans to average together, use odd numbers only #MAY NEED UI IN FUTURE",
                $@"boxcar_filter		=	0		#0=off, when using boxcar_averaging, only keep peaks seen in this number of scans #MAY NEED UI IN FUTURE",
                $@"								#  currently being averaged together. When on, signal_to_noise is not used.",
                $@"boxcar_filter_ppm	=	5		#Tolerance in ppm for matching peaks across spectra in boxcar_filter #MAY NEED UI IN FUTURE",
                $@"mz_min				=	0		#Sets lower bound of spectrum m/z range to analyze, 0=off",
                $@"mz_max				=	0		#Sets upper bound of spectrum m/z range to analyze, 0=off",
                $@"smooth				=	0		#Peforms Savitzky-Golay smoothing of peaks data. 0=off",
                $@"								#  Not recommended for high resolution data.",
                $@"",
                $@"# Parameters used to customize the Hardklor analysis. Some of these parameters will drastically",
                $@"# affect the analysis speed and results. Please consult the documentation and choose carefully!",
                $@"algorithm			=	Version2	#Algorithms include: Basic, Version1, Version2",
                $@"charge_algorithm	=	Quick		#Preferred method for feature charge identification.",
                $@"									#  Values are: Quick, FFT, Patterson, Senko, None",
                $@"									#  If None is set, all charge states are assumed, slowing Hardklor",
                $@"charge_min			=	1			#Lowest charge state allowed in the analysis. #MAY NEED UI IN FUTURE",
                $@"charge_max			=	{MaxCharge}			#Highest charge state allowed in the analysis. #MAY NEED UI IN FUTURE",
                $@"correlation			=	{_searchSettings.SettingsHardklor.CorrelationThreshold}	#Correlation threshold to accept a peptide feature. #NEED UI",
                $@"averagine_mod		=	0			#Formula containing modifications to the averagine model.",
                $@"									#  Read documentation carefully before using! 0=off",
                $@"mz_window			=	5.25		#Breaks spectrum into windows not larger than this value for Version1 algorithm.",
                $@"sensitivity			=	2			#Values are 0 (lowest) to 3 (highest). Increasing sensitivity",
                $@"									#  identifies more features near the noise where the isotope distribution",
                $@"									#  may not be fully visible. However, these features are also more",
                $@"									#  likely to be false.",
                $@"depth				=	2			#Depth of combinatorial analysis. This is the maximum number of overlapping",
                $@"									#  features allowed in any mz_window. Each increase requires exponential",
                $@"									#  computation. In other words, keep this as low as necessary!!!",
                $@"max_features		=	12			#Maximum number of potential features in an mz_window to combinatorially solve.",
                $@"									#  Setting this too high results in wasted computation time trying to mix-and-match",
                $@"									#  highly improbable features.",
                $@"molecule_max_mz 	= 	5000		#Maximum m/z of molecules to detect. Set this higher than largest expected molecule.",
                $@"",
                $@"# Parameters used by Skyline",
                $@"report_averagine		=	1		# include feature's averagine formula and mass shift in report e.g. C12H5[+1.23]",
                $@"",
                $@"# Parameters used to customize the Hardklor output",
                $@"distribution_area	=	1	#Report sum of distribution peaks instead of highest peak only. 0=off, 1=on",
                $@"xml					=	0	#Output results as XML. 0=off, 1=on #MAY NEED UI IN FUTURE",
                $@"",
                $@"isotope_data	=	""{_isotopesFilename}""	# Using Skyline's isotope abundance values",
                $@"",
                $@"# Below this point is where files to be analyzed should go. They should be listed contain ",
                $@"# both the input file name, and the output file name. Each file to be analyzed should begin ",
                $@"# on a new line. By convention Hardklor output should have this extension: .hk",
                $@"",
                TextUtil.LineSeparate(_inputsAndOutputs.Select(kvp => ($@"""{kvp.Key}""	""{kvp.Value}""")))
            );
        }

        private double GetResolution()
        {
            var resolution = _searchSettings.SettingsHardklor.Resolution;
            if (Equals(_searchSettings.SettingsHardklor.Instrument, FullScanMassAnalyzerType.qit))
            {
                resolution =
                    resolution / 5000.0; // per Hardklor source code CHardklor2::CalcFWHM(double mz, double res, int iType)
            }
            return resolution;
        }

        public static SpectrumSummaryList LoadSpectrumSummaries(MsDataFileUri msDataFileUri)
        {
            var summaries = new List<SpectrumSummary>();
            MsDataFileImpl dataFile;
            try
            {
                // Only need MS1 for our purposes, and centroided data if possible
                dataFile = msDataFileUri.OpenMsDataFile(false, true, true, true, false);
                dataFile.GetSpectrum(0);
            }
            catch (Exception)
            {
                // Retry on the chance that the failure was inability to do centroiding
                dataFile = msDataFileUri.OpenMsDataFile(false, true, false, false, false);
            }
            using (dataFile)
            {
                foreach (var spectrumIndex in Enumerable.Range(0, dataFile.SpectrumCount))
                {
                    var spectrum = dataFile.GetSpectrum(spectrumIndex);
                    summaries.Add(SpectrumSummary.FromSpectrum(spectrum));
                }
            }

            return new SpectrumSummaryList(summaries);
        }

        public KdeAligner PerformAlignment(SpectrumSummaryList spectra1, SpectrumSummaryList spectra2)
        {
            var similarityMatrix = spectra1.GetSimilarityMatrix(this, this._progressStatus, spectra2);
            var kdeAligner = new KdeAligner();
            var pointsToBeAligned = similarityMatrix.FindBestPath(false).ToList();
            kdeAligner.Train(pointsToBeAligned.Select(pt=>pt.X).ToArray(), pointsToBeAligned.Select(pt=>pt.Y).ToArray(), CancellationToken.None);
            return kdeAligner;
        }

        public void PerformAllAlignments()
        {
            foreach (var path in SpectrumFileNames)
            {
                _spectrumSummaryLists[path] = LoadSpectrumSummaries(path);
            }

            foreach (var entry1 in _spectrumSummaryLists)
            {
                foreach (var entry2 in _spectrumSummaryLists)
                {
                    if (Equals(entry1.Key, entry2.Key))
                    {
                        continue;
                    }

                    _progressStatus = _progressStatus.ChangeMessage(
                        string.Format(DdaSearchResources.HardklorSearchEngine_PerformAllAlignments_Performing_retention_time_alignment__0__vs__1_, entry1.Key.GetFileName(), entry2.Key.GetFileName()));

                    try
                    {
                        _alignments[Tuple.Create(entry1.Key, entry2.Key)] =
                            PerformAlignment(entry1.Value, entry2.Value);
                    }
                    catch (Exception x)
                    {
                        Trace.TraceWarning(@"Error performing alignment between {0} and {1}: {2}", entry1.Key, entry2.Key, x);
                    }
                }
            }
        }
    }
}
 