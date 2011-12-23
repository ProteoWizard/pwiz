/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.Data
{
    public class DbWorkspace : DbEntity<DbWorkspace>
    {
        public DbWorkspace()
        {
            MsDataFiles = new List<DbMsDataFile>();
            Peptides = new List<DbPeptide>();
            PeptideAnalyses = new List<DbPeptideAnalysis>();
            Modifications = new List<DbModification>();
            Settings = new List<DbSetting>();
            TracerDefs = new List<DbTracerDef>();
            
        }
        public virtual ICollection<DbMsDataFile> MsDataFiles { get; set; }
        public virtual int MsDataFileCount { get; set; }
        public virtual ICollection<DbPeptide> Peptides{ get; set;}
        public virtual int PeptideCount { get; set; }
        public virtual ICollection<DbPeptideAnalysis> PeptideAnalyses { get; set; }
        public virtual int PeptideAnalysisCount { get; set; }
        public virtual ICollection<DbModification> Modifications { get; set; }
        public virtual int ModificationCount { get; set; }
        public virtual ICollection<DbSetting> Settings { get; set; }
        public virtual int SettingCount { get; set; }
        public virtual DbSetting GetSetting(String name)
        {
            foreach (DbSetting setting in Settings)
            {
                if (setting.Name == name)
                {
                    return setting;
                }
            }
            return null;
        }
        public virtual ICollection<DbTracerDef> TracerDefs { get; set; }
        public virtual int TracerDefCount { get; set; }

        public virtual int SchemaVersion { get; set; }

        public virtual String DataFilePath { get; set; }
    }
}
