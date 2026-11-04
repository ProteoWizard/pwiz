/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using pwiz.BiblioSpec;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class ImportPeptideSearch
    {
        public ImportPeptideSearch()
        {
            SearchFilenames = new string[0];
            CutoffScore = Settings.Default.LibraryResultCutOff;
            SpectrumSourceFiles = new Dictionary<string, FoundResultsFilePossibilities>();
            IrtStandard = null;

            _matcher = new LibKeyModificationMatcher();
            UserDefinedTypedMods = new HashSet<StaticMod>();
        }

        public enum eFeatureDetectionPhase
        {
            none, fullscan_settings, hardklor_settings
        }

        public string[] SearchFilenames { get;  set; }
        public double CutoffScore { get; set; }
        public Library DocLib { get; private set; }
        public Dictionary<string, FoundResultsFilePossibilities> SpectrumSourceFiles { get; set; }
        public AbstractDdaSearchEngine SearchEngine { get; set; }
        public AbstractDdaConverter DdaConverter { get; set; }

        public bool HasDocLib { get { return DocLib != null; } }
        public IrtStandard IrtStandard { get; set; }
        public bool IsDDASearch { get; set; }
        public bool IsDIASearch { get; set; }
        public bool IsGpfData { get; set; }
        public bool IsFeatureDetection { get; set; }
        public int RemainingStepsInSearch { get; set; } // In the case of Hardklor+Bullseye there may be several steps
        public HardklorSettings SettingsHardklor { get; set; }
        private readonly LibKeyModificationMatcher _matcher;
        private IsotopeLabelType DefaultHeavyLabelType { get; set; }
        public HashSet<StaticMod> UserDefinedTypedMods { get; private set; }
        public PeptideModifications MatcherPepMods { get { return _matcher.MatcherPepMods; } }
        public IList<StaticMod> MatcherHeavyMods { get { return MatcherPepMods.GetModifications(DefaultHeavyLabelType); } }


        public class HardklorSettings
        {
            public HardklorSettings(FullScanMassAnalyzerType instrument, double resolution,
                double idotpMin, double signalToNoise, IEnumerable<int> charges, double intensityCutoffPPM, double rtTolerance)
            {
                Instrument = instrument;
                Resolution = resolution;
                // We think in terms of "normalized contrast angle", Hardklor thinks about "cosine angle" -  we convert when we write the Hardklor config file.
                MinIdotP = idotpMin; 
                SignalToNoise = signalToNoise;
                Charges = charges.ToList();
                MinIntensityPPM = intensityCutoffPPM;
                RetentionTimeTolerance = rtTolerance;
            }

            [Track]
            public double MinIdotP { get; private set; } // We think in terms of "normalized contrast angle", Hardklor thinks about "cosine angle" -  we convert when we write the Hardklor config file.
            [Track]
            public double SignalToNoise { get; private set; }
            [Track]
            public FullScanMassAnalyzerType Instrument { get; private set; }
            [Track]
            public double Resolution { get; private set; }
            [Track]
            public List<int> Charges { get; set; } // A list of desired charges for BlibBuild
            [Track]
            public double MinIntensityPPM { get; set; } // Ignore any features whose intensity is less than xxx PPM of the total of all features in a replicate

            public double RetentionTimeTolerance { get; private set; } // For aligning Bullseye output

            // We think in terms of "normalized contrast angle" (NCA), Hardklor thinks about "cosine angle" (CA).
            // NCA = 1.0 - (acos(CA) * 2 / PI);
            // so
            // CA = PI/2 * cos(1-NCA)
            public static double CosineAngleFromNormalizedContrastAngle(double normalizedContrastAngle) => Statistics.NormalizedContrastAngleToAngle(Math.Min(1.0, normalizedContrastAngle));
            public static double NormalizedContrastAngleFromCosineAngle(double cosineAngle) => Statistics.AngleToNormalizedContrastAngle(Math.Min(1.0, cosineAngle));

        }


        /// <summary>
        /// Gives the search engine (and any spectrum converter already chosen) the spectrum files,
        /// the FASTA and the document's enzyme, and adds a converter when the engine cannot read
        /// the spectrum files as they are.
        /// </summary>
        /// <param name="document">The document whose enzyme and missed cleavages the search uses.</param>
        /// <param name="spectrumFiles">The files to search.</param>
        /// <param name="fastaFile">The FASTA to search against.</param>
        /// <param name="createConverter">Makes the converter for the workflow when one is needed.</param>
        public void PrepareSearch(SrmDocument document, MsDataFileUri[] spectrumFiles, string fastaFile,
            Func<AbstractDdaConverter> createConverter)
        {
            var peptideSettings = document.Settings.PeptideSettings;
            SearchEngine.SetEnzyme(peptideSettings.Enzyme, peptideSettings.DigestSettings.MaxMissedCleavages);
            SearchEngine.SetSpectrumFiles(spectrumFiles);
            DdaConverter?.SetSpectrumFiles(spectrumFiles);
            SearchEngine.SetFastaFiles(fastaFile);

            AbstractDdaConverter.MsdataFileFormat requiredFormat = IsFeatureDetection
                ? AbstractDdaConverter.MsdataFileFormat.mzML // Hardklor reads only mzML
                : AbstractDdaConverter.MsdataFileFormat.mz5;
            if (DdaConverter == null &&
                (spectrumFiles.Any(f => SearchEngine.GetSearchFileNeedsConversion(f, out requiredFormat)))/* ||
                !FullScan.SpectrumClassFilter.IsEmpty*/) // CONSIDER(MCC): consider spectrum filters in GetSearchFileNeedsConversion and apply them in Converter
            {
                DdaConverter = createConverter();
                DdaConverter.SetSpectrumFiles(spectrumFiles);
                DdaConverter.SetRequiredOutputFormat(requiredFormat);
            }
            else if (DdaConverter != null &&
                     DdaConverter.ConvertedSpectrumSources.Any(f => SearchEngine.GetSearchFileNeedsConversion(f, out requiredFormat)))
            {
                DdaConverter.SetRequiredOutputFormat(requiredFormat);
            }
        }

        public BiblioSpecLiteBuilder GetLibBuilder(SrmDocument doc, string docFilePath, bool includeAmbiguousMatches, bool isFeatureDetection = false)
        {
            if (isFeatureDetection)
            {
                // Avoid confusion with any document library
                // Change filename hint from "foo\bar\baz.sky" to "foo\bar\baz detected features.sky" so we get library name "baz detected features.blib"
                var docfile_ext = Path.GetExtension(docFilePath);
                docFilePath = docFilePath.Substring(0, docFilePath.Length-docfile_ext.Length) + @" " + ModelResources.ImportPeptideSearch_GetLibBuilder_detected_features + docfile_ext; 
            }
            string outputPath = BiblioSpecLiteSpec.GetLibraryFileName(docFilePath);

            // Check to see if the library is already there, and if it is, 
            // "Append" instead of "Create"
            bool libraryExists = File.Exists(outputPath);
            var libraryBuildAction = LibraryBuildAction.Create;
            if (libraryExists)
            {
                if (doc.Settings.HasDocumentLibrary && !isFeatureDetection)
                {
                    libraryBuildAction = LibraryBuildAction.Append;
                }
                else
                {
                    // If the document does not have a document library, then delete the one that we have found
                    // CONSIDER: it may be that user is trying to re-import, in which case this file is probably in use
                    FileEx.SafeDelete(outputPath);
                    FileEx.SafeDelete(Path.ChangeExtension(outputPath, BiblioSpecLiteSpec.EXT_REDUNDANT));
                }
            }

            string name = Path.GetFileNameWithoutExtension(docFilePath);
            return new BiblioSpecLiteBuilder(name, outputPath, SearchFilenames, null, !isFeatureDetection)
            {
                Action = libraryBuildAction,
                KeepRedundant = true,
                CutOffScore = CutoffScore,
                Id = Helpers.MakeId(name),
                Charges = IsFeatureDetection ? SettingsHardklor.Charges : null, // Optional list of charges, if non-empty BlibBuild will ignore any not listed here
                IncludeAmbiguousMatches = includeAmbiguousMatches
            };
        }

        public static void ClosePeptideSearchLibraryStreams(SrmDocument doc)
        {
            BiblioSpecLiteLibrary docLib;
            if (!doc.Settings.PeptideSettings.Libraries.TryGetDocumentLibrary(out docLib))
                return;

            foreach (var stream in docLib.ReadStreams)
                stream.CloseStream();
        }

        /// <summary>
        /// Delete the document library so a search can write it afresh: the document's read streams
        /// on it are closed and the cached <see cref="Library"/> is evicted from
        /// <paramref name="libraryManager"/> first, since without both the delete fails or the next
        /// load is served from the cache; then the .blib and its redundant library go.
        /// </summary>
        public static void DeleteDocumentLibrary(SrmDocument doc, LibraryManager libraryManager, string docBlibPath)
        {
            ClosePeptideSearchLibraryStreams(doc);
            var docLibSpecs = doc.Settings.PeptideSettings.Libraries.LibrarySpecs
                .Where(s => s != null && s.IsDocumentLibrary).ToArray();
            if (docLibSpecs.Length > 0)
                libraryManager.ReleaseLibraries(docLibSpecs);
            FileEx.SafeDelete(docBlibPath, true);
            FileEx.SafeDelete(BiblioSpecLiteSpec.GetRedundantName(docBlibPath), true);
        }

        /// <summary>
        /// Closes and evicts the library <see cref="LoadPeptideSearchLibrary"/> loaded, when no document
        /// uses it, so the library can be built again.
        /// </summary>
        public void ReleasePeptideSearchLibrary(LibraryManager libraryManager, LibrarySpec libSpec)
        {
            if (DocLib is BiblioSpecLiteLibrary blib)
            {
                foreach (var stream in blib.ReadStreams)
                    stream.CloseStream();
            }
            else
            {
                DocLib?.ReadStream?.CloseStream();
            }
            libraryManager.ReleaseLibraries(libSpec);
            DocLib = null;
        }

        public bool LoadPeptideSearchLibrary(LibraryManager libraryManager, LibrarySpec libSpec, IProgressMonitor monitor)
        {
            if (libSpec == null)
            {
                return false;
            }

            DocLib = libraryManager.TryGetLibrary(libSpec) ??
                     libraryManager.LoadLibrary(libSpec, () => new DefaultFileLoadMonitor(monitor));

            return DocLib != null;
        }

        public SrmDocument AddDocumentSpectralLibrary(SrmDocument doc, LibrarySpec libSpec)
        {
            return doc.ChangeSettings(doc.Settings.ChangePeptideLibraries(lib =>
            {
                var libSpecs = new List<LibrarySpec>();
                var libs = new List<Library>();
                if (libSpec.IsDocumentLibrary)
                {
                    libSpecs.Add(libSpec);
                    libs.Add(DocLib);
                    int skipCount = lib.HasDocumentLibrary ? 1 : 0;
                    libSpecs.AddRange(lib.LibrarySpecs.Skip(skipCount));
                    libs.AddRange(lib.Libraries.Skip(skipCount));
                    lib = lib.ChangeDocumentLibrary(true);
                }
                else
                {
                    for (int i = 0; i < lib.LibrarySpecs.Count; i++)
                    {
                        var spec = lib.LibrarySpecs[i];
                        if (spec.IsDocumentLibrary || spec.Name != libSpec.Name)
                        {
                            libSpecs.Add(spec);
                            libs.Add(lib.Libraries[i]);
                        }
                    }
                    libSpecs.Add(libSpec);
                    libs.Add(DocLib);
                }

                if (lib.RankId != null && !libSpec.PeptideRankIds.Contains(lib.RankId))
                {
                    lib = lib.ChangeRankId(null);
                }
                return lib.ChangeLibraries(libSpecs, libs);
            }));
        }

        public static SrmDocument AddRetentionTimePredictor(SrmDocument doc, LibrarySpec libSpec)
        {
            var predictor = CreateRetentionTimePredictor(libSpec);
            SaveRetentionTimePredictor(predictor);
            return ChangeRetentionTimePredictor(doc, predictor);
        }

        /// <summary>
        /// A retention time predictor using the iRT values in a calibrated library, named uniquely
        /// among the saved calculators and predictors.
        /// </summary>
        public static RetentionTimeRegression CreateRetentionTimePredictor(LibrarySpec libSpec)
        {
            var calc = new RCalcIrt(
                Helpers.GetUniqueName(libSpec.Name, Settings.Default.RTScoreCalculatorList.Select(lib => lib.Name).ToArray()),
                libSpec.FilePath);
            return new RetentionTimeRegression(
                Helpers.GetUniqueName(libSpec.Name, Settings.Default.RetentionTimeList.Select(rt => rt.Name).ToArray()),
                calc, null, null, DEFAULT_RT_WINDOW, new List<MeasuredRetentionTime>());
        }

        public static void SaveRetentionTimePredictor(RetentionTimeRegression predictor)
        {
            Settings.Default.RTScoreCalculatorList.Add(predictor.Calculator);
            Settings.Default.RetentionTimeList.Add(predictor);
        }

        public static SrmDocument ChangeRetentionTimePredictor(SrmDocument doc, RetentionTimeRegression predictor)
        {
            return doc.ChangeSettings(
                doc.Settings.ChangePeptideSettings(
                    doc.Settings.PeptideSettings.ChangePrediction(
                        doc.Settings.PeptideSettings.Prediction.ChangeRetentionTime(predictor))));
        }

        public static void GetLibIrtProviders(Library lib, IrtStandard standard, IProgressMonitor monitor,
            out IRetentionTimeProvider[] irtProviders, out List<IrtStandard> autoStandards, out DbIrtPeptide[] cirtPeptides)
        {
            monitor?.UpdateProgress(new ProgressStatus().ChangePercentComplete(-1));

            irtProviders = lib.RetentionTimeProvidersIrt.ToArray();
            if (!irtProviders.Any())
                irtProviders = lib.RetentionTimeProviders.ToArray();

            var isAuto = standard.IsAuto;
            autoStandards = isAuto
                ? IrtStandard.BestMatch(irtProviders.SelectMany(provider => provider.PeptideRetentionTimes).Select(rt => rt.PeptideSequence))
                : null;

            if (ReferenceEquals(standard, IrtStandard.CIRT_SHORT) || isAuto && autoStandards.Count == 0)
            {
                var libPeptides = new TargetMap<bool>(irtProviders
                    .SelectMany(provider => provider.PeptideRetentionTimes)
                    .Select(rt => new KeyValuePair<Target, bool>(rt.PeptideSequence, true)));
                cirtPeptides = IrtStandard.CIRT.Peptides.Where(pep => libPeptides.ContainsKey(pep.ModifiedTarget)).ToArray();
            }
            else
            {
                cirtPeptides = new DbIrtPeptide[0];
            }
        }

        public static ProcessedIrtAverages ProcessRetentionTimes(int? numCirt, IRetentionTimeProvider[] irtProviders,
            DbIrtPeptide[] standardPeptides, DbIrtPeptide[] cirtPeptides, IrtRegressionType regressionType, IProgressMonitor monitor, out DbIrtPeptide[] newStandardPeptides)
        {
            newStandardPeptides = null;
            var processed = !numCirt.HasValue
                ? RCalcIrt.ProcessRetentionTimes(monitor, irtProviders, standardPeptides, new DbIrtPeptide[0], regressionType)
                : RCalcIrt.ProcessRetentionTimesCirt(monitor, irtProviders, cirtPeptides, numCirt.Value, regressionType, out newStandardPeptides);
            return processed;
        }

        public static void CreateIrtDb(string path, ProcessedIrtAverages processed, DbIrtPeptide[] standardPeptides, bool recalibrate, IrtRegressionType regressionType, IProgressMonitor monitor)
        {
            DbIrtPeptide[] newStandards = null;
            if (recalibrate)
            {
                monitor.UpdateProgress(new ProgressStatus().ChangeSegments(0, 2));
                newStandards = processed.RecalibrateStandards(standardPeptides).ToArray();
                processed = RCalcIrt.ProcessRetentionTimes(monitor,
                    processed.ProviderData.Select(data => data.RetentionTimeProvider).ToArray(),
                    newStandards.ToArray(), Array.Empty<DbIrtPeptide>(), regressionType);
            }
            IrtDb.CreateIrtDb(path).UpdatePeptides((newStandards ?? standardPeptides).Concat(processed.DbIrtPeptides).ToList(), monitor);
        }

        public bool VerifyRetentionTimes(IEnumerable<string> resultsFiles)
        {
            foreach (var resultsFile in resultsFiles)
            {
                LibraryRetentionTimes retentionTimes;
                if (DocLib.TryGetRetentionTimes(MsDataFileUri.Parse(resultsFile), out retentionTimes))
                {
                    if (retentionTimes.PeptideRetentionTimes.Any(t => t.RetentionTime <= 0))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public void InitializeSpectrumSourceFiles(SrmDocument document)
        {
            if (!IsDDASearch){
                if (DocLib == null)
                    return;

                var measuredResults = document.Settings.MeasuredResults;
                foreach (var dataFile in DocLib.LibraryFiles.FilePaths)
                {
                    var msDataFilePath = new MsDataFilePath(dataFile);
                    var noExtName = msDataFilePath.GetFileNameWithoutExtension();

                    // A caller may have pre-populated SpectrumSourceFiles (e.g. DiannSearchDlg
                    // seeds the files it just searched). The pre-population key may not match
                    // the blib's storage form — DIA-NN libraries store ".raw" filenames with
                    // extension but ".mzML" filenames without — so accept either as a match.
                    string lookupKey;
                    if (SpectrumSourceFiles.ContainsKey(dataFile))
                        lookupKey = dataFile;
                    else if (SpectrumSourceFiles.ContainsKey(noExtName))
                        lookupKey = noExtName;
                    else
                    {
                        SpectrumSourceFiles[dataFile] = new FoundResultsFilePossibilities(noExtName);
                        lookupKey = dataFile;
                    }

                    // If a matching file is already in the document, then don't include
                    // this library spectrum source in the set of files to find.
                    if (measuredResults != null && measuredResults.FindMatchingMSDataFile(MsDataFileUri.Parse(dataFile)) != null)
                        continue;

                    if (SpectrumSourceFiles[lookupKey].ExactMatch == null &&
                        File.Exists(dataFile) && DataSourceUtil.IsDataSource(dataFile))
                    {
                        // We've found the dataFile in the exact location
                        // specified in the document library, so just add it
                        // to the "FOUND" list.
                        SpectrumSourceFiles[lookupKey].ExactMatch = msDataFilePath.ToString();
                    }
                }
                DocLib.ReadStream.CloseStream();
            }
        }

        public IEnumerable<string> GetDirsToSearch(string documentDirectory)
        {
            if (!string.IsNullOrWhiteSpace(documentDirectory))
            {
                // Search the directory of the document, its parent, and its subdirectories
                yield return documentDirectory;

                DirectoryInfo parentDir = Directory.GetParent(documentDirectory);
                if (parentDir != null)
                {
                    yield return parentDir.ToString();
                }

                foreach (var subdir in GetSubdirectories(documentDirectory))
                    yield return subdir;
            }

            if (SearchFilenames != null)
            {
                // Search the directories of the search files
                foreach (var searchFilename in SearchFilenames)
                {
                    string searchFileDirectory = Path.GetDirectoryName(searchFilename);
                    yield return searchFileDirectory;

                    if (searchFileDirectory == null)
                        continue;

                    DirectoryInfo parentDir = Directory.GetParent(searchFileDirectory);
                    if (parentDir != null)
                    {
                        yield return parentDir.ToString();
                    }
                }
            }
        }

        public static IEnumerable<string> GetSubdirectories(string dir)
        {
            try
            {
                return Directory.EnumerateDirectories(dir);
            }
            catch (Exception)
            {
                // No permissions on folder
                return new string[0];
            }
        }

        public void UpdateSpectrumSourceFilesFromDirs(IEnumerable<string> dirPaths, bool overwrite, ILongWaitBroker longWaitBroker)
        {
            if (!overwrite && SpectrumSourceFiles.Values.All(s => s.HasMatches))
                return;

            Array.ForEach(dirPaths.Distinct().ToArray(), dir => FindDataFiles(dir, overwrite, longWaitBroker));
        }

        private void FindDataFiles(string directory, bool overwrite, ILongWaitBroker longWaitBroker)
        {
            // Don't search if every spectrum source file has an exact match and an alternate match
            if (directory == null || !Directory.Exists(directory) || (!overwrite && SpectrumSourceFiles.Values.All(s => s.HasMatches)))
                return;

            if (longWaitBroker != null)
            {
                longWaitBroker.Message =
                    string.Format(Resources.ImportResultsControl_FindResultsFiles_Searching_for_matching_results_files_in__0__, directory);
            }

            try
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (longWaitBroker != null && longWaitBroker.IsCanceled)
                        return;

                    if (DataSourceUtil.IsDataSource(entry))
                        TryMatch(entry, overwrite);
                }
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
                // No permissions on folder
            }
        }

        public void TryMatch(string potentialMatch, bool overwrite)
        {
            if (string.IsNullOrEmpty(potentialMatch))
                return;

            foreach (var spectrumSourceFile in SpectrumSourceFiles.Keys)
            {
                if (spectrumSourceFile == null || (!overwrite && SpectrumSourceFiles[spectrumSourceFile].HasMatches))
                    continue;

                if (string.Equals(Path.GetFileName(spectrumSourceFile), Path.GetFileName(potentialMatch), StringComparison.OrdinalIgnoreCase))
                {
                    if (overwrite || !SpectrumSourceFiles[spectrumSourceFile].HasExactMatch)
                    {
                        SpectrumSourceFiles[spectrumSourceFile].ExactMatch = potentialMatch;
                    }
                }
                else if (MeasuredResults.IsBaseNameMatch(Path.GetFileNameWithoutExtension(spectrumSourceFile),
                    Path.GetFileNameWithoutExtension(potentialMatch)) &&
                    (overwrite || !SpectrumSourceFiles[spectrumSourceFile].HasAlternateMatch))
                {
                    SpectrumSourceFiles[spectrumSourceFile].AlternateMatch = potentialMatch;
                }
            }
        }

        public IEnumerable<FoundResultsFile> GetFoundResultsFiles(bool excludeSpectrumSourceFiles = false)
        {
            return !excludeSpectrumSourceFiles
                ? SpectrumSourceFiles.Values.Where(s => s.HasMatch)
                    .Select(s => new FoundResultsFile(s.Name, s.ExactMatch ?? s.AlternateMatch))
                    .OrderBy(fr => fr.Name, new NaturalFilenameComparer()).ToList()
                : SpectrumSourceFiles.Values.Where(s => s.HasAlternateMatch)
                    .Select(s => new FoundResultsFile(s.Name, s.AlternateMatch))
                    .OrderBy(fr => fr.Name, new NaturalFilenameComparer()).ToList();
        }

        public IEnumerable<string> GetMissingResultsFiles(bool excludeSpectrumSourceFiles = false)
        {
            return !excludeSpectrumSourceFiles
                ? SpectrumSourceFiles.Where(s => !s.Value.HasMatch)
                    .Select(s => s.Key)
                    .OrderBy(s => s, new NaturalFilenameComparer())
                : SpectrumSourceFiles.Where(s => !s.Value.HasAlternateMatch)
                    .Select(s => s.Key)
                    .OrderBy(s => s, new NaturalFilenameComparer());
        }

        public bool InitializeModifications(SrmDocument document)
        {
            if (DocLib == null && !IsDDASearch)
                return false;

            InitializeUserDefinedTypedMods(document);
            if (!IsDDASearch)
              UpdateModificationMatches(document);
            return true;
        }

        private void InitializeUserDefinedTypedMods(SrmDocument document)
        {
            var mods = document.Settings.PeptideSettings.Modifications;
            foreach (var type in mods.GetModificationTypes())
            {
                // Set the default heavy type to the first heavy type encountered.
                if (!ReferenceEquals(type, IsotopeLabelType.light) && DefaultHeavyLabelType == null)
                    DefaultHeavyLabelType = type;

                foreach (var mod in mods.GetModificationsByName(type.Name).Modifications.Where(m => !m.IsUserSet))
                    UserDefinedTypedMods.Add(mod);
            }

            var staticMods = new TypedModifications(IsotopeLabelType.light, mods.StaticModifications);
            var heavyMods = new TypedModifications(DefaultHeavyLabelType, mods.GetModifications(DefaultHeavyLabelType));

            foreach (var mod in staticMods.Modifications.Union(heavyMods.Modifications))
                UserDefinedTypedMods.Add(mod);
        }

        public void UpdateModificationMatches(SrmDocument document)
        {
            _matcher.ClearMatches();
            _matcher.CreateMatches(document.Settings, DocLib.Keys, Settings.Default.StaticModList, Settings.Default.HeavyModList, DocLib.Name);
        }

        public IEnumerable<StaticMod> GetMatchedMods()
        {
            var allMods = MatcherPepMods.StaticModifications.Union(MatcherHeavyMods);
            return allMods.Where(mod => !UserDefinedTypedMods.Any(mod.Equivalent));
        }

        public List<string> GetUnmatchedSequences()
        {
            return _matcher.UnmatchedSequences;
        }

        public SrmSettings AddModifications(SrmDocument document, PeptideModifications modifications)
        {
            if (!IsDDASearch)
            {
                _matcher.MatcherPepMods = modifications;
                return document.Settings.ChangePeptideModifications(mods => _matcher.SafeMergeImplicitMods(document));
            }
            else
            {
                return document.Settings.ChangePeptideSettings(
                    document.Settings.PeptideSettings.ChangeModifications(modifications));
            }
        }

        public static IEnumerable<FoundResultsFile> EnsureUniqueNames(IList<FoundResultsFile> files)
        {
            // Enforce uniqueness in names (might be constructed from list of files a.raw, a.mzML)
            return Helpers.EnsureUniqueNames(files.Select(f => f.Name).ToList()).Zip(files.Select(f => f.Path),
                (name, path) => new FoundResultsFile(name, path));
        }

        public static SrmDocument PrepareImportFasta(SrmDocument document)
        {
            // First preserve the state of existing document nodes in the tree
            // Todo: There are better ways to do this than this brute force method; revisit later.
            if (document.PeptideGroupCount > 0)
                document = ChangeAutoManageChildren(document, PickLevel.all, false);
            var pick = document.Settings.PeptideSettings.Libraries.Pick;
            if (pick != PeptidePick.library && pick != PeptidePick.both)
                document = document.ChangeSettings(document.Settings.ChangePeptideLibraries(lib => lib.ChangePick(PeptidePick.library)));
            return document;
        }

        public static SrmDocument ChangeAutoManageChildren(SrmDocument document, PickLevel which, bool autoPick)
        {
            var refine = new RefinementSettings { AutoPickChildrenAll = which, AutoPickChildrenOff = !autoPick };
            return refine.Refine(document);
        }

        public static SrmDocument ImportFasta(SrmDocument document, string fastaPath, IrtStandard irtStandard, IProgressMonitor monitor,
            IdentityPath to, out IdentityPath firstAdded, out IdentityPath nextAdd, out List<PeptideGroupDocNode> peptideGroupsNew)
        {
            var importer = new FastaImporter(document, irtStandard);
            using (TextReader reader = File.OpenText(fastaPath))
            {
                peptideGroupsNew = importer.Import(reader, monitor, Helpers.CountLinesInFile(fastaPath)).ToList();
                document = document.AddPeptideGroups(peptideGroupsNew, false, to, out firstAdded, out nextAdd);
            }
            return document;
        }

        /// <summary>
        /// Sets the peak scoring model that trains when results are imported: an mProphet model when
        /// <paramref name="autoTrain"/> is true, or the default model when the document has decoys.
        /// Returns null when <paramref name="autoTrain"/> is true but the document has no decoys to
        /// train the mProphet model with.
        /// </summary>
        public static SrmDocument ChangeAutoTrain(SrmDocument document, bool autoTrain)
        {
            var hasDecoys = document.Peptides.Any(pep => pep.IsDecoy);
            if (autoTrain)
            {
                if (!hasDecoys)
                    return null;
                return document.ChangeSettings(document.Settings.ChangePeptideIntegration(i =>
                    i.ChangeAutoTrain(PeptideIntegration.AutoTrainType.mprophet_model)));
            }
            if (hasDecoys)
            {
                return document.ChangeSettings(document.Settings.ChangePeptideIntegration(i =>
                    i.ChangeAutoTrain(PeptideIntegration.AutoTrainType.default_model)));
            }
            return document;
        }

        public static SrmDocument RemoveProteinsByPeptideCount(SrmDocument document, int minPeptides)
        {
            return minPeptides > 0 ? new RefinementSettings {MinPeptidesPerProtein = minPeptides}.Refine(document) : document;
        }

        public static SrmDocument AddStandardsToDocument(SrmDocument doc, IrtStandard standard)
        {
            return AddStandardsToDocument(doc, standard, out _);
        }

        /// <param name="doc">The document to add the standard peptides to</param>
        /// <param name="standard">The iRT standard</param>
        /// <param name="proteinsRemoved">The proteins removed because all their peptides are iRT standard peptides</param>
        public static SrmDocument AddStandardsToDocument(SrmDocument doc, IrtStandard standard, out int proteinsRemoved)
        {
            proteinsRemoved = 0;
            if (standard == null || standard.IsEmpty)
                return doc;

            // Move iRT proteins to top
            var irtPeptides = new HashSet<Target>(RCalcIrt.IrtPeptides(doc));
            var proteins = new List<PeptideGroupDocNode>(doc.PeptideGroups);
            var proteinsIrt = new List<PeptideGroupDocNode>();
            for (var i = 0; i < proteins.Count; i++)
            {
                var nodePepGroup = proteins[i];
                if (nodePepGroup.Peptides.All(nodePep => irtPeptides.Contains(new Target(nodePep.ModifiedSequence))))
                {
                    //proteinsIrt.Add(nodePepGroup);
                    proteins.RemoveAt(i--);
                }
            }

            var standardMap = new TargetMap<bool>(standard.Peptides.Select(pep => new KeyValuePair<Target, bool>(pep.ModifiedTarget, true)));
            var docStandards = new TargetMap<bool>(doc.Peptides
                .Where(nodePep => standardMap.ContainsKey(nodePep.ModifiedTarget))
                .Select(nodePep => new KeyValuePair<Target, bool>(nodePep.ModifiedTarget, true)));
            if (standard.HasDocument && standard.Peptides.Any(pep => !docStandards.ContainsKey(pep.ModifiedTarget)))
                return standard.ImportTo(doc);

            var modMatcher = new ModificationMatcher();
            modMatcher.CreateMatches(doc.Settings, standard.Peptides.Select(pep => pep.ModifiedTarget.ToString()),
                Settings.Default.StaticModList, Settings.Default.HeavyModList);
            var settingsWithNoMinIon = doc.Settings.ChangeTransitionSettings(t => t.ChangeLibraries(t.Libraries.ChangeMinIonCount(0)));
            var group = new PeptideGroupDocNode(new PeptideGroup(), Annotations.EMPTY, ModelResources.ImportFastaControl_ImportFasta_iRT_standards, null,
                standard.Peptides.Select(pep => modMatcher.GetModifiedNode(pep.ModifiedTarget.ToString()).ChangeSettings(settingsWithNoMinIon, SrmSettingsDiff.ALL).ChangeStandardType(StandardType.IRT)
                ).ToArray(), false);
            //var transitions = group.Peptides.SelectMany(p => p.TransitionGroups.SelectMany(t => t.Transitions.Select(t2 => t2.Id)));
            //Console.WriteLine(transitions.Count());
            proteinsRemoved = doc.PeptideGroupCount - proteins.Count;
            proteins.Insert(0, group);
            return (SrmDocument) doc.ChangeChildrenChecked(proteins.Cast<DocNode>().ToArray());

            //if (proteinsIrt.Any())
            //    return (SrmDocument)result.ChangeChildrenChecked(proteins.Cast<DocNode>().ToArray());
            //return result;
        }

        /// <summary>
        /// Adds the molecules feature detection found to the document: one molecule list for each
        /// library in the document that is not in <paramref name="existingLibraries"/>, with the
        /// molecules in natural sort order and only the precursor charges the transition filter
        /// allows. Returns null when feature detection added no library.
        /// </summary>
        public static SrmDocument AddDetectedFeatures(SrmDocument document, PeptideLibraries existingLibraries,
            IProgressMonitor progressMonitor, IProgressStatus status)
        {
            progressMonitor.UpdateProgress(status);
            if (Equals(document.Settings.PeptideSettings.Libraries.LibrarySpecs, existingLibraries.LibrarySpecs))
                return null;

            var docNew = document;
            foreach (var lib in document.Settings.PeptideSettings.Libraries.Libraries.Where(l =>
                         !existingLibraries.Libraries.Contains(l)))
            {
                if (lib != null)
                    docNew = AddDetectedFeatures(docNew, lib, status, progressMonitor);
                progressMonitor.UpdateProgress(status.ChangePercentComplete(100));
            }
            return docNew;
        }

        private static SrmDocument AddDetectedFeatures(SrmDocument document, Library lib, IProgressStatus status, IProgressMonitor progressMonitor)
        {
            var adducts = new HashSet<Adduct>(document.Settings.TransitionSettings.Filter.SmallMoleculeFragmentAdducts);
            var nodes = new List<PeptideDocNode>();
            var keys = lib.Keys.OrderBy(k => ViewLibraryPepInfo.MakeCompareKey(new ViewLibraryPepInfo(k))).ToArray();
            var keyCount = keys.Length;
            for (var k = 0; k < keyCount;)
            {
                var key = keys[k];
                var customMolecule = CustomMolecule.FromSmallMoleculeLibraryAttributes(key.SmallMoleculeLibraryAttributes);
                var mass = customMolecule.GetMass(MassType.Monoisotopic);
                var peptide = new Peptide(customMolecule);
                var children = new List<TransitionGroupDocNode>();
                // Look for multiple charges of same molecule
                while (k < keyCount && Equals(key.SmallMoleculeLibraryAttributes, keys[k].SmallMoleculeLibraryAttributes))
                {
                    key = keys[k];
                    progressMonitor.UpdateProgress(status.ChangePercentComplete(80 * k / keyCount));
                    k++;
                    // Filter on user-requested charge state
                    if (!document.Settings.TransitionSettings.Filter.PeptidePrecursorCharges.Any(charge => Equals(charge.AdductCharge, key.Adduct.AdductCharge)))
                    {
                        continue;
                    }
                    Assume.IsTrue(lib.TryLoadSpectrum(key, out SpectrumPeaksInfo _));
                    var precursor = new TransitionGroup(peptide, key.Adduct, IsotopeLabelType.light);
                    var precursorTransition = new Transition(precursor, key.Adduct, null,
                        customMolecule, IonType.precursor);
                    var precursorTransitionDocNode = new TransitionDocNode(precursorTransition,
                        Annotations.EMPTY, null, mass,
                        TransitionDocNode.TransitionQuantInfo.DEFAULT, null,
                        null);
                    lib.TryGetLibInfo(key, out var libInfo);
                    var precursorDocNode = new TransitionGroupDocNode(precursor, Annotations.EMPTY,
                        document.Settings, null, libInfo, null, null,
                        new[] { precursorTransitionDocNode },
                        false); // We will turn on autoManage in the next step
                    children.Add(precursorDocNode);
                }
                if (children.Any())
                {
                    var peptideDocNode = new PeptideDocNode(peptide, document.Settings, null, null,
                        null,
                        children.ToArray(), false); // We will turn on autoManage in the next step
                    nodes.Add(peptideDocNode);
                    adducts.Add(key.Adduct);
                }
            }

            var newPeptideGroup = new PeptideGroup();
            var newPeptideGroupDocNode = new PeptideGroupDocNode(newPeptideGroup,
                Annotations.EMPTY,
                lib.Name,
                null, nodes.ToArray(), true);

            // Make sure that transition settings filters include any newly used adduct types, and both precursor and fragment ions
            var transitionSettings = document.Settings.TransitionSettings;
            var filter = transitionSettings.Filter
                .ChangeSmallMoleculeFragmentAdducts(adducts.ToList())
                .ChangeSmallMoleculePrecursorAdducts(adducts.ToList())
                .ChangeSmallMoleculeIonTypes(new[] { IonType.custom, IonType.precursor });
            var newTransitionSettings = transitionSettings.ChangeFilter(filter);
            progressMonitor.UpdateProgress(status.ChangePercentComplete(90));

            // Add new nodes with auto select enabled, being careful not to mess with any existing nodes
            var docTmp = (SrmDocument)(document.ChangeChildren(new List<DocNode>(){newPeptideGroupDocNode})); // Temp doc with just the new nodes
            docTmp = docTmp.ChangeSettings(docTmp.Settings.ChangeTransitionSettings(newTransitionSettings)); // Update the settings
            // Now apply auto pick so that M+1, M+2 etc precursors get created
            docTmp = ChangeAutoManageChildren(docTmp, PickLevel.precursors | PickLevel.transitions, true);

            // Copy the resulting nodes to the actual doc, and update its settings too
            return ((SrmDocument)document.Add(docTmp.MoleculeGroups.First())).
                ChangeSettings(docTmp.Settings.ChangeTransitionSettings(newTransitionSettings));
        }

        public class FoundResultsFile
        {
            public string Name { get; set; }
            public string Path { get; set; }

            public FoundResultsFile(string name, string path)
            {
                Name = name;
                Path = path;
            }
        }

        /// <summary>
        /// Stores possible matches for spectrum source files.
        /// </summary>
        public class FoundResultsFilePossibilities
        {
            /// <summary>
            /// The name of the spectrum source file without extension.
            /// </summary>
            public string Name { get; private set; }

            /// <summary>
            /// The path to a match for the spectrum source file where the filename matches exactly.
            /// </summary>
            public string ExactMatch
            {
                get { return _exactMatch; }
                set { _exactMatch = value != null ? Path.GetFullPath(SampleHelp.GetPathFilePart(value)) : null; }  // This may actually be a MsDataFileUri string
            }
            private string _exactMatch;

            /// <summary>
            /// The path to a match for the spectrum source file where the filestem matches, but the extension doesn't.
            /// </summary>
            public string AlternateMatch
            {
                get { return _alternateMatch; }
                set { _alternateMatch = value != null ? Path.GetFullPath(SampleHelp.GetPathFilePart(value)) : null; }  // This may actually be a MsDataFileUri string
            }
            private string _alternateMatch;

            public FoundResultsFilePossibilities(string name)
            {
                Name = name;
                ExactMatch = null;
                AlternateMatch = null;
            }

            public FoundResultsFilePossibilities(string name, string path)
            {
                Name = name;
                ExactMatch = path;
                AlternateMatch = path;
            }

            public bool HasMatch { get { return HasExactMatch || HasAlternateMatch; } }
            public bool HasMatches { get { return HasExactMatch && HasAlternateMatch; } }
            public bool HasExactMatch { get { return ExactMatch != null; } }
            public bool HasAlternateMatch { get { return AlternateMatch != null; } }
        }

        public const double DEFAULT_RT_WINDOW = 10.0;
    }
}
