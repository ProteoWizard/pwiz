using System;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.DataBinding
{
    public class EntitiesChangedEvent : EventArgs
    {
        public EntitiesChangedEvent()
        {
            AddedEntities = new object[0];
            ChangedEntities = new object[0];
            RemovedEntities = new object[0];
        }
        public EntitiesChangedEvent(EntitiesChangedEvent that)
        {
            AddedEntities = that.AddedEntities;
            ChangedEntities = that.ChangedEntities;
            RemovedEntities = that.RemovedEntities;
        }
        public ICollection<object> AddedEntities { get; private set; }
        public EntitiesChangedEvent SetAddedEntities(ICollection<object> addedEntities)
        {
            return new EntitiesChangedEvent(this){AddedEntities = Array.AsReadOnly(new HashSet<object>(addedEntities).ToArray())};
        }
        public ICollection<object> ChangedEntities { get; private set; }
        public EntitiesChangedEvent SetChangedEntities(ICollection<object> changedEntities)
        {
            return new EntitiesChangedEvent(this){ChangedEntities = Array.AsReadOnly(new HashSet<object>(changedEntities).ToArray())};
        }
        public ICollection<object> RemovedEntities { get; private set; }
        public EntitiesChangedEvent SetRemovedEntities(ICollection<object> removedEntities)
        {
            return new EntitiesChangedEvent(this){ChangedEntities = Array.AsReadOnly(new HashSet<object>(removedEntities).ToArray())};
        }
    }
}
