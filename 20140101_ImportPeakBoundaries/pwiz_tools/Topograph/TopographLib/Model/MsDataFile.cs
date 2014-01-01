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
using System.Threading;
using NHibernate;
using NHibernate.Criterion;
using pwiz.Common.Collections;
using pwiz.ProteowizardWrapper;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model.Data;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Model
{
    public class MsDataFile : EntityModel<long, MsDataFileData>
    {
        private byte[] _msLevels;

        public MsDataFile(Workspace workspace, DbMsDataFile dbMsDataFile) : this(workspace, dbMsDataFile.GetId(), new MsDataFileData(dbMsDataFile))
        {
        }

        public MsDataFile(Workspace workspace, long id, MsDataFileData data) : base(workspace, id, data)
        {
            Id = id;
        }
        public MsDataFile(Workspace workspace, long id) : base(workspace, id)
        {
        }


        public long Id { get; private set; }

        public void Init(CancellationToken cancellationToken, MsDataFileImpl msDataFileImpl)
        {
            if (Data.Times == null)
            {
                double[] times = null;
                double[] totalIonCurrent = null;
                byte[] msLevels = null;
                if (msDataFileImpl.ChromatogramCount > 0)
                {
                    times = msDataFileImpl.GetScanTimes();
                    totalIonCurrent = msDataFileImpl.GetTotalIonCurrent();
                }
                if (times == null || times.Length != msDataFileImpl.SpectrumCount)
                {
                    msDataFileImpl.GetScanTimesAndMsLevels(cancellationToken, out times, out msLevels);
                }
                Workspace.RunOnEventQueue(() =>
                    {
                        var data = Data;
                        if (times != null)
                        {
                            data = data.SetTimes(times);
                        }
                        if (totalIonCurrent != null)
                        {
                            data = data.SetTotalIonCurrent(totalIonCurrent);
                        }
                        if (msLevels != null)
                        {
                            data = data.SetMsLevels(msLevels);
                        }
                        Data = data;
                    });
            }
        }
        public String Name { get { return Data.Name; } }
        public String Label {
            get { return Data.Label;} 
            set {Data = Data.SetLabel(value);}}
        public String Cohort
        {
            get { return Data.Cohort;} 
            set
            {
                Data = Data.SetCohort(value);
            }
        }
        public double? TimePoint
        {
            get
            {
                return Data.TimePoint;
            }
            set
            {
                Data = Data.SetTimePoint(value);
            }
        }
        public string Sample
        {
            get { return Data.Sample; }
            set { Data = Data.SetSample(value); }
        }
        public PrecursorPoolValue? PrecursorPool
        {
            get { return Data.PrecursorPool; } 
            set { Data = Data.SetPrecursorPool(value); }
        }
        
        public int GetMsLevel(int scanIndex, MsDataFileImpl msDataFileImpl)
        {
            if (Data.MsLevels != null && Data.MsLevels[scanIndex] != 0)
            {
                return Data.MsLevels[scanIndex];
            }
            if (_msLevels == null)
            {
                if (Data.MsLevels == null)
                {
                    _msLevels = new byte[GetSpectrumCount()];
                }
                else
                {
                    _msLevels = Data.MsLevels.ToArray();
                }
            }
            int msLevel = msDataFileImpl.GetMsLevel(scanIndex);
            _msLevels[scanIndex] = (byte) msLevel;
            return msLevel;
        }
        public int GetSpectrumCount()
        {
            return Data.Times == null ? 0 : Data.Times.Count;
        }
        public double GetTime(int scanIndex)
        {
            scanIndex = Math.Min(scanIndex, GetSpectrumCount() - 1);
            if (scanIndex < 0)
            {
                return 0;
            }
            return Data.Times[scanIndex];
        }
        public int FindScanIndex(double time)
        {
            int index = CollectionUtil.BinarySearch(Data.Times, time);
            if (index < 0)
            {
                index = ~index;
            }
            index = Math.Min(index, GetSpectrumCount() - 1);
            return index;
        }
        public bool HasTimes()
        {
            return null != Data.Times;
        }
        public override string ToString()
        {
            return Label ?? Name;
        }
        public bool HasSearchResults(Peptide peptide)
        {
            using (var session = Workspace.OpenSession())
            {
                var criteria = session.CreateCriteria(typeof (DbPeptideSpectrumMatch))
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
                    int index = Workspace.PeptideAnalyses.IndexOfKey(peptideFileAnalysis.PeptideAnalysis.GetId());
                    if (index >= 0)
                    {
                        result.Add(PeptideFileAnalysis.GetPeptideFileAnalysis(
                            Workspace.PeptideAnalyses[index],
                            peptideFileAnalysis));
                    }
                }
            }
            return result;
        }

        public RegressionWithOutliers RegressTimes(MsDataFile target, out IList<String> modifiedPeptideSequences)
        {
            var targetDict = target.GetRetentionTimesByModifiedSequence();
            var myTimes = new List<double>();
            var targetTimes = new List<double>();
            modifiedPeptideSequences = new List<string>();
            using (var myEnumerator = GetRetentionTimesByModifiedSequence().GetEnumerator())
            {
                using (var targetEnumerator = target.GetRetentionTimesByModifiedSequence().GetEnumerator())
                {
                    bool bContinue = myEnumerator.MoveNext() && targetEnumerator.MoveNext();
                    while (bContinue)
                    {
                        int compare = targetDict.KeyComparer.Compare(myEnumerator.Current.Key, targetEnumerator.Current.Key);
                        if (compare == 0)
                        {
                            myTimes.Add(myEnumerator.Current.Value);
                            targetTimes.Add(targetEnumerator.Current.Value);
                            modifiedPeptideSequences.Add(myEnumerator.Current.Key);
                            bContinue = myEnumerator.MoveNext() && targetEnumerator.MoveNext();
                        }
                        else if (compare < 0)
                        {
                            bContinue = myEnumerator.MoveNext();
                        }
                        else
                        {
                            bContinue = targetEnumerator.MoveNext();
                        }
                    }
                }
            }
            return new RegressionWithOutliers(myTimes, targetTimes);
        }

        public RetentionTimeAlignment GetRetentionTimeAlignment(MsDataFile other)
        {
            var key = new RetentionTimeAlignments.AlignmentKey(Id, other.Id);
            var alignmentValue = Workspace.RetentionTimeAlignments.GetAlignment(key);
            if (null != alignmentValue)
            {
                return alignmentValue;
            }
            RetentionTimeAlignment result;
            IList<string> regressedPeptides;
            var regressionWithOutliers = RegressTimes(other, out regressedPeptides);
            var refined = regressionWithOutliers.Refine();
            if (refined != null)
            {
                result = new RetentionTimeAlignment(refined.Slope, refined.Intercept);
            }
            else
            {
                result = RetentionTimeAlignment.Invalid;
            }
            Workspace.RetentionTimeAlignments.AddAlignment(key, result);
            return result;
        }

        private ImmutableSortedList<string, double> GetRetentionTimesByModifiedSequence()
        {
            return Data.RetentionTimesByModifiedSequence;
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

        public void Save(ISession session)
        {
            var dbMsDataFile = session.Get<DbMsDataFile>(Id);
            UpdateBinary(session, dbMsDataFile);
            dbMsDataFile.Label = Label;
            dbMsDataFile.Sample = Sample;
            dbMsDataFile.TimePoint = TimePoint;
            dbMsDataFile.Cohort = Cohort;
            dbMsDataFile.PrecursorPool = PrecursorPool.HasValue ? PrecursorPool.Value.ToPersistedString() : null;
            session.Update(dbMsDataFile);
        }

        public void SaveBinary(ISession session)
        {
            var dbMsDataFile = session.Get<DbMsDataFile>(Id);
            UpdateBinary(session, dbMsDataFile);
            session.Update(dbMsDataFile);
        }

        private void UpdateBinary(ISession session, DbMsDataFile dbMsDataFile)
        {
            if (null != Data.Times && null == dbMsDataFile.Times)
            {
                dbMsDataFile.Times = Data.Times.ToArray();
            }
            if (null != Data.TotalIonCurrent && null == dbMsDataFile.TotalIonCurrent)
            {
                dbMsDataFile.TotalIonCurrent = Data.TotalIonCurrent.ToArray();
            }
            if (null != Data.MsLevels)
            {
                var msLevels = Data.MsLevels.ToArray();
                if (null != dbMsDataFile.MsLevels && dbMsDataFile.MsLevels.Length == msLevels.Length)
                {
                    for (int i = 0; i < msLevels.Length; i++)
                    {
                        if (0 == msLevels[i])
                        {
                            msLevels[i] = dbMsDataFile.MsLevels[i];
                        }
                    }
                }
            }
        }

        public void Merge(DbMsDataFile dbMsDataFile)
        {
            Merge(new MsDataFileData(dbMsDataFile));
        }

        public void Merge(MsDataFileData msDataFileData)
        {
//            var newData = DataProperty<MsDataFileData>.MergeAll(MsDataFileData.MergeableProperties, _data, _savedData,
//                                                                msDataFileData);
//            if (!Equals(msDataFileData, _savedData))
//            {
//                _savedData = msDataFileData;
//            }
//            bool changed = !Equals(newData, _data);
//            if (changed)
//            {
//                _data = newData;
//                OnChange();
//            }
        }
        public bool IsBinaryDirty
        {
            get { return !Data.EqualsBinary(GetData(Workspace.SavedData)); }
        }
        public double? GetTotalIonCurrent(double startTime, double endTime)
        {
            if (Data.TotalIonCurrent == null || Data.MsLevels == null)
            {
                return null;
            }
            double total = 0;
            int startIndex = FindScanIndex(startTime);
            int endIndex = FindScanIndex(endTime);
            for (int i = startIndex; i <= endIndex; i++)
            {
                if (_msLevels[i] != 1)
                {
                    continue;
                }
                int? prevIndex = GetPrevMs1Index(i);
                int? nextIndex = GetNextMs1Index(i);
                double width;
                if (prevIndex.HasValue && nextIndex.HasValue)
                {
                    width = (Data.Times[nextIndex.Value] - Data.Times[prevIndex.Value]) / 2;
                }
                else if (prevIndex.HasValue)
                {
                    width = Data.Times[i] - Data.Times[prevIndex.Value];
                }
                else if (nextIndex.HasValue)
                {
                    width = Data.Times[nextIndex.Value] - Data.Times[i];
                }
                else
                {
                    width = 0;
                }
                total += Data.TotalIonCurrent[i] * width;
            }
            return total;
        }
        private int? GetPrevMs1Index(int index)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                if (_msLevels[i] == 1)
                {
                    return i;
                }
            }
            return null;
        }
        private int? GetNextMs1Index(int index)
        {
            for (int i = index + 1; i < _msLevels.Length; i++)
            {
                if (_msLevels[i] == 1)
                {
                    return i;
                }
            }
            return null;
        }

        public override MsDataFileData GetData(WorkspaceData workspaceData)
        {
            MsDataFileData msDataFileData;
            workspaceData.MsDataFiles.TryGetValue(Id, out msDataFileData);
            return msDataFileData;
        }

        public override WorkspaceData SetData(WorkspaceData workspaceData, MsDataFileData value)
        {
            return workspaceData.SetMsDataFiles(workspaceData.MsDataFiles.Replace(Id, value));
        }
    }
}
