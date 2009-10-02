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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using NHibernate;
using NHibernate.Criterion;
using pwiz.ProteowizardWrapper;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class MsDataFile : AnnotatedEntityModel<DbMsDataFile>
    {
        private String _label;
        private String cohort;
        private double? timePoint;
        public MsDataFile(Workspace workspace, DbMsDataFile msDataFile) : base(workspace, msDataFile)
        {
            MsDataFileData = new MsDataFileData(this, msDataFile);
        }

        public MsDataFileData MsDataFileData { get; private set; }

        public void Init(String path, MsDataFileImpl msDataFileImpl)
        {
            MsDataFileData.Init(path, msDataFileImpl);
        }
        protected override void Load(DbMsDataFile entity)
        {
            base.Load(entity);
            Name = entity.Name;
            Label = entity.Label;
            Cohort = entity.Cohort;
            TimePoint = entity.TimePoint;
        }

        protected override DbMsDataFile UpdateDbEntity(ISession session)
        {
            var msDataFile = base.UpdateDbEntity(session);
            msDataFile.Label = Label;
            msDataFile.Cohort = cohort;
            msDataFile.TimePoint = timePoint;
            return msDataFile;
        }

        public String Name { get; private set; }
        public String Path
        {
            get { return MsDataFileData.Path; }
            set { MsDataFileData.Path = value;}
        }
        public String Label {
            get { return _label;} 
            set {SetIfChanged(ref _label, value);}}
        public String Cohort
        {
            get { return cohort;} 
            set
            {
                SetIfChanged(ref cohort, value);
            }
        }
        public double? TimePoint
        {
            get
            {
                return timePoint;
            }
            set
            {
                SetIfChanged(ref timePoint, value);
            }
        }
        
        public int GetMsLevel(int scanIndex, MsDataFileImpl msDataFileImpl)
        {
            return MsDataFileData.GetMsLevel(scanIndex, msDataFileImpl);
        }
        public int GetSpectrumCount()
        {
            return MsDataFileData.GetSpectrumCount();
        }
        public double GetTime(int scanIndex)
        {
            return MsDataFileData.GetTime(scanIndex);
        }
        public int FindScanIndex(double time)
        {
            return MsDataFileData.FindScanIndex(time);
        }
        public bool HasTimes()
        {
            return MsDataFileData.HasTimes();
        }
        public override string ToString()
        {
            return Name;
        }
        public bool HasSearchResults(Peptide peptide)
        {
            using (var session = Workspace.OpenSession())
            {
                var criteria = session.CreateCriteria(typeof (DbPeptideSearchResult))
                    .Add(Restrictions.Eq("MsDataFile", session.Load<DbMsDataFile>(Id)))
                    .Add(Restrictions.Eq("Peptide", session.Load<DbPeptide>(peptide.Id)));
                return criteria.List().Count > 0;
            }
        }
        public List<PeptideFileAnalysis> GetFileAnalyses()
        {
            var result = new List<PeptideFileAnalysis>();
            using (var session = Workspace.OpenSession())
            {
                var criteria = session.CreateCriteria(typeof (DbPeptideFileAnalysis))
                    .Add(Restrictions.Eq("MsDataFile", session.Load<DbMsDataFile>(Id)));
                foreach (DbPeptideFileAnalysis peptideFileAnalysis in criteria.List())
                {
                    result.Add(PeptideFileAnalysis.GetPeptideFileAnalysis(
                        Workspace.PeptideAnalyses.GetPeptideAnalysis(peptideFileAnalysis.PeptideAnalysis),
                        peptideFileAnalysis));
                }
            }
            return result;
        }
    }
}
