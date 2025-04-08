using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using pwiz.Common.Collections;
using pwiz.Common.PeakFinding;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.RetentionTimes.PeakImputation
{
#if false
    public class ImputedBoundariesCollection
    {
        public static readonly Producer<Parameter, ImputedBoundariesCollection> PRODUCER = new Producer();
        private Dictionary<Key, PeakBounds> _boundsDictionary;

        public PeakBounds GetImputedBounds(ChromatogramSetId chromatogramSetId, ChromFileInfoId fileId,
            IdentityPath peptideIdentityPath)
        {
            var key = new Key(chromatogramSetId, fileId, peptideIdentityPath);
            _boundsDictionary.TryGetValue(key, out var bounds);
            return bounds;
        }

        private ImputedBoundariesCollection(Dictionary<Key, PeakBounds> dictionary)
        {
            _boundsDictionary = dictionary;
        }

        protected bool Equals(ImputedBoundariesCollection other)
        {
            return CollectionUtil.EqualsDeep(_boundsDictionary, other._boundsDictionary);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ImputedBoundariesCollection)obj);
        }

        public override int GetHashCode()
        {
            return CollectionUtil.GetHashCodeDeep(_boundsDictionary);
        }

        public static ImputedBoundariesCollection MakeImputedBounds(SrmDocument document, List<LibraryAlignments> libraryAlignmentsList, IEnumerable<IdentityPath> moleculeIdentityPaths)
        {
            if (!document.Settings.HasResults)
            {
                return null;
            }

            var dictionary = new Dictionary<Key, PeakBounds>();
            foreach (var identityPath in moleculeIdentityPaths)
            {
                var bestPeakBounds = FindBestPeakBounds(document, identityPath, libraryAlignmentsList);
                if (bestPeakBounds == null)
                {
                    continue;
                }
                
            }

            foreach (var chromatogramSet in document.MeasuredResults.Chromatograms)
            {
                foreach (var fileInfo in chromatogramSet.MSDataFileInfos)
                {

                }
            }
            foreach (var moleculePeaks in moleculePeaksList)
            {
                if (moleculePeaks.BestPeak == null)
                {
                    continue;
                }
                foreach (var chromatogramSet in document.MeasuredResults.Chromatograms)
                {
                    foreach (var chromFileInfo in chromatogramSet.MSDataFileInfos)
                    {
                        var replicateFileId = new ReplicateFileId(chromatogramSet.Id, chromFileInfo.FileId);
                        if (!Equals(replicateFileId, moleculePeaks.BestPeak.ReplicateFileInfo.ReplicateFileId))
                        {
                            var alignmentFunction = alignmentData.Alignments?.GetAlignment(replicateFileId) ?? AlignmentFunction.IDENTITY;
                            dictionary.Add(Tuple.Create(replicateFileId, moleculePeaks.PeptideIdentityPath), moleculePeaks.ExemplaryPeakBounds.ReverseAlignPreservingWidth(alignmentFunction).ToPeakBounds());
                        }
                    }
                }

            }
            return new ImputedBoundariesCollection(dictionary);
        }
        
        public class Parameter
        {
            public Parameter(SrmDocument document, IEnumerable<IdentityPath> peptideIdentityPaths)
            {
                Document = document;
                PeptideIdentityPaths = ImmutableList.ValueOf(peptideIdentityPaths);
            }

            public SrmDocument Document { get; }
            public ImmutableList<IdentityPath> PeptideIdentityPaths
            {
                get;
            }

            protected bool Equals(Parameter other)
            {
                return ReferenceEquals(Document, other.Document) && Equals(PeptideIdentityPaths, other.PeptideIdentityPaths);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Parameter)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (RuntimeHelpers.GetHashCode(Document) * 397) ^ PeptideIdentityPaths.GetHashCode();
                }
            }
        }

        private class Producer : Producer<Parameter, ImputedBoundariesCollection>
        {
            public override ImputedBoundariesCollection ProduceResult(ProductionMonitor productionMonitor, Parameter parameter, IDictionary<WorkOrder, object> inputs)
            {
                var libraryAlignmentsList = inputs.Values.OfType<LibraryAlignments>().ToList();

                var peakImputationRows = inputs.Values.OfType<PeakImputationRows>().FirstOrDefault();
                if (peakImputationRows == null)
                {
                    return null;
                }

                return MakeImputedBounds(parameter.Document, peakImputationRows.MoleculePeaks,
                    peakImputationRows.AlignmentData);
            }

            public override IEnumerable<WorkOrder> GetInputs(Parameter parameter)
            {
                var alignmentTarget = AlignmentTarget.GetAlignmentTarget(parameter.Document);
                if (alignmentTarget == null)
                {
                    yield break;
                }
                foreach (var library in parameter.Document.Settings.PeptideSettings.Libraries.Libraries)
                {
                    if (true == library?.IsLoaded)
                    {
                        yield return LibraryAlignments.PRODUCER.MakeWorkOrder(
                            new LibraryAlignments.Parameter(alignmentTarget, library));
                    }
                }
            }
        }

        private class Key
        {
            public Key(ChromatogramSetId chromatogramSetId, ChromFileInfoId fileId, IdentityPath identityPath)
            {
                ChromatogramSetId = chromatogramSetId;
                FileId = fileId;
                IdentityPath = identityPath;
            }

            public ChromatogramSetId ChromatogramSetId { get;  }
            public ChromFileInfoId FileId { get; }
            public IdentityPath IdentityPath { get; }

            protected bool Equals(Key other)
            {
                return ReferenceEquals(ChromatogramSetId, other.ChromatogramSetId) && ReferenceEquals(FileId, other.FileId) && IdentityPath.Equals(other.IdentityPath);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Key)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = RuntimeHelpers.GetHashCode(ChromatogramSetId);
                    hashCode = (hashCode * 397) ^ RuntimeHelpers.GetHashCode(FileId);
                    hashCode = (hashCode * 397) ^ IdentityPath.GetHashCode();
                    return hashCode;
                }
            }
        }

        private static PeakBounds FindBestPeakBounds(SrmDocument document, IdentityPath identityPath, List<LibraryAlignments> libraryAlignmentsList)
        {
            var docNode = (PeptideDocNode)document.FindNode(identityPath);
            if (docNode == null) 
                return null;
            var targets = new[]{docNode.ModifiedTarget};
            foreach (var library in document.Settings.PeptideSettings.Libraries.Libraries)
            {
                if (true != library?.IsLoaded)
                {
                    continue;
                }

                var libraryAlignments =
                    libraryAlignmentsList.FirstOrDefault(lib => lib.Param.Library.Name == library.Name);
                if (libraryAlignments == null && libraryAlignmentsList.Count > 0)
                {
                    continue;
                }
                ExplicitPeakBounds bestPeakBounds = null;
                AlignmentFunction bestAlignmentFunction = null;
                foreach (var filePath in library.LibraryFiles.FilePaths)
                {
                    var peakBounds = library.GetExplicitPeakBounds(MsDataFileUri.Parse(filePath), targets);
                    if (peakBounds != null)
                    {
                        if (bestPeakBounds == null || bestPeakBounds.Score > peakBounds.Score)
                        {
                            if (libraryAlignments != null)
                            {
                                var alignmentFunction = libraryAlignments.GetAlignmentFunction(filePath);
                                if (alignmentFunction == null)
                                {
                                    continue;
                                }
                                bestAlignmentFunction = alignmentFunction;
                            }
                            bestPeakBounds = peakBounds;
                        }
                    }
                }

                if (bestPeakBounds != null)
                {
                    if (bestAlignmentFunction == null)
                    {
                        return new PeakBounds(bestPeakBounds.StartTime, bestPeakBounds.EndTime);
                    }

                    var midTime = (bestPeakBounds.StartTime + bestPeakBounds.EndTime) / 2;
                    var halfWidth = (bestPeakBounds.EndTime - bestPeakBounds.StartTime) / 2;
                    var newMidTime = bestAlignmentFunction.GetX(midTime);
                    return new PeakBounds(newMidTime - halfWidth, newMidTime + halfWidth);
                }
            }

            return null;
        }
    }
#endif
}
