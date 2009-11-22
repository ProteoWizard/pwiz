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
using System.Linq;
using System.Collections.Generic;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class Modifications : SettingCollection<DbWorkspace, String, DbModification, Modification>
    {
        public Modifications(Workspace workspace, DbWorkspace dbWorkspace)
            : base(workspace, dbWorkspace)
        {
        }
        protected override IEnumerable<KeyValuePair<string, DbModification>> GetChildren(DbWorkspace parent)
        {
            foreach (var modification in parent.Modifications)
            {
                yield return new KeyValuePair<string, DbModification>(modification.Symbol, modification);
            }
        }

        protected override int GetChildCount(DbWorkspace parent)
        {
            return parent.ModificationCount;
        }

        protected override void SetChildCount(DbWorkspace parent, int childCount)
        {
            parent.ModificationCount = childCount;
        }

        public override Modification WrapChild(DbModification entity)
        {
            return new Modification(Workspace, entity);
        }
    }
}