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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public class ResultFileAlignments : Immutable
    {
        public static readonly ResultFileAlignments EMPTY = new ResultFileAlignments();
        private Dictionary<MsDataFileUri, AlignmentSource> _alignmentSources 
            = new Dictionary<MsDataFileUri, AlignmentSource>();
        private Dictionary<AlignmentSource, PiecewiseLinearMap> _alignmentFunctions 
            = new Dictionary<AlignmentSource, PiecewiseLinearMap>();

        private DocumentKey _documentKey;

        public ResultFileAlignments(SrmDocument document,
            Dictionary<MsDataFileUri, PiecewiseLinearMap> alignmentFunctions, ICollection<MsDataFileUri> filePaths)
        {
            _documentKey = new DocumentKey(document);
            var measuredResults = document.MeasuredResults;
            if (measuredResults == null)
            {
                return;
            }
            _alignmentSources = GetAlignmentSources(document, null, filePaths);

            foreach (var chromatogramSet in measuredResults.Chromatograms)
            {
                foreach (var msDataFileInfo in chromatogramSet.MSDataFileInfos)
                {
                    if (_alignmentSources.TryGetValue(msDataFileInfo.FilePath, out var source))
                    {
                        if (alignmentFunctions.TryGetValue(msDataFileInfo.FilePath, out var piecewiseLinearMap))
                        {
                            _alignmentFunctions[source] = piecewiseLinearMap;
                        }
                        else if (!_alignmentFunctions.ContainsKey(source))
                        {
                            _alignmentFunctions[source] = null;
                        }
                    }
                }
            }
        }

        private ResultFileAlignments()
        {
        }

        public IEnumerable<KeyValuePair<MsDataFileUri, PiecewiseLinearMap>> GetAlignmentFunctions()
        {
            foreach (var kvp in _alignmentSources)
            {
                if (_alignmentFunctions.TryGetValue(kvp.Value, out var alignmentFunction))
                {
                    yield return new KeyValuePair<MsDataFileUri, PiecewiseLinearMap>(kvp.Key, alignmentFunction);
                }
            }
        }

        public bool IsEmpty
        {
            get
            {
                return !GetAlignmentFunctions().Any();
            }
        }

        public PiecewiseLinearMap GetAlignmentFunction(MsDataFileUri msDataFileUri)
        {
            if (!_alignmentSources.TryGetValue(msDataFileUri, out var source))
            {
                return null;
            }

            _alignmentFunctions.TryGetValue(source, out var alignmentFunction);
            return alignmentFunction;
        }

        public class AlignmentSource : Immutable
        {
            private int _targetsHashCode;
            private int _timesHashCode;
            public AlignmentSource(ImmutableList<ImmutableList<Target>> targets, ImmutableList<float> times)
            {
                Targets = targets;
                RetentionTimes = times;
                _targetsHashCode = targets.GetHashCode();
                _timesHashCode = times.GetHashCode();
            }

            public ImmutableList<ImmutableList<Target>> Targets { get; }
            public ImmutableList<float> RetentionTimes { get; }

            protected bool Equals(AlignmentSource other)
            {
                return _targetsHashCode == other._targetsHashCode && _timesHashCode == other._timesHashCode && Targets.Equals(other.Targets) && RetentionTimes.Equals(other.RetentionTimes);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((AlignmentSource)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_targetsHashCode * 397) ^ _timesHashCode;
                }
            }

            public IEnumerable<KeyValuePair<IEnumerable<Target>, double>> GetTimesDictionary()
            {
                return Targets.Zip(RetentionTimes, (targets, time) =>
                    new KeyValuePair<IEnumerable<Target>, double>(targets, time));
            }
        }

        public static Dictionary<MsDataFileUri, AlignmentSource> GetAlignmentSources(SrmDocument document, AlignmentTarget alignmentTarget, ICollection<MsDataFileUri> dataFileUris)
        {
            var result = new Dictionary<MsDataFileUri, AlignmentSource>();
            bool useCurrentPeaks = alignmentTarget?.UseCurrentPeaks ?? false;
            
            var measuredResults = document.MeasuredResults;
            if (measuredResults == null)
            {
                return result;
            }
            Dictionary<Target, Tuple<PeptideDocNode, ImmutableList<Target>, bool>> targets = new Dictionary<Target, Tuple<PeptideDocNode, ImmutableList<Target>, bool>>();
            foreach (var molecule in document.Molecules)
            {
                if (molecule.IsDecoy)
                {
                    continue;
                }

                var target = molecule.ModifiedTarget;
                if (molecule.HasResults && !targets.ContainsKey(target))
                {
                    var typedSequences = document.Settings.GetTargets(molecule).ToImmutable();
                    if (alignmentTarget == null || alignmentTarget.ScoreTargets(typedSequences).HasValue)
                    {
                        targets.Add(target, Tuple.Create(molecule, typedSequences, molecule.AnyReintegratedPeaks()));
                    }
                }
            }

            var orderedMolecules = targets.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();
            var allFileTargets = new HashSet<ImmutableList<ImmutableList<Target>>>();
            for (int iReplicate = 0; iReplicate < measuredResults.Chromatograms.Count; iReplicate++)
            {
                foreach (var chromFileInfo in measuredResults.Chromatograms[iReplicate].MSDataFileInfos)
                {
                    if (result.ContainsKey(chromFileInfo.FilePath))
                    {
                        continue;
                    }

                    if (false == dataFileUris?.Contains(chromFileInfo.FilePath))
                    {
                        continue;
                    }

                    var fileTargets = new List<ImmutableList<Target>>();
                    var retentionTimes = new List<float>();
                    foreach (var (molecule, typedSequences, anyReintegrated) in orderedMolecules)
                    {
                        foreach (var transitionGroup in molecule.TransitionGroups)
                        {
                            var transitionGroupChromInfo =
                                transitionGroup.GetChromInfo(iReplicate, chromFileInfo.FileId);
                            float? apexTime;
                            if (useCurrentPeaks)
                            {
                                apexTime = transitionGroupChromInfo?.RetentionTime;
                            }
                            else if (anyReintegrated)
                            {
                                apexTime = transitionGroupChromInfo?.ReintegratedPeak?.ApexTime;
                            }
                            else
                            {
                                apexTime = transitionGroupChromInfo?.OriginalPeak?.ApexTime;
                            }
                            if (apexTime.HasValue)
                            {
                                fileTargets.Add(typedSequences);
                                retentionTimes.Add(apexTime.Value);
                                break;
                            }
                        }
                    }

                    if (!fileTargets.Any())
                    {
                        continue;
                    }
                    var alignmentSource = new AlignmentSource(fileTargets.ToImmutable(), retentionTimes.ToImmutable());
                    if (!allFileTargets.Add(alignmentSource.Targets) && allFileTargets.TryGetValue(alignmentSource.Targets, out var existingTargets))
                    {
                        alignmentSource = new AlignmentSource(existingTargets, alignmentSource.RetentionTimes);
                    }
                    result.Add(chromFileInfo.FilePath, alignmentSource);
                }
            }
            return result;
        }

        public ResultFileAlignments ChangeDocument(AlignmentTarget target, SrmDocument newDocument, ICollection<MsDataFileUri> dataFiles, ILoadMonitor loadMonitor, ref IProgressStatus status)
        {
            if (!newDocument.Settings.HasResults)
            {
                return EMPTY;
            }
            var result = ChangeProp(ImClone(this), im =>
            {
                im._documentKey = new DocumentKey(newDocument);
            });
            var newSources = GetAlignmentSources(newDocument, target, dataFiles);
            if (CollectionUtil.EqualsDeep(_alignmentSources, newSources))
            {
                return result;
            }

            var missingSources = new List<AlignmentSource>();
            var newAlignmentFunctions = new Dictionary<AlignmentSource, PiecewiseLinearMap>();
            foreach (var newSource in newSources.Values.Distinct())
            {
                if (_alignmentFunctions.TryGetValue(newSource, out var alignmentFunction))
                {
                    newAlignmentFunctions.Add(newSource, alignmentFunction);
                }
                else
                {
                    missingSources.Add(newSource);
                }
            }

            if (missingSources.Count == 0 || target == null)
            {
                return ChangeProp(result, im =>
                {
                    im._alignmentSources = newSources;
                    im._alignmentFunctions = newAlignmentFunctions;
                });
            }

            using var cancellationTokenSource = new PollingCancellationToken(() => loadMonitor.IsCanceled)
            {
                PollingInterval = 1000
            };
            int completedCount = 0;
            var loopProgressStatus = status;
            ParallelEx.For(0, missingSources.Count, iSource =>
            {
                var source = missingSources[iSource];
                var timesDict = source.GetTimesDictionary();
                var alignmentFunction = target.PerformAlignment(timesDict, cancellationTokenSource.Token);
                lock (newAlignmentFunctions)
                {
                    newAlignmentFunctions[source] = alignmentFunction;
                    loadMonitor.UpdateProgress(loopProgressStatus = loopProgressStatus.ChangePercentComplete(completedCount * 100 / missingSources.Count));
                    completedCount++;
                }
            }, threadName:nameof(ResultFileAlignments));
            status = loopProgressStatus;
            return ChangeProp(result, im =>
            {
                im._alignmentSources = newSources;
                im._alignmentFunctions = newAlignmentFunctions;
            });
        }

        private class DocumentKey
        {
            private readonly WeakReference<IList<DocNode>> _documentChildren;
            private readonly IList<ChromatogramSet> _chromatograms;
            private readonly int _hashCode;
            public DocumentKey(SrmDocument document)
            {
                var documentChildren = document.Children;
                _chromatograms = document.MeasuredResults?.Chromatograms;
                Assume.IsTrue(ReferenceEquals(documentChildren, document.Children));
                Assume.IsTrue(ReferenceEquals(_chromatograms, document.MeasuredResults?.Chromatograms));
                _hashCode = (RuntimeHelpers.GetHashCode(documentChildren) * 397) ^
                            RuntimeHelpers.GetHashCode(_chromatograms);
                _documentChildren = new WeakReference<IList<DocNode>>(documentChildren);
            }

            protected bool Equals(DocumentKey other)
            {
                if (!_documentChildren.TryGetTarget(out var myChildren) ||
                    !other._documentChildren.TryGetTarget(out var otherChildren))
                {
                    return false;
                }

                if (!ReferenceEquals(myChildren, otherChildren))
                {
                    return false;
                }

                return ReferenceEquals(_chromatograms, other._chromatograms);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((DocumentKey)obj);
            }

            public override int GetHashCode()
            {
                return _hashCode;
            }
        }

        public bool IsUpToDate(SrmDocument document)
        {
            if (!document.Settings.HasResults)
            {
                return Equals(EMPTY);
            }
            return Equals(new DocumentKey(document), _documentKey);
        }

        protected bool Equals(ResultFileAlignments other)
        {
            return CollectionUtil.EqualsDeep(_alignmentSources, other._alignmentSources) && CollectionUtil.EqualsDeep(_alignmentFunctions, other._alignmentFunctions);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ResultFileAlignments)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = _alignmentSources != null ? CollectionUtil.GetHashCodeDeep(_alignmentSources) : 0;
                hashCode = (hashCode * 397) ^ (_alignmentFunctions != null ? CollectionUtil.GetHashCodeDeep(_alignmentFunctions) : 0);
                return hashCode;
            }
        }
    }
}
