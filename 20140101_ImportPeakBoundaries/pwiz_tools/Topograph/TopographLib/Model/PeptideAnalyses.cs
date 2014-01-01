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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Topograph.Model.Data;

namespace pwiz.Topograph.Model
{
    public class PeptideAnalyses : EntityModelList<long, PeptideAnalysisData, PeptideAnalysis>
    {
        public PeptideAnalyses(Workspace workspace) : base(workspace)
        {
            
        }

        protected override ImmutableSortedList<long, PeptideAnalysis> CreateEntityList()
        {
            var data = Workspace.Data.PeptideAnalyses;
            if (data == null)
            {
                return ImmutableSortedList<long, PeptideAnalysis>.EMPTY;
            }
            return
                ImmutableSortedList.FromValues(data.Select(entry => new KeyValuePair<long, PeptideAnalysis>(
                    entry.Key, new PeptideAnalysis(Workspace, entry.Key, entry.Value))));
        }

        public override IList<PeptideAnalysis> DeepClone()
        {
            return new Workspace(Workspace).PeptideAnalyses;
        }

        protected override ImmutableSortedList<long, PeptideAnalysisData> GetData(WorkspaceData workspaceData)
        {
            return workspaceData.PeptideAnalyses;
        }

        protected override WorkspaceData SetData(WorkspaceData workspaceData, ImmutableSortedList<long, PeptideAnalysisData> data)
        {
            return workspaceData.SetPeptideAnalyses(data);
        }

        public override long GetKey(PeptideAnalysis value)
        {
            return value.Id;
        }

        protected override PeptideAnalysis CreateEntityForKey(long key, PeptideAnalysisData data)
        {
            return new PeptideAnalysis(Workspace, key, data);
        }

        protected override bool CheckDirty(PeptideAnalysisData data, PeptideAnalysisData savedData)
        {
            return data.CheckDirty(savedData);
        }
    }
}
