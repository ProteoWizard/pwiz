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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using NHibernate;
using NHibernate.Criterion;
using pwiz.Common.DataAnalysis;
using pwiz.ProteowizardWrapper;
using pwiz.Topograph.Data;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Model
{
    public class MsDataFile : AnnotatedEntityModel<DbMsDataFile>
    {
        public static bool UseLoessRegression = false;
        private String _label;
        private String _cohort;
        private String _sample;
        private double? _timePoint;
        private IDictionary<long, RetentionTimeAlignment> _alignments 
            = new Dictionary<long, RetentionTimeAlignment>();
        private IList<SearchResultInfo> _searchResults;

        public MsDataFile(Workspace workspace, DbMsDataFile msDataFile) : base(workspace, msDataFile)
        {
        }

        [Browsable(false)]
        public MsDataFileData MsDataFileData { get; private set; }

        public void Init(MsDataFileImpl msDataFileImpl)
        {
            MsDataFileData.Init(msDataFileImpl);
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
            yield return Property<MsDataFile, string>(
                m => m._sample, (m, v) => m._sample = v,
                e => e.Sample, (e, v) => e.Sample = v);
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
        public string Sample
        {
            get
            {
                return _sample;
            }
            set
            {
                SetIfChanged(ref _sample, value);
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
            return Label ?? Name;
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

        public RegressionWithOutliers RegressTimes(MsDataFile target, out IList<Peptide> peptides)
        {
            var mySearchResults = ListSearchResults();
            var targetDict = target.ListSearchResults().ToDictionary(s => s.PeptideId, s => s);
            var myTimes = new List<double>();
            var targetTimes = new List<double>();
            peptides = new List<Peptide>();
            foreach (var mySearchResult in mySearchResults)
            {
                SearchResultInfo targetSearchResult;
                if (!targetDict.TryGetValue(mySearchResult.PeptideId, out targetSearchResult))
                {
                    continue;
                }
                if (mySearchResult.FirstTracerCount == targetSearchResult.FirstTracerCount)
                {
                    myTimes.Add(GetTime(mySearchResult.FirstDetectedScan));
                    targetTimes.Add(target.GetTime(targetSearchResult.FirstDetectedScan));
                    peptides.Add(Workspace.Peptides.GetChild(mySearchResult.PeptideId));
                }
                else
                {
                    if (mySearchResult.LastTracerCount == targetSearchResult.LastTracerCount)
                    {
                        myTimes.Add(GetTime(mySearchResult.LastDetectedScan));
                        targetTimes.Add(GetTime(targetSearchResult.LastDetectedScan));
                        peptides.Add(Workspace.Peptides.GetChild(mySearchResult.PeptideId));
                    }
                }
            }
            return new RegressionWithOutliers(myTimes, targetTimes);
        }

        public RetentionTimeAlignment GetRetentionTimeAlignment(MsDataFile other)
        {
            var alignments = _alignments;
            lock (alignments)
            {
                try
                {
                    RetentionTimeAlignment result;
                    if (alignments.TryGetValue(other.Id.Value, out result))
                    {
                        return result;
                    }
                    if (!UseLoessRegression)
                    {
                        IList<Peptide> regressedPeptides;
                        var regressionWithOutliers = RegressTimes(other, out regressedPeptides);
                        var refined = regressionWithOutliers.Refine();
                        if (refined != null)
                        {
                            double minX = refined.OriginalTimes.Min();
                            double maxX = refined.OriginalTimes.Max();
                            result = RetentionTimeAlignment.GetRetentionTimeAlignment(
                                new[] { minX, maxX },
                                new[]
                                {
                                    minX*refined.Slope + refined.Intercept,
                                    maxX*refined.Slope + refined.Intercept
                                });
                        }
                    }
                    else
                    {
                        var lstMySearchResults = ListSearchResults();
                        var lstOtherSearchResults = other.ListSearchResults();
                        var otherSearchResults = lstOtherSearchResults.ToDictionary(s => s.PeptideId, s => s);
                        var dict = new Dictionary<double, double>();
                        foreach (var searchResult1 in lstMySearchResults)
                        {
                            SearchResultInfo searchResult2;
                            if (!otherSearchResults.TryGetValue(searchResult1.PeptideId, out searchResult2))
                            {
                                continue;
                            }
                            if (searchResult1.FirstTracerCount == searchResult2.FirstTracerCount)
                            {
                                dict[GetTime(searchResult1.FirstDetectedScan)] =
                                    other.GetTime(searchResult2.FirstDetectedScan);
                            }
                            if (searchResult1.LastTracerCount == searchResult2.LastTracerCount)
                            {
                                dict[GetTime(searchResult1.LastDetectedScan)] = other.GetTime(searchResult2.LastDetectedScan);
                            }
                        }
                        var xValues = dict.Keys.ToArray();
                        Array.Sort(xValues);
                        var yValues = xValues.Select(x => dict[x]).ToArray();
                        var loessInterpolator = new LoessInterpolator(.1, 0);
                        var weights = Enumerable.Repeat(1.0, xValues.Count()).ToArray();
                        var smoothedPoints = loessInterpolator.Smooth(xValues, yValues, weights);
                        result = RetentionTimeAlignment.GetRetentionTimeAlignment(xValues, smoothedPoints);
                    }
                    alignments.Add(other.Id.Value, result);
                    return result;
                }
                catch (Exception exception)
                {
                    if (Workspace.SessionFactory == null)
                    {
                        // If the workspace has been closed, just let the exception propagate up to the highest level
                        throw;
                    }
                    ErrorHandler.LogException("Retention time alignment", "Error aligning " + Name + " with " + other.Name, exception);
                    alignments.Add(other.Id.Value, null);
                    return null;
                }
            }
        }
        private IList<SearchResultInfo> ListSearchResults()
        {
            if (_searchResults != null)
            {
                return _searchResults;
            }
            var searchResults = new List<DbPeptideSearchResult>();
            using (var session = Workspace.OpenSession())
            {
                session.CreateCriteria(typeof (DbPeptideSearchResult))
                    .Add(Restrictions.Eq("MsDataFile", session.Load<DbMsDataFile>(Id)))
                    .List(searchResults);
            }
            return _searchResults = searchResults.Select(s=>new SearchResultInfo(s)).ToArray();
        }

        public override int CompareTo(object obj)
        {
            var that = obj as MsDataFile;
            if (null == that)
            {
                return base.CompareTo(obj);
            }
            return NameComparers.CompareReplicateNames(ToString(), that.ToString());
        }

        private class SearchResultInfo
        {
            public SearchResultInfo(DbPeptideSearchResult dbPeptideSearchResult)
            {
                PeptideId = dbPeptideSearchResult.Peptide.Id.Value;
                MsDataFileId = dbPeptideSearchResult.MsDataFile.Id.Value;
                FirstDetectedScan = dbPeptideSearchResult.FirstDetectedScan;
                FirstTracerCount = dbPeptideSearchResult.LastTracerCount;
                LastDetectedScan = dbPeptideSearchResult.LastDetectedScan;
                LastTracerCount = dbPeptideSearchResult.LastTracerCount;
            }
            public long PeptideId { get; private set; }
            public long MsDataFileId { get; private set; }
            public int FirstDetectedScan { get; private set; }
            public int FirstTracerCount { get; private set; }
            public int LastDetectedScan { get; private set; }
            public int LastTracerCount { get; private set; }
        }
    }
}
