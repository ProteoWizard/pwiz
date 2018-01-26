using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class FiguresOfMerit : Immutable
    {
        public static FiguresOfMerit EMPTY = new FiguresOfMerit();

        public double? LimitOfDetection { get; private set; }
        public double? LimitOfQuantification { get; private set; }
        [Browsable(false)]
        public string Units { get; private set; }

        public FiguresOfMerit ChangeLimitOfDetection(double? limitOfDetection)
        {
            return ChangeProp(ImClone(this), im => im.LimitOfDetection = limitOfDetection);
        }

        public FiguresOfMerit ChangeLimitOfQuantification(double? limitOfQuantification)
        {
            return ChangeProp(ImClone(this), im => im.LimitOfQuantification = limitOfQuantification);
        }

        public FiguresOfMerit ChangeUnits(string units)
        {
            return ChangeProp(ImClone(this), im => im.Units = units);
        }

        public override string ToString()
        {
            var parts = new List<string>();
            string format = string.IsNullOrEmpty(Units) ? Formats.CalibrationCurve : Formats.Concentration;
            if (LimitOfDetection.HasValue)
            {
                parts.Add("LOD: " + LimitOfDetection.Value.ToString(format));
            }
            if (LimitOfQuantification.HasValue)
            {
                parts.Add("LOQ: " + LimitOfQuantification.Value.ToString(format));
            }
            if (!parts.Any())
            {
                return string.Empty;
            }
            if (!string.IsNullOrEmpty(Units))
            {
                parts.Add(Units);
            }
            return TextUtil.SpaceSeparate(parts);
        }
    }
}
