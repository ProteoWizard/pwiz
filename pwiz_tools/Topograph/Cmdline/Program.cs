using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NHibernate;
using NHibernate.Criterion;
using pwiz.CLI.msdata;
using topog;
using turnover.Data;
using turnover.Enrichment;
using turnover.Model;

namespace Cmdline
{
    class Program
    {
        static void Main(string[] args)
        {
            String dbPath = args[0];
            String msDataFile = null;
            if (args.Length > 1)
            {
                msDataFile = args[1];
            }

            Workspace workspace = new Workspace(dbPath);
            ISession session = workspace.OpenSession();
            List<DbPeptideSearchResult> peptideSearchResults = new List<DbPeptideSearchResult>();
            ICriteria criteria = session.CreateCriteria(typeof (DbPeptideSearchResult));
            criteria.AddOrder(Order.Asc("FirstDetectedScan"));
            Dictionary<DbMsDataFile, MpeCalculator> mpeCalculators = new Dictionary<DbMsDataFile, MpeCalculator>();
            criteria.List(peptideSearchResults);
            EnrichmentDef enrichment = workspace.GetEnrichmentDef();
            String outputFile = Path.Combine(Path.GetDirectoryName(dbPath),
                                             Path.GetFileNameWithoutExtension(dbPath) +
                                             (msDataFile == null ? "" : "-" + msDataFile) + "-results.tsv");
            TextWriter writer = new StreamWriter(outputFile);
            foreach (var peptideSearchResult in peptideSearchResults)
            {
                if (msDataFile != null)
                {
                    if (!peptideSearchResult.MsDataFile.Path.ToLower().Contains(msDataFile.ToLower()))
                    {
                        continue;
                    }
                }

                MpeCalculator mpeCalculator;
                if (!mpeCalculators.TryGetValue(peptideSearchResult.MsDataFile, out mpeCalculator))
                {
                    mpeCalculator = new MpeCalculator();
                    mpeCalculators.Add(peptideSearchResult.MsDataFile, mpeCalculator);
                }
                
                PeptideCalculator peptideCalculator = new PeptideCalculator(enrichment, peptideSearchResult, writer);
                mpeCalculator.AddPeptideCalculator(peptideCalculator);
            }
            foreach (var pair in mpeCalculators)
            {
                Console.Out.WriteLine("Processing " + pair.Key.Path);
                writer.WriteLine("Processing " + pair.Key.Path);
                pair.Value.ProcessFile(new MSDataFile(pair.Key.Path));
            }
        }
    }
}
