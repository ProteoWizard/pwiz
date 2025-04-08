using System.Collections.Generic;
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.PeakFinding;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.RetentionTimes.PeakImputation
{
    public class BestPeakBounds
    {
        public static readonly Producer<Parameter, BestPeakBounds> PRODUCER =
            Producer.FromFunction<Parameter, BestPeakBounds>(GetBestPeakBounds);

        public BestPeakBounds(Library library, string spectrumSourceFile, PeakBounds peakBounds)
        {
            Library = library;
            PeakBounds = peakBounds;
            SpectrumSourceFile = spectrumSourceFile;
        }

        public Library Library { get; }
        public PeakBounds PeakBounds { get; }
        public string SpectrumSourceFile { get; }

        protected bool Equals(BestPeakBounds other)
        {
            return Equals(Library, other.Library) && Equals(PeakBounds, other.PeakBounds) && SpectrumSourceFile == other.SpectrumSourceFile;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((BestPeakBounds)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Library != null ? Library.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (PeakBounds != null ? PeakBounds.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (SpectrumSourceFile != null ? SpectrumSourceFile.GetHashCode() : 0);
                return hashCode;
            }
        }

        public class Parameter
        {
            public Parameter(Library library, IEnumerable<Target> targets)
            {
                Library = library;
                Targets = targets.ToImmutable();
            }

            public Library Library { get; }
            public ImmutableList<Target> Targets { get; }
        }

        public static BestPeakBounds GetBestPeakBounds(ProductionMonitor productionMonitor, Parameter parameter)
        {
            return GetBestPeakBounds(productionMonitor.CancellationToken, parameter);
        }

        public static BestPeakBounds GetBestPeakBounds(CancellationToken cancellationToken, Parameter parameter)
        {
            var library = parameter.Library;
            if (!library.IsLoaded)
            {
                return null;
            }

            ExplicitPeakBounds bestPeakBounds = null;
            string bestFile = null;
            foreach (var filePath in library.LibraryFiles.FilePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var peakBounds = library.GetExplicitPeakBounds(MsDataFileUri.Parse(filePath), parameter.Targets);
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

            return new BestPeakBounds(parameter.Library, bestFile, new PeakBounds(bestPeakBounds.StartTime, bestPeakBounds.EndTime));
        }
    }
}
