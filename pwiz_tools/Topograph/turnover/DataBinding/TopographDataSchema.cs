using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Common.DataBinding;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.DataBinding
{
    public class TopographDataSchema : DataSchema
    {
        public TopographDataSchema(Workspace workspace)
        {
            Workspace = workspace;
        }

        public Workspace Workspace { get; private set; }

        private readonly ICollection<DataRowsChangedEventHandler> _dataRowsChangedEvent 
            = new HashSet<DataRowsChangedEventHandler>();
        public override event DataRowsChangedEventHandler DataRowsChanged
        {
            add
            {
                lock (_dataRowsChangedEvent)
                {
                    if (_dataRowsChangedEvent.Count == 0)
                    {
                        Workspace.EntitiesChange += Workspace_EntitiesChange;
                    }
                    _dataRowsChangedEvent.Add(value);
                }
            }
            remove
            {
                lock (_dataRowsChangedEvent)
                {
                    _dataRowsChangedEvent.Remove(value);
                    if (_dataRowsChangedEvent.Count == 0)
                    {
                        Workspace.EntitiesChange -= Workspace_EntitiesChange;
                    }
                }
            }
        }

        void Workspace_EntitiesChange(EntitiesChangedEventArgs entitiesChangedEventArgs)
        {
            var dataRowsChangedEventArgs
                = new DataRowsChangedEventArgs()
                      {
                          Added = entitiesChangedEventArgs.GetNewEntities().ToArray(),
                          Deleted = entitiesChangedEventArgs.GetRemovedEntities().ToArray(),
                          Changed = entitiesChangedEventArgs.GetChangedEntities().ToArray(),
                      };
            ICollection<DataRowsChangedEventHandler> eventHandlers;
            lock (_dataRowsChangedEvent)
            {
                eventHandlers = _dataRowsChangedEvent.ToArray();
            }
            foreach (var eventHandler in eventHandlers)
            {
                eventHandler.Invoke(this, dataRowsChangedEventArgs);
            }
        }
    }
}
