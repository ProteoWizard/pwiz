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
using pwiz.Common.DataBinding.Attributes;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model.Data;

namespace pwiz.Topograph.Model
{
    public class PeptideAnalysis : EntityModel<long, PeptideAnalysisData>
    {
        private IDictionary<long, CalculatedPeaks> _calculatedPeaks;
        public PeptideAnalysis(Workspace workspace, long id, PeptideAnalysisData savedData) : base(workspace, id, savedData)
        {
            FileAnalyses = new PeptideFileAnalyses(this);
        }

        public PeptideAnalysis(Workspace workspace, long id) : base(workspace, id)
        {
            FileAnalyses = new PeptideFileAnalyses(this);
        }

        public PeptideAnalysis(Workspace workspace, PeptideAnalysis peptideAnalysis) : base(workspace, peptideAnalysis.Id)
        {
            _chromatogramRefCount = 0;
        }

        [Browsable(false)]
        public long Id { get { return Key; } }

        public void SetCalculatedPeaks(IList<CalculatedPeaks> calculatedPeaksList)
        {
            if (!calculatedPeaksList.All(cp => ReferenceEquals(this, cp.PeptideAnalysis)))
            {
                throw new ArgumentException("Wrong Peptide Analysis");
            }
            _calculatedPeaks = calculatedPeaksList.ToDictionary(peaks => peaks.PeptideFileAnalysis.Id);
            var data = Data;
            foreach (var entry in _calculatedPeaks)
            {
                PeptideFileAnalysisData peptideFileAnalysisData;
                if (!data.FileAnalyses.TryGetValue(entry.Key, out peptideFileAnalysisData))
                {
                    continue;
                }
                peptideFileAnalysisData = peptideFileAnalysisData.SetPeaks(entry.Value.AutoFindPeak, entry.Value.ToPeakSetData());
                data = data.SetFileAnalyses(data.FileAnalyses.Replace(entry.Key, peptideFileAnalysisData));
            }
            Data = data;
        }

        public CalculatedPeaks GetCalculatePeaks(PeptideFileAnalysis peptideFileAnalysis)
        {
            if (null == _calculatedPeaks)
            {
                return null;
            }
            CalculatedPeaks calculatedPeaks;
            _calculatedPeaks.TryGetValue(peptideFileAnalysis.Id, out calculatedPeaks);
            return calculatedPeaks;
        }

        public void InvalidatePeaks()
        {
            _calculatedPeaks = null;
            foreach (var fileAnalysis in FileAnalyses)
            {
                fileAnalysis.InvalidatePeaks();
            }
        }

        public void EnsurePeaksCalculated()
        {
            if (0 == GetChromatogramRefCount())
            {
                throw new InvalidOperationException("No chromatograms");
            }
            if (null != _calculatedPeaks)
            {
                return;
            }
            var peaksList = new List<CalculatedPeaks>();
            var peptideFileAnalyses = FileAnalyses.ToArray();
            Array.Sort(peptideFileAnalyses,
                (f1, f2) => (null == f1.PsmTimes).CompareTo(null == f2.PsmTimes)
            );
            bool changed = false;
            foreach (var peptideFileAnalysis in peptideFileAnalyses)
            {
                if (peptideFileAnalysis.ChromatogramSet == null
                    || !peptideFileAnalysis.IsMzKeySetComplete(peptideFileAnalysis.ChromatogramSet.Chromatograms.Keys))
                {
                    continue;
                }
                
                var peaks = peptideFileAnalysis.CalculatedPeaks;
                if (peaks == null || changed)
                {
                    peaks = CalculatedPeaks.Calculate(peptideFileAnalysis, peaksList);
                    changed = changed || peaks != null;
                }
                peaksList.Add(peaks);
            }
            SetCalculatedPeaks(peaksList);
        }

        public PeptideFileAnalysisData GetFileAnalysisData(long id)
        {
            if (null == Data)
            {
                return null;
            }
            PeptideFileAnalysisData fileAnalysisData;
            Data.FileAnalyses.TryGetValue(id, out fileAnalysisData);
            return fileAnalysisData;
        }

        public void SetFileAnalysisData(long id, PeptideFileAnalysisData data)
        {
            if (Data == null)
            {
                return;
            }
            if (data == null)
            {
                Data.SetFileAnalyses(Data.FileAnalyses.RemoveKey(id));
            }
            else
            {
                Data.SetFileAnalyses(Data.FileAnalyses.Replace(id, data));
            }
        }
             

        private TurnoverCalculator _turnoverCalculator;
        private int _chromatogramRefCount;

        public Peptide Peptide { get { return Workspace.Peptides.FindByKey(Data.PeptideId); } }
        [OneToMany(ForeignKey = "PeptideAnalysis")]
        public PeptideFileAnalyses FileAnalyses
        {
            get; private set;
        }
        public PeptideFileAnalysis GetFileAnalysis(long id)
        {
            PeptideFileAnalysis peptideFileAnalysis;
            FileAnalyses.TryGetValue(id, out peptideFileAnalysis);
            return peptideFileAnalysis;
        }
        
        public ValidationStatus? GetValidationStatus() 
        {
            ValidationStatus? result = null;
            foreach (var peptideFileAnalysis in FileAnalyses)
            {
                if (result == null)
                {
                    result = peptideFileAnalysis.ValidationStatus;
                }
                else
                {
                    if (result != peptideFileAnalysis.ValidationStatus)
                    {
                        return null;
                    }
                }
            }
            return result;
        }

