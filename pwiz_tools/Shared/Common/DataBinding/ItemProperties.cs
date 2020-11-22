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

        public ImmutableList<DataPropertyDescriptor> AsImmutableList()
        {
            return _properties;
        }
    }
}
