using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SProCoP
{
    class SProCoPUtil
    {
    }
    static class Constants
    {
        public const string TRUE_STRING = "1";              // Not L10N
        public const string FALSE_STRING = "0";             // Not L10N
        public const int ARGUMENT_COUNT = 4;
    }
    public enum ArgumentIndices
    {
        qc_runs,
        hq_ms,
        create_meta,
        mma_value
    }
}
