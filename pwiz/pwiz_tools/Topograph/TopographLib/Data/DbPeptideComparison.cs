using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace turnover.Data
{
    public class DbPeptideComparison : DbAnnotatedEntity<DbPeptideComparison>
    {
        public DbPeptideComparison()
        {
            Datas = new List<DbPeptideComparisonData>();
            Mzs = new List<DbPeptideComparisonMz>();
        }
        
        public virtual DbPeptide Peptide { get; set; }
        public virtual int Charge { get; set; }
        public virtual ICollection<DbPeptideComparisonData> Datas { get; set; }
        public virtual ICollection<DbPeptideComparisonMz> Mzs { get; set; }
    }
}
