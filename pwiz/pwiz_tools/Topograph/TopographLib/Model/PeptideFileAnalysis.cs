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
using System.ComponentModel;
using System.Linq;
using NHibernate;
using NHibernate.Criterion;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model.Data;
using pwiz.Topograph.MsData;

namespace pwiz.Topograph.Model
{
    public class PeptideFileAnalysis : EntityModel<long, PeptideFileAnalysisData>
    {
        private TracerChromatograms _tracerChromatograms;
        private TracerChromatograms _tracerChromatogramsSmoothed;

        public static PeptideFileAnalysis GetPeptideFileAnalysis(
            PeptideAnalysis peptideAnalysis, DbPeptideFileAnalysis dbPeptideFileAnalysis)
        {
            PeptideFileAnalysis peptideFileAnalysis;
            peptideAnalysis.FileAnalyses.TryGetValue(dbPeptideFileAnalysis.GetId(), out peptideFileAnalysis);
            return peptideFileAnalysis;
        }

        public PeptideFileAnalysis(PeptideAnalysis peptideAnalysis, long id, PeptideFileAnalysisData peptideFileAnalysisData)
            : base(peptideAnalysis.Workspace, id, peptideFileAnalysisData)
        {
            PeptideAnalysis = peptideAnalysis;
        }

        public PeptideAnalysis PeptideAnalysis { get; private set; }
        [Browsable(false)]
        public long Id { get { return Key; } }
        public void Save(ISession session)
        {
            var dbPeptideFileAnalysis = session.Get<DbPeptideFileAnalysis>(Id);
            dbPeptideFileAnalysis.AutoFindPeak = AutoFindPeak;
            dbPeptideFileAnalysis.Note = Data.Note;
            dbPeptideFileAnalysis.ValidationStatus = Data.ValidationStatus;
            session.Update(dbPeptideFileAnalysis);
            if (null != CalculatedPeaks)
            {
                CalculatedPeaks.Save(session);
            }
        }

        [Browsable(false)]
        public PeptideFileAnalysisData.PeakSet PeakData { get { return Data.Peaks; } }

        [Browsable(false)]
        public CalculatedPeaks CalculatedPeaks
        {
            get { return PeptideAnalysis.GetCalculatePeaks(this); }
        }

        [Browsable(false)]
        public ChromatogramSetData ChromatogramSet
        {
            get { return Data.ChromatogramSet; }
            set { Data = Data.SetChromatogramSet(value); }
        }

        [Browsable(false)]
        public long? ChromatogramSetId { get { return Data.ChromatogramSetId; } }

        [Browsable(false)]
        public Peptide Peptide { get { return PeptideAnalysis.Peptide; } }
        public MsDataFile MsDataFile
        {
            get { return Workspace.MsDataFiles.FindByKey(Data.MsDataFileId); }
        }
        [Browsable(false)]
        public String Protein { get { return Peptide.ProteinName; } }
        [Browsable(false)]
        public String Sequence { get { return Peptide.Sequence; } }
        public double ChromatogramStartTime
        {
            get { return Data.ChromatogramStartTime; }
        }
        public double ChromatogramEndTime
        {
            get { return Data.ChromatogramEndTime; }
        }
        [Browsable(false)]
        public PsmTimes PsmTimes
        {
            get
            {
                return Data.PsmTimes;
            }
        }
        [Browsable(false)]
        public int PsmCount
        {
            get
            {
                return null == PsmTimes ? 0 : PsmTimes.TotalCount;
            }
        }
        [Browsable(false)]
        public int TracerCount { get { return Peptide.MaxTracerCount; } }
        [Browsable(false)]
        public double? PeakStartTime { get { return CalculatedPeaks == null ? null : CalculatedPeaks.StartTime; } }
        [Browsable(false)]
        public double? PeakEndTime { get { return CalculatedPeaks == null ? null : CalculatedPeaks.EndTime; } }

        public double? PeakStart 
        {
            get { return PeakData.Peaks.Count == 0 ? (double?) null : PeakData.Peaks.Min(peak => peak.StartTime); }
        }
        public double? PeakEnd
        {
            get { return PeakData.Peaks.Count == 0 ? (double?) null : PeakData.Peaks.Max(peak => peak.EndTime); }
        }

