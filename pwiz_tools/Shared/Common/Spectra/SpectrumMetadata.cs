using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Spectra
{
    public class SpectrumMetadata : Immutable
    {
        private ImmutableList<ImmutableList<SpectrumPrecursor>> _precursorsByMsLevel =
            ImmutableList<ImmutableList<SpectrumPrecursor>>.EMPTY;
        public int MsLevel
        {
            get { return _precursorsByMsLevel.Count + 1; }
        }

        public ImmutableList<SpectrumPrecursor> GetPrecursors(int msLevel)
        {
            if (msLevel < 1 || msLevel > _precursorsByMsLevel.Count)
            {
                return ImmutableList<SpectrumPrecursor>.EMPTY;
            }

            return _precursorsByMsLevel[msLevel - 1];
        }

        public SpectrumMetadata ChangePrecursors(IEnumerable<IEnumerable<SpectrumPrecursor>> precursorsByMsLevel)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im._precursorsByMsLevel = ImmutableList.ValueOf(precursorsByMsLevel.Select(ImmutableList.ValueOf));
            });
        }

        public bool NegativeCharge { get; private set; }

        public SpectrumMetadata ChangeNegativeCharge(bool negativeCharge)
        {
            return ChangeProp(ImClone(this), im => im.NegativeCharge = negativeCharge);
        }

        public string ScanDescription { get; private set; }

        public SpectrumMetadata ChangeScanDescription(string scanDescription)
        {
            if (scanDescription == ScanDescription)
            {
                return this;
            }

            return ChangeProp(ImClone(this), im => im.ScanDescription = scanDescription);
        }
    }
}
