using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Clustering
{
    public class DendrogramData : Immutable
    {
        private int[,] _mergeIndices;
        private double[] _mergeDistances;
        public DendrogramData(int[,] mergeIndices, double[] mergeDistances)
        {
            if (mergeIndices.GetLength(1) != 2)
            {
                throw new ArgumentException(nameof(mergeIndices));
            }
            if (mergeIndices.GetLength(0) != mergeDistances.Length)
            {
                throw new ArgumentException(nameof(mergeDistances));
            }
            _mergeIndices = mergeIndices;
            _mergeDistances = mergeDistances;
        }

        public int LeafCount
        {
            get
            {
                return _mergeDistances.Length + 1;
            }
        }

        public IEnumerable<Tuple<double, double, double, double>> GetLines(IList<double> leafLocations, bool rectilinear)
        {
            if (leafLocations.Count < LeafCount)
            {
                throw new ArgumentException(nameof(leafLocations));
            }
            var nodes = new List<Tuple<double, double>>(LeafCount + _mergeDistances.Length);
            nodes.AddRange(leafLocations.Take(LeafCount).Select(location=>Tuple.Create(location, 0.0)));
            for (int i = 0; i < _mergeDistances.Length; i++)
            {
                var left = nodes[_mergeIndices[i, 0]];
                var right = nodes[_mergeIndices[i, 1]];
                double maxHeight = Math.Max(left.Item2, right.Item2);
                var newNode = Tuple.Create((left.Item1 + right.Item1) / 2,
                    maxHeight + _mergeDistances[i]);
                nodes.Add(newNode);
                if (rectilinear)
                {
                    double joinHeight = maxHeight + _mergeDistances[i] / 10;
                    yield return Tuple.Create(left.Item1, left.Item2, left.Item1, joinHeight);
                    yield return Tuple.Create(left.Item1, joinHeight, right.Item1, joinHeight);
                    yield return Tuple.Create(right.Item1, right.Item2, right.Item1, joinHeight);
                    yield return Tuple.Create(newNode.Item1, joinHeight, newNode.Item1, newNode.Item2);
                }
                else
                {
                    yield return Tuple.Create(left.Item1, left.Item2, newNode.Item1, newNode.Item2);
                    yield return Tuple.Create(right.Item1, right.Item2, newNode.Item1, newNode.Item2);
                }

            }
        }
    }
}
