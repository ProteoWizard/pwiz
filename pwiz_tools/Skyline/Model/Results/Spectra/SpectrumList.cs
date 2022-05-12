using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.Spectra;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumList
    {
        public static readonly SpectrumList EMPTY = new SpectrumList(ImmutableList.Empty<SpectrumMetadata>());
        public SpectrumList(IEnumerable<SpectrumMetadata> spectrumMetadatas)
        {
            SpectrumMetadatas = ImmutableList.ValueOfOrEmpty(spectrumMetadatas);
        }

        public ImmutableList<SpectrumMetadata> SpectrumMetadatas { get; private set; }

        public SpectrumList Filter(SpectrumSelector selector)
        {
            return new SpectrumList(SpectrumMetadatas.Where(selector.Matches));
        }
    }
}
