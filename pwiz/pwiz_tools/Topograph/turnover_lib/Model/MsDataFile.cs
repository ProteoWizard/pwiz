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
using System.IO;
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
        private String _cohort;
        private double? _timePoint;
        public MsDataFile(Workspace workspace, DbMsDataFile msDataFile) : base(workspace, msDataFile)
        {
        }

        public MsDataFileData MsDataFileData { get; private set; }

        public void Init(String path, MsDataFileImpl msDataFileImpl)
        {
            MsDataFileData.Init(path, msDataFileImpl);
        }
        protected override IEnumerable<ModelProperty> GetModelProperties()
        {
            foreach (var property in base.GetModelProperties())
            {
                yield return property;
            }
            yield return Property<MsDataFile,String>(m=>m._label, (m,v)=>m._label =v, e=>e.Label,(e,v)=>e.Label=v);
            yield return Property<MsDataFile, String>(
                m => m._cohort, (m, v) => m._cohort = v, 
                e => e.Cohort, (e, v) => e.Cohort = v);
            yield return Property<MsDataFile, double?>(
                m => m._timePoint, (m, v) => m._timePoint = v,
                e => e.TimePoint, (e, v) => e.TimePoint = v);
        }
        protected override void Load(DbMsDataFile entity)
        {
            Name = entity.Name;
            MsDataFileData = new MsDataFileData(this, entity);
            base.Load(entity);
        }

        protected override DbMsDataFile UpdateDbEntity(ISession session)
        {
            var msDataFile = base.UpdateDbEntity(session);
            msDataFile.Label = Label;
            msDataFile.Cohort = _cohort;
            msDataFile.TimePoint = _timePoint;
            session.Save(new DbChangeLog(this));
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
            get { return _cohort;} 
            set
            {
                SetIfChanged(ref _cohort, value);
            }
        }
        public double? TimePoint
        {
            get
            {
                return _timePoint;
            }
            set
            {
                SetIfChanged(ref _timePoint, value);
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
