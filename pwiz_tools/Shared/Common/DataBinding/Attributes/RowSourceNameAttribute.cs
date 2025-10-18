using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pwiz.Common.DataBinding.Attributes
{
    public class RowSourceNameAttribute : Attribute
    {
        public RowSourceNameAttribute(string name)
        {
            Name = name;
        }
        public string Name { get; }
    }
}
