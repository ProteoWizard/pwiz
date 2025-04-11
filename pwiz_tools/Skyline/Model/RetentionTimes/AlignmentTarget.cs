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

        public PiecewiseLinearMap PerformAlignment(Dictionary<Target, double> times, CancellationToken cancellationToken)
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
                    var slope = statRT.Slope(stat);
                    var intercept = statRT.Intercept(stat);
                    return PiecewiseLinearMap.FromValues(new Dictionary<double, double>
                    {
                        { 0, intercept},
                        { 1, slope + intercept }
                    });
                }
                case RegressionMethodRT.kde:
                {
                    if (xValues.Count <= 1)
                    {
                        return null;
                    }

                    var kdeAligner = new KdeAligner(-1, -1);
                    kdeAligner.Train(xValues.ToArray(), yValues.ToArray(), cancellationToken);
                    kdeAligner.GetSmoothedValues(out var xSmoothed, out var ySmoothed);
                    return PiecewiseLinearMap.FromValues(xSmoothed, ySmoothed);
                }
                case RegressionMethodRT.log:
                    // TODO
                    var x = "TODO";
                    return null;
                case RegressionMethodRT.loess:
                {
                    var loessAligner = new LoessAligner(-1, -1, 0.4);
                    loessAligner.Train(xValues.ToArray(), yValues.ToArray(), cancellationToken);
                    loessAligner.GetSmoothedValues(out var xSmoothed, out var ySmoothed);
                    return PiecewiseLinearMap.FromValues(xSmoothed, ySmoothed);
                }
                default:
                    return null;
            }

        }

        public static AlignmentTarget GetAlignmentTarget(SrmDocument document)
        {
            TryGetAlignmentTarget(document.Settings, out var alignmentTarget);
            return alignmentTarget;
        }

        public static bool TryGetAlignmentTarget(SrmSettings settings, out AlignmentTarget alignmentTarget)
        {
            var irtCalculator = settings.PeptideSettings.Prediction.RetentionTime?.Calculator as RCalcIrt;
            if (irtCalculator == null)
            {
                alignmentTarget = null;
                return true;
            }

            if (!irtCalculator.IsUsable)
            {
                alignmentTarget = null;
                return false;
            }
            // TODO: use actual regression type
            var regressionType = RegressionMethodRT.kde;
            if (irtCalculator.RegressionType == IrtRegressionType.LOWESS)
            {
                regressionType = RegressionMethodRT.loess;
            }

            alignmentTarget = new AlignmentTarget(regressionType, irtCalculator);
            return true;
        }
    }
}
