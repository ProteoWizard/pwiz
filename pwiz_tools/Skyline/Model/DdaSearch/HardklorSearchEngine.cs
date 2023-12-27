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

                            _paramsFilename = string.IsNullOrEmpty(skylineWorkingDirectory)
                                ? Path.GetTempFileName()
                                : Path.Combine(skylineWorkingDirectory, @"Hardklor.conf");
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
                _progressStatus = _progressStatus.ChangeErrorException(ex).ChangeMessage(string.Format(Resources.DdaSearch_Search_failed__0, ex.Message));
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
            public double mass; // Declared mono mass
            public string massString; // Original text representation of mono mass
            public string avergineAndOffset; // The value declared in the avergine column e.g. "H104C54N15O16[+4.761216]"
            public double intensity; // "Summed Intensity" column
            public double rt; // "Best RTime" column
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
                // The shape of the isotope distribution is pretty much determined by everything but
                // hydrogen, so create a lookup based on avergine formulas with H removed
                var featuresByCNOS = new Dictionary<string, List<hkFeatureDetail>>();
                var contents = new List<string[]>();
                var updatedFiles = new HashSet<int>();
                var updates = new List<hkFeatureDetail>();
                for (var fileIndex = 0; fileIndex < bfiles.Length; fileIndex++)
                {
                    var file = bfiles[fileIndex];
                    var lines = File.ReadAllLines(file);
                    contents.Add(lines);
                    int l = 1;
                    foreach (var line in lines.Skip(1))
                    {
                        var col = line.Split('\t');
                        var averagine_no_H = col[15].Split('[')[0].Split('C')[1];
                        var feature = new hkFeatureDetail()
                        {
                            charge = int.Parse(col[4], CultureInfo.InvariantCulture),
                            mass = double.Parse(col[5], CultureInfo.InvariantCulture),
                            massString = col[5],
                            avergineAndOffset = col[15],
                            intensity = double.Parse(col[8], CultureInfo.InvariantCulture),
                            rt = double.Parse(col[11], CultureInfo.InvariantCulture),
                            fileIndex = fileIndex,
                            lineIndex = l,
                            updated = false
                        };
                        var mzCalc = Adduct.FromChargeProtonated(feature.charge).MzFromNeutralMass(feature.mass, MassType.Monoisotopic);
                        var mzDeclared = double.Parse(col[6], CultureInfo.InvariantCulture); // 'Base Isotope Peak'
                        if (Math.Abs(mzCalc - mzDeclared) > .001)
                        {
                            // We're interested in the m/z of the mono peak, which may not be the biggest in the envelope
                            col[6] = mzCalc.ToString(@"F4", CultureInfo.InvariantCulture);
                            lines[l] = string.Join(TextUtil.SEPARATOR_TSV_STR, col);
                            updatedFiles.Add(fileIndex); // We'll want to write that back to the file
                        }

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
                var ppm = GetPPM();
                var rtToler = this._searchSettings.SettingsHardklor.FeatureRetentionTimeTolerance ?? double.MaxValue;
                foreach (var featureByCNOS in featuresByCNOS)
                {
                    // Starting with the strongest signal, see if any other entries from similar RT are similar mass.
                    // If they are, update the mass information to match the more solid ID.
                    var ordered = featureByCNOS.Value.OrderByDescending(v => v.intensity).ToArray();
                    for (var i = 0; i < ordered.Length; i++)
                    {
                        var hkFeatureDetailI = ordered[i];
                        if (hkFeatureDetailI.updated)
                        {
                            continue;
                        }
                        var mass = hkFeatureDetailI.mass;
                        if (mass == 0)
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
                            if (Math.Abs(hkFeatureDetailI.rt - hkFeatureDetailJ.rt) > rtToler)
                            {
                                continue; // Not an RT match
                            }
                            var diff = Math.Abs((hkFeatureDetailJ.mass - mass) / mass) * 1.0E6;
                            if (diff <= ppm)
                            {
                                hkFeatureDetailJ.mass = mass;
                                hkFeatureDetailJ.massString = hkFeatureDetailI.massString;
                                hkFeatureDetailJ.avergineAndOffset = hkFeatureDetailI.avergineAndOffset;
                                hkFeatureDetailJ.updated = true; // No need to compare to others in this list
                                updates.Add(hkFeatureDetailJ);  // We will write these values back to the file
                            }
                        }
                    }
                }

                // Now write back any adjustments before we hand off the files to BlibBuild
                foreach (var update in updates.Where(u => u.updated))
                {
                    var col = contents[update.fileIndex][update.lineIndex].Split('\t');
                    col[15] = update.avergineAndOffset;
                    col[5] = update.massString; 
                    col[6] = Adduct.FromChargeProtonated(update.charge).MzFromNeutralMass(update.mass, MassType.Monoisotopic).ToString(@"F4", CultureInfo.InvariantCulture);
                    contents[update.fileIndex][update.lineIndex] = string.Join(TextUtil.SEPARATOR_TSV_STR, col);
                    updatedFiles.Add(update.fileIndex);
                }
                foreach (var f in updatedFiles)
                {
                    File.WriteAllLines(bfiles[f], contents[f]);  // Update the current file
                }
            }
        }


        private static string GetBullseyeKronikFilename(string hkFile)
        {
            return hkFile + @".bs.kro";
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
                        FileEx.SafeDelete(GetBullseyeKronikFilename(hkFile), true); // The Bullseye result file
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

        private string GenerateHardklorConfigFile(string skylineWorkingDirectory)
        {
            _inputsAndOutputs = new SortedDictionary<MsDataFileUri, string>();
            var workingDirectory = string.IsNullOrEmpty(skylineWorkingDirectory) ? Path.GetTempPath() : skylineWorkingDirectory;
            int? isCentroided = null;

            foreach (var input in SpectrumFileNames)
            {
                _inputsAndOutputs.Add(input, $@"{Path.Combine(workingDirectory, input.GetFileName())}.hk");
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
    }
}
 