using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.Model
{
    public struct MzRange
    {
        public MzRange(double value) : this(value,value)
        {
        }
        public MzRange(double min, double max) : this()
        {
            Min = min;
            Max = max;
        }
        public MzRange Union(double value)
        {
            return new MzRange(Math.Min(Min, value), Math.Max(Max, value));
        }
        public bool Contains(double value)
        {
            return value >= Min && value <= Max;
        }
        public double Center
        {
            get { return (Min + Max) / 2; }
        }
        public bool ContainsWithMassAccuracy(double value, double massAccuracy)
        {
            if (value < Min)
            {
                return (Min - value)*massAccuracy < Min;
            }
            if (value > Max)
            {
                return (value - Max)*massAccuracy < Max;
            }
            return true;
        }
        public double Distance(double value)
        {
            if (value < Min)
            {
                return Min - value;
            }
            if (value > Max)
            {
                return value - Max;
            }
            return 0;
        }
        public double Min { get; private set; }
        public double Max { get; private set; }
        public override String ToString()
        {
            if (Min == Max)
            {
                return Max.ToString();
            }
            return Min + "-" + Max;
        }
        public String ToString(String format)
        {
            if (Min == Max)
            {
                return Max.ToString(format);
            }
            return Min.ToString(format) + "-" + Max.ToString(format);
        }
    }
}
