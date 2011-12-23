using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace turnover.Data
{
    public class DbPeptideDataTracer : DbEntity<DbPeptideDataTracer>
    {
        public virtual DbPeptideSearchResult PeptideSearchResult { get; set; }
        public virtual int TracerCount { get; set; }
        public virtual double Percent { get; set; }
    }
}
