using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public abstract class AbstractSetting<E> : EntityModel<E> where E:DbEntity<E>
    {
        protected AbstractSetting(Workspace workspace, E entity) : base(workspace, entity)
        {
        }
        protected AbstractSetting(Workspace workspace) : base(workspace)
        {
        }

        public abstract WorkspaceVersion GetWorkspaceVersion(WorkspaceVersion workspaceVersion, E entity);
        public WorkspaceVersion Merge(WorkspaceVersion workspaceVersion, E entity)
        {
            workspaceVersion = GetWorkspaceVersion(workspaceVersion, entity);
            Merge(entity);
            return workspaceVersion;
        }
    }
}
