using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results
{
    public class InjectionGroup
    {
        private QueueWorker<PeptideChromDataSets> _chromDataSets;

        public InjectionGroup(SrmDocument document, string documentFilePath)
        {
            Document = document;
            DocumentFilePath = documentFilePath;
        }

        public SrmDocument Document { get; }
        public string DocumentFilePath { get; }


    }
}
