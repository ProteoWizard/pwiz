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
using pwiz.Common.Collections;

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
        public override string CutoffScoreName => string.Empty; // not used for now
        public override string CutoffScoreLabel => DdaSearchResources.HardklorSearchEngine_CutoffScoreLabel_Min_isotope__dot_product_;
        public override double DefaultCutoffScore => 0; // not used for now
        public override Bitmap SearchEngineLogo => Resources.HardklorLogo;

        public override string SearchEngineBlurb => DdaSearchResources.HardklorSearchEngine_SearchEngineBlurb;

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
                    _success = AlignReplicates();
                    if (_success)
                    {
                        _success = FindSimilarFeatures();
                        if (!_success)
                        {
                            _progressStatus = _progressStatus.ChangeMessage(string.Format(DdaSearchResources.DdaSearch_Search_failed__0, DdaSearchResources.HardklorSearchEngine_Run_See_Hardklor_Bullseye_log_for_details));
                        }
                    }
                }
                else
                {
                    // Hardklor is not L10N ready, so take care to run its process under InvariantCulture
                    Func<string> RunHardklor = () =>
                    {
                        string exeName;
                        string args;
                        string message;
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
                            message = string.Format(DdaSearchResources.HardklorSearchEngine_Run_Searching_for_peptide_like_features_with__0_, exeName);
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
                                   @"--timer " + // Show performance info
                                   $@"""{hkFile}"" ""{mzFile}""";
                            // + " ""{matchFile}"" ""{noMatchFile}"""; // We're not messing with MS2 (yet?)
                            message = string.Format(DdaSearchResources.HardklorSearchEngine_Run_Searching_for_persistent_features_in_Hardklor_results_with__0_, exeName);
                        }
                        UpdateProgress(_progressStatus = _progressStatus.ChangeSegmentName(message));
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

        private class hkFeatureDetail : IEquatable<hkFeatureDetail>
        {
            public double mzObserved; // Observed mz
            public string strMass; // Text representation of mono mass
            public double parsedMass; // Parsed value of original mono mass text
            public string avergineAndOffset; // The value declared in the avergine column e.g. "H104C54N15O16[+4.761216]"
            public double summedIntensity; // The value declared in the "Summed Intensity" column
            public double quality => summedIntensity; // Define relative quality for sorting before attempting sample to sample peak unification
            public double rt; // "Best RTime" column value
            public double rtStart; // "First RTime" column value, plus some tolerance to the left
            public double rtEnd; // "Last RTime" column value, plus some tolerance to the right
            public double rtAligned; // RT of best scoring occurence when found in more than one file
            public int charge; // Declared z
            public int fileIndex; // Which file it's from
            public int lineIndex; // Which line in that file
            public bool discard; // Set true when a Hardklor feature should not be passed along to BiblioSpec
            public List<hkFeatureDetail> alignsWith; // Set of features determined to also be occurrences of this feature

            public string strMassAndAlignedRT => FormatMassAndAlignedRT(strMass, rtAligned);
            public static string FormatMassAndAlignedRT(string strMass, double rtAligned) => $@"mass{strMass}_RT{rtAligned.ToString(@"0.0000", CultureInfo.InvariantCulture)}";

            public override string ToString()
            {
                return $@"f={fileIndex}:{lineIndex} q={quality} m={strMass} z={charge} s={rtStart} t={rt} e={rtEnd} mz={mzObserved} {avergineAndOffset}";
            }

            public bool Equals(hkFeatureDetail other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return fileIndex == other.fileIndex && lineIndex == other.lineIndex;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((hkFeatureDetail)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = mzObserved.GetHashCode();
                    hashCode = (hashCode * 397) ^ (strMass != null ? strMass.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ parsedMass.GetHashCode();
                    hashCode = (hashCode * 397) ^ (avergineAndOffset != null ? avergineAndOffset.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ summedIntensity.GetHashCode();
                    hashCode = (hashCode * 397) ^ rt.GetHashCode();
                    hashCode = (hashCode * 397) ^ rtStart.GetHashCode();
                    hashCode = (hashCode * 397) ^ rtEnd.GetHashCode();
                    hashCode = (hashCode * 397) ^ rtAligned.GetHashCode();
                    hashCode = (hashCode * 397) ^ charge;
                    hashCode = (hashCode * 397) ^ fileIndex;
                    hashCode = (hashCode * 397) ^ lineIndex;
                    hashCode = (hashCode * 397) ^ discard.GetHashCode();
                    hashCode = (hashCode * 397) ^ CollectionUtil.GetHashCodeDeep(alignsWith.Select(a => (a.fileIndex, a.lineIndex)).ToArray());
                    return hashCode;
                }
            }
        }

        private List<hkFeatureDetail> _featuresAll = new List<hkFeatureDetail>();
        private Dictionary<string, int[]> _featureMatches;
        private Dictionary<string, hkFeatureDetail[]> _orderedFeaturesByCNOS;
        private string[] _bfiles;
        private List<string[]> _contents = new List<string[]>();
        private double[] _summedIntensityPerFile;
        private double _ppm;

        private bool AlignReplicates()
        {
            // Final step - try to unify similar features in the various Bullseye result files
            _bfiles = SpectrumFileNames.Select(GetSearchResultFilepath).ToArray();
            if (_bfiles.Length == 0)
            {
                return false;
            }

            // Do retention time alignment on the raw data
            if (!PerformAllAlignments())
            {
                return false;
            }

            return true;
        }

        private bool FindSimilarFeatures()
        {
            UpdateProgress(_progressStatus = _progressStatus.ChangeSegmentName(string.Format(DdaSearchResources.HardklorSearchEngine_FindSimilarFeatures_Looking_for_features_occurring_in_multiple_replicates)));
            // Parse all the Bullseye output files
            ReadFeatures();

            // Watch for IDs that are mistaken hits on higher charge states e.g. claiming z=2 on an isotope envelope that's z=4 
            CleanupBadCharges();

            // The shape of the isotope distribution is pretty much determined by everything but
            // hydrogen, so create a lookup based on avergine formulas with H removed
            CreateLookupByCNOS();

            // We have a dictionary of all known isotope distributions (that is, the averagine formulas without H)
            // and all known masses and intensities. Now to find occurrences of these across the RT-aligned replicates
            // Look at similar features and combine them into references to a single feature
            CombineSimilarFeaturesAcrossReplicates();

            // Tag low quality features for discard
            DiscardLowQualityFeatures();

            // We asked Hardklor to write its mass values at greater than normal precision, let's peel
            // that back to 4 decimal places - but we have to watch for collisions
            TrimMassDigits();

            // If a feature appears in the same file more than once, eliminate all but the best
            EliminateMultipleHitsInSameFile();

            // Write back any adjustments before we hand off the files to BlibBuild, so it understands 
            // that the IDs we matched are the same things
            // We'll add a column for feature names, where name consists of mass and aligned RT e.g. mass123.45_RT23.45678
            UpdateFilesForBiblioSpec();

            return _featuresAll.Count(f => !f.discard) > 0;
        }

        // Watch for IDs that are mistaken hits on higher charge states e.g. claiming z=2 on an isotope envelope that's z=4 
        private void CleanupBadCharges()
        {
            _ppm = GetPPM();

            for (var fileIndex = 0; fileIndex < _bfiles.Length; fileIndex++)
            {
                var index = fileIndex;
                var byMz = _featuresAll.Where(f => f.fileIndex == index).OrderBy(f => f.mzObserved).ThenBy(f => f.charge).ToArray();
                for (var i = 1; i < byMz.Length; i++)
                {
                    if (byMz[i].mzObserved == byMz[i - 1].mzObserved)
                    {
                        for (var factor = 2; factor < 4; factor++)
                        {
                            if (byMz[i].charge == factor * byMz[i - 1].charge)
                            {
                                if (SimilarMass(byMz[i - 1].parsedMass, byMz[i].parsedMass / factor))
                                {
                                    if (SimilarAvergines(byMz[i - 1].avergineAndOffset, byMz[i].avergineAndOffset, factor))
                                    {
                                        byMz[i - 1].discard = true; // This is a mistaken ID - should have picked up on the peaks between the peaks
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Determine if avergine declarations are similar, taking scale factor into account
        private bool SimilarAvergines(string avergineAndOffsetA, string avergineAndOffsetB, int factor)
        {
            if (ParsedMolecule.TryParseFormula(avergineAndOffsetA, out var molA, out _) &&
                ParsedMolecule.TryParseFormula(avergineAndOffsetB, out var molB, out _))
            {
                // CNOS determines most of the isotope envelope
                var diff = 0;
                foreach (var cnos in new []{ BioMassCalc.C, BioMassCalc.N, BioMassCalc.O, BioMassCalc.S})
                {
                    molA.Molecule.Dictionary.TryGetValue(cnos, out var nosA);
                    molB.Molecule.Dictionary.TryGetValue(cnos, out var nosB);
                    var diffElement = (nosB - factor * nosA);
                    if (Math.Abs(diffElement) > 1)
                    {
                        return false; // A little wiggle is OK...
                    }
                    diff += diffElement; // ... so long as it all adds up to zero
                }
                if (diff == 0)
                {
                    return true;
                }
            }
            return false;
        }

        // The shape of the isotope distribution is pretty much determined by everything but
        // hydrogen, so create a lookup based on avergine formulas with H removed
        private void CreateLookupByCNOS()
        {
            var  featuresByCNOS = new Dictionary<string, List<hkFeatureDetail>>();

            // Group features by the contents of their avergine formulas omitting H, since H has minimal effect on isotope
            // envelope and is essentially just another way Hardklor expresses a mass shift
            foreach (var feature in _featuresAll.Where(f => !f.discard))
            {
                var averagine_no_H = feature.avergineAndOffset.Split('[')[0].Split('C')[1]; // e.g. "H120C119N33O35S1[+0.12]" => "119N33O35S1"
                if (!featuresByCNOS.TryGetValue(averagine_no_H, out var featureList))
                {
                    featuresByCNOS.Add(averagine_no_H, new List<hkFeatureDetail>() { feature });
                }
                else
                {
                    featureList.Add(feature);
                }
            }

            // Now order by Hardklor score
            _orderedFeaturesByCNOS = featuresByCNOS.ToDictionary(featureByCNOS => featureByCNOS.Key, featureByCNOS => featureByCNOS.Value.OrderByDescending(v => v.quality).ToArray());
        }

        // Parse all the Bullseye output files
        private void ReadFeatures()
        {
            _contents = new List<string[]>();
            _summedIntensityPerFile = new double[_bfiles.Length];
            for (var fileIndex = 0; fileIndex < _bfiles.Length; fileIndex++)
            {
                var file = _bfiles[fileIndex];
                _summedIntensityPerFile[fileIndex] = 0;
                var lines = File.ReadAllLines(file);
                _contents.Add(lines);
                int l = 1;
                foreach (var line in lines.Skip(1))
                {
                    var col = line.Split('\t');
                    var rtParsed = double.Parse(col[11], CultureInfo.InvariantCulture); // "Best RTime"
                    var rtFirst = double.Parse(col[9], CultureInfo.InvariantCulture);  // "First RTime"
                    var rtLast = double.Parse(col[10], CultureInfo.InvariantCulture);  // "Last RTime"
                    var rtToler = 1.5 * (rtLast - rtFirst); // Allow a little wiggle when aligning similar features - within a peak width of the better peak (Hardklor declares absurdly narrow peak bounds)
                    var summedIntensity = double.Parse(col[8], CultureInfo.InvariantCulture);
                    var feature = new hkFeatureDetail()
                    {
                        charge = int.Parse(col[4], CultureInfo.InvariantCulture),
                        strMass = col[5],
                        parsedMass = double.Parse(col[5], CultureInfo.InvariantCulture),
                        mzObserved = double.Parse(col[6], CultureInfo.InvariantCulture),
                        summedIntensity = summedIntensity,
                        rt = rtParsed,
                        rtAligned = rtParsed,
                        rtStart = rtFirst - rtToler,
                        rtEnd = rtLast + rtToler,
                        avergineAndOffset = col[15],
                        fileIndex = fileIndex,
                        lineIndex = l,
                        discard = false,
                        alignsWith = new List<hkFeatureDetail>()
                    };
                    _featuresAll.Add(feature);
                    _summedIntensityPerFile[fileIndex] += summedIntensity;
                    l++;
                }
            }
        }

        // Write back any adjustments before we hand off the files to BlibBuild, so it understands 
        // that the IDs we matched are the same things
        // We'll add a column for feature names, where name consists of mass and aligned RT e.g. mass123.45_RT23.45678
        private void UpdateFilesForBiblioSpec()
        {
            for (var f = 0; f < _bfiles.Length; f++)
            {
                _contents[f][0] += $@"{TextUtil.SEPARATOR_TSV_STR}FeatureName"; // This must agree with pwiz_tools\BiblioSpec\src\HardklorReader.cpp
            }
            foreach (var feature in _featuresAll)
            {
                if (feature.discard)
                {
                    _contents[feature.fileIndex][feature.lineIndex] = string.Empty; // This feature got folded into something else
                    continue;
                }
                var col = _contents[feature.fileIndex][feature.lineIndex].Split('\t');
                col[5] = feature.strMass;
                col[15] = feature.avergineAndOffset;
                _contents[feature.fileIndex][feature.lineIndex] = string.Join(TextUtil.SEPARATOR_TSV_STR, col);
                // And add a feature name as mass and aligned RT
                _contents[feature.fileIndex][feature.lineIndex] += $@"{TextUtil.SEPARATOR_TSV_STR}{feature.strMassAndAlignedRT}";
            }


            // And write it all back, preserving a copy of the original
            for (var f = 0; f < _bfiles.Length; f++)
            {
                // Note the original
                var unaligned = GetBullseyeKronikUnalignedFilename(_bfiles[f]);
                FileEx.SafeDelete(unaligned);
                File.Copy(_bfiles[f], unaligned);
                // Update for handoff to BiblioSpec
                File.WriteAllLines(_bfiles[f], _contents[f].Where(line => !string.IsNullOrEmpty(line)));  // Update the current file
            }
        }

        private bool RTOverlap(double s1, double e1, double s2, double e2) // RT start, end of peaks
        {
            var toler = Math.Max(_searchSettings.SettingsHardklor.RetentionTimeTolerance, Math.Max(e1 - s1, e2 - s2)); // Use Hardklor peak width as a clue for overlap
            return (s1-toler) <= e2 && (e1+toler) >= s2;
        }

        private bool SimilarMass(double massJ, double massI)
        {
            var massDiff = (Math.Abs(massJ - massI) / massI) * 1.0E6;
            return massDiff <= _ppm;
        }

        // Look at similar features and combine them into references to a single feature
        private void CombineSimilarFeaturesAcrossReplicates()
        {
            _featureMatches = new Dictionary<string, int[]>();
            foreach (var cnos in _orderedFeaturesByCNOS)
            {
                var ordered = cnos.Value;
                var count = ordered.Length;
                var matchIndex = Enumerable.Repeat(-1, count).ToArray();
                _featureMatches.Add(cnos.Key, matchIndex);

                // Starting with the highest score, see if any other entries from similar RT are similar m/z.
                // If they are, update the mass information to match the more solid ID.
                for (var i = 0; i < count; i++)
                {
                    if (matchIndex[i] >= 0)
                    {
                        continue; // Already aligned to something else
                    }

                    var hkFeatureDetailI = ordered[i];
                    var massI = hkFeatureDetailI.parsedMass;

                    if (massI == 0)
                    {
                        continue;
                    }

                    // Look at lower-quality features and decide if they're actually occurrences of this feature
                    // Note that we may reassign a previous match it if involves a smaller mass shift
                    for (var j = i + 1; j < count; j++)
                    {
                        var hkFeatureDetailJ = ordered[j];
                        if (SimilarMass(hkFeatureDetailJ.parsedMass, massI))
                        {
                            // Isotope envelopes agree, masses are similar - does RT agree?
                            if (hkFeatureDetailI.fileIndex != hkFeatureDetailJ.fileIndex)
                            {
                                var fileI = SpectrumFileNames[hkFeatureDetailI.fileIndex];
                                var fileJ = SpectrumFileNames[hkFeatureDetailJ.fileIndex];
                                if (_alignments.TryGetValue(Tuple.Create(fileI, fileJ), out var alignmentI) &&
                                    _alignments.TryGetValue(Tuple.Create(fileJ, fileI), out var alignmentJ))
                                {
                                    var rtJIStart = alignmentJ.GetValue(hkFeatureDetailJ.rtStart); // Warp rt J into I rt space
                                    var rtJIEnd = alignmentJ.GetValue(hkFeatureDetailJ.rtEnd); // Warp rt J into I rt space
                                    if (RTOverlap(hkFeatureDetailI.rtStart, hkFeatureDetailI.rtEnd, rtJIStart, rtJIEnd))
                                    {
                                        matchIndex[j] = i; // J is the same feature as I
                                    }
                                    else
                                    {
                                        var rtIJStart = alignmentI.GetValue(hkFeatureDetailI.rtStart); // Warp rt I into J rt space
                                        var rtIJEnd = alignmentI.GetValue(hkFeatureDetailI.rtEnd); // Warp rt I into J rt space
                                        if (RTOverlap(hkFeatureDetailJ.rtStart, hkFeatureDetailJ.rtEnd, rtIJStart, rtIJEnd))
                                        {
                                            matchIndex[j] = i; // J is the same feature as I
                                        }
                                    }
                                }
                            }
                            else if (RTOverlap(hkFeatureDetailI.rtStart, hkFeatureDetailI.rtEnd,  hkFeatureDetailJ.rtStart, hkFeatureDetailJ.rtEnd))
                            {
                                matchIndex[j] = i; // J is the same feature as I
                            }
                        }
                    }
                }
            }

            // Update the features list to indicate matches across replicates
            if (_featureMatches.Any())
            {
                foreach (var ordered in _orderedFeaturesByCNOS)
                {
                    var details = ordered.Value;
                    var matchIndex = _featureMatches[ordered.Key];
                    for (var j = 0; j < matchIndex.Length; j++)
                    {
                        if (matchIndex[j] >= 0) // This feature was aligned to another, unify the descriptions
                        {
                            var hkFeatureDetailI = details[matchIndex[j]]; // The feature this aligned to
                            var hkFeatureDetailJ = details[j];
                            hkFeatureDetailJ.strMass = hkFeatureDetailI.strMass; // Make the mass descriptions match exactly
                            hkFeatureDetailJ.parsedMass = hkFeatureDetailI.parsedMass; // Make the mass descriptions match exactly
                            hkFeatureDetailJ.rtAligned = hkFeatureDetailI.rtAligned; // The RT of the "best" feature
                            hkFeatureDetailJ.avergineAndOffset = hkFeatureDetailI.avergineAndOffset; // Make the formulas match exactly (but not the observed m/z necessarily)
                            hkFeatureDetailJ.alignsWith.Add(hkFeatureDetailI);
                            hkFeatureDetailI.alignsWith.Add(hkFeatureDetailJ);
                        }
                    }
                }
            }
        }

        // Tag low quality features for discard - if all occurrences (where "all" may mean single) are low quality tag for discard
        private void DiscardLowQualityFeatures()
        {
            // Remove low quality hits
            var summedIntensityCutoff = _searchSettings.SettingsHardklor.MinIntensityPPM * 1.0E-6;
            bool BelowIntensityCutoff(hkFeatureDetail hkFeatureDetail)
            {
                return (hkFeatureDetail.summedIntensity / _summedIntensityPerFile[hkFeatureDetail.fileIndex]) < summedIntensityCutoff;
            }
            foreach (var feature in _featuresAll.Where(BelowIntensityCutoff).Where(feature => feature.alignsWith.Count == 0 || feature.alignsWith.All(BelowIntensityCutoff)))
            {
                feature.discard = true; // All occurrences are poor quality, and if they align with anything it's also low quality
            }
        }

        // If a feature appears in the same file more than once, eliminate all but the best
        private void EliminateMultipleHitsInSameFile()
        {
            foreach (var CNOS in _featureMatches.Keys)
            {
                var features = _orderedFeaturesByCNOS[CNOS]; // In descending order of "quality"
                var matches = _featureMatches[CNOS]; // Also in  descending order of "quality"
                for (var i = 0; i < matches.Length; i++)
                {
                    if (matches[i] == -1) // This is the one others may have aligned to
                    {
                        var hits = new HashSet<Tuple<int,int>> { new Tuple<int, int>(features[i].fileIndex, features[i].charge )};
                        for (var j = i + 1; j < matches.Length; j++)
                        {
                            if (matches[j] == i)
                            {
                                // Feature j was aligned to feature i
                                if (!hits.Add(new Tuple<int, int>(features[j].fileIndex, features[j].charge)))
                                {
                                    // An instance of better quality with this charge has already been noted for this file
                                    features[j].discard = true; // Don't pass it through to BiblioSpec
                                }
                            }
                        }
                    }
                }
            }
        }

        // We asked Hardklor to write its mass values at greater than normal precision, let's peel
        // that back to 4 decimal places - but we have to watch for collisions
        private void TrimMassDigits()
        {
            for (var digits = 4; digits < 9; digits++)
            {
                var featuresByRoundedMassAndRT = new Dictionary<string, List<hkFeatureDetail>>();
                var format = $@"0.{new string('0', digits)}";
                var needsTrim = false;
                foreach (var feature in _featuresAll)
                {
                    var rounded = hkFeatureDetail.FormatMassAndAlignedRT(feature.parsedMass
                        .ToString(format, CultureInfo.InvariantCulture), feature.rtAligned);
                    if (feature.strMassAndAlignedRT.Length > rounded.Length)
                    {
                        needsTrim = true;
                        if (!featuresByRoundedMassAndRT.TryGetValue(rounded, out var featureList))
                        {
                            featuresByRoundedMassAndRT.Add(rounded, new List<hkFeatureDetail>() { feature });
                        }
                        else
                        {
                            featureList.Add(feature);
                        }
                    }
                }

                if (!needsTrim)
                {
                    break;
                }

                foreach (var byRoundedMass in featuresByRoundedMassAndRT)
                {
                    if (byRoundedMass.Value.Select(f => f.avergineAndOffset).Distinct().Count() == 1)
                    {
                        // All describing the same formula, RT, and mass
                        foreach (var hkFeatureDetail in byRoundedMass.Value)
                        {
                            hkFeatureDetail.strMass = hkFeatureDetail.parsedMass.ToString(format, CultureInfo.InvariantCulture); // Update to the abbreviated mass description
                        }
                    }
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

        public override void SetCutoffScore(double cutoffScore)
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
                $@"correlation			=	{ImportPeptideSearch.HardklorSettings.CosineAngleFromNormalizedContrastAngle(_searchSettings.SettingsHardklor.MinIdotP)}	#Correlation threshold to accept a peptide feature. #NEED UI",
                $@"averagine_mod		=	0			#Formula containing modifications to the averagine model.",
                $@"									#  Read documentation carefully before using! 0=off",
                $@"mz_window			=	5.25		#Breaks spectrum into windows not larger than this value for Version1 algorithm.",
                $@"sensitivity			=	2		#Values are 0 (lowest) to 3 (highest). Increasing sensitivity",
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
            return spectra1.PerformAlignment(this, _progressStatus, spectra2);
        }

        public bool PerformAllAlignments()
        {
            foreach (var path in SpectrumFileNames)
            {
                UpdateProgress(_progressStatus = _progressStatus.ChangeSegmentName(string.Format(DdaSearchResources.HardklorSearchEngine_PerformAllAlignments_Preparing_for_retention_time_alignment_on___0__, path.GetFileName())));
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

                    UpdateProgress(_progressStatus = _progressStatus.NextSegment().ChangeSegmentName(
                        string.Format(DdaSearchResources.HardklorSearchEngine_PerformAllAlignments_Performing_retention_time_alignment__0__vs__1_, entry1.Key.GetFileName(), entry2.Key.GetFileName())));

                    try
                    {
                        _alignments[Tuple.Create(entry1.Key, entry2.Key)] = PerformAlignment(entry1.Value, entry2.Value);
                    }
                    catch (Exception x)
                    {
                        _progressStatus = _progressStatus.ChangeMessage(string.Format(DdaSearchResources.HardklorSearchEngine_PerformAllAlignments_Error_performing_alignment_between__0__and__1____2_, entry1.Key, entry2.Key, x));
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
 