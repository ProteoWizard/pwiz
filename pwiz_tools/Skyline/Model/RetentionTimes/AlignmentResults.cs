using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public class LibraryAlignments
    {
        public static readonly LibraryAlignments EMPTY =
            new LibraryAlignments(Array.Empty<KeyValuePair<string, PiecewiseLinearMap>>());
        private Dictionary<string, LibraryAlignment> _alignmentFunctions;

        public LibraryAlignments(IEnumerable<KeyValuePair<string, PiecewiseLinearMap>> alignmentFunctions)
        {
            _alignmentFunctions = alignmentFunctions.ToDictionary(kvp=>kvp.Key, kvp=>new LibraryAlignment(kvp.Value));
        }

        public AlignmentFunction GetAlignmentFunction(string name)
        {
            _alignmentFunctions.TryGetValue(name, out var result);
            return result?.AlignmentFunction;
        }

        public AlignmentFunction GetAlignmentFunction(MsDataFileUri msDataFileUri)
        {
            var name = msDataFileUri.GetFileNameWithoutExtension();
            foreach (var entry in _alignmentFunctions)
            {
                if (name == Path.GetFileNameWithoutExtension(entry.Key))
                {
                    return entry.Value?.AlignmentFunction;
                }
            }

            return null;
        }

        public IEnumerable<KeyValuePair<string, PiecewiseLinearMap>> GetAllAlignmentFunctions()
        {
            return _alignmentFunctions.Select(kvp=>new KeyValuePair<string, PiecewiseLinearMap>(kvp.Key, kvp.Value.ForwardMap));
        }

        private class LibraryAlignment
        {
            public LibraryAlignment(PiecewiseLinearMap forwardMap)
            {
                ForwardMap = forwardMap;
                ReverseMap = ForwardMap.ReverseMap();
            }

            public PiecewiseLinearMap ForwardMap { get; }
            public PiecewiseLinearMap ReverseMap { get; }

            public AlignmentFunction AlignmentFunction
            {
                get
                {
                    return AlignmentFunction.Define(ForwardMap.GetY, ReverseMap.GetY);
                }
            }
        }
    }
}
