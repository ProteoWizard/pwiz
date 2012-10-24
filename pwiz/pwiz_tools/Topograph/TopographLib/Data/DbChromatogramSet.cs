using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.Data
{
    public class DbChromatogramSet : DbEntity<DbChromatogramSet>
    {
        public DbChromatogramSet()
        {
            Chromatograms = new List<DbChromatogram>(0);
        }
        public virtual DbPeptideFileAnalysis PeptideFileAnalysis { get; set; }
        public virtual ICollection<DbChromatogram> Chromatograms { get; set; }
        public virtual int ChromatogramCount { get; set; }
        public virtual byte[] TimesBytes { get; set; }
        public virtual byte[] ScanIndexesBytes { get; set; }
        public virtual double[] Times
        {
            get
            {
                return ArrayConverter.FromBytes<double>(TimesBytes);
            }
            set
            {
                TimesBytes = ArrayConverter.ToBytes(value);
            }
        }
        public virtual int[] ScanIndexes
        {
            get
            {
                return ArrayConverter.FromBytes<int>(ScanIndexesBytes);
            }
            set
            {
                ScanIndexesBytes = ArrayConverter.ToBytes(value);
            }
        }
        public virtual Dictionary<MzKey, DbChromatogram> GetChromatogramDict()
        {
            var result = new Dictionary<MzKey, DbChromatogram>();
            foreach (var chromatogram in Chromatograms)
            {
                result.Add(chromatogram.MzKey, chromatogram);
            }
            return result;
        }

    }
}