        public static DbPeptideFileAnalysis CreatePeptideFileAnalysis(ISession session, MsDataFile msDataFile, DbPeptideAnalysis dbPeptideAnalysis, ILookup<long, double> psmTimesByDataFileId)
        {
            var dbMsDataFile = session.Load<DbMsDataFile>(msDataFile.Id);
            var startEndTime = DecideChromatogramStartEndTime(msDataFile, psmTimesByDataFileId);
            return new DbPeptideFileAnalysis
                       {
                           ChromatogramStartTime = startEndTime.Key,
                           ChromatogramEndTime = startEndTime.Value,
                           MsDataFile = dbMsDataFile,
                           PeptideAnalysis = dbPeptideAnalysis,
                           AutoFindPeak = true,
                           PsmCount = psmTimesByDataFileId[msDataFile.Id].Count(),
                       };
        }

        private static KeyValuePair<double, double> DecideChromatogramStartEndTime(MsDataFile msDataFile, ILookup<long, double> psmTimesByDataFileId)
        {
            var workspace = msDataFile.Workspace;
            var chromTimeAroundMs2Id = workspace.GetChromTimeAroundMs2Id();
            var timesInThisFile = psmTimesByDataFileId[msDataFile.Id].ToArray();
            if (timesInThisFile.Length > 0)
            {
                return new KeyValuePair<double, double>(timesInThisFile.Min() - chromTimeAroundMs2Id,
                                                        timesInThisFile.Max() + chromTimeAroundMs2Id);
            }
            var alignedTimes = new List<double>();
            foreach (var grouping in psmTimesByDataFileId)
            {
                MsDataFile otherFile;
                if (!workspace.MsDataFiles.TryGetValue(grouping.Key, out otherFile))
                {
                    continue;
                }
                var alignment = otherFile.GetRetentionTimeAlignment(msDataFile);
                if (alignment.IsInvalid)
                {
                    continue;
                }
                alignedTimes.Add(alignment.GetTargetTime(grouping.Min()));
                alignedTimes.Add(alignment.GetTargetTime(grouping.Max()));
            }
            if (alignedTimes.Count > 0)
            {
                return new KeyValuePair<double, double>(alignedTimes.Min() - chromTimeAroundMs2Id,
                                                        alignedTimes.Max() + chromTimeAroundMs2Id);
            }
            var extraTime = workspace.GetExtraChromTimeWithoutMs2Id();
            var minTime = psmTimesByDataFileId.SelectMany(grouping => grouping).Min();
            var maxTime = psmTimesByDataFileId.SelectMany(grouping => grouping).Max();
            return new KeyValuePair<double, double>(minTime - chromTimeAroundMs2Id - extraTime, maxTime + chromTimeAroundMs2Id + extraTime);
        }

        public static PeptideFileAnalysis EnsurePeptideFileAnalysis(PeptideAnalysis peptideAnalysis, MsDataFile msDataFile)
        {
            var workspace = peptideAnalysis.Workspace;
            using (var session = workspace.OpenSession())
            {
                var criteria = session.CreateCriteria(typeof (DbPeptideFileAnalysis))
                    .Add(Restrictions.Eq("PeptideAnalysis", session.Load<DbPeptideAnalysis>(peptideAnalysis.Id)))
                    .Add(Restrictions.Eq("MsDataFile", session.Load<DbMsDataFile>(msDataFile.Id)));
                var dbPeptideAnalysis = (DbPeptideFileAnalysis) criteria.UniqueResult();
                if (dbPeptideAnalysis != null)
                {
                    return GetPeptideFileAnalysis(peptideAnalysis, dbPeptideAnalysis);
                }
            }
            if (!msDataFile.HasTimes())
            {
                return null;
            }
            using (var session = workspace.OpenWriteSession())
            {
                var dbPeptideAnalysis = session.Load<DbPeptideAnalysis>(peptideAnalysis.Id);
                var psmTimesByDataFileId = dbPeptideAnalysis.Peptide.PsmTimesByDataFileId(session);
                var dbPeptideFileAnalysis = CreatePeptideFileAnalysis(session, msDataFile, dbPeptideAnalysis, psmTimesByDataFileId);
                if (dbPeptideFileAnalysis == null)
                {
                    return null;
                }

                session.BeginTransaction();
                session.Save(dbPeptideFileAnalysis);
                dbPeptideAnalysis.FileAnalysisCount++;
                session.Update(dbPeptideAnalysis);
                session.Save(new DbChangeLog(peptideAnalysis));
                session.Transaction.Commit();
                workspace.ChromatogramGenerator.SetRequeryPending();
                workspace.DatabasePoller.LoadAndMergeChanges(null);
                return workspace.PeptideAnalyses.FindByKey(dbPeptideAnalysis.GetId())
                    .FileAnalyses.FindByKey(dbPeptideFileAnalysis.GetId());
            }
        }

