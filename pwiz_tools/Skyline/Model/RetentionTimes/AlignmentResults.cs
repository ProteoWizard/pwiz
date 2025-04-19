using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public class Alignments
    {
        public static readonly Alignments EMPTY =
            new Alignments(LibraryFiles.EMPTY, Array.Empty<KeyValuePair<string, PiecewiseLinearMap>>());
        private Dictionary<string, Alignment> _alignmentFunctions;

        public Alignments(LibraryFiles libraryFiles, IEnumerable<KeyValuePair<string, PiecewiseLinearMap>> alignmentFunctions)
        {
            _alignmentFunctions = new Dictionary<string, Alignment>();
            List<string> files = null;
            if (libraryFiles == null)
            {
                files = new List<string>();
            }

            foreach (var entry in alignmentFunctions)
            {
                if (entry.Value != null)
                {
                    _alignmentFunctions.Add(entry.Key, new Alignment(entry.Value));
                    files?.Add(entry.Key);
                }
            }

            LibraryFiles = libraryFiles ?? new LibraryFiles(files);
        }

        public AlignmentFunction GetAlignmentFunction(string name, bool forward)
        {
            _alignmentFunctions.TryGetValue(name, out var result);
            return result?.GetAlignmentFunction(forward);
        }

        public AlignmentFunction GetAlignmentFunction(MsDataFileUri msDataFileUri, bool forward)
        {
            int index = LibraryFiles.FindIndexOf(msDataFileUri);
            if (index < 0)
            {
                return null;
            }

            return _alignmentFunctions[LibraryFiles[index]]?.GetAlignmentFunction(forward);
        }

        public bool ContainsFile(string file)
        {
            return _alignmentFunctions.ContainsKey(file);
        }

        public IEnumerable<KeyValuePair<string, PiecewiseLinearMap>> GetAllAlignmentFunctions()
        {
            return _alignmentFunctions.Select(kvp=>new KeyValuePair<string, PiecewiseLinearMap>(kvp.Key, kvp.Value.ForwardMap));
        }

        public LibraryFiles LibraryFiles { get; private set; }

        private class Alignment
        {
            public Alignment(PiecewiseLinearMap forwardMap)
            {
                ForwardMap = forwardMap;
                ReverseMap = ForwardMap.ReverseMap();
            }

            public PiecewiseLinearMap ForwardMap { get; }
            public PiecewiseLinearMap ReverseMap { get; }

            public AlignmentFunction GetAlignmentFunction(bool forward)
            {
                if (forward)
                {
                    return AlignmentFunction.Define(ForwardMap.GetY, ReverseMap.GetY);
                }
                else
                {
                    return AlignmentFunction.Define(ReverseMap.GetY, ForwardMap.GetY);
                }
            }
        }
    }
}
