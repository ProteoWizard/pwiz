using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using pwiz.Common.PeakFinding;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.RetentionTimes.PeakImputation
{
    public class PeakBoundaryImputer
    {
        private Dictionary<string, LibraryInfo> _libraryInfos = new Dictionary<string, LibraryInfo>();

        public PeakBoundaryImputer()
        {
            ImputedBoundsProducer = Producer.FromFunction<ImputedBoundsParameter, ImputedPeakBounds>(ProduceImputedBounds);
        }
        
        private class LibraryInfo
        {
            private Dictionary<Tuple<AlignmentTarget, string>, AlignmentFunction> _alignmentFunctions =
                new Dictionary<Tuple<AlignmentTarget, string>, AlignmentFunction>();
            private Dictionary<Target, BestPeakBounds> _bestPeakBounds = new Dictionary<Target, BestPeakBounds>();
            public LibraryInfo(Library library, Dictionary<Target, double>[] allRetentionTimes)
            {
                Library = library;
                AllRetentionTimes = allRetentionTimes;
            }

            public Library Library { get; }
            public Dictionary<Target, double>[] AllRetentionTimes { get; set; }

            public AlignmentFunction GetAlignmentFunction(CancellationToken cancellationToken,
                AlignmentTarget alignmentTarget, string spectrumSourceFile)
            {
                var key = Tuple.Create(alignmentTarget, spectrumSourceFile);
                AlignmentFunction alignmentFunction;
                lock (this)
                {
                    if (_alignmentFunctions.TryGetValue(key,
                            out alignmentFunction))
                    {
                        return alignmentFunction;
                    }
                }

                int fileIndex = Library.LibraryFiles.FilePaths.IndexOf(spectrumSourceFile);
                if (fileIndex < 0)
                {
                    return null;
                }

                alignmentFunction = alignmentTarget.PerformAlignment(AllRetentionTimes[fileIndex],
                    cancellationToken);
                lock (this)
                {
                    _alignmentFunctions[key] = alignmentFunction;
                }
                return alignmentFunction;
            }

            public BestPeakBounds GetBestPeakBounds(CancellationToken cancellationToken, Target target)
            {
                BestPeakBounds bestPeakBounds;
                lock (this)
                {
                    if (_bestPeakBounds.TryGetValue(target, out bestPeakBounds))
                    {
                        return bestPeakBounds;
                    }

                    bestPeakBounds = BestPeakBounds.GetBestPeakBounds(cancellationToken,
                        new BestPeakBounds.Parameter(Library, new[] { target }));
                    _bestPeakBounds[target] = bestPeakBounds;
                    return bestPeakBounds;
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
                return ReferenceEquals(Document, other.Document) && Equals(IdentityPath, other.IdentityPath) && Equals(AlignmentTarget, other.AlignmentTarget);
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

                libraryInfo = new LibraryInfo(library, library.GetAllRetentionTimes());
                _libraryInfos[library.Name] = libraryInfo;
                return libraryInfo;
            }
        }

        public ImputedPeakBounds GetImputedPeakBounds(CancellationToken cancellationToken, SrmDocument document,
            IdentityPath identityPath, MsDataFileUri filePath)
        {
            var peptideDocNode = (PeptideDocNode)document.FindNode(identityPath);
            if (peptideDocNode == null)
            {
                return null;
            }

            var alignmentTarget = AlignmentTarget.GetAlignmentTarget(document);
            foreach (var library in document.Settings.PeptideSettings.Libraries.Libraries)
            {
                if (true != library?.IsLoaded)
                {
                    continue;
                }

                var libraryInfo = GetLibraryInfo(library);
                var bestPeakBounds = libraryInfo.GetBestPeakBounds(cancellationToken, peptideDocNode.ModifiedTarget);
                if (bestPeakBounds == null)
                {
                    continue;
                }

                if (alignmentTarget == null)
                {
                    return new ImputedPeakBounds(bestPeakBounds.PeakBounds, library.Name,
                        bestPeakBounds.SpectrumSourceFile);
                }
                var fileIndex = library.LibraryFiles.FindIndexOf(filePath);
                if (fileIndex < 0)
                {
                    continue;
                }

                var fileAlignment = libraryInfo.GetAlignmentFunction(cancellationToken, alignmentTarget,
                    library.LibraryFiles.FilePaths[fileIndex]);
                if (fileAlignment == null)
                {
                    continue;
                }
                var libraryAlignment = libraryInfo.GetAlignmentFunction(cancellationToken, alignmentTarget,
                    bestPeakBounds.SpectrumSourceFile);
                if (libraryAlignment == null)
                {
                    continue;
                }
                var midTime = (bestPeakBounds.PeakBounds.StartTime + bestPeakBounds.PeakBounds.EndTime) / 2;
                var halfPeakWidth = (bestPeakBounds.PeakBounds.EndTime - bestPeakBounds.PeakBounds.StartTime) / 2;
                var newMidTime = fileAlignment.GetY(libraryAlignment.GetX(midTime));
                var imputedBounds = new PeakBounds(newMidTime - halfPeakWidth, newMidTime + halfPeakWidth);
                return new ImputedPeakBounds(imputedBounds, library.Name, bestPeakBounds.SpectrumSourceFile);
            }

            return null;
        }

        private ImputedPeakBounds ProduceImputedBounds(ProductionMonitor productionMonitor,
            ImputedBoundsParameter parameter)
        {
            return GetImputedPeakBounds(productionMonitor.CancellationToken, parameter.Document, parameter.IdentityPath,
                parameter.FilePath);
        }
        public Producer<ImputedBoundsParameter, ImputedPeakBounds> ImputedBoundsProducer { get;  }
    }
}
