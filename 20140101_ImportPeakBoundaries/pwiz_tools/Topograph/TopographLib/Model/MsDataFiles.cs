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
    public class MsDataFiles : EntityModelList<long, MsDataFileData, MsDataFile>
    {
        public MsDataFiles(Workspace workspace) : base(workspace)
        {
        }

        protected override ImmutableSortedList<long, MsDataFile> CreateEntityList()
        {
            var data = Workspace.Data.MsDataFiles;
            if (data == null)
            {
                return ImmutableSortedList<long, MsDataFile>.EMPTY;
            }
            return
                ImmutableSortedList.FromValues(data.Select(entry => new KeyValuePair<long, MsDataFile>(
                    entry.Key, new MsDataFile(Workspace, entry.Key, entry.Value))));
        }

        public override IList<MsDataFile> DeepClone()
        {
            return Workspace.Clone().MsDataFiles;
        }

        protected override MsDataFile CreateEntityForKey(long key, MsDataFileData data)
        {
            return new MsDataFile(Workspace, key, data);
        }

        public override long GetKey(MsDataFile value)
        {
            return value.Id;
        }

        protected override ImmutableSortedList<long, MsDataFileData> GetData(WorkspaceData workspaceData)
        {
            return workspaceData.MsDataFiles;
        }

        protected override WorkspaceData SetData(WorkspaceData workspaceData, ImmutableSortedList<long, MsDataFileData> data)
        {
            return workspaceData.SetMsDataFiles(data);
        }
    }
}
