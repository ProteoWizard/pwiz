using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Data.Snapshot
{
    public class LoadPeptideAnalysisSnapshot : ILongOperationJob
    {
        public LoadPeptideAnalysisSnapshot(Workspace workspace, ICollection<long> peptideAnalysisIds, bool loadChromatograms)
        {
            Workspace = workspace;
            PeptideAnalyses = new Dictionary<long, PeptideAnalysis>();
            foreach (var id in peptideAnalysisIds)
            {
                PeptideAnalyses[id] = null;
            }
            LoadChromatograms = loadChromatograms;
        }

        public Workspace Workspace { get; private set; }
        public Dictionary<long, PeptideAnalysis> PeptideAnalyses { get; private set; }
        public bool LoadChromatograms { get; private set; }

        public void Run(LongOperationBroker longOperationBroker)
        {
            int attemptNumber = 1;
            longOperationBroker.UpdateStatusMessage("Loading peptide analyses");
            while (true)
            {
                if (Workspace.Reconciler.LoadPeptideAnalyses(PeptideAnalyses, LoadChromatograms))
                {
                    break;
                }
                attemptNumber++;
                longOperationBroker.UpdateStatusMessage("Loading peptide analyses.  Attempt #" + attemptNumber);
            }
        }

        public bool Cancel()
        {
            return true;
        }
    }
}
