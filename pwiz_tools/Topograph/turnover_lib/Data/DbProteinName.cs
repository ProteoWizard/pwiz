using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.Data
{
    public class DbProteinName
    {
        public virtual String Name { get; set; }
        public virtual String Description { get; set; }
        public virtual DbProteinIdType IdType { get; set; }
    }
}