        public void SetValidationStatus(ValidationStatus? value)
        {
            if (value == null)
            {
                return;
            }
            foreach (var peptideFileAnalysis in FileAnalyses)
            {
                peptideFileAnalysis.ValidationStatus = value.Value;
            }
        }

        public double? MassAccuracy
        {
            get
            {
                return Data.MassAccuracy;
            }
            set
            {
                if (Data.MassAccuracy == value)
                {
                    return;
                }
                Data = Data.SetMassAccuracy(value);
            }
        }

        public double GetMassAccuracy()
        {
            return MassAccuracy ?? Workspace.GetMassAccuracy();
        }

        public void Save(ISession session)
        {
            var dbPeptideAnalysis = session.Get<DbPeptideAnalysis>(Id);
            dbPeptideAnalysis.MassAccuracy = Data.MassAccuracy;
            dbPeptideAnalysis.MinCharge = Data.MinCharge;
            dbPeptideAnalysis.MaxCharge = Data.MaxCharge;
            dbPeptideAnalysis.ExcludedMasses = Data.ExcludedMasses.ToByteArray();
            session.Update(dbPeptideAnalysis);
            foreach (var fileAnalysis in FileAnalyses)
            {
                fileAnalysis.Save(session);
            }
            session.Save(new DbChangeLog(this));
        }

        public Dictionary<int,IList<MzRange>> GetMzs()
        {
            var result = new Dictionary<int, IList<MzRange>>();
            for (int charge = MinCharge; charge <= MaxCharge; charge ++)
            {
                result.Add(charge, GetTurnoverCalculator().GetMzs(charge));
            }
            return result;
        }

        public int MinCharge 
        { 
            get
            {
                return Data.MinCharge;
            }
            set
            {
                if (MinCharge == value)
                {
                    return;
                }
                Data = Data.SetMinCharge(value);
            }
        }

        public int MaxCharge 
        { 
            get
            {
                return Data.MaxCharge;
            }
            set
            {
                if (value == MaxCharge)
                {
                    return;
                }
                Data = Data.SetMaxCharge(value);
            } 
        }

        public override void Update(WorkspaceChangeArgs workspaceChange, PeptideAnalysisData data)
        {
            bool invalidatePeaks = workspaceChange.HasPeakPickingChange || data.CheckRecalculatePeaks(Data);
            base.Update(workspaceChange, data);
            FileAnalyses.Update(workspaceChange);
            if (invalidatePeaks)
            {
                InvalidatePeaks();
            }
        }

        public int GetMassCount()
        {
            return GetTurnoverCalculator().MassCount;
        }

        [Browsable(false)]
        public ExcludedMasses ExcludedMasses
        {
            get { return Data.ExcludedMasses; }
        }

        public TurnoverCalculator GetTurnoverCalculator()
        {
            var turnoverCalculator = _turnoverCalculator;
            if (turnoverCalculator != null)
            {
                return turnoverCalculator;
            }
            _turnoverCalculator = turnoverCalculator = new TurnoverCalculator(Workspace, Peptide.Sequence);
            return turnoverCalculator;
        }

        public IList<PeptideFileAnalysis> GetFileAnalyses(bool filterRejects)
        {
            if (filterRejects)
            {
                return FileAnalyses
                    .Where(fileAnalysis => ValidationStatus.reject != fileAnalysis.ValidationStatus)
                    .ToArray();
            }
            return FileAnalyses.ToArray();
        }
        public String GetLabel()
        {
            String label = Peptide.Sequence;
            if (label.Length > 15)
            {
                label = label.Substring(0, 5) + "..." + label.Substring(label.Length - 7, 7);
            }
            return label;
        }
        public int GetChromatogramRefCount()
        {
            return _chromatogramRefCount;
        }
        public IDisposable IncChromatogramRefCount()
        {
            _chromatogramRefCount++;
            return new RefCountHolder(this);
        }
        public void DecChromatogramRefCount()
        {
            _chromatogramRefCount--;
        }
        [Browsable(false)]
        public bool ChromatogramsWereLoaded { get { return Data.ChromatogramsWereLoaded; } }
        class RefCountHolder : IDisposable
        {
            private PeptideAnalysis _peptideAnalysis;
            public RefCountHolder(PeptideAnalysis peptideAnalysis)
            {
                _peptideAnalysis = peptideAnalysis;
            }
            public void Dispose()
            {
                if (_peptideAnalysis != null)
                {
                    _peptideAnalysis.DecChromatogramRefCount();
                    _peptideAnalysis = null;
                }
            }
        }

        public override PeptideAnalysisData GetData(WorkspaceData workspaceData)
        {
            PeptideAnalysisData peptideAnalysisData = null;
            if (null != workspaceData.PeptideAnalyses)
            {
                workspaceData.PeptideAnalyses.TryGetValue(Id, out peptideAnalysisData);
            }
            return peptideAnalysisData;
        }

        public override WorkspaceData SetData(WorkspaceData workspaceData, PeptideAnalysisData value)
        {
            return workspaceData.SetPeptideAnalyses(workspaceData.PeptideAnalyses.Replace(Id, value));
        }
    }
}
