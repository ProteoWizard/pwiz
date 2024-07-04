using System;
using System.Collections.Generic;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;

namespace pwiz.Skyline.Model.DdaSearch
{
    public class DataFileAlignments : AlignmentSet<MsDataFileUri, AlignmentFunction>
    {
        private Dictionary<Tuple<MsDataFileUri, MsDataFileUri>, AlignmentFunction> _alignmentFunctions = new Dictionary<Tuple<MsDataFileUri, MsDataFileUri>, AlignmentFunction>();

        protected override AlignmentFunction GetAlignment(MsDataFileUri to, MsDataFileUri from)
        {
            if (_alignmentFunctions.TryGetValue(Tuple.Create(to, from), out var alignmentFunction))
            {
                return alignmentFunction;
            }

            if (_alignmentFunctions.TryGetValue(Tuple.Create(from, to), out alignmentFunction))
            {
                return Reverse(alignmentFunction);
            }

            return null;
        }

        protected override IEnumerable<KeyValuePair<MsDataFileUri, AlignmentFunction>> GetAvailableAlignmentsTo(MsDataFileUri alignTo)
        {
            foreach (var entry in _alignmentFunctions)
            {
                if (Equals(entry.Key.Item1, alignTo))
                {
                    yield return new KeyValuePair<MsDataFileUri, AlignmentFunction>(entry.Key.Item2, entry.Value);
                }

                if (Equals(entry.Key.Item2, alignTo))
                {
                    yield return new KeyValuePair<MsDataFileUri, AlignmentFunction>(entry.Key.Item1,
                        Reverse(entry.Value));
                }
            }
        }

        public AlignmentFunction BuildAlignmentFunction(MsDataFileUri alignTo, MsDataFileUri alignFrom,
            int maxStopovers)
        {
            var path = FindAlignmentPath(alignTo, alignFrom, maxStopovers);
            if (path == null)
            {
                return null;
            }
            return AlignmentFunction.FromParts(path);
        }

        public static AlignmentFunction Reverse(AlignmentFunction alignmentFunction)
        {
            return AlignmentFunction.Define(alignmentFunction.GetY, alignmentFunction.GetX);
        }

        public int Count
        {
            get
            {
                return _alignmentFunctions.Count;
            }
        }

        public void Add(MsDataFileUri to, MsDataFileUri from, KdeAligner kdeAligner)
        {
            Add(to, from, AlignmentFunction.Define(kdeAligner.GetValue, kdeAligner.GetValueReversed));
        }

        public void Add(MsDataFileUri to, MsDataFileUri from, AlignmentFunction alignmentFunction)
        {
            _alignmentFunctions.Add(Tuple.Create(to, from), alignmentFunction);
        }
    }
}
