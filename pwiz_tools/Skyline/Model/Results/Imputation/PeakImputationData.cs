using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class PeakImputationData
    {
        public class Parameters : Immutable
        {
            public Parameters(SrmDocument document)
            {
                Document = document;
            }

            public SrmDocument Document { get; }

            public bool OverwriteManualPeaks { get; private set; }

            public Parameters ChangeOverwriteManualPeaks(bool value)
            {
                return ChangeProp(ImClone(this), im => im.OverwriteManualPeaks = value);
            }

            public double? CutoffScore { get; private set; }


            public Parameters ChangeCutoffScore(double? value)
            {
                return ChangeProp(ImClone(this), im => im.CutoffScore = value);
            }

            
        }
    }
}
