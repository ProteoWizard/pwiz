using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using pwiz.Common.PeakFinding;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.RetentionTimes.PeakImputation
{
    public class ImputedPeakBounds
    {
        public static readonly Producer<Parameter, ImputedPeakBounds> PRODUCER = new Producer();

        public ImputedPeakBounds(PeakBounds peakBounds, string library, string spectrumSourceFile)
        {
            PeakBounds = peakBounds;
            Library = library;
            SpectrumSourceFile = spectrumSourceFile;
        }

        public PeakBounds PeakBounds
        {
            get;
        }

        public string Library { get; }
        public string SpectrumSourceFile { get; }


        public class Parameter
        {
            public Parameter(SrmDocument document, IdentityPath identityPath, MsDataFileUri filePath, AlignmentTarget alignmentTarget)
            {
                Document = document;
                IdentityPath = identityPath;
                FilePath = filePath;
                AlignmentTarget = alignmentTarget;
            }
            public SrmDocument Document { get; }
            public IdentityPath IdentityPath { get; }
            public MsDataFileUri FilePath { get; }
            public AlignmentTarget AlignmentTarget { get; }

            protected bool Equals(Parameter other)
            {
                return ReferenceEquals(Document, other.Document) && IdentityPath.Equals(other.IdentityPath) && FilePath.Equals(other.FilePath) && Equals(AlignmentTarget, other.AlignmentTarget);
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
                    var hashCode = RuntimeHelpers.GetHashCode(Document);
                    hashCode = (hashCode * 397) ^ IdentityPath.GetHashCode();
                    hashCode = (hashCode * 397) ^ FilePath.GetHashCode();
                    hashCode = (hashCode * 397) ^ (AlignmentTarget?.GetHashCode() ?? 0);
                    return hashCode;
                }
            }
        }

        private class Producer : Producer<Parameter, ImputedPeakBounds>
        {
            public override ImputedPeakBounds ProduceResult(ProductionMonitor productionMonitor, Parameter parameter, IDictionary<WorkOrder, object> inputs)
            {
                foreach (var library in parameter.Document.Settings.PeptideSettings.Libraries.Libraries)
                {
                    var bestPeakBounds = FindBestPeakBounds(library, inputs);
                    if (bestPeakBounds == null)
                    {
                        continue;
                    }

                    var libraryAlignment = inputs.Values.OfType<LibraryAlignments>()
                        .FirstOrDefault(alignments => Equals(library.Name, alignments.Param.Library.Name));
                    var libraryAlignmentFunction =
                        libraryAlignment?.GetAlignmentFunction(bestPeakBounds.SpectrumSourceFile);
                    var fileAlignmentFunction = libraryAlignment?.GetAlignmentFunction(parameter.FilePath);
                    if (libraryAlignmentFunction == null && fileAlignmentFunction == null)
                    {
                        return new ImputedPeakBounds(bestPeakBounds.PeakBounds, library.Name,
                            bestPeakBounds.SpectrumSourceFile);
                    }

                    if (libraryAlignmentFunction == null || fileAlignmentFunction == null)
                    {
                        return null;
                    }

                    var midTime = (bestPeakBounds.PeakBounds.StartTime + bestPeakBounds.PeakBounds.EndTime) / 2;
                    var halfPeakWidth = (bestPeakBounds.PeakBounds.EndTime - bestPeakBounds.PeakBounds.StartTime) / 2;
                    var newMidTime = fileAlignmentFunction.GetY(libraryAlignmentFunction.GetX(midTime));
                    var imputedBounds = new PeakBounds(newMidTime - halfPeakWidth, newMidTime + halfPeakWidth);
                    return new ImputedPeakBounds(imputedBounds, library.Name, bestPeakBounds.SpectrumSourceFile);
                }

                return null;
            }

            private BestPeakBounds FindBestPeakBounds(Library library, IDictionary<WorkOrder, object> inputs)
            {
                if (library == null)
                {
                    return null;
                }

                foreach (var entry in inputs)
                {
                    if (entry.Key.WorkParameter is BestPeakBounds.Parameter bestPeakBoundsParameter && entry.Value is BestPeakBounds bestPeakBounds)
                    {
                        if (Equals(bestPeakBoundsParameter.Library?.Name, library.Name))
                        {
                            return bestPeakBounds;
                        }
                    }
                }

                return null;
            }

            public override IEnumerable<WorkOrder> GetInputs(Parameter parameter)
            {
                var peptideDocNode = (PeptideDocNode) parameter.Document.FindNode(parameter.IdentityPath);
                if (peptideDocNode == null)
                {
                    yield break;
                }
                foreach (var library in parameter.Document.Settings.PeptideSettings.Libraries.Libraries)
                {
                    if (true != library?.IsLoaded)
                    {
                        continue;
                    }

                    if (parameter.AlignmentTarget != null)
                    {
                        yield return LibraryAlignments.PRODUCER.MakeWorkOrder(
                            new LibraryAlignments.Parameter(parameter.AlignmentTarget, library));
                    }

                    yield return BestPeakBounds.PRODUCER.MakeWorkOrder(
                        new BestPeakBounds.Parameter(library, new[] { peptideDocNode.ModifiedTarget }));
                }
            }
        }
    }
}
