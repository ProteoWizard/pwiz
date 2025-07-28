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
using System.Runtime.CompilerServices;
using System.Threading;
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

        public PeakBoundaryImputer(SrmDocument document) : this(document, null)
        {

        }
        public PeakBoundaryImputer(SrmDocument document, MProphetResultsHandler mProphetResultsHandler)
        {
            Document = document;
            AlignmentTarget = AlignmentTarget.GetAlignmentTarget(document);
            MProphetResultsHandler = mProphetResultsHandler;
        }

        public SrmDocument Document { get; }
        public AlignmentTarget AlignmentTarget { get; }

        public MProphetResultsHandler MProphetResultsHandler { get; }

        public PeakBoundaryImputer ChangeDocument(SrmDocument newDocument)
        {
            if (ReferenceEquals(newDocument, Document))
            {
                return this;
            }

            var result = new PeakBoundaryImputer(newDocument);
            IList<KeyValuePair<LibraryInfoKey, LibraryInfo>> libraryInfos;
            lock (_libraryInfos)
            {
                libraryInfos = _libraryInfos.ToList();
            }
            var libraries = newDocument.Settings.PeptideSettings.Libraries;
            foreach (var entry in libraryInfos)
            {
                var libraryInfo = entry.Value;
                var newLibrary = libraries.Libraries.FirstOrDefault(lib => lib?.Name == libraryInfo.Library.Name);
                if (newLibrary == null || !Equals(newLibrary, libraryInfo.Library))
                {
                    continue;
                }

                if (entry.Key.BatchName != null)
                {
                    var newLibraryFiles = new LibraryFiles(newDocument.Settings.GetSpectrumSourceFilesInBatch(newLibrary.LibraryFiles, entry.Key.BatchName));
                    if (!newLibraryFiles.SequenceEqual(entry.Value.LibraryFiles))
                    {
                        continue;
                    }
                }

                var newLibraryInfo = libraryInfo.ChangeAlignment(newDocument.Settings.DocumentRetentionTimes
                    .GetLibraryAlignment(libraryInfo.Library.Name)?.Alignments);
                if (newLibraryInfo != null)
                {
                    result._libraryInfos.Add(entry.Key, newLibraryInfo);
                }
            }

            return result;
        }

        private class LibraryInfo
        {
            private Dictionary<ImmutableList<Target>, ExemplaryPeak> _bestPeakBounds = new Dictionary<ImmutableList<Target>, ExemplaryPeak>();
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

            public ExemplaryPeak GetExemplaryPeak(CancellationToken cancellationToken, ImmutableList<Target> targets)
            {
                lock (this)
                {
                    if (_bestPeakBounds.TryGetValue(targets, out var exemplaryPeak))
                    {
                        return exemplaryPeak;
                    }

                    exemplaryPeak = FindExemplaryPeak(cancellationToken, targets);
                    _bestPeakBounds[targets] = exemplaryPeak;
                    return exemplaryPeak;
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

            private ExemplaryPeak FindExemplaryPeak(CancellationToken cancellationToken, IList<Target> targets)
            {
                ExplicitPeakBounds bestPeakBounds = null;
                string bestFile = null;
                foreach (var keyValuePair in GetAllExplicitPeakBounds(targets))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var peakBounds = keyValuePair.Value;
                    if (peakBounds.IsEmpty || double.IsNaN(peakBounds.Score))
                    {
                        continue;
                    }
                    if (bestPeakBounds == null || bestPeakBounds.Score > peakBounds.Score)
                    {
                        bestPeakBounds = peakBounds;
                        bestFile = keyValuePair.Key;
                    }
                }

                if (bestPeakBounds == null)
                {
                    return null;
                }

                return new ExemplaryPeak(Library, bestFile, bestPeakBounds.PeakBounds);
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

        public class ImputedBoundsParameter : Immutable
        {
            public ImputedBoundsParameter(SrmDocument document, IdentityPath identityPath, MsDataFileUri filePath)
            {
                Document = document;
                IdentityPath = identityPath;
                AlignmentTarget = AlignmentTarget.GetAlignmentTarget(document);
                FilePath = filePath;
            }
            
            public SrmDocument Document { get; }
            public IdentityPath IdentityPath { get; }
            public AlignmentTarget AlignmentTarget { get; private set; }
            public MsDataFileUri FilePath { get; private set; }

            public ImputedBoundsParameter ChangeAlignmentTarget(AlignmentTarget value)
            {
                return ChangeProp(ImClone(this), im => im.AlignmentTarget = value);
            }

            protected bool Equals(ImputedBoundsParameter other)
            {
                return ReferenceEquals(Document, other.Document) && Equals(IdentityPath, other.IdentityPath) && Equals(AlignmentTarget, other.AlignmentTarget) && Equals(FilePath, other.FilePath);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((ImputedBoundsParameter)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = RuntimeHelpers.GetHashCode(Document);
                    hashCode = (hashCode * 397) ^ IdentityPath.GetHashCode();
                    hashCode = (hashCode * 397) ^ (AlignmentTarget != null ? AlignmentTarget.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ FilePath?.GetHashCode() ?? 0;
                    return hashCode;
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

                libraryInfo = new LibraryInfo(library, GetLibraryFilesForBatch(library, batchName), Document.Settings.DocumentRetentionTimes.GetLibraryAlignment(library.Name)?.Alignments);
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

            return new LibraryFiles(Document.Settings.GetSpectrumSourceFilesInBatch(library.LibraryFiles, batchName));
        }

        public ImputedPeak GetImputedPeakBounds(CancellationToken cancellationToken, IdentityPath identityPath,
            ChromatogramSet chromatogramSet, MsDataFileUri filePath)
        {
            return GetImputedPeakBounds(cancellationToken, identityPath, chromatogramSet, filePath, false, false);
        }

        private ImputedPeak GetImputedPeakBounds(CancellationToken cancellationToken, IdentityPath identityPath,
            ChromatogramSet chromatogramSet,
            MsDataFileUri filePath, bool explicitBoundsOnly, bool noRecalculateScores)
        {
            var peptideDocNode = (PeptideDocNode)Document.FindNode(identityPath);
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

            var targets = Document.Settings.GetTargets(peptideDocNode).ToImmutable();
            foreach (var batchName in batchNames)
            {
                foreach (var library in Document.Settings.PeptideSettings.Libraries.Libraries)
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
                    var exemplaryPeak = libraryInfo.GetExemplaryPeak(cancellationToken, targets);
                    if (exemplaryPeak == null)
                    {
                        continue;
                    }

                    return GetImputedPeakFromLibraryPeak(cancellationToken, libraryInfo, exemplaryPeak, filePath);
                }
            }

            if (explicitBoundsOnly)
            {
                return null;
            }

            return GetImputedPeakFromDocument(cancellationToken, batchNames, identityPath, filePath, noRecalculateScores);
        }

        private ImputedPeak GetImputedPeakFromLibraryPeak(CancellationToken cancellationToken, LibraryInfo libraryInfo, ExemplaryPeak exemplaryPeak,
            MsDataFileUri filePath)
        {
            if (AlignmentTarget == null)
            {
                return new ImputedPeak(exemplaryPeak.PeakBounds, exemplaryPeak);
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
                exemplaryPeak.SpectrumSourceFile);
            if (libraryAlignment == null)
            {
                return null;
            }

            return MakeImputedPeak(libraryAlignment, exemplaryPeak, fileAlignment);
        }

        public static ImputedPeak MakeImputedPeak(AlignmentFunction sourceAlignmentFunction, ExemplaryPeak exemplaryPeak,
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
                targetAlignmentFunction.GetY(sourceAlignmentFunction.GetX(exemplaryPeak.PeakBounds.StartTime)),
                targetAlignmentFunction.GetY(sourceAlignmentFunction.GetX(exemplaryPeak.PeakBounds.EndTime)));
            return new ImputedPeak(imputedBounds, exemplaryPeak);
        }

        public ExplicitPeakBounds GetExplicitPeakBounds(PeptideDocNode peptideDocNode, ChromatogramSet chromatogramSet, MsDataFileUri filePath)
        {
            var explicitPeakBounds = Document.Settings.GetExplicitPeakBounds(peptideDocNode, filePath);
            if (explicitPeakBounds == null)
            {
                return null;
            }
            var imputationSettings = Document.Settings.PeptideSettings.Imputation;
            if (Equals(imputationSettings, ImputationSettings.DEFAULT))
            {
                return explicitPeakBounds;
            }

            if (!(imputationSettings.MaxPeakWidthVariation.HasValue || imputationSettings.MaxRtShift.HasValue) &&
                !explicitPeakBounds.IsEmpty)
            {
                return explicitPeakBounds;
            }

            var identityPath = GetIdentityPath(peptideDocNode);
            if (identityPath == null)
            {
                return explicitPeakBounds;
            }
            var imputedPeak = GetImputedPeakBounds(CancellationToken.None, identityPath, chromatogramSet, filePath, true, false);
            if (imputedPeak == null)
            {
                return explicitPeakBounds;
            }
            if (!explicitPeakBounds.IsEmpty && IsAcceptable(imputationSettings, new PeakBounds(explicitPeakBounds.StartTime, explicitPeakBounds.EndTime), imputedPeak))
            {
                return explicitPeakBounds;
            }

            return new ExplicitPeakBounds(imputedPeak.PeakBounds.StartTime, imputedPeak.PeakBounds.EndTime, ExplicitPeakBounds.UNKNOWN_SCORE);
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

        private ImputedPeak GetImputedPeakFromDocument(CancellationToken cancellationToken, List<string> batchNames, IdentityPath identityPath,
            MsDataFileUri filePath, bool noRecalculateScores)
        {
            var bestKeyValuePair = GetBestScoredPeak(identityPath, batchNames, noRecalculateScores);
            var bestPeak = bestKeyValuePair.Value;
            if (bestPeak == null)
            {
                return null;
            }

            var bestFile = bestKeyValuePair.Key;
            var exemplaryPeak = new ExemplaryPeak(null, bestFile.ToString(), bestPeak.PeakBounds);
            if (AlignmentTarget == null || Equals(filePath, bestFile))
            {
                return MakeImputedPeak(AlignmentFunction.IDENTITY, exemplaryPeak, AlignmentFunction.IDENTITY);
            }

            var sourceAlignment = GetAlignmentFunction(cancellationToken, bestFile);
            if (sourceAlignment == null)
            {
                return null;
            }

            var targetAlignment = GetAlignmentFunction(cancellationToken, filePath);
            if (targetAlignment == null)
            {
                return null;
            }

            return MakeImputedPeak(sourceAlignment, exemplaryPeak, targetAlignment);
        }

        private KeyValuePair<MsDataFileUri, ScoredPeak> GetBestScoredPeak(IdentityPath identityPath, IList<string> batchNames, bool noRecalculateScores)
        {
            Dictionary<MsDataFileUri, ScoredPeak> scoredPeaks;
            if (MProphetResultsHandler == null)
            {
                scoredPeaks = GetScoredPeaks(identityPath);
            }
            else
            {
                scoredPeaks = GetReintegratedPeaks(identityPath);
            }
            if (scoredPeaks == null || scoredPeaks.Count == 0)
            {
                return default;
            }

            foreach (var batchName in batchNames)
            {
                HashSet<MsDataFileUri> filePaths = null;
                if (batchName != null)
                {
                    filePaths = GetFilesInBatch(batchName).ToHashSet();
                }
                ScoredPeak bestPeak = null;
                MsDataFileUri bestFile = null;
                foreach (var keyValuePair in scoredPeaks)
                {
                    if (false == filePaths?.Contains(keyValuePair.Key))
                    {
                        continue;
                    }
                    var peak = keyValuePair.Value;
                    if (bestPeak == null || peak.Score > bestPeak.Score)
                    {
                        bestPeak = peak;
                        bestFile = keyValuePair.Key;
                    }
                }

                if (bestPeak != null)
                {
                    return new KeyValuePair<MsDataFileUri, ScoredPeak>(bestFile, bestPeak);
                }
            }
            return default;
        }

        private IEnumerable<MsDataFileUri> GetFilesInBatch(string batch)
        {
            if (Document.MeasuredResults == null)
            {
                return Array.Empty<MsDataFileUri>();
            }

            return Document.Settings.MeasuredResults.Chromatograms.Where(chrom => batch == chrom.BatchName)
                .SelectMany(chrom => chrom.MSDataFilePaths);
        }

        private Dictionary<MsDataFileUri, ScoredPeak> GetScoredPeaks(IdentityPath identityPath)
        {
            var result = new Dictionary<MsDataFileUri, ScoredPeak>();
            var peptideDocNode = (PeptideDocNode)Document.FindNode(identityPath);
            if (peptideDocNode == null)
            {
                return result;
            }

            bool anyReintegrated = peptideDocNode.TransitionGroups.SelectMany(tg => tg.Results).SelectMany(list => list)
                .Any(c => c.ReintegratedPeak != null);

            var measuredResults = Document.Settings.MeasuredResults;
            if (measuredResults == null)
            {
                return result;
            }

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
                        if (result.ContainsKey(fileInfo.FilePath))
                        {
                            continue;
                        }

                        result[fileInfo.FilePath] = scoredPeak;
                    }
                }
            }

            return result;
        }
        private Dictionary<MsDataFileUri, ScoredPeak> GetReintegratedPeaks(IdentityPath identityPath)
        {
            var result = new Dictionary<MsDataFileUri, ScoredPeak>();
            var peptideDocNode = (PeptideDocNode)Document.FindNode(identityPath);
            if (peptideDocNode == null)
            {
                return result;
            }
            var measuredResults = Document.Settings.MeasuredResults;
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

                    result.Add(chromFileInfo.FilePath, peakFeatureStatistics.BestScoredPeak);
                }
            }

            return result;
        }


        private AlignmentFunction GetAlignmentFunction(CancellationToken cancellationToken, MsDataFileUri filePath)
        {
            if (AlignmentTarget == null)
            {
                return AlignmentFunction.IDENTITY;
            }

            lock (_alignmentFunctions)
            {
                if (!_alignmentFunctions.TryGetValue(filePath, out var alignmentFunction))
                {
                    alignmentFunction = Document.Settings.DocumentRetentionTimes.GetAlignmentFunction(
                        Document.Settings.PeptideSettings.Libraries, filePath, true);
                    _alignmentFunctions[filePath] = alignmentFunction;
                }

                return alignmentFunction;
            }
        }

        private IdentityPath GetIdentityPath(PeptideDocNode peptideDocNode)
        {
            if (peptideDocNode.Peptide.FastaSequence != null &&
                Document.FindNodeIndex(peptideDocNode.Peptide.FastaSequence) >= 0)
            {
                return new IdentityPath(peptideDocNode.Peptide.FastaSequence, peptideDocNode.Peptide);
            }

            var moleculeGroup =
                Document.MoleculeGroups.FirstOrDefault(mg => mg.FindNodeIndex(peptideDocNode.Peptide) >= 0);
            if (moleculeGroup == null)
            {
                return null;
            }

            return new IdentityPath(moleculeGroup.PeptideGroup, peptideDocNode.Peptide);
        }

        public ImputedPeak GetImputedPeak(PeptideDocNode peptideDocNode, ChromatogramSet chromatogramSet, MsDataFileUri filePath, PeakBounds candidatePeak)
        {
            var identityPath = GetIdentityPath(peptideDocNode);
            if (identityPath == null)
            {
                return null;
            }

            return GetImputedPeak(CancellationToken.None, identityPath, chromatogramSet, filePath, candidatePeak);
        }

        public ImputedPeak GetImputedPeak(CancellationToken cancellationToken, IdentityPath peptideIdentityPath, ChromatogramSet chromatogramSet, MsDataFileUri filePath,
            PeakBounds candidatePeak)
        {
            var imputedPeak = GetImputedPeakBounds(cancellationToken, peptideIdentityPath, chromatogramSet, filePath);
            if (imputedPeak == null)
            {
                return null;
            }

            if (IsAcceptable(Document.Settings.PeptideSettings.Imputation, candidatePeak, imputedPeak))
            {
                return null;
            }

            return imputedPeak;
        }

        public ImputedPeak GetImputedPeakQuick(IdentityPath peptideIdentityPath, ChromatogramSet chromatogramSet, 
            MsDataFileUri filePath)
        {
            return GetImputedPeakBounds(CancellationToken.None, peptideIdentityPath, chromatogramSet, filePath, false, true);
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
                    var imputedPeak = GetImputedPeak(cancellationToken, peptideIdentityPath, chromatogramSet, chromFileInfo.FilePath, peakBounds);
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

        public ModifiedDocument ImputePeakBoundaries(ProductionMonitor productionMonitor, List<IdentityPath> peptidePaths)
        {
            var originalDocument = Document;
            var changedPaths = new List<PropertyName>();
            var document = Document.BeginDeferSettingsChanges();
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
