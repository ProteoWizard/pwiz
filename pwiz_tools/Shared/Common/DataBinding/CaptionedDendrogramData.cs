using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;

namespace pwiz.Common.DataBinding
{
    public class CaptionedDendrogramData
    {
        public CaptionedDendrogramData(DendrogramData dendrogramData, IEnumerable<CaptionedValues> captionLevels)
        {
            DendrogramData = dendrogramData;
            CaptionLevels = ImmutableList.ValueOf(captionLevels);
            foreach (var captionLevel in CaptionLevels)
            {
                if (captionLevel.Values.Count != dendrogramData.LeafCount)
                {
                    throw new ArgumentException(@"Wrong number of captions", nameof(captionLevels));
                }
            }
        }

        public DendrogramData DendrogramData { get; private set; }

        public ImmutableList<CaptionedValues> CaptionLevels { get; private set; }

        public int LeafCount => DendrogramData.LeafCount;

        public IEnumerable<IColumnCaption> GetLeafCaptions()
        {
            for (int iLeaf = 0; iLeaf < LeafCount; iLeaf++)
            {
                yield return CaptionComponentList.SpaceSeparate(CaptionLevels.Select(level => level.Values[iLeaf]).ToList());
            }
        }
    }
}
