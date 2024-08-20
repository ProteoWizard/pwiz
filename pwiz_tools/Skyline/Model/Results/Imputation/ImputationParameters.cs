using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class ImputationParameters : Immutable
    {
        public ImputationParameters(SrmDocument document)
        {
            Document = document;
        }

        public SrmDocument Document { get; }

        public bool OverwriteManual { get; private set; }

        public ImputationParameters ChangeOverwriteManual(bool value)
        {
            return ChangeProp(ImClone(this), im => im.OverwriteManual = value);
        }

        
    }
}
