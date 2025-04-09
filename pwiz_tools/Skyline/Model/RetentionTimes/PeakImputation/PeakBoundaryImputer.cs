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
using pwiz.Common.PeakFinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.RetentionTimes.PeakImputation
{
    public class PeakBoundaryImputer
    {
        private static WeakReference<PeakBoundaryImputer> _sharedInstance;
        private Dictionary<string, LibraryInfo> _libraryInfos = new Dictionary<string, LibraryInfo>();
        private Dictionary<MsDataFileUri, AlignmentFunction> _alignmentFunctions =
            new Dictionary<MsDataFileUri, AlignmentFunction>();
        private Dictionary<IdentityPath, Dictionary<MsDataFileUri, ScoredPeak>> _scoredPeaks =
            new SerializableDictionary<IdentityPath, Dictionary<MsDataFileUri, ScoredPeak>>();

        public PeakBoundaryImputer(SrmDocument document)
        {
            Document = document;
            AlignmentTarget = AlignmentTarget.GetAlignmentTarget(document);
        }

        public SrmDocument Document { get; }
        public AlignmentTarget AlignmentTarget { get; }

        public PeakBoundaryImputer ChangeDocument(SrmDocument newDocument)
        {
            if (ReferenceEquals(newDocument, Document))
            {
                return this;
            }

            var result = new PeakBoundaryImputer(newDocument);
            lock (this)
            {
                foreach (var libraryInfo in _libraryInfos.Values)
                {
                    var newLibrary = newDocument.Settings.PeptideSettings.Libraries.Libraries.FirstOrDefault(lib => lib?.Name == libraryInfo.Library.Name);
                    if (!Equals(newLibrary, libraryInfo.Library))
                    {
                        continue;
                    }

                    var newLibraryInfo = libraryInfo.ChangeAlignmentTarget(result.AlignmentTarget);
                    if (newLibraryInfo != null)
                    {
                        result._libraryInfos.Add(newLibraryInfo.Library.Name, newLibraryInfo);
                    }
                }
            }

            return result;
        }

        public static PeakBoundaryImputer GetInstance(SrmDocument document)
        {
            lock (typeof(PeakBoundaryImputer))
            {
                PeakBoundaryImputer peakBoundaryImputer = null;
                if (true == _sharedInstance?.TryGetTarget(out peakBoundaryImputer))
                {
                    if (ReferenceEquals(peakBoundaryImputer.Document, document))
                    {
                        return peakBoundaryImputer;
                    }
                }

                peakBoundaryImputer =
                    peakBoundaryImputer?.ChangeDocument(document) ?? new PeakBoundaryImputer(document);
                _sharedInstance = new WeakReference<PeakBoundaryImputer>(peakBoundaryImputer);
                return peakBoundaryImputer;
            }
        }



        public PeakBoundaryImputer()
        {
        }
        
        private class LibraryInfo
        {
            private Dictionary<string, AlignmentFunction> _alignmentFunctions =
                new Dictionary<string, AlignmentFunction>();
            private Dictionary<Target, ExemplaryPeak> _bestPeakBounds = new Dictionary<Target, ExemplaryPeak>();
            public LibraryInfo(Library library, Dictionary<Target, double>[] allRetentionTimes, AlignmentTarget alignmentTarget)
            {
                Library = library;
                AllRetentionTimes = allRetentionTimes;
                AlignmentTarget = alignmentTarget;
            }

            public Library Library { get; }
            public Dictionary<Target, double>[] AllRetentionTimes { get; set; }
            public AlignmentTarget AlignmentTarget { get; }

            public AlignmentFunction GetAlignmentFunction(CancellationToken cancellationToken, string spectrumSourceFile)
            {
                if (AlignmentTarget == null)
                {
                    return AlignmentFunction.IDENTITY;
                }
                AlignmentFunction alignmentFunction;
                lock (this)
                {
                    if (_alignmentFunctions.TryGetValue(spectrumSourceFile, out alignmentFunction))
                    {
                        return alignmentFunction;
                    }
                }

                int fileIndex = Library.LibraryFiles.FilePaths.IndexOf(spectrumSourceFile);
                if (fileIndex < 0)
                {
                    return null;
                }

                alignmentFunction = AlignmentTarget.PerformAlignment(AllRetentionTimes[fileIndex], cancellationToken);
                lock (this)
                {
                    _alignmentFunctions[spectrumSourceFile] = alignmentFunction;
                }
                return alignmentFunction;
            }

            public ExemplaryPeak GetExemplaryPeak(CancellationToken cancellationToken, Target target)
            {
                lock (this)
                {
                    if (_bestPeakBounds.TryGetValue(target, out var exemplaryPeak))
                    {
                        return exemplaryPeak;
                    }

                    exemplaryPeak = FindExemplaryPeak(cancellationToken, new[] { target });
                    _bestPeakBounds[target] = exemplaryPeak;
                    return exemplaryPeak;
                }
            }

            public IEnumerable<KeyValuePair<string, ExplicitPeakBounds>> GetAllExplicitPeakBounds(IList<Target> targets)
            {
                foreach (var filePath in Library.LibraryFiles.FilePaths)
                {
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

                return new ExemplaryPeak(Library, bestFile, new PeakBounds(bestPeakBounds.StartTime, bestPeakBounds.EndTime));
            }

            public LibraryInfo ChangeAlignmentTarget(AlignmentTarget alignmentTarget)
            {
                if (Equals(alignmentTarget, AlignmentTarget))
                {
                    return this;
                }

                lock (this)
                {
                    var newLibraryInfo = new LibraryInfo(Library, AllRetentionTimes, alignmentTarget);
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

        private LibraryInfo GetLibraryInfo(Library library)
        {
            lock (this)
            {
                if (_libraryInfos.TryGetValue(library.Name, out var libraryInfo))
                {
                    if (Equals(library, libraryInfo.Library))
                    {
                        return libraryInfo;
                    }
                }

                libraryInfo = new LibraryInfo(library, library.GetAllRetentionTimes(), AlignmentTarget.GetAlignmentTarget(Document));
                _libraryInfos[library.Name] = libraryInfo;
                return libraryInfo;
            }
        }

        public ImputedPeak GetImputedPeakBounds(CancellationToken cancellationToken, IdentityPath identityPath,
            MsDataFileUri filePath)
        {
            return GetImputedPeakBounds(cancellationToken, identityPath, filePath, false);
        }

        private ImputedPeak GetImputedPeakBounds(CancellationToken cancellationToken, IdentityPath identityPath, MsDataFileUri filePath, bool explicitBoundsOnly)
        {
            var peptideDocNode = (PeptideDocNode)Document.FindNode(identityPath);
            if (peptideDocNode == null)
            {
                return null;
            }

            foreach (var library in Document.Settings.PeptideSettings.Libraries.Libraries)
            {
                if (true != library?.IsLoaded)
                {
                    continue;
                }

                if (!library.HasExplicitBounds || !library.UseExplicitPeakBounds)
                {
                    continue;
                }

                var libraryInfo = GetLibraryInfo(library);
                var exemplaryPeak = libraryInfo.GetExemplaryPeak(cancellationToken, peptideDocNode.ModifiedTarget);
                if (exemplaryPeak == null)
                {
                    continue;
                }

                return GetImputedPeakFromLibraryPeak(cancellationToken, libraryInfo, exemplaryPeak, filePath);
            }

            if (explicitBoundsOnly)
            {
                return null;
            }
            return GetImputedPeakFromDocument(cancellationToken, identityPath, filePath);
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

        private ImputedPeak MakeImputedPeak(AlignmentFunction sourceAlignmentFunction, ExemplaryPeak exemplaryPeak,
            AlignmentFunction targetAlignmentFunction)
        {
            var midTime = (exemplaryPeak.PeakBounds.StartTime + exemplaryPeak.PeakBounds.EndTime) / 2;
            var halfPeakWidth = (exemplaryPeak.PeakBounds.EndTime - exemplaryPeak.PeakBounds.StartTime) / 2;
            var newMidTime = targetAlignmentFunction.GetY(sourceAlignmentFunction.GetX(midTime));
            var imputedBounds = new PeakBounds(newMidTime - halfPeakWidth, newMidTime + halfPeakWidth);
            return new ImputedPeak(imputedBounds, exemplaryPeak);
        }

        public ExplicitPeakBounds GetExplicitPeakBounds(PeptideDocNode peptideDocNode, MsDataFileUri filePath)
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

            var moleculeGroup = Document.MoleculeGroups.FirstOrDefault(mg => mg.FindNodeIndex(peptideDocNode.Peptide) >= 0);
            if (moleculeGroup == null)
            {
                return explicitPeakBounds;
            }

            var identityPath = new IdentityPath(moleculeGroup.PeptideGroup, peptideDocNode.Peptide);
            var imputedPeak = GetImputedPeakBounds(CancellationToken.None, identityPath, filePath, false);
            if (IsAcceptable(imputationSettings, explicitPeakBounds, imputedPeak))
            {
                return explicitPeakBounds;
            }

            return new ExplicitPeakBounds(imputedPeak.PeakBounds.StartTime, imputedPeak.PeakBounds.EndTime, ExplicitPeakBounds.UNKNOWN_SCORE);
        }

        private bool IsAcceptable(ImputationSettings imputationSettings, ExplicitPeakBounds peakBounds, ImputedPeak imputedPeak)
        {
            if (peakBounds.IsEmpty)
            {
                return false;
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
                    return true;
                }
            }

            return false;
        }

        private ImputedPeak GetImputedPeakFromDocument(CancellationToken cancellationToken, IdentityPath identityPath,
            MsDataFileUri filePath)
        {
            var scoredPeaks = GetScoredPeaks(identityPath);
            ScoredPeak bestPeak = null;
            MsDataFileUri bestFile = null;
            foreach (var keyValuePair in scoredPeaks)
            {
                var peak = keyValuePair.Value;
                if (bestPeak == null || peak.Score > bestPeak.Score)
                {
                    bestPeak = peak;
                    bestFile = keyValuePair.Key;
                }
            }

            if (bestPeak == null)
            {
                return null;
            }

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

        private Dictionary<MsDataFileUri, ScoredPeak> GetScoredPeaks(IdentityPath identityPath)
        {
            lock (_scoredPeaks)
            {
                if (_scoredPeaks.TryGetValue(identityPath, out var scoredPeaks))
                {
                    return scoredPeaks;
                }

                scoredPeaks = CalculateScoredPeaks(identityPath);
                _scoredPeaks[identityPath] = scoredPeaks;
                return scoredPeaks;
            }
        }

        private AlignmentFunction GetAlignmentFunction(CancellationToken cancellationToken, MsDataFileUri filePath)
        {
            if (AlignmentTarget == null)
            {
                return AlignmentFunction.IDENTITY;
            }

            lock (this)
            {
                if (_alignmentFunctions.TryGetValue(filePath, out var alignmentFunction))
                {
                    return alignmentFunction;
                }

                alignmentFunction = CalculateAlignmentFunction(cancellationToken, filePath);
                _alignmentFunctions[filePath] = alignmentFunction;
                return alignmentFunction;
            }
        }

        private Dictionary<MsDataFileUri, ScoredPeak> CalculateScoredPeaks(IdentityPath identityPath)
        {
            var result = new Dictionary<MsDataFileUri, ScoredPeak>();
            var measuredResults = Document.Settings.MeasuredResults;
            if (measuredResults == null)
            {
                return result;
            }

            var peptideDocNode = (PeptideDocNode)Document.FindNode(identityPath);
            if (peptideDocNode == null)
            {
                return result;
            }

            var scoringModel = GetScoringModel();
            for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
            {
                var chromatogramSet = measuredResults.Chromatograms[replicateIndex];
                foreach (var chromFileInfo in chromatogramSet.MSDataFileInfos)
                {
                    if (result.ContainsKey(chromFileInfo.FilePath))
                    {
                        continue;
                    }

                    var onDemandFeatureCalculator = new OnDemandFeatureCalculator(
                        scoringModel.PeakFeatureCalculators,
                        Document.Settings, peptideDocNode, replicateIndex, chromFileInfo);
                    var bestPeak = peptideDocNode.TransitionGroups.SelectMany(tg =>
                        onDemandFeatureCalculator.GetCandidatePeakGroups(tg.TransitionGroup)).OrderByDescending(peak=>peak.Score.ModelScore).FirstOrDefault();
                    if (bestPeak?.Score?.ModelScore != null)
                    {
                        result.Add(chromFileInfo.FilePath, new ScoredPeak(new PeakBounds(bestPeak.MinStartTime, bestPeak.MaxEndTime), bestPeak.Score.ModelScore.Value));
                    }
                }
            }

            return result;
        }

        private AlignmentFunction CalculateAlignmentFunction(CancellationToken cancellationToken, MsDataFileUri filePath)
        {
            var replicateIndex = FindReplicateIndex(filePath, out var chromFileInfoId);
            if (replicateIndex < 0)
            {
                return null;
            }

            var documentStandards = Document.Settings.GetPeptideStandards(StandardType.IRT);
            IEnumerable<PeptideDocNode> peptideDocNodes;
            if (documentStandards.Count == 0)
            {
                peptideDocNodes = Document.Molecules;
            }
            else
            {
                peptideDocNodes = documentStandards.Select(idPeptideDocNode => idPeptideDocNode.PeptideDocNode);
            }

            var observedTimes = new Dictionary<Target, double>();
            foreach (var peptideDocNode in peptideDocNodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = peptideDocNode.ModifiedTarget;
                if (observedTimes.ContainsKey(target))
                {
                    continue;
                }

                var peptideChromInfo = peptideDocNode.GetSafeChromInfo(replicateIndex)
                    .FirstOrDefault(chromInfo => ReferenceEquals(chromInfo.FileId, chromFileInfoId));
                if (peptideChromInfo?.RetentionTime != null)
                {
                    observedTimes.Add(target, peptideChromInfo.RetentionTime.Value);
                }
            }

            return AlignmentTarget.PerformAlignment(observedTimes, cancellationToken);
        }

        private int FindReplicateIndex(MsDataFileUri filePath, out ChromFileInfoId fileId)
        {
            var measuredResults = Document.Settings.MeasuredResults;
            if (measuredResults == null)
            {
                fileId = null;
                return -1;
            }

            for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
            {
                var chromatogramSet = measuredResults.Chromatograms[replicateIndex];
                var chromFileInfo = chromatogramSet.GetFileInfo(filePath);
                if (chromFileInfo != null)
                {
                    fileId = chromFileInfo.FileId;
                    return replicateIndex;
                }
            }

            fileId = null;
            return -1;
        }

        private PeakScoringModelSpec GetScoringModel()
        {
            var scoringModel = Document.Settings.PeptideSettings.Integration.PeakScoringModel;
            if (true == scoringModel?.IsTrained)
            {
                return scoringModel;
            }
            return LegacyScoringModel.DEFAULT_MODEL;
        }


        private class ScoredPeak
        {
            public ScoredPeak(PeakBounds peakBounds, double score)
            {
                PeakBounds = peakBounds;
                Score = score;
            }

            public PeakBounds PeakBounds { get; }
            public double Score { get; }
        }
    }
}
