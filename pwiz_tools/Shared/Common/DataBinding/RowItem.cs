using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Common.DataBinding
{
    public class RowItem
    {
        public RowItem(object key, object value) : this(null, IdentifierPath.Root, key, value)
        {
        }
        public RowItem(RowItem parent, IdentifierPath sublistId, object key, object value)
        {
            Parent = parent;
            SublistId = sublistId;
            Key = key;
            Value = value;
        }

        public RowItem Parent { get; private set; }
        public IdentifierPath SublistId { get; private set; }
        public object Key { get; private set; }
        public object Value { get; private set; }
    }
}
