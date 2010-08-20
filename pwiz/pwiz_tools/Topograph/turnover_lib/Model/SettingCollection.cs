using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public abstract class SettingCollection<P,K,E,C> : EntityModelCollection<P, K, E, C>
        where P : DbEntity<P>
        where E : DbEntity<E>
        where C : AbstractSetting<E>
    {
        protected SettingCollection(Workspace workspace, P parent) : base(workspace,parent)
        {
        }

        public WorkspaceVersion MergeChildren(P parent, WorkspaceVersion workspaceVersion, Dictionary<K,E> newChildren)
        {
            var result = UpdateChildren(workspaceVersion, newChildren, true);
            SavedEntity = parent;
            return result;
        }

        private WorkspaceVersion UpdateChildren(WorkspaceVersion workspaceVersion, Dictionary<K,E> newChildren, bool fromDatabase)
        {
            foreach (var entry in newChildren)
            {
                var model = GetChild(entry.Key);
                if (model == null)
                {
                    model = WrapChild(entry.Value);
                    if (!fromDatabase)
                    {
                        model.SavedEntity = null;
                    }
                    workspaceVersion = model.GetWorkspaceVersion(workspaceVersion, null);
                    AddChild(entry.Key, model);
                }
                else
                {
                    workspaceVersion = model.GetWorkspaceVersion(workspaceVersion, entry.Value);
                    var savedEntity = model.SavedEntity;
                    model.Merge(entry.Value);
                    if (!fromDatabase)
                    {
                        model.SavedEntity = savedEntity;
                    }
                }
            }
            foreach (var entry in _childDict.ToArray())
            {
                if (fromDatabase && entry.Value.IsNew())
                {
                    continue;
                }
                if (!newChildren.ContainsKey(entry.Key))
                {
                    workspaceVersion = entry.Value.GetWorkspaceVersion(workspaceVersion, null);
                    RemoveChild(entry.Key);
                }
            }
            return workspaceVersion;
        }
        public WorkspaceVersion UpdateFromUi(WorkspaceVersion workspaceVersion, Dictionary<K,E> newChildren)
        {
            return UpdateChildren(workspaceVersion, newChildren, false);
        }
        public WorkspaceVersion CurrentWorkspaceVersion(WorkspaceVersion savedWorkspaceVersion)
        {
            var currentWorkspaceVersion = savedWorkspaceVersion;
            foreach (var child in ListChildren())
            {
                currentWorkspaceVersion = child.GetWorkspaceVersion(currentWorkspaceVersion, child.SavedEntity);
            }
            return currentWorkspaceVersion;
        }
    }
}
