using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace pwiz.Common.Colors
{
    public class NumericColorScheme : AbstractColorScheme<double>
    {
        public NumericColorScheme()
        {
        }
        public double MaxAbsZScore { get; set; }

        public override void Calibrate(IEnumerable<double> values)
        {
            var maxZScore = 0.0;
            foreach (var value in values)
            {
                maxZScore = Math.Max(maxZScore, Math.Abs(value));
            }

            MaxAbsZScore = maxZScore;
        }

        public override Color? GetColor(double zScore)
        {
            double zScoreRange = Math.Max(MaxAbsZScore, 1.0);
            var lightness = (int)(Math.Max(0, zScoreRange - Math.Abs(zScore)) * (255 / zScoreRange));
            if (zScore >= 0)
            {
                return Color.FromArgb(255, lightness, lightness);
            }
            else
            {
                return Color.FromArgb(lightness, lightness, 255);
            }
        }
    }
}
