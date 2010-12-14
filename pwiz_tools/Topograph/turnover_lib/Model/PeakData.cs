using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Crawdad;

namespace pwiz.Topograph.Model
{
    public class PeakData<TK>
    {
        public PeakData(TK chromatogramKey, CrawdadPeak crawdadPeak)
        {
            ChromatogramKey = chromatogramKey;
            CrawdadPeak = crawdadPeak;
        }

        public TK ChromatogramKey { get; private set; }
        public CrawdadPeak CrawdadPeak { get; private set; }
    }
}
