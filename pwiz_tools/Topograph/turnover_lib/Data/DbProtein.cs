using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.Data
{
    public class DbProtein : DbEntity<DbProtein>
    {
        public virtual String Sequence { get; set; }
    }
}
