using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.Query
{
    public class QueryColumn : Attribute
    {
        public String Format { get; set; }
        public String FullName { get; set; }
    }
}
