using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public class ResultFileAlignments : Immutable
    {
        public static readonly ResultFileAlignments EMPTY = new ResultFileAlignments();
        private Dictionary<MsDataFileUri, AlignmentSource> _alignmentSources 
            = new Dictionary<MsDataFileUri, AlignmentSource>();
        private Dictionary<AlignmentSource, ReversibleMap> _alignmentFunctions 
            = new Dictionary<AlignmentSource, ReversibleMap>();

        private DocumentKey _documentKey;

        public ResultFileAlignments(SrmDocument document,
            Dictionary<MsDataFileUri, PiecewiseLinearMap> alignmentFunctions, ICollection<MsDataFileUri> filePaths)
        {
            _documentKey = new DocumentKey(document);
            AlignmentTarget = AlignmentTarget.GetAlignmentTarget(document);
            if (AlignmentTarget == null)
            {
                return;
            }
            var measuredResults = document.MeasuredResults;
            if (measuredResults == null)
            {
                return;
            }
            _alignmentSources = GetAlignmentSources(document, filePaths);

            foreach (var chromatogramSet in measuredResults.Chromatograms)
            {
                foreach (var msDataFileInfo in chromatogramSet.MSDataFileInfos)
                {
                    if (_alignmentSources.TryGetValue(msDataFileInfo.FilePath, out var source))
                    {
                        if (alignmentFunctions.TryGetValue(msDataFileInfo.FilePath, out var piecewiseLinearMap))
                        {
                            _alignmentFunctions[source] = piecewiseLinearMap.ToReversibleMap();
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

        public IEnumerable<KeyValuePair<MsDataFileUri, ReversibleMap>> GetAlignmentFunctions()
        {
            foreach (var kvp in _alignmentSources)
            {
                if (_alignmentFunctions.TryGetValue(kvp.Value, out var alignmentFunction))
                {
                    yield return new KeyValuePair<MsDataFileUri, ReversibleMap>(kvp.Key, alignmentFunction);
                }
            }
        }

        public ReversibleMap GetAlignmentFunction(MsDataFileUri msDataFileUri)
        {
            if (!_alignmentSources.TryGetValue(msDataFileUri, out var source))
            {
                return null;
            }

            _alignmentFunctions.TryGetValue(source, out var alignmentFunction);
            return alignmentFunction;
        }

        public AlignmentTarget AlignmentTarget { get; private set; }
        public class AlignmentSource : Immutable
        {
            private int _targetsHashCode;
            private int _timesHashCode;
            public AlignmentSource(ImmutableList<Target> targets, ImmutableList<float> times)
            {
                Targets = targets;
                RetentionTimes = times;
                _targetsHashCode = targets.GetHashCode();
                _timesHashCode = times.GetHashCode();
            }

            public ImmutableList<Target> Targets { get; }
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

            public Dictionary<Target, double> GetTimesDictionary()
            {
                return Targets.Zip(RetentionTimes, Tuple.Create)
                    .ToDictionary(tuple => tuple.Item1, tuple => (double) tuple.Item2);
            }
        }

        public static Dictionary<MsDataFileUri, AlignmentSource> GetAlignmentSources(SrmDocument document, ICollection<MsDataFileUri> dataFileUris)
        {
            var result = new Dictionary<MsDataFileUri, AlignmentSource>();
            
            var measuredResults = document.MeasuredResults;
            if (measuredResults == null)
            {
                return result;
            }
            Dictionary<Target, PeptideDocNode> targets = new Dictionary<Target, PeptideDocNode>();
            foreach (var molecule in document.Molecules)
            {
                var target = molecule.ModifiedTarget;
                if (molecule.HasResults && !targets.ContainsKey(target))
                {
                    targets.Add(target, molecule);
                }
            }

            var orderedMolecules = targets.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();
            var allFileTargets = new HashSet<ImmutableList<Target>>();
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

                    var fileTargets = new List<Target>();
                    var retentionTimes = new List<float>();
                    foreach (var molecule in orderedMolecules)
                    {
                        foreach (var transitionGroup in molecule.TransitionGroups)
                        {
                            var transitionGroupChromInfo =
                                transitionGroup.GetChromInfo(iReplicate, chromFileInfo.FileId);
                            if (null != transitionGroupChromInfo?.RetentionTime)
                            {
                                fileTargets.Add(molecule.ModifiedTarget);
                                retentionTimes.Add(transitionGroupChromInfo.RetentionTime.Value);
                                break;
                            }
                        }
                    }

                    if (!fileTargets.Any())
                    {
                        continue;
                    }
                    CheckForDuplicates(fileTargets);
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

        private static void CheckForDuplicates(IList<Target> targets)
        {
            var dictionary = new Dictionary<Target, int>();
            for (int iTarget = 0; iTarget < targets.Count; iTarget++)
            {
                var target = targets[iTarget];
                if (dictionary.TryGetValue(target, out var otherIndex))
                {
                    Console.Out.WriteLine("Target found at {0} and {1}", otherIndex, iTarget);
                }
                else
                {
                    dictionary.Add(target, iTarget);
                }
            }
        }

        public ResultFileAlignments ChangeDocument(AlignmentTarget target, SrmDocument newDocument, ICollection<MsDataFileUri> dataFiles, ILoadMonitor loadMonitor, ref IProgressStatus status)
        {
            var newSources = GetAlignmentSources(newDocument, dataFiles);
            if (CollectionUtil.EqualsDeep(_alignmentSources, newSources))
            {
                return ChangeProp(ImClone(this), im=>im._documentKey = new DocumentKey(newDocument));
            }

            var missingSources = new List<AlignmentSource>();
            var newAlignmentFunctions = new Dictionary<AlignmentSource, ReversibleMap>();
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
            
            if (missingSources.Count == 0)
            {
                return ChangeProp(ImClone(this), im =>
                {
                    im._alignmentSources = newSources;
                    im._alignmentFunctions = newAlignmentFunctions;
                });
            }

            using var cancellationTokenSource = new PollingCancellationToken(() => loadMonitor.IsCanceled)
            {
                PollingInterval = 1000
            };
            for (int iSource = 0; iSource < missingSources.Count; iSource++)
            {
                loadMonitor.UpdateProgress(status = status.ChangePercentComplete(iSource * 100 / missingSources.Count));
                var source = missingSources[iSource];
                var timesDict = source.GetTimesDictionary();
                var alignmentFunction = target.PerformAlignment(timesDict, cancellationTokenSource.Token)?.ToReversibleMap();
                newAlignmentFunctions[source] = alignmentFunction;
            }

            return ChangeProp(ImClone(this), im =>
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
            return Equals(new DocumentKey(document), _documentKey);
        }
    }
}
