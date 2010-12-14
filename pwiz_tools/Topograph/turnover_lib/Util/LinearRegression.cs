using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.Util
{
    public class LinearRegression
    {
        public double Correlation { get; set; }
        public double Slope { get; set; }
        public double SlopeError { get; set; }
        public double Intercept { get; set; }
    }
}
