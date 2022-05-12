using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.Spectra;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumMetadataList : IReadOnlyList<SpectrumMetadata>
    {
        public SpectrumMetadataList(IEnumerable<SpectrumMetadata> spectrumMetadatas)
        {
            AllSpectra = ImmutableList.ValueOfOrEmpty(spectrumMetadatas);
            Ms1Spectra = ImmutableList.ValueOf(AllSpectra.Where(spectrum => spectrum.MsLevel == 1));
            var spectraByPrecursor = new List<KeyValuePair<double, ImmutableList<SpectrumMetadata>>>();
            var precursorGroups = AllSpectra
                .SelectMany(spectrum => spectrum.GetPrecursors(1).Select(p => Tuple.Create(p.PrecursorMz, spectrum)))
                .GroupBy(tuple => tuple.Item1, tuple => tuple.Item2);
            foreach (var group in precursorGroups)
            {
                spectraByPrecursor.Add(new KeyValuePair<double, ImmutableList<SpectrumMetadata>>(group.Key.RawValue, ImmutableList.ValueOf(group)));
            }
            SpectraByPrecursor = ImmutableSortedList.FromValues(spectraByPrecursor);
        }

        public int Count
        {
            get { return AllSpectra.Count; }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<SpectrumMetadata> GetEnumerator()
        {
            return AllSpectra.GetEnumerator();
        }

        public SpectrumMetadata this[int index] => AllSpectra[index];

        public ImmutableList<SpectrumMetadata> AllSpectra { get; private set; }
        public ImmutableList<SpectrumMetadata> Ms1Spectra { get; private set; }
        public ImmutableSortedList<double, ImmutableList<SpectrumMetadata>> SpectraByPrecursor { get; private set; }
    }
}
