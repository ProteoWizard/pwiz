using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProteomeDb.DataModel
{
    public class DbOrganism : DbEntity<DbOrganism>
    {
        public DbOrganism()
        {
            Digestions = new List<DbDigestion>();
            Proteins = new List<DbProtein>();
        }
        public virtual String Name { get; set; }
        public virtual String Description { get; set; }
        public virtual ICollection<DbDigestion> Digestions { get; set; }
        public virtual ICollection<DbProtein> Proteins { get; set; }
    }
}
