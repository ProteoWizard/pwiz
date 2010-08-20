using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.Query
{
    public class BuiltInQuery
    {
        public BuiltInQuery(String name, String hql)
        {
            Name = name;
            Hql = hql;
        }
        public String Name { get; private set; }
        public String Hql { get; private set; }
        public override String ToString() {
            return Name;
        }
    }

    public static class BuiltInQueries
    {
        public static BuiltInQuery TracerAmounts = new BuiltInQuery(
            "Tracer Amounts",
            string.Join("\n", new [] 
            {
                "SELECT T.TracerPercent,",
	            "T.Score,",
            	"T.PrecursorEnrichment,",
            	"T.Turnover,",
	            "T.PeptideFileAnalysis.MsDataFile.Name AS File,",
	            "T.PeptideFileAnalysis.MsDataFile.TimePoint AS TimePoint,",
	            "T.PeptideFileAnalysis.MsDataFile.Cohort AS Cohort,",
	            "T.PeptideFileAnalysis.PeptideAnalysis.Peptide.FullSequence AS Peptide,",
	            "T.PeptideFileAnalysis.PeptideAnalysis.Peptide.Protein AS Protein,",
	            "T.PeptideFileAnalysis.PeptideAnalysis.Peptide.ProteinDescription AS ProteinDescription,",
	            "T.PeptideFileAnalysis AS Analysis",
                "FROM DbPeptideDistribution T",
                "WHERE T.PeptideQuantity = 0"
            }));

        public static BuiltInQuery PeptideAreas = new BuiltInQuery(
            "Peptide Areas",
            string.Join("\n", new[]
            {
                "Select SUM(P.TotalArea) * MIN(D.Score) AS Area,",
	            "MIN(D.Score) AS Score,",
	            "P.PeptideFileAnalysis.MsDataFile.Name AS File,",
	            "P.PeptideFileAnalysis.MsDataFile.TimePoint AS TimePoint,",
	            "P.PeptideFileAnalysis.MsDataFile.Cohort AS Cohort,",
	            "P.PeptideFileAnalysis.PeptideAnalysis.Peptide.FullSequence AS Peptide,",
	            "P.PeptideFileAnalysis.PeptideAnalysis.Peptide.Protein AS Protein,",
	            "P.PeptideFileAnalysis.PeptideAnalysis.Peptide.ProteinDescription AS ProteinDescription,",
	            "P.PeptideFileAnalysis AS Analysis",
                "FROM DbPeak P, DbPeptideDistribution D",
                "WHERE P.PeptideFileAnalysis = D.PeptideFileAnalysis",
                "AND D.PeptideQuantity= 0",
                "GROUP BY P.PeptideFileAnalysis",
            }));
        public static IList<BuiltInQuery> List
        {
            get
            {
                return new[] {TracerAmounts,PeptideAreas};
            }
        }
    }
}
