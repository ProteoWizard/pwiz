using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class RegressionResult : Immutable
    {
        public double? Slope { get; private set; }

        public RegressionResult ChangeSlope(double? slope)
        {
            return ChangeProp(ImClone(this), im => im.Slope = slope);
        }
        public double? Intercept { get; private set; }

        public RegressionResult ChangeIntercept(double? intercept)
        {
            return ChangeProp(ImClone(this), im => im.Intercept = intercept);
        }
        public double? RSquared { get; private set; }

        public RegressionResult ChangeRSquared(double? rSquared)
        {
            return ChangeProp(ImClone(this), im => im.RSquared = rSquared);
        }
    }
}
