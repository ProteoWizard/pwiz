using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;

namespace pwiz.Common.Controls.Clustering
{
    public class DendrogramFormat
    {
        public DendrogramFormat(DendrogramData data, IEnumerable<KeyValuePair<double, double>> leafLocations,
            IEnumerable<IEnumerable<Color>> colors)
        {
            Data = data;
            LeafLocations = ImmutableList.ValueOf(leafLocations);
            if (colors != null)
            {
                Colors = ImmutableList.ValueOf(colors.Select(ImmutableList.ValueOf));
                if (Colors.Count != LeafLocations.Count)
                {
                    throw new ArgumentException(@"Wrong number of colors", nameof(colors));
                }

                if (Colors.Count > 0)
                {
                    ColorLevelCount = Colors[0].Count;
                    if (Colors.Any(c => c.Count != ColorLevelCount))
                    {
                        throw new ArgumentException(@"Inconsistent number of colors", nameof(colors));
                    }
                }
            }
            else
            {
                Colors = ImmutableList<ImmutableList<Color>>.EMPTY;
            }
        }

        public DendrogramData Data { get; private set; }

        public ImmutableList<KeyValuePair<double, double>> LeafLocations { get; private set; }
        public ImmutableList<ImmutableList<Color>> Colors { get; private set; }
        public int ColorLevelCount { get; private set; }
    }
}

