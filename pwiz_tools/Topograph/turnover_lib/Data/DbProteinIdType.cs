using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.Data
{
    public class DbProteinIdType : DbEntity<DbProteinIdType>
    {
        public virtual String Name { get; set; }
        public virtual int Priority { get; set; }
    }
}
