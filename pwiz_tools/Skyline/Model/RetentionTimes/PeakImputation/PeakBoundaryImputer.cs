using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using pwiz.Common.PeakFinding;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.RetentionTimes.PeakImputation
{
    public class PeakBoundaryImputer
    {
        private static WeakReference<PeakBoundaryImputer> _instance;

        public static PeakBoundaryImputer GetInstance()
        {
            lock (typeof(PeakBoundaryImputer))
            {
                if (true == _instance?.TryGetTarget(out var peakBoundaryImputer))
                {
                    return peakBoundaryImputer;
                }

                peakBoundaryImputer = new PeakBoundaryImputer();
                _instance = new WeakReference<PeakBoundaryImputer>(peakBoundaryImputer);
                return peakBoundaryImputer;
            }
        }


        private Dictionary<string, LibraryInfo> _libraryInfos = new Dictionary<string, LibraryInfo>();

        public PeakBoundaryImputer()
        {
            ImputedBoundsProducer = Producer.FromFunction<ImputedBoundsParameter, ImputedPeak>(ProduceImputedBounds);
        }
        
        private class LibraryInfo
        {
            private Dictionary<Tuple<AlignmentTarget, string>, AlignmentFunction> _alignmentFunctions =
                new Dictionary<Tuple<AlignmentTarget, string>, AlignmentFunction>();
            private Dictionary<Target, ExemplaryPeak> _bestPeakBounds = new Dictionary<Target, ExemplaryPeak>();
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

            private ExemplaryPeak FindExemplaryPeak(CancellationToken cancellationToken, IList<Target> targets)
            {
                ExplicitPeakBounds bestPeakBounds = null;
                string bestFile = null;
                foreach (var filePath in Library.LibraryFiles.FilePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var peakBounds = Library.GetExplicitPeakBounds(MsDataFileUri.Parse(filePath), targets);
                    if (peakBounds != null)
                    {
                        if (bestPeakBounds == null || bestPeakBounds.Score > peakBounds.Score)
                        {
                            bestPeakBounds = peakBounds;
                            bestFile = filePath;
                        }
                    }
                }

                if (bestPeakBounds == null)
                {
                    return null;
                }

                return new ExemplaryPeak(Library, bestFile, new PeakBounds(bestPeakBounds.StartTime, bestPeakBounds.EndTime));
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

                libraryInfo = new LibraryInfo(library, library.GetAllRetentionTimes());
                _libraryInfos[library.Name] = libraryInfo;
                return libraryInfo;
            }
        }

        public ImputedPeak GetImputedPeakBounds(CancellationToken cancellationToken, SrmDocument document,
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
                var exemplaryPeak = libraryInfo.GetExemplaryPeak(cancellationToken, peptideDocNode.ModifiedTarget);
                if (exemplaryPeak == null)
                {
                    continue;
                }

                if (alignmentTarget == null)
                {
                    return new ImputedPeak(exemplaryPeak.PeakBounds, exemplaryPeak);
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
                    exemplaryPeak.SpectrumSourceFile);
                if (libraryAlignment == null)
                {
                    continue;
                }
                var midTime = (exemplaryPeak.PeakBounds.StartTime + exemplaryPeak.PeakBounds.EndTime) / 2;
                var halfPeakWidth = (exemplaryPeak.PeakBounds.EndTime - exemplaryPeak.PeakBounds.StartTime) / 2;
                var newMidTime = fileAlignment.GetY(libraryAlignment.GetX(midTime));
                var imputedBounds = new PeakBounds(newMidTime - halfPeakWidth, newMidTime + halfPeakWidth);
                return new ImputedPeak(imputedBounds, exemplaryPeak);
            }

            return null;
        }

        private ImputedPeak ProduceImputedBounds(ProductionMonitor productionMonitor,
            ImputedBoundsParameter parameter)
        {
            return GetImputedPeakBounds(productionMonitor.CancellationToken, parameter.Document, parameter.IdentityPath,
                parameter.FilePath);
        }
        public Producer<ImputedBoundsParameter, ImputedPeak> ImputedBoundsProducer { get;  }

        public ExplicitPeakBounds GetExplicitPeakBounds(SrmDocument document, PeptideDocNode peptideDocNode,
            MsDataFileUri filePath)
        {
            var explicitPeakBounds = document.Settings.GetExplicitPeakBounds(peptideDocNode, filePath);
            if (explicitPeakBounds == null)
            {
                return null;
            }
            var imputationSettings = document.Settings.PeptideSettings.Imputation;
            if (Equals(imputationSettings, ImputationSettings.DEFAULT))
            {
                return explicitPeakBounds;
            }

            if (!(imputationSettings.MaxPeakWidthVariation.HasValue || imputationSettings.MaxRtShift.HasValue) &&
                !explicitPeakBounds.IsEmpty)
            {
                return explicitPeakBounds;
            }

            var moleculeGroup = document.MoleculeGroups.FirstOrDefault(mg => mg.FindNodeIndex(peptideDocNode.Peptide) >= 0);
            if (moleculeGroup == null)
            {
                return explicitPeakBounds;
            }

            var identityPath = new IdentityPath(moleculeGroup.PeptideGroup, peptideDocNode.Peptide);
            var imputedPeak = GetImputedPeakBounds(CancellationToken.None, document, identityPath, filePath);
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
    }
}
