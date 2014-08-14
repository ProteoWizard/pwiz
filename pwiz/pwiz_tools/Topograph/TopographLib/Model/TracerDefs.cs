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
    public class TracerDefs : AbstractSettings<string, TracerDefData>
    {
        public TracerDefs(Workspace workspace) : base(workspace)
        {
        }

        protected override void Diff(WorkspaceChangeArgs workspaceChange, ImmutableSortedList<string, TracerDefData> newValues, ImmutableSortedList<string, TracerDefData> oldValues)
        {
            if (!Equals(newValues.Keys, oldValues.Keys))
            {
                workspaceChange.AddChromatogramMassChange();
                return;
            }
            for (int i = 0; i < newValues.Count; i++)
            {
                var newValue = newValues.Values[i];
                var oldValue = oldValues.Values[i];
                if (!newValue.EqualMasses(oldValue))
                {
                    workspaceChange.AddChromatogramMassChange();
                }
                if (!newValue.EqualPeakPicking(oldValue))
                {
                    workspaceChange.AddPeakPickingChange();
                }
            }
            if (!Equals(newValues, oldValues))
            {
                workspaceChange.AddSettingChange();
            }
        }

        public override bool Save(ISession session, DbWorkspace dbWorkspace)
        {
            var existing =
                session.CreateCriteria<DbTracerDef>()
                       .Add(Restrictions.Eq("Workspace", dbWorkspace))
                       .List<DbTracerDef>()
                       .ToDictionary(dbTracerDef=>dbTracerDef.Name);
            return SaveChangedEntities(session, existing,
                                dbTracerDef => new TracerDefData(dbTracerDef),
                                UpdateDbTracerDef,
                                key => new DbTracerDef
                                           {
                                               Name = key,
                                               Workspace = dbWorkspace,
                                           });
        }

        private void UpdateDbTracerDef(DbTracerDef dbTracerDef, TracerDefData tracerDefData)
        {
            dbTracerDef.AtomCount = tracerDefData.AtomCount;
            dbTracerDef.AtomPercentEnrichment = tracerDefData.AtomPercentEnrichment;
            dbTracerDef.DeltaMass = tracerDefData.DeltaMass;
            dbTracerDef.FinalEnrichment = tracerDefData.FinalEnrichment;
            dbTracerDef.InitialEnrichment = tracerDefData.InitialEnrichment;
            dbTracerDef.IsotopesEluteEarlier = tracerDefData.IsotopesEluteEarlier;
            dbTracerDef.IsotopesEluteLater = tracerDefData.IsotopesEluteLater;
            dbTracerDef.Name = tracerDefData.Name;
            dbTracerDef.TracerSymbol = tracerDefData.TracerSymbol;
        }

        protected override ImmutableSortedList<string, TracerDefData> GetData(WorkspaceData workspaceData)
        {
            return workspaceData.TracerDefs;
        }

        protected override WorkspaceData SetData(WorkspaceData workspaceData, ImmutableSortedList<string, TracerDefData> value)
        {
            return workspaceData.SetTracerDefs(value);
        }
    }
}
