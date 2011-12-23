using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace turnover.Data
{
    public class DbPeptideComparisonMz : DbAnnotatedEntity<DbPeptideComparisonMz>
    {
        public virtual DbPeptideComparison PeptideComparison { get; set; }
        public virtual int MzIndex { get; set; }
        public virtual double Mz { get; set; }
    }
}
