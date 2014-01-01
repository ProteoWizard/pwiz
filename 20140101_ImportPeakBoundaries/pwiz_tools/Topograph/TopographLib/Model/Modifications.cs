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

using System.Linq;
using NHibernate;
using NHibernate.Criterion;
using pwiz.Common.Collections;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model.Data;

namespace pwiz.Topograph.Model
{
    public class Modifications : AbstractSettings<string, double>
    {
        public Modifications(Workspace workspace) : base(workspace)
        {
        }

        protected override void Diff(WorkspaceChangeArgs workspaceChange, ImmutableSortedList<string, double> newValues, ImmutableSortedList<string, double> oldValues)
        {
            if (!Equals(newValues, oldValues))
            {
                workspaceChange.AddChromatogramMassChange();
            }
        }

        public override bool Save(ISession session, DbWorkspace dbWorkspace)
        {
            var existingModifications = session.CreateCriteria<DbModification>()
                                  .Add(Restrictions.Eq("Workspace", dbWorkspace))
                                  .List<DbModification>()
                                  .ToDictionary(dbModification=>dbModification.Symbol);
            return SaveChangedEntities(session, existingModifications, 
                dbModification=>dbModification.DeltaMass,
                (dbModification, value)=>dbModification.DeltaMass = value,
                key=>new DbModification
                         {
                             Symbol = key,
                             Workspace = dbWorkspace
                         });
        }

        protected override ImmutableSortedList<string, double> GetData(WorkspaceData workspaceData)
        {
            return workspaceData.Modifications;
        }

        protected override WorkspaceData SetData(WorkspaceData workspaceData, ImmutableSortedList<string, double> value)
        {
            return workspaceData.SetModifications(value);
        }
    }
}