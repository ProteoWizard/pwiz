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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class MsDataFiles : EntityModelCollection<DbWorkspace, long, DbMsDataFile, MsDataFile>
    {
        public MsDataFiles(Workspace workspace, DbWorkspace dbWorkspace) : base(workspace, dbWorkspace)
        {
            
        }
        protected override bool TrustChildCount { get { return false; } }

        protected override IEnumerable<KeyValuePair<long, DbMsDataFile>> GetChildren(DbWorkspace parent)
        {
            foreach (var dbMsDataFile in parent.MsDataFiles)
            {
                yield return new KeyValuePair<long, DbMsDataFile>(dbMsDataFile.Id.Value, dbMsDataFile);
            }
        }

        public override MsDataFile WrapChild(DbMsDataFile entity)
        {
            return new MsDataFile(Workspace, entity);
        }

        protected override int GetChildCount(DbWorkspace parent)
        {
            return parent.MsDataFileCount;
        }

        protected override void SetChildCount(DbWorkspace parent, int childCount)
        {
            parent.MsDataFileCount = childCount;
        }
        public MsDataFile GetMsDataFile(DbMsDataFile dbMsDataFile)
        {
            return GetChild(dbMsDataFile.Id.Value);
        }
    }
}
