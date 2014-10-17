/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Topograph.Model;
using pwiz.Topograph.Model.Data;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Data.Snapshot
{
    public class LoadPeptideAnalysisSnapshot
    {
        public LoadPeptideAnalysisSnapshot(Workspace workspace, ICollection<long> peptideAnalysisIds)
        {
            Workspace = workspace;
            PeptideAnalysisIds = ImmutableList.ValueOf(peptideAnalysisIds);
        }

        public Workspace Workspace { get; private set; }
        public ICollection<long> PeptideAnalysisIds { get; private set; }

        public void Run(LongOperationBroker longOperationBroker)
        {
            int attemptNumber = 1;
            longOperationBroker.UpdateStatusMessage("Loading peptide analyses");
            while (true)
            {
                var savedData = Workspace.SavedData;
                var requestedPeptideAnalyses = new Dictionary<long, bool>();
                foreach (var id in PeptideAnalysisIds)
                {
                    PeptideAnalysisData existing;
                    if (null != savedData.PeptideAnalyses && savedData.PeptideAnalyses.TryGetValue(id, out existing))
                    {
                        if (existing.ChromatogramsWereLoaded)
                        {
                            continue;
                        }
                    }
                    requestedPeptideAnalyses[id] = true;
                }
                if (requestedPeptideAnalyses.Count == 0)
                {
                    return;
                }
                if (Workspace.DatabasePoller.TryLoadAndMergeChanges(requestedPeptideAnalyses))
                {
                    return;
                }
                attemptNumber++;
                longOperationBroker.UpdateStatusMessage("Loading peptide analyses.  Attempt #" + attemptNumber);
            }
        }
    }
}
