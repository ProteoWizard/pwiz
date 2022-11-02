using System.Collections.Generic;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding
{
    public class ItemProperties : AbstractReadOnlyList<DataPropertyDescriptor>
    {
        public static readonly ItemProperties EMPTY = new ItemProperties(ImmutableList.Empty<DataPropertyDescriptor>());
        private ImmutableList<DataPropertyDescriptor> _properties;
        private Dictionary<string, int> _nameToIndex;
        public ItemProperties(IEnumerable<DataPropertyDescriptor> properties)
        {
            _properties = ImmutableList.ValueOf(properties);
            _nameToIndex = new Dictionary<string, int>(_properties.Count);
            for (int i = 0; i < _properties.Count; i++)
            {
                _nameToIndex.Add(_properties[i].Name, i);
            }
        }

        public static ItemProperties FromList(IEnumerable<DataPropertyDescriptor> properties)
        {
            return properties as ItemProperties ?? new ItemProperties(properties);
        }

        public override int Count => _properties.Count;

        public override DataPropertyDescriptor this[int index] => _properties[index];

        public int IndexOfName(string propertyName)
        {
            if (_nameToIndex.TryGetValue(propertyName, out var index))
            {
                return index;
            }
            return -1;
        }

        public DataPropertyDescriptor FindByName(string propertyName)
        {
            int index = IndexOfName(propertyName);
            return index >= 0 ? _properties[index] : null;
        }

        public ImmutableList<DataPropertyDescriptor> AsImmutableList()
        {
            return _properties;
        }

        protected bool Equals(ItemProperties other)
        {
            return _properties.Equals(other._properties);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ItemProperties) obj);
        }

        public override int GetHashCode()
        {
            return _properties.GetHashCode();
        }
    }
}
