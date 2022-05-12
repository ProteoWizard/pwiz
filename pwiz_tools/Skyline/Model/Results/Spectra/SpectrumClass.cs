using System.Collections.Generic;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumClass : SkylineObject
    {
        public SpectrumClass(SkylineDataSchema dataSchema, string ms2Precursors, IDictionary<string, FileSpectrumInfo> files) : base(dataSchema)
        {
            Ms2Precursors = ms2Precursors;
            Files = files;
        }

        public string Ms2Precursors { get; private set; }

        public IDictionary<string, FileSpectrumInfo> Files { get; }
    }
}
