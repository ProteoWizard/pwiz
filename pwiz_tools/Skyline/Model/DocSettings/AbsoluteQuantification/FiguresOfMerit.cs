using System.ComponentModel;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class FiguresOfMerit : Immutable
    {
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
    }
}
