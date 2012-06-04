//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Xml;
using System.Text;
using System.Data.SQLite;
using System.Linq;

using NHibernate;
using NHibernate.Linq;

using pwiz.CLI.msdata;
using pwiz.CLI.proteome;
using proteome = pwiz.CLI.proteome;

namespace IDPicker.DataModel
{
    public class Exporter : IDisposable
    {
        ISession session;

        public DataFilter DataFilter { get; set; }

        public Exporter (string idpDbFilepath)
        {
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(idpDbFilepath))
            {
                this.session = sessionFactory.OpenSession();
            }
        }

        public Exporter (ISession session)
        {
            if (session == null)
                throw new ArgumentNullException("session");

            this.session = session.SessionFactory.OpenSession();
        }

        public void WriteProteins (string outputFilepath, bool addDecoys)
        {
            var pd = new ProteomeData();
            var pl = new ProteinListSimple();

            var queryRows = session.CreateQuery("SELECT DISTINCT pro.Accession, pro.IsDecoy, pro.Description, pro.Sequence " +
                                                DataFilter.GetFilteredQueryString(DataFilter.FromProtein))
                                   .List<object[]>();

            foreach (var queryRow in queryRows)
                if ((bool) queryRow[1] == false) // skip decoys from the query
                    pl.proteins.Add(new proteome.Protein((string) queryRow[0],
                                                         pl.proteins.Count,
                                                         (string) queryRow[2],
                                                         (string) queryRow[3]));

            if (addDecoys)
                foreach (var queryRow in queryRows)
                    if ((bool) queryRow[1] == false) // skip decoys from the query
                        pl.proteins.Add(new proteome.Protein("rev_" + (string) queryRow[0],
                                                             pl.proteins.Count,
                                                             String.Empty, // decoys have no description
                                                             new string(((string) queryRow[3]).Reverse().ToArray())));

            pd.proteinList = pl;
            ProteomeDataFile.write(pd, outputFilepath);
        }

        public IList<string> WriteSpectra()
        {
            return WriteSpectra(new MSDataFile.WriteConfig());
        }

        public IList<string> WriteSpectra (MSDataFile.WriteConfig config)
        {
            var outputPaths = new List<string>();

            foreach (SpectrumSource ss in session.Query<SpectrumSource>())
            {
                if (ss.Metadata == null)
                    continue;

                string outputSuffix;
                switch (config.format)
                {
                    case MSDataFile.Format.Format_mzML: outputSuffix = ".mzML"; break;
                    case MSDataFile.Format.Format_mzXML: outputSuffix = ".mzXML"; break;
                    case MSDataFile.Format.Format_MGF: outputSuffix = ".mgf"; break;
                    case MSDataFile.Format.Format_MS2: outputSuffix = ".ms2"; break;
                    default:
                        config.format = MSDataFile.Format.Format_mzML;
                        outputSuffix = ".mzML";
                        break;
                }

                MSDataFile.write(ss.Metadata, ss.Name + outputSuffix, config);

                outputPaths.Add(ss.Name + outputSuffix);
            }
            return outputPaths;
        }

        public void Dispose ()
        {
            session.Dispose();
        }
    }

    public static class ExportExtensions
    {
        public static void WriteAttribute<T> (this XmlWriter writer, string name, T value)
        {
            writer.WriteAttributeString(name, value.ToString());
        }
    }
}
