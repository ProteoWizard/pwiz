using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pwiz.Skyline.Model.Results
{
    public class ProductFilterId
    {
        public ProductFilterId(SpectrumProductFilter spectrumProductFilter, int extraInfoOffset)
        {
            SpectrumProductFilter = spectrumProductFilter;
            ExtraInfoOffset = extraInfoOffset;
        }

        public SpectrumProductFilter SpectrumProductFilter { get; }
        public int ExtraInfoOffset { get; }
    }
}
