using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.Data
{
    public class DbProteinSetMember : DbEntity<DbProteinSetMember>
    {
        public virtual DbProtein Protein { get; set; }
        public virtual DbProteinSet ProteinSet { get; set; }
    }
}
