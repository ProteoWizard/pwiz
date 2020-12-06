using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding
{
    public class CaptionedValues
    {
        public CaptionedValues(IColumnCaption caption, Type valueType, IEnumerable values)
        {
            Caption = caption;
            ValueType = valueType;
            Values = ImmutableList.ValueOf(values.Cast<object>());
        }

        public IColumnCaption Caption { get; private set; }

        public Type ValueType { get; private set; }
        public ImmutableList<object> Values { get; private set; }

        public ImmutableList<Color?> Colors { get; private set; }

        public int ValueCount {get{ return Values.Count; }}
    }
}
