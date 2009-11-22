using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class Modification : AbstractSetting<DbModification>
    {
        public Modification(Workspace workspace) : base(workspace)
        {
            
        }
        public Modification(Workspace workspace, DbModification dbModification) : base(workspace, dbModification)
        {
            
        }

        protected override IEnumerable<ModelProperty> GetModelProperties()
        {
            foreach (var p in base.GetModelProperties())
            {
                yield return p;
            }
            yield return
                Property<Modification, String>(m => m.Symbol, (m, v) => m.Symbol = v, e => e.Symbol,
                                               (e, v) => e.Symbol = v);
            yield return
                Property<Modification, double>(m => m.DeltaMass, (m, v) => m.DeltaMass = v, e => e.DeltaMass,
                                               (e, v) => e.DeltaMass = v);
        }

        public String Symbol { get; set; }
        public double DeltaMass { get; set; }
        public override WorkspaceVersion GetWorkspaceVersion(WorkspaceVersion workspaceVersion, DbModification entity)
        {
            if (entity == null || entity.DeltaMass != DeltaMass)
            {
                return workspaceVersion.IncMassVersion();
            }
            return workspaceVersion;
        }
        protected override DbModification ConstructEntity(NHibernate.ISession session)
        {
            return new DbModification();
        }
    }
}
