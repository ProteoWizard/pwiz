
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class TracerDefs : SimpleChildCollection<DbWorkspace, String, DbTracerDef>
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

        protected override void SetParent(DbTracerDef child, DbWorkspace parent)
        {
            child.Workspace = parent;
        }
    }
}