        public bool AutoFindPeak
        {
            get
            {
                return Data.AutoFindPeak;
            }
        }

        public ValidationStatus ValidationStatus
        {
            get { return Data.ValidationStatus; }
            set 
            { 
                if (ValidationStatus == value)
                {
                    return;
                }
                Data = Data.SetValidationStatus(value);
            }
        }

        [Browsable(false)]
        public ExcludedMasses ExcludedMasses
        {
            get
            {
                return PeptideAnalysis.ExcludedMasses;
            }
        }
        public String GetLabel()
        {
            return PeptideAnalysis.GetLabel() + " " + MsDataFile.Label;
        }

        [Browsable(false)]
        public TurnoverCalculator TurnoverCalculator
        {
            get
            {
                return PeptideAnalysis.GetTurnoverCalculator();
            }
        }
        public ChromatogramSetData GetChromatograms()
        {
            return ChromatogramSet;
        }
        public bool IsMzKeySetComplete(ICollection<MzKey> mzKeys)
        {
            for (int charge = PeptideAnalysis.MinCharge; charge <= PeptideAnalysis.MaxCharge; charge ++)
            {
                for (int massIndex = 0; massIndex < PeptideAnalysis.GetMassCount(); massIndex++)
                {
                    if (!mzKeys.Contains(new MzKey(charge, massIndex)))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        [Browsable(false)]
        public int MinCharge 
        {
            get { return PeptideAnalysis.MinCharge; }
        }
        [Browsable(false)]
        public int MaxCharge
        {
            get { return PeptideAnalysis.MaxCharge; }
        }

        public bool SetChromatograms(AnalysisChromatograms analysisChromatograms)
        {
            if (analysisChromatograms.MinCharge != MinCharge || analysisChromatograms.MaxCharge != MaxCharge)
            {
                return false;
            }
            Data = Data.SetChromatogramSet(new ChromatogramSetData(analysisChromatograms));
            return true;
        }
        public void SetCalculatedPeaks(CalculatedPeaks calculatedPeaks)
        {
            if (calculatedPeaks.AutoFindPeak)
            {
                throw new InvalidOperationException();
            }
            Data = Data.SetPeaks(false, calculatedPeaks.ToPeakSetData());
            PeptideAnalysis.InvalidatePeaks();
        }

        public void SetAutoFindPeak()
        {
            Data = Data.SetAutoFindPeak(true);
        }

        public TracerChromatograms GetTracerChromatograms(bool smoothed)
        {
            var result = smoothed ? _tracerChromatogramsSmoothed : _tracerChromatograms;
            if (result == null)
            {
                result = new TracerChromatograms(this, ChromatogramSet, smoothed);
                if (smoothed)
                {
                    _tracerChromatogramsSmoothed = result;
                }
                else
                {
                    _tracerChromatograms = result;
                }
            }
            return result;
        }

        public void InvalidatePeaks()
        {
            _tracerChromatograms = null;
            _tracerChromatogramsSmoothed = null;
        }

        public override PeptideFileAnalysisData GetData(WorkspaceData workspaceData)
        {
            var peptideAnalysisData = PeptideAnalysis.GetData(workspaceData);
            PeptideFileAnalysisData peptideFileAnalysisData = null;
            if (null != peptideAnalysisData)
            {
                peptideAnalysisData.FileAnalyses.TryGetValue(Id, out peptideFileAnalysisData);
            }
            return peptideFileAnalysisData;
        }

        public override WorkspaceData SetData(WorkspaceData workspaceData, PeptideFileAnalysisData value)
        {
            var peptideAnalysisData = PeptideAnalysis.GetData(workspaceData);
            peptideAnalysisData =
                peptideAnalysisData.SetFileAnalyses(peptideAnalysisData.FileAnalyses.Replace(Id, value));
            return PeptideAnalysis.SetData(workspaceData, peptideAnalysisData);
        }
    }
}