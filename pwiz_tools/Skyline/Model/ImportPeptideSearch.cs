﻿/*
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
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

        public string[] SearchFilenames { get; set; }
        public double CutoffScore { get; set; }
        public Library DocLib { get; private set; }
        public Dictionary<string, FoundResultsFilePossibilities> SpectrumSourceFiles { get; set; }

        public bool HasDocLib { get { return DocLib != null; } }
        public IrtStandard IrtStandard { get; set; }
        private readonly LibKeyModificationMatcher _matcher;
        private IsotopeLabelType DefaultHeavyLabelType { get; set; }
        public HashSet<StaticMod> UserDefinedTypedMods { get; private set; }
        public PeptideModifications MatcherPepMods { get { return _matcher.MatcherPepMods; } }
        public IList<StaticMod> MatcherHeavyMods { get { return MatcherPepMods.GetModifications(DefaultHeavyLabelType); } }

        public BiblioSpecLiteBuilder GetLibBuilder(SrmDocument doc, string docFilePath, bool includeAmbiguousMatches)
        {
            string outputPath = BiblioSpecLiteSpec.GetLibraryFileName(docFilePath);

            // Check to see if the library is already there, and if it is, 
            // "Append" instead of "Create"
            bool libraryExists = File.Exists(outputPath);
            var libraryBuildAction = LibraryBuildAction.Create;
            if (libraryExists)
            {
                if (doc.Settings.HasDocumentLibrary)
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
            return new BiblioSpecLiteBuilder(name, outputPath, SearchFilenames)
            {
                Action = libraryBuildAction,
                KeepRedundant = true,
                CutOffScore = CutoffScore,
                Id = Helpers.MakeId(name),
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
            var calc = new RCalcIrt(
                Helpers.GetUniqueName(libSpec.Name, Settings.Default.RTScoreCalculatorList.Select(lib => lib.Name).ToArray()),
                libSpec.FilePath);
            var predictor = new RetentionTimeRegression(
                Helpers.GetUniqueName(libSpec.Name, Settings.Default.RetentionTimeList.Select(rt => rt.Name).ToArray()),
                calc, null, null, EditRTDlg.DEFAULT_RT_WINDOW, new List<MeasuredRetentionTime>());
            Settings.Default.RTScoreCalculatorList.Add(calc);
            Settings.Default.RetentionTimeList.Add(predictor);
            return doc.ChangeSettings(
                doc.Settings.ChangePeptideSettings(
                    doc.Settings.PeptideSettings.ChangePrediction(
                        doc.Settings.PeptideSettings.Prediction.ChangeRetentionTime(predictor))));
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
            if (DocLib == null)
                return;

            var measuredResults = document.Settings.MeasuredResults;
            foreach (var dataFile in DocLib.LibraryFiles.FilePaths)
            {
                var msDataFilePath = new MsDataFilePath(dataFile);
                SpectrumSourceFiles[dataFile] = new FoundResultsFilePossibilities(msDataFilePath.GetFileNameWithoutExtension());

                // If a matching file is already in the document, then don't include
                // this library spectrum source in the set of files to find.
                if (measuredResults != null && measuredResults.FindMatchingMSDataFile(MsDataFileUri.Parse(dataFile)) != null)
                    continue;

                if (File.Exists(dataFile) && DataSourceUtil.IsDataSource(dataFile))
                {
                    // We've found the dataFile in the exact location
                    // specified in the document library, so just add it
                    // to the "FOUND" list.
                    SpectrumSourceFiles[dataFile].ExactMatch = msDataFilePath.ToString();
                }
            }
            DocLib.ReadStream.CloseStream();
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
                    yield return Path.GetDirectoryName(searchFilename);
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

                    if (entry != null && DataSourceUtil.IsDataSource(entry))
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

                if (Path.GetFileName(spectrumSourceFile).Equals(Path.GetFileName(potentialMatch)))
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
                ? SpectrumSourceFiles.Values.Where(s => s.HasMatch).Select(s => new FoundResultsFile(s.Name, s.ExactMatch ?? s.AlternateMatch)).ToList()
                : SpectrumSourceFiles.Values.Where(s => s.HasAlternateMatch).Select(s => new FoundResultsFile(s.Name, s.AlternateMatch)).ToList();
        }

        public IEnumerable<string> GetMissingResultsFiles(bool excludeSpectrumSourceFiles = false)
        {
            return !excludeSpectrumSourceFiles
                ? SpectrumSourceFiles.Where(s => !s.Value.HasMatch).Select(s => s.Key)
                : SpectrumSourceFiles.Where(s => !s.Value.HasAlternateMatch).Select(s => s.Key);
        }

        public bool InitializeModifications(SrmDocument document)
        {
            if (DocLib == null)
                return false;

            InitializeUserDefinedTypedMods(document);
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
            _matcher.CreateMatches(document.Settings, DocLib.Keys, Settings.Default.StaticModList, Settings.Default.HeavyModList);
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
            _matcher.MatcherPepMods = modifications;
            return document.Settings.ChangePeptideModifications(mods => _matcher.SafeMergeImplicitMods(document));
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

        public static SrmDocument ImportFasta(SrmDocument document, string fastaPath, IProgressMonitor monitor,
            IdentityPath to, out IdentityPath firstAdded, out IdentityPath nextAdd, out List<PeptideGroupDocNode> peptideGroupsNew)
        {
            var importer = new FastaImporter(document, false);
            using (TextReader reader = File.OpenText(fastaPath))
            {
                peptideGroupsNew = importer.Import(reader, monitor, Helpers.CountLinesInFile(fastaPath)).ToList();
                document = document.AddPeptideGroups(peptideGroupsNew, false, to, out firstAdded, out nextAdd);
            }
            return document;
        }

        public static SrmDocument RemoveProteinsByPeptideCount(SrmDocument document, int minPeptides)
        {
            return minPeptides > 0 ? new RefinementSettings {MinPeptidesPerProtein = minPeptides}.Refine(document) : document;
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
    }

    public class ImportPeptideSearchManager : BackgroundLoader, IFeatureScoreProvider
    {
        private SrmDocument _document;
        private IList<IPeakFeatureCalculator> _cacheCalculators;
        private PeakTransitionGroupFeatureSet _cachedFeatureScores;

        public override void ClearCache()
        {
        }

        protected override bool StateChanged(SrmDocument document, SrmDocument previous)
        {
            return document.Settings.PeptideSettings.Integration.AutoTrain;
        }

        protected override string IsNotLoadedExplained(SrmDocument document)
        {
            if (document.Settings.PeptideSettings.Integration.AutoTrain &&
                document.Settings.HasResults && document.MeasuredResults.IsLoaded)
            {
                return @"ImportPeptideSearchManager: Model not trained";
            }
            return null;
        }

        protected override IEnumerable<IPooledStream> GetOpenStreams(SrmDocument document)
        {
            yield break;
        }

        protected override bool IsCanceled(IDocumentContainer container, object tag)
        {
            return false;
        }

        protected override bool LoadBackground(IDocumentContainer container, SrmDocument document, SrmDocument docCurrent)
        {
            var loadMonitor = new LoadMonitor(this, container, container.Document);

            IPeakScoringModel scoringModel = new MProphetPeakScoringModel(
                Path.GetFileNameWithoutExtension(container.DocumentFilePath), null as LinearModelParams,
                MProphetPeakScoringModel.GetDefaultCalculators(docCurrent), true);

            var targetDecoyGenerator = new TargetDecoyGenerator(docCurrent, scoringModel, this, loadMonitor);

            // Get scores for target and decoy groups.
            List<IList<float[]>> targetTransitionGroups, decoyTransitionGroups;
            targetDecoyGenerator.GetTransitionGroups(out targetTransitionGroups, out decoyTransitionGroups);
            if (!decoyTransitionGroups.Any())
                throw new InvalidDataException();

            // Set intial weights based on previous model (with NaN's reset to 0)
            var initialWeights = new double[scoringModel.PeakFeatureCalculators.Count];
            // But then set to NaN the weights that have unknown values for this dataset
            for (var i = 0; i < initialWeights.Length; ++i)
            {
                if (!targetDecoyGenerator.EligibleScores[i])
                    initialWeights[i] = double.NaN;
            }
            var initialParams = new LinearModelParams(initialWeights);

            // Train the model.
            scoringModel = scoringModel.Train(targetTransitionGroups, decoyTransitionGroups, targetDecoyGenerator, initialParams, null, null, scoringModel.UsesSecondBest, true, loadMonitor);

            SrmDocument docNew;
            do
            {
                docCurrent = container.Document;
                docNew = docCurrent.ChangeSettings(docCurrent.Settings.ChangePeptideIntegration(i =>
                    i.ChangeAutoTrain(false).ChangePeakScoringModel((PeakScoringModelSpec) scoringModel)));

                // Reintegrate peaks
                var resultsHandler = new MProphetResultsHandler(docNew, (PeakScoringModelSpec) scoringModel, _cachedFeatureScores);
                resultsHandler.ScoreFeatures(loadMonitor);
                if (resultsHandler.IsMissingScores())
                    throw new InvalidDataException(Resources.ImportPeptideSearchManager_LoadBackground_The_current_peak_scoring_model_is_incompatible_with_one_or_more_peptides_in_the_document_);
                docNew = resultsHandler.ChangePeaks(loadMonitor);
            }
            while (!CompleteProcessing(container, docNew, docCurrent));

            return true;
        }

        public PeakTransitionGroupFeatureSet GetFeatureScores(SrmDocument document, IPeakScoringModel scoringModel,
            IProgressMonitor progressMonitor)
        {
            if (!ReferenceEquals(document, _document) ||
                !ArrayUtil.EqualsDeep(_cacheCalculators, scoringModel.PeakFeatureCalculators))
            {
                _document = document;
                _cacheCalculators = scoringModel.PeakFeatureCalculators;
                _cachedFeatureScores = document.GetPeakFeatures(_cacheCalculators, progressMonitor);
            }
            return _cachedFeatureScores;
        }
    }
}
