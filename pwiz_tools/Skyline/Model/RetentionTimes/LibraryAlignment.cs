using System.Collections.Generic;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public class LibraryAlignment
    {
        public LibraryAlignment(Library library, Alignments alignments)
        {
            Library = library;
            Alignments = alignments;
        }

        public Library Library { get; private set; }
        public Alignments Alignments { get; private set; }

        public IEnumerable<double> GetNormalizedRetentionTimes(ICollection<string> spectrumSourceFiles, IList<Target> targets)
        {
            var times = Library.GetRetentionTimesWithSequences(Alignments.LibraryFiles, targets);
            if (times == null)
            {
                yield break;
            }

            for (int iFile = 0; iFile < times.Length; iFile++)
            {
                if (false == spectrumSourceFiles?.Contains(Library.LibraryFiles.FilePaths[iFile]))
                {
                    continue;
                }
                if (times[iFile].Count > 0)
                {
                    var alignment = Alignments.GetAlignmentFunction(Alignments.LibraryFiles[iFile], true);
                    if (alignment != null)
                    {
                        foreach (var time in times[iFile])
                        {
                            var normalizedTime = alignment.GetX(time);
                            // Console.Out.WriteLine("Normalizing time {0} in file {1} to {2}", time, Alignments.LibraryFiles[iFile], normalizedTime);
                            yield return normalizedTime;
                        }
                    }
                }
            }
        }
    }
}
