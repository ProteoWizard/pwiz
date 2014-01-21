using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace turnover.Data
{
    public class DbPeptideComparisonData : DbAnnotatedEntity<DbPeptideComparisonData>
    {
        public virtual DbPeptideComparison PeptideComparison { get; set; }
        public virtual DbPeptideSearchResult PeptideSearchResult { get; set; }
    }
}
