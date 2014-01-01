/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
    public class PeptideFileAnalyses : EntityModelList<long, PeptideFileAnalysisData, PeptideFileAnalysis>
    {
        public PeptideFileAnalyses(PeptideAnalysis peptideAnalysis) : base(peptideAnalysis.Workspace)
        {
            PeptideAnalysis = peptideAnalysis;
        }

        public PeptideAnalysis PeptideAnalysis { get; private set; }
        protected override ImmutableSortedList<long, PeptideFileAnalysis> CreateEntityList()
        {
            return ImmutableSortedList.FromValues(
                PeptideAnalysis.Data.FileAnalyses.Select(
                    pair => new KeyValuePair<long, PeptideFileAnalysis>(pair.Key, new PeptideFileAnalysis(PeptideAnalysis, pair.Key, pair.Value))));
        }

        public override IList<PeptideFileAnalysis> DeepClone()
        {
            return Workspace.Clone().PeptideAnalyses.FindByKey(PeptideAnalysis.Id).FileAnalyses;
        }

        protected override ImmutableSortedList<long, PeptideFileAnalysisData> GetData(WorkspaceData workspaceData)
        {
            if (null == workspaceData.PeptideAnalyses)
            {
                return null;
            }
            PeptideAnalysisData peptideAnalysisData;
            if (!workspaceData.PeptideAnalyses.TryGetValue(PeptideAnalysis.Id, out peptideAnalysisData))
            {
                return null;
            }
            return peptideAnalysisData.FileAnalyses;
        }

        protected override WorkspaceData SetData(WorkspaceData workspaceData, ImmutableSortedList<long, PeptideFileAnalysisData> data)
        {
            PeptideAnalysisData peptideAnalysisData;
            workspaceData.PeptideAnalyses.TryGetValue(PeptideAnalysis.Id, out peptideAnalysisData);
            peptideAnalysisData = peptideAnalysisData.SetFileAnalyses(data);
            return workspaceData.SetPeptideAnalyses(workspaceData.PeptideAnalyses
                .Replace(PeptideAnalysis.Id, peptideAnalysisData));
        }

        protected override PeptideFileAnalysis CreateEntityForKey(long key, PeptideFileAnalysisData data)
        {
            return new PeptideFileAnalysis(PeptideAnalysis, key, data);
        }

        public override long GetKey(PeptideFileAnalysis value)
        {
            return value.Id;
        }
    }
}
