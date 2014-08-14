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
    public class Peptides : EntityModelList<long, PeptideData, Peptide>
    {
        public Peptides(Workspace workspace) : base(workspace)
        {
            
        }

        protected override ImmutableSortedList<long, Peptide> CreateEntityList()
        {
            return
                ImmutableSortedList.FromValues(Workspace.Data.Peptides
                .Select(entry => new KeyValuePair<long, Peptide>(
                    entry.Key, new Peptide(Workspace, entry.Key, entry.Value))));
        }

        public override IList<Peptide> DeepClone()
        {
            return Workspace.Clone().Peptides;
        }

        protected override Peptide CreateEntityForKey(long key, PeptideData data)
        {
            return new Peptide(Workspace, key, data);
        }

        public override long GetKey(Peptide value)
        {
            return value.Id;
        }

        protected override ImmutableSortedList<long, PeptideData> GetData(WorkspaceData workspaceData)
        {
            return workspaceData.Peptides;
        }

        protected override WorkspaceData SetData(WorkspaceData workspaceData, ImmutableSortedList<long, PeptideData> data)
        {
            return workspaceData.SetPeptides(data);
        }
    }
}
