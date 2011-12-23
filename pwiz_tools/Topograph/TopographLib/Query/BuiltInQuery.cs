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
                "SELECT F.TracerPercent,",
	            "F.DeconvolutionScore,",
            	"F.PrecursorEnrichment,",
            	"F.Turnover,",
                "F.TurnoverScore",                
	            "F.MsDataFile.Name AS File,",
	            "F.MsDataFile.TimePoint AS TimePoint,",
	            "F.MsDataFile.Cohort AS Cohort,",
	            "F.PeptideAnalysis.Peptide.FullSequence AS Peptide,",
	            "F.PeptideAnalysis.Peptide.Protein AS Protein,",
	            "F.PeptideAnalysis.Peptide.ProteinDescription AS ProteinDescription,",
	            "F AS Analysis",
                "FROM DbPeptideFileAnalysis F",
            }));

        public static BuiltInQuery PeptideAreas = new BuiltInQuery(
            "Peptide Areas",
            string.Join("\n", new[]
            {
                "Select SUM(P.TotalArea) AS Area,",
	            "P.PeptideFileAnalysis.DeconvolutionScore as Score,",
	            "P.PeptideFileAnalysis.MsDataFile.Name AS File,",
	            "P.PeptideFileAnalysis.MsDataFile.TimePoint AS TimePoint,",
	            "P.PeptideFileAnalysis.MsDataFile.Cohort AS Cohort,",
	            "P.PeptideFileAnalysis.PeptideAnalysis.Peptide.FullSequence AS Peptide,",
	            "P.PeptideFileAnalysis.PeptideAnalysis.Peptide.Protein AS Protein,",
	            "P.PeptideFileAnalysis.PeptideAnalysis.Peptide.ProteinDescription AS ProteinDescription,",
	            "P.PeptideFileAnalysis AS Analysis",
                "FROM DbPeak P",
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
