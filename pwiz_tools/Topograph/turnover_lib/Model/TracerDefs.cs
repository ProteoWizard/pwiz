
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;

namespace pwiz.Topograph.Model
{
    public class TracerDefs : SettingCollection<DbWorkspace, String, DbTracerDef, TracerDefModel>
    {
        public TracerDefs(Workspace workspace, DbWorkspace dbWorkspace) : base(workspace, dbWorkspace)
        {
        }

        protected override IEnumerable<KeyValuePair<String, DbTracerDef>> GetChildren(DbWorkspace parent)
        {
            foreach (var tracerDef in parent.TracerDefs)
            {
                yield return new KeyValuePair<String, DbTracerDef>(tracerDef.Name, tracerDef);
            }
        }

        protected override int GetChildCount(DbWorkspace parent)
        {
            return parent.TracerDefCount;
        }

        protected override void SetChildCount(DbWorkspace parent, int childCount)
        {
            parent.TracerDefCount = childCount;
        }

        public override TracerDefModel WrapChild(DbTracerDef entity)
        {
            return new TracerDefModel(Workspace, entity);
        }
    }
}
