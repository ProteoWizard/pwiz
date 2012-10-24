using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.Data
{
    public class DbProteinName : DbEntity<DbProteinName>
    {
        public virtual DbProtein Protein { get; set; }
        public virtual String SeqId { get; set; }
        public virtual String Description { get; set; }

    }
}
