using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class DefaultPeakScores
    {
        public float? Intensity { get; private set; }
        public float? CoelutionCount { get; private set; }
        public bool Identified { get; private set; }
        public float? Dotp { get; private set; }
        public float? Shape { get; private set; }
        public float? WeightedCoelution { get; private set; }
        public float? RetentionTimeDifference { get; private set; }
    }
}
