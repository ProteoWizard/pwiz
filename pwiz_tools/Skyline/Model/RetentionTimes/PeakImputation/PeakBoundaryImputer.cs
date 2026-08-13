/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Linq;
using System.Threading;
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;
using pwiz.Common.PeakFinding;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.CommonMsData;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.RetentionTimes.PeakImputation
{
    public class PeakBoundaryImputer
    {
        private readonly Dictionary<LibraryInfoKey, LibraryInfo> _libraryInfos = new Dictionary<LibraryInfoKey, LibraryInfo>();
        private readonly Dictionary<MsDataFileUri, AlignmentFunction> _alignmentFunctions =
            new Dictionary<MsDataFileUri, AlignmentFunction>();

        public PeakBoundaryImputer(SrmSettings settings) : this(settings, null)
        {
            
        }
        
        public PeakBoundaryImputer(SrmDocument document) : this(document.Settings, null)
        {
        }

        public PeakBoundaryImputer(SrmSettings settings, MProphetResultsHandler mProphetResultsHandler)
        {
            Settings = settings;
            MProphetResultsHandler = mProphetResultsHandler;
            settings.TryGetAlignmentTarget(out var alignmentTarget);
            AlignmentTarget = alignmentTarget;
        }
        
        public PeakBoundaryImputer(SrmDocument document, MProphetResultsHandler mProphetResultsHandler) : this(document.Settings, mProphetResultsHandler)
        {
        }

        public SrmSettings Settings { get; }
        public AlignmentTarget AlignmentTarget { get; }

        public MProphetResultsHandler MProphetResultsHandler { get; }

        private class LibraryInfo
        {
            private Dictionary<ImmutableList<Target>, ImmutableList<SourcedPeak>> _bestPeakBounds = new Dictionary<ImmutableList<Target>, ImmutableList<SourcedPeak>>();
            public LibraryInfo(Library library, LibraryFiles libraryFiles, Alignments alignment)
            {
                Library = library;
                LibraryFiles = libraryFiles;
                Alignments = alignment;
            }

            public Library Library { get; }
            public LibraryFiles LibraryFiles { get; private set; }

            public Alignments Alignments { get; }

            public AlignmentFunction GetAlignmentFunction(CancellationToken cancellationToken, string spectrumSourceFile)
            {
                return Alignments?.GetAlignmentFunction(spectrumSourceFile, true);
            }

            public IList<SourcedPeak> GetExemplaryPeaks(CancellationToken cancellationToken, ImmutableList<Target> targets)
            {
                lock (this)
                {
                    if (_bestPeakBounds.TryGetValue(targets, out var exemplaryPeaks))
                    {
                        return exemplaryPeaks;
                    }

                    exemplaryPeaks = FindExemplaryPeaks(cancellationToken, targets);
                    _bestPeakBounds[targets] = exemplaryPeaks;
                    return exemplaryPeaks;
                }
            }

            public IEnumerable<KeyValuePair<string, ExplicitPeakBounds>> GetAllExplicitPeakBounds(IList<Target> targets)
            {
                foreach (var filePath in LibraryFiles.FilePaths)
                {
                    if (false == Alignments?.ContainsFile(filePath))
                    {
                        continue;
                    }
                    var peakBounds = Library.GetExplicitPeakBounds(MsDataFileUri.Parse(filePath), targets);
                    if (peakBounds != null)
                    {
                        yield return new KeyValuePair<string, ExplicitPeakBounds>(filePath, peakBounds);
                    }
                }
            }

            private ImmutableList<SourcedPeak> FindExemplaryPeaks(CancellationToken cancellationToken, IList<Target> targets)
            {
                var bestScoringPeaks = GetAllExplicitPeakBounds(targets)
                    .Where(peak => !peak.Value.IsEmpty && !double.IsNaN(peak.Value.Score))
                    .GroupBy(peak => peak.Value.Score)
                    .OrderBy(group => group.Key).FirstOrDefault();
                if (bestScoringPeaks == null)
                {
                    return null;
                }

                return bestScoringPeaks.Select(kvp =>
                    new SourcedPeak(PeakSource.FromLibrary(Library, kvp.Key), kvp.Value.ToScoredPeak())).ToImmutable();
            }

            public LibraryInfo ChangeAlignment(Alignments alignments)
            {
                if (Equals(alignments, Alignments))
                {
                    return this;
                }

                lock (this)
                {
                    var newLibraryInfo = new LibraryInfo(Library, LibraryFiles, alignments);
                    foreach (var bestPeakBounds in _bestPeakBounds)
                    {
                        newLibraryInfo._bestPeakBounds.Add(bestPeakBounds.Key, bestPeakBounds.Value);
                    }

                    return newLibraryInfo;
                }
            }
        }
        private LibraryInfo GetLibraryInfo(Library library, string batchName)
        {
            lock (_libraryInfos)
            {
                var key = new LibraryInfoKey(library.Name, batchName);
                if (_libraryInfos.TryGetValue(key, out var libraryInfo))
                {
                    if (Equals(library, libraryInfo.Library))
                    {
                        return libraryInfo;
                    }
                }

                libraryInfo = new LibraryInfo(library, GetLibraryFilesForBatch(library, batchName), Settings.DocumentRetentionTimes.GetLibraryAlignment(library.Name)?.Alignments);
                _libraryInfos[key] = libraryInfo;
                return libraryInfo;
            }
        }

        private LibraryFiles GetLibraryFilesForBatch(Library library, string batchName)
        {
            if (batchName == null)
            {
                return library.LibraryFiles;
            }

            return new LibraryFiles(Settings.GetSpectrumSourceFilesInBatch(library.LibraryFiles, batchName));
        }

        public ImputedPeak GetImputedPeakBounds(CancellationToken cancellationToken, PeptideDocNode peptideDocNode,
            ChromatogramSet chromatogramSet, MsDataFileUri filePath)
        {
            return GetImputedPeakBounds(cancellationToken, peptideDocNode, chromatogramSet, filePath, false);
        }

        private ImputedPeak GetImputedPeakBounds(CancellationToken cancellationToken, PeptideDocNode peptideDocNode,
            ChromatogramSet chromatogramSet,
            MsDataFileUri filePath, bool explicitBoundsOnly)
        {
            if (peptideDocNode == null)
            {
                return null;
            }

            List<string> batchNames = new List<string>();
            if (string.IsNullOrEmpty(chromatogramSet?.BatchName))
            {
                batchNames.Add(null);
            }
            else
            {
                batchNames.Add(chromatogramSet.BatchName);
            }

            var targets = Settings.GetTargets(peptideDocNode).ToImmutable();
            foreach (var batchName in batchNames)
            {
                foreach (var library in Settings.PeptideSettings.Libraries.Libraries)
                {
                    if (library == null)
                    {
                        continue;
                    }
                    if (!library.IsLoaded)
                    {
                        return null;
                    }
                    if (!library.HasExplicitBounds || !library.UseExplicitPeakBounds)
                    {
                        continue;
                    }

                    var libraryInfo = GetLibraryInfo(library, batchName);
                    var exemplaryPeaks = libraryInfo.GetExemplaryPeaks(cancellationToken, targets);
                    if (exemplaryPeaks == null || exemplaryPeaks.Count == 0)
                    {
                        continue;
                    }

                    return GetMedianPeak(exemplaryPeaks.Select(peak => GetImputedPeakFromLibraryPeak(cancellationToken, libraryInfo, peak, filePath)));
                }
            }

            if (explicitBoundsOnly)
            {
                return null;
            }

            return GetImputedPeakFromDocument(cancellationToken, batchNames, peptideDocNode, filePath);
        }

        private ImputedPeak GetImputedPeakFromLibraryPeak(CancellationToken cancellationToken, LibraryInfo libraryInfo, SourcedPeak exemplaryPeak,
            MsDataFileUri filePath)
        {
            if (AlignmentTarget == null || filePath == null)
            {
                return new ImputedPeak(exemplaryPeak.Peak.PeakBounds, exemplaryPeak);
            }
            var library = libraryInfo.Library;
            var fileIndex = libraryInfo.Library.LibraryFiles.FindIndexOf(filePath);
            if (fileIndex < 0)
            {
                return null;
            }

            var fileAlignment = libraryInfo.GetAlignmentFunction(cancellationToken,
                library.LibraryFiles.FilePaths[fileIndex]);
            if (fileAlignment == null)
            {
                return null;
            }

            var libraryAlignment = libraryInfo.GetAlignmentFunction(cancellationToken,
                exemplaryPeak.Source.FilePath);
            if (libraryAlignment == null)
            {
                return null;
            }

            return MakeImputedPeak(libraryAlignment, exemplaryPeak, fileAlignment);
        }

        public static ImputedPeak MakeImputedPeak(AlignmentFunction sourceAlignmentFunction, SourcedPeak exemplaryPeak,
            AlignmentFunction targetAlignmentFunction)
        {
            // Console.Out.WriteLine("Start time {0} in file {1} normalized to {2} aligned to {3}",
            //     exemplaryPeak.PeakBounds.StartTime, exemplaryPeak.SpectrumSourceFile,
            //     sourceAlignmentFunction.GetX(exemplaryPeak.PeakBounds.StartTime),
            //     targetAlignmentFunction.GetY(sourceAlignmentFunction.GetX(exemplaryPeak.PeakBounds.StartTime)));
            // Console.Out.WriteLine("End time {0} in file {1} normalized to {2} aligned to {3}",
            //     exemplaryPeak.PeakBounds.EndTime, exemplaryPeak.SpectrumSourceFile,
            //     sourceAlignmentFunction.GetX(exemplaryPeak.PeakBounds.EndTime),
            //     targetAlignmentFunction.GetY(sourceAlignmentFunction.GetX(exemplaryPeak.PeakBounds.EndTime)));

            var imputedBounds = new PeakBounds(
                targetAlignmentFunction.GetY(sourceAlignmentFunction.GetX(exemplaryPeak.Peak.StartTime)),
                targetAlignmentFunction.GetY(sourceAlignmentFunction.GetX(exemplaryPeak.Peak.EndTime)));
            return new ImputedPeak(imputedBounds, exemplaryPeak);
        }

        private bool IsAcceptable(ImputationSettings imputationSettings, PeakBounds peakBounds, ImputedPeak imputedPeak)
        {
            if (peakBounds == null)
            {
                return !imputationSettings.ImputeMissingPeaks;
            }
            if (imputationSettings.MaxRtShift.HasValue)
            {
                var peakTime = (peakBounds.StartTime + peakBounds.EndTime) / 2;
                var imputedPeakTime = (imputedPeak.PeakBounds.StartTime + imputedPeak.PeakBounds.EndTime) / 2;
                var rtShift = Math.Abs(peakTime - imputedPeakTime);
                if (rtShift > imputationSettings.MaxRtShift.Value)
                {
                    return false;
                }
            }

            if (imputationSettings.MaxPeakWidthVariation.HasValue)
            {
                var peakWidth = (peakBounds.EndTime - peakBounds.StartTime) / 2;
                var imputedPeakWidth = (imputedPeak.PeakBounds.EndTime - imputedPeak.PeakBounds.StartTime) / 2;
                if (Math.Abs(imputedPeakWidth - peakWidth) >
                    imputationSettings.MaxPeakWidthVariation * imputedPeakWidth)
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsAcceptable(ChromPeak chromPeak, ImputedPeak imputedPeak)
        {
            if (!Settings.PeptideSettings.Imputation.HasImputation)
            {
                return true;
            }

            var peakBounds = chromPeak.IsEmpty ? null : new PeakBounds(chromPeak.StartTime, chromPeak.EndTime);
            return IsAcceptable(Settings.PeptideSettings.Imputation, peakBounds, imputedPeak);
        }

        private ImputedPeak GetImputedPeakFromDocument(CancellationToken cancellationToken, List<string> batchNames, PeptideDocNode peptideDocNode,
            MsDataFileUri filePath)
        {
            var bestPeaks = GetBestScoredPeaks(cancellationToken, peptideDocNode, batchNames);
            if (bestPeaks == null)
            {
                return null;
            }

            var imputedPeaks = new List<ImputedPeak>();
            foreach (var bestPeak in bestPeaks)
            {
                if (AlignmentTarget == null || bestPeaks.Count == 1 && Equals(filePath?.ToString(), bestPeaks[0].Source.FilePath))
                {
                    imputedPeaks.Add(MakeImputedPeak(AlignmentFunction.IDENTITY, bestPeak, AlignmentFunction.IDENTITY));
                    continue;
                }

                var sourceAlignment = GetAlignmentFunction(cancellationToken, MsDataFileUri.Parse(bestPeak.Source.FilePath));
                if (sourceAlignment == null)
                {
                    continue;
                }

                var targetAlignment = GetAlignmentFunction(cancellationToken, filePath);
                if (targetAlignment == null)
                {
                    continue;
                }

                imputedPeaks.Add(MakeImputedPeak(sourceAlignment, bestPeak, targetAlignment));
            }

            return GetMedianPeak(imputedPeaks);
        }

        private ImputedPeak GetMedianPeak(IEnumerable<ImputedPeak> imputedPeaks)
        {
            var orderedPeaks = imputedPeaks.Where(peak => null != peak)
                .OrderBy(peak => peak.PeakBounds.StartTime + peak.PeakBounds.EndTime).ToList();
            if (orderedPeaks.Count == 0)
            {
                return null;
            }

            if ((orderedPeaks.Count & 1) == 1)
            {
                return orderedPeaks[orderedPeaks.Count / 2];
            }

            return MergeImputedPeaks(new[]
                { orderedPeaks[orderedPeaks.Count / 2 - 1], orderedPeaks[orderedPeaks.Count / 2] });
        }

        private ImputedPeak MergeImputedPeaks(IList<ImputedPeak> imputedPeaks)
        {
            if (imputedPeaks.Count == 0)
            {
                return null;
            }

            if (imputedPeaks.Count == 1)
            {
                return imputedPeaks[0];
            }

            var peakBounds = new PeakBounds(imputedPeaks.Select(peak => peak.PeakBounds.StartTime).Mean(),
                imputedPeaks.Select(peak => peak.PeakBounds.EndTime).Mean());
            var peakSource =
                new PeakSource(UniqueOrDefault(imputedPeaks.Select(peak => peak.ExemplaryPeak.Source.FilePath)))
                    .ChangeLibraryName(UniqueOrDefault(imputedPeaks.Select(peak => peak.ExemplaryPeak.Source.LibraryName)))
                    .ChangeReplicateName(UniqueOrDefault(imputedPeaks.Select(peak => peak.ExemplaryPeak.Source.ReplicateName)));
            var scoredPeak = new ScoredPeakBounds(
                (float)imputedPeaks.Select(peak => (double)peak.ExemplaryPeak.Peak.ApexTime).Mean(),
                (float)imputedPeaks.Select(peak => (double)peak.ExemplaryPeak.Peak.StartTime).Mean(),
                (float)imputedPeaks.Select(peak => (double)peak.ExemplaryPeak.Peak.EndTime).Mean(),
                UniqueOrDefault(imputedPeaks.Select(peak => peak.ExemplaryPeak.Peak.Score)));
            var sourcedPeak = new SourcedPeak(peakSource, scoredPeak);
            return new ImputedPeak(peakBounds, sourcedPeak);
        }

        private static T UniqueOrDefault<T>(IEnumerable<T> items)
        {
            var distinct = items.Distinct().ToList();
            if (distinct.Count == 1)
            {
                return distinct[0];
            }

            return default;
        }

        /// <summary>
        /// Returns the best scoring peak from 
        /// </summary>
        private IList<SourcedPeak> GetBestScoredPeaks(CancellationToken cancellationToken, PeptideDocNode peptideDocNode, IList<string> batchNames)
        {
            Dictionary<MsDataFileUri, SourcedPeak> scoredPeaks;
            if (MProphetResultsHandler == null)
            {
                scoredPeaks = GetScoredPeaks(peptideDocNode);
            }
            else
            {
                scoredPeaks = GetReintegratedPeaks(peptideDocNode);
            }
            if (scoredPeaks == null || scoredPeaks.Count == 0)
            {
                return null;
            }

            foreach (var batchName in batchNames)
            {
                HashSet<MsDataFileUri> filePaths = null;
                var peaksInBatch = scoredPeaks.Values.AsEnumerable();
                if (batchName != null)
                {
                    filePaths = GetFilesInBatch(batchName).ToHashSet();
                    peaksInBatch = scoredPeaks.Where(kvp => filePaths.Contains(kvp.Key)).Select(kvp=>kvp.Value);
                }

                var bestScoringPeaks = peaksInBatch.GroupBy(peak => peak.Peak.Score)
                    .OrderByDescending(group => group.Key).FirstOrDefault();
                if (bestScoringPeaks == null)
                {
                    continue;
                }

                return bestScoringPeaks.ToList();
            }
            return null;
        }

        private IEnumerable<MsDataFileUri> GetFilesInBatch(string batch)
        {
            if (Settings.MeasuredResults == null)
            {
                return Array.Empty<MsDataFileUri>();
            }

            return Settings.MeasuredResults.Chromatograms.Where(chrom => batch == chrom.BatchName)
                .SelectMany(chrom => chrom.MSDataFilePaths);
        }

        private Dictionary<MsDataFileUri, SourcedPeak> GetScoredPeaks(PeptideDocNode peptideDocNode)
        {
            if (peptideDocNode == null)
            {
                return null;
            }

            bool anyReintegrated = peptideDocNode.AnyReintegratedPeaks();

            var measuredResults = Settings.MeasuredResults;
            if (measuredResults == null)
            {
                return null;
            }
            var result = new Dictionary<MsDataFileUri, SourcedPeak>();
            for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
            {
                var chromatogramSet = measuredResults.Chromatograms[replicateIndex];
                foreach (var transitionGroup in peptideDocNode.TransitionGroups)
                {
                    foreach (var transitionGroupChromInfo in transitionGroup.GetSafeChromInfo(replicateIndex))
                    {
                        if (transitionGroupChromInfo.UserSet == UserSet.TRUE ||
                            transitionGroupChromInfo.OptimizationStep != 0)
                        {
                            continue;
                        }

                        var scoredPeak = anyReintegrated ? transitionGroupChromInfo.ReintegratedPeak : transitionGroupChromInfo.OriginalPeak;
                        if (scoredPeak == null)
                        {
                            continue;
                        }

                        var fileInfo = chromatogramSet.GetFileInfo(transitionGroupChromInfo.FileId);
                        if (fileInfo == null)
                        {
                            return null;
                        }
                        if (result.ContainsKey(fileInfo.FilePath))
                        {
                            continue;
                        }
                        result[fileInfo.FilePath] =
                            new SourcedPeak(PeakSource.FromChromFile(chromatogramSet, fileInfo.FilePath), scoredPeak);
                    }
                }
            }
            return result;
        }

        private Dictionary<MsDataFileUri, SourcedPeak> GetReintegratedPeaks(PeptideDocNode peptideDocNode)
        {
            var result = new Dictionary<MsDataFileUri, SourcedPeak>();
            if (peptideDocNode == null)
            {
                return result;
            }
            var measuredResults = Settings.MeasuredResults;
            for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
            {
                var chromatogramSet = measuredResults.Chromatograms[replicateIndex];
                foreach (var chromFileInfo in chromatogramSet.MSDataFileInfos)
                {
                    if (result.ContainsKey(chromFileInfo.FilePath))
                    {
                        continue;
                    }

                    var peakFeatureStatistics = MProphetResultsHandler.GetPeakFeatureStatistics(peptideDocNode.Peptide, chromFileInfo.FileId);
                    if (peakFeatureStatistics == null || peakFeatureStatistics.BestPeakIndex < 0)
                    {
                        continue;
                    }

                    if (peakFeatureStatistics.BestPeakIndex < 0)
                    {
                        continue;
                    }

                    result.Add(chromFileInfo.FilePath,
                        new SourcedPeak(PeakSource.FromChromFile(chromatogramSet, chromFileInfo.FilePath),
                            peakFeatureStatistics.BestScoredPeak));
                }
            }

            return result;
        }


        private AlignmentFunction GetAlignmentFunction(CancellationToken cancellationToken, MsDataFileUri filePath)
        {
            if (AlignmentTarget == null || filePath == null)
            {
                return AlignmentFunction.IDENTITY;
            }

            lock (_alignmentFunctions)
            {
                if (!_alignmentFunctions.TryGetValue(filePath, out var alignmentFunction))
                {
                    alignmentFunction = Settings.DocumentRetentionTimes.GetRunToRunAlignmentFunction(
                        Settings.PeptideSettings.Libraries, filePath, true);
                    _alignmentFunctions[filePath] = alignmentFunction;
                }

                return alignmentFunction;
            }
        }

        public ImputedPeak GetImputedPeak(PeptideDocNode peptideDocNode, ChromatogramSet chromatogramSet, MsDataFileUri filePath, PeakBounds candidatePeak)
        {
            return GetImputedPeak(CancellationToken.None, peptideDocNode, chromatogramSet, filePath, candidatePeak);
        }

        public ImputedPeak GetImputedPeak(CancellationToken cancellationToken, PeptideDocNode peptideDocNode, ChromatogramSet chromatogramSet, MsDataFileUri filePath,
            PeakBounds candidatePeak)
        {
            var imputedPeak = GetImputedPeakBounds(cancellationToken, peptideDocNode, chromatogramSet, filePath);
            if (imputedPeak == null)
            {
                return null;
            }

            if (IsAcceptable(Settings.PeptideSettings.Imputation, candidatePeak, imputedPeak))
            {
                return null;
            }

            return imputedPeak;
        }

        public ImputedPeak GetImputedPeakQuick(PeptideDocNode peptideDocNode, ChromatogramSet chromatogramSet, 
            MsDataFileUri filePath)
        {
            return GetImputedPeakBounds(CancellationToken.None, peptideDocNode, chromatogramSet, filePath, false);
        }

        public SourcedPeak GetExemplaryPeak(PeptideDocNode peptideDocNode)
        {
            return GetImputedPeakBounds(CancellationToken.None, peptideDocNode, null, null)?.ExemplaryPeak;
        }

        public SrmDocument ImputePeak(CancellationToken cancellationToken, SrmDocument document, IdentityPath peptideIdentityPath)
        {
            var peptideDocNode = (PeptideDocNode)document.FindNode(peptideIdentityPath);
            if (peptideDocNode == null)
            {
                return document;
            }

            var measuredResults = document.Settings.MeasuredResults;
            if (measuredResults == null)
            {
                return document;
            }
            for (int iReplicate = 0; iReplicate < measuredResults.Chromatograms.Count; iReplicate++)
            {
                var chromatogramSet = measuredResults.Chromatograms[iReplicate];
                foreach (var chromFileInfo in chromatogramSet.MSDataFileInfos)
                {
                    var peakBounds = GetPeakBounds(peptideDocNode, iReplicate, chromFileInfo.FileId);
                    var imputedPeak = GetImputedPeak(cancellationToken, peptideDocNode, chromatogramSet, chromFileInfo.FilePath, peakBounds);
                    if (imputedPeak != null)
                    {
                        foreach (var transitionGroupDocNode in peptideDocNode.TransitionGroups)
                        {
                            var groupPath = new IdentityPath(peptideIdentityPath,
                                transitionGroupDocNode.TransitionGroup);
                            document = document.ChangePeak(groupPath, chromatogramSet.Name, chromFileInfo.FilePath,
                                null, imputedPeak.PeakBounds.StartTime, imputedPeak.PeakBounds.EndTime,
                                UserSet.REINTEGRATED, null, false);
                        }
                    }
                }
            }

            return document;
        }

        private PeakBounds GetPeakBounds(PeptideDocNode peptideDocNode, int replicateIndex,
            ChromFileInfoId chromFileInfoId)
        {
            double minStartTime = double.MaxValue;
            double maxEndTime = double.MinValue;
            foreach (var transitionGroupChromInfo in peptideDocNode.TransitionGroups.SelectMany(tg =>
                         tg.GetSafeChromInfo(replicateIndex)))
            {
                if (!ReferenceEquals(chromFileInfoId, transitionGroupChromInfo.FileId))
                {
                    continue;
                }

                if (transitionGroupChromInfo.StartRetentionTime.HasValue)
                {
                    minStartTime = Math.Min(transitionGroupChromInfo.StartRetentionTime.Value, minStartTime);
                }

                if (transitionGroupChromInfo.EndRetentionTime.HasValue)
                {
                    maxEndTime = Math.Max(maxEndTime, transitionGroupChromInfo.EndRetentionTime.Value);
                }
            }

            if (minStartTime >= maxEndTime)
            {
                return null;
            }

            return new PeakBounds(minStartTime, maxEndTime);
        }

        public ModifiedDocument ImputePeakBoundaries(SrmDocument document, ProductionMonitor productionMonitor, List<IdentityPath> peptidePaths)
        {
            var originalDocument = document;
            var changedPaths = new List<PropertyName>();
            document = document.BeginDeferSettingsChanges();
            for (int iPeptide = 0; iPeptide < peptidePaths.Count; iPeptide++)
            {
                productionMonitor.CancellationToken.ThrowIfCancellationRequested();
                productionMonitor.SetProgress(iPeptide * 100 / peptidePaths.Count);
                var peptidePath = peptidePaths[iPeptide];
                var newDoc = ImputePeak(productionMonitor.CancellationToken, document, peptidePath);
                if (!ReferenceEquals(document, newDoc))
                {
                    document = newDoc;
                    var auditLogProperty = PropertyName.ROOT
                        .SubProperty(document.FindNode(peptidePath.GetIdentity(0)).AuditLogText)
                        .SubProperty(document.FindNode(peptidePath).AuditLogText);
                    changedPaths.Add(auditLogProperty);
                }
            }

            if (changedPaths.Count == 0)
            {
                return null;
            }
            var messageType = MessageType.imputed_boundaries;
            AuditLogEntry auditLogEntry;
            if (changedPaths.Count == 1)
            {
                auditLogEntry = AuditLogEntry.CreateSimpleEntry(messageType, originalDocument.DocumentType, changedPaths[0].ToString());
            }
            else
            {
                auditLogEntry = AuditLogEntry.CreateSimpleEntry(messageType, originalDocument.DocumentType, MessageArgs.Create(changedPaths.Count).Args);
            }


            return new ModifiedDocument(document.EndDeferSettingsChanges(originalDocument,
                new SrmSettingsChangeMonitor(new SilentProgressMonitor(productionMonitor.CancellationToken),
                    string.Empty, new ProgressStatus()))).ChangeAuditLogEntry(auditLogEntry);
        }

        private class LibraryInfoKey
        {
            public LibraryInfoKey(string libraryName, string batchName)
            {
                LibraryName = libraryName;
                BatchName = string.IsNullOrEmpty(batchName) ? null : batchName;
            }
            public string LibraryName { get; private set; }
            public string BatchName { get; private set; }

            protected bool Equals(LibraryInfoKey other)
            {
                return LibraryName == other.LibraryName && BatchName == other.BatchName;
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((LibraryInfoKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((LibraryName != null ? LibraryName.GetHashCode() : 0) * 397) ^ (BatchName != null ? BatchName.GetHashCode() : 0);
                }
            }
        }
    }
}
