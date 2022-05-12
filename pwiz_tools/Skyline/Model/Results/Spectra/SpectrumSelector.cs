using System;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumSelector : Immutable
    {
        public string ScanDescription { get; private set; }
        public bool? NegativeCharge { get; private set; }
        public SpectrumPrecursors Precursors { get; private set; }

        public bool Matches(SpectrumMetadata spectrumMetadata)
        {
            if (ScanDescription != null)
            {
                if (ScanDescription.Length == 0)
                {
                    if (!string.IsNullOrEmpty(spectrumMetadata.ScanDescription))
                    {
                        return false;
                    }
                }
                else if (ScanDescription != spectrumMetadata.ScanDescription)
                {
                    return false;
                }
            }

            throw new NotImplementedException();
            // if (Precursors != null && !Precursors.Equals(spectrumMetadata.Precursors))
            // {
            //     return false;
            // }
            //
            // return true;
        }
    }
}
