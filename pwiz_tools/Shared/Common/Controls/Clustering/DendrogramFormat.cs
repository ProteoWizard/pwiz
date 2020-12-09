using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using pwiz.Common.DataAnalysis.Clustering;
using ZedGraph;

//using System.Collections.Immutable;


namespace pwiz.Common.Controls.Clustering
{
    public class DendrogramFormat
    {
        public DendrogramFormat(DendrogramData data, IEnumerable<KeyValuePair<double, double>> leafLocations,
            IEnumerable<IEnumerable<Color>> colors)
        {
            Data = data;
            LeafLocations = new List<KeyValuePair<double, double>>(leafLocations);
            Colors = new List<List<Color>>();
            if (colors != null)
            {
                foreach (var color in colors)
                    Colors.Add(color.ToList());
                if (Colors.Count != LeafLocations.Count)
                {
                    throw new ArgumentException(@"Wrong number of colors", nameof(colors));
                }

                if (Colors.Count > 0)
                {
                    ColorLevelCount = Colors[0].Count;
                    foreach (var color in Colors)
                    {
                        if (color.Count != ColorLevelCount)
                            throw new ArgumentException(@"Inconsistent number of colors", nameof(colors));
                    }
                }
            }
        }

        public DendrogramData Data { get; private set; }

        public List<KeyValuePair<double, double>> LeafLocations { get; private set; }
        public List<List<Color>> Colors { get; private set; }
        public int ColorLevelCount { get; private set; }
    }
}
