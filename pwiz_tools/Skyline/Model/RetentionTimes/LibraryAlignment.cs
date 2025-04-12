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

        public IEnumerable<double> GetAlignedRetentionTimes(IList<Target> targets)
        {
            var times = Library.GetRetentionTimesWithSequences(Alignments.LibraryFiles, targets);
            if (times == null)
            {
                yield break;
            }

            for (int iFile = 0; iFile < times.Length; iFile++)
            {
                if (times[iFile].Count > 0)
                {
                    var alignment = Alignments.GetAlignmentFunction(Alignments.LibraryFiles[iFile], true);
                    if (alignment != null)
                    {
                        foreach (var time in times[iFile])
                        {
                            yield return alignment.GetX(time);
                        }
                    }
                }
            }
        }
    }
}
