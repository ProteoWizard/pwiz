using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.DataAnalysis.Clustering
{
    public class PcaResults<TLabel>
    {
        public PcaResults(IEnumerable<TLabel> itemLabels, IEnumerable<IEnumerable<double>> itemComponents)
        {
            ItemLabels = ImmutableList.ValueOf(itemLabels);
            ItemComponents = ImmutableList.ValueOf(itemComponents.Select(ImmutableList.ValueOf));
            if (ItemComponents.Count != ItemLabels.Count)
            {
                throw new ArgumentException(@"Wrong number of items", nameof(itemComponents));
            }

            ComponentCount = ItemComponents[0].Count;

            for (int i = 1; i < ItemComponents.Count; i++)
            {
                if (ItemComponents[i].Count != ComponentCount)
                {
                    throw new ArgumentException(string.Format(@"Wrong number of values in list#{0}", i), nameof(itemComponents));
                }
            }
        }

        public ImmutableList<TLabel> ItemLabels { get; }
        public int ComponentCount { get; }
        public ImmutableList<ImmutableList<double>> ItemComponents { get; }
    }
}
