using System.Collections.Generic;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.RetentionTimes.PeakImputation
{
    public class LibraryAlignment
    {
        private Dictionary<string, AlignmentFunction> _alignmentFunctions;
        public LibraryAlignment(Library library, string batchName, AlignmentTarget alignmentTarget, Dictionary<string, AlignmentFunction> alignmentFunctions)
        {
            Library = library;
            BatchName = batchName;
            AlignmentTarget = alignmentTarget;
            _alignmentFunctions = alignmentFunctions;
        }

        public Library Library { get; }
        public string BatchName { get; }
        public AlignmentTarget AlignmentTarget { get; }
        public AlignmentFunction GetAlignmentFunction(string spectrumSourceFile)
        {
            _alignmentFunctions.TryGetValue(spectrumSourceFile, out var alignmentFunction);
            return alignmentFunction;
        }
    }
}