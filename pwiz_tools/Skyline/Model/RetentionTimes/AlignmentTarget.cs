using System.Collections.Generic;
using System.Threading;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.RetentionTimes
{
    public class AlignmentTarget
    {
        public AlignmentTarget(RegressionMethodRT regressionMethod, IRetentionScoreCalculator calculator)
        {
            RegressionMethod = regressionMethod;
            Calculator = calculator;
        }

        public RegressionMethodRT RegressionMethod { get; }
        public IRetentionScoreCalculator Calculator { get; }

        protected bool Equals(AlignmentTarget other)
        {
            return RegressionMethod == other.RegressionMethod && Equals(Calculator, other.Calculator);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((AlignmentTarget)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)RegressionMethod * 397) ^ (Calculator != null ? Calculator.GetHashCode() : 0);
            }
        }

        public AlignmentFunction PerformAlignment(Dictionary<Target, double> times, CancellationToken cancellationToken)
        {
            var xValues = new List<double>();
            var yValues = new List<double>();
            foreach (var kvp in times)
            {
                var xValue = Calculator.ScoreSequence(kvp.Key);
                if (xValue.HasValue)
                {
                    xValues.Add(xValue.Value);
                    yValues.Add(kvp.Value);
                }
            }

            switch (RegressionMethod)
            {
                case RegressionMethodRT.linear:
                {
                    var statRT = new Statistics(yValues);
                    var stat = new Statistics(xValues);
                    var regressionLineElement = new RegressionLineElement(statRT.Slope(stat), statRT.Intercept(stat));
                    var regressionLine =
                        new RegressionLine(regressionLineElement.Slope, regressionLineElement.Intercept);
                    return AlignmentFunction.Define(regressionLine.GetY, regressionLine.GetX);
                }
                case RegressionMethodRT.kde:
                {
                    if (xValues.Count <= 1)
                    {
                        return null;
                    }

                    var kdeAligner = new KdeAligner(-1, -1);
                    kdeAligner.Train(xValues.ToArray(), yValues.ToArray(), cancellationToken);
                    return AlignmentFunction.Define(kdeAligner.GetValue, kdeAligner.GetValueReversed);
                }
                case RegressionMethodRT.log:
                    // TODO
                    var x = "TODO";
                    return null;
                case RegressionMethodRT.loess:
                {
                    var loessAligner = new LoessAligner(-1, -1, 0.4);
                    loessAligner.Train(xValues.ToArray(), yValues.ToArray(), cancellationToken);
                    return AlignmentFunction.Define(loessAligner.GetValue, loessAligner.GetValueReversed);
                }
                default:
                    return null;
            }

        }

        public static AlignmentTarget GetAlignmentTarget(SrmDocument document)
        {
            var irtCalculator = document.Settings.PeptideSettings.Prediction.RetentionTime?.Calculator as RCalcIrt;
            if (true != irtCalculator?.IsUsable)
            {
                return null;
            }

            var regressionType = RegressionMethodRT.kde;
            if (irtCalculator.RegressionType == IrtRegressionType.LOWESS)
            {
                regressionType = RegressionMethodRT.loess;
            }

            return new AlignmentTarget(regressionType, irtCalculator);
        }
    }
}
