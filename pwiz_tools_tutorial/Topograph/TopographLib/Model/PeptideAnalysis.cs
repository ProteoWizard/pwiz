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
using pwiz.Topograph.Data;
using pwiz.Topograph.Data.Snapshot;
using pwiz.Topograph.Enrichment;

namespace pwiz.Topograph.Model
{
    public class PeptideAnalysis : AnnotatedEntityModel<DbPeptideAnalysis>
    {
        private int _minCharge;
        private int _maxCharge;
        private TurnoverCalculator _turnoverCalculator;
        private int _chromatogramRefCount;
        private double? _massAccuracy;
        private WorkspaceVersion _workspaceVersion;

        public PeptideAnalysis(Workspace workspace, DbPeptideAnalysis dbPeptideAnalysis) : base(workspace, dbPeptideAnalysis)
        {
            FileAnalyses = new PeptideFileAnalyses(this, dbPeptideAnalysis);
            ExcludedMzs.ChangedEvent += ExcludedMzs_ChangedEvent;
            SetWorkspaceVersion(workspace.WorkspaceVersion);
            ChromatogramsWereLoaded = true;
        }

        public PeptideAnalysis(Workspace workspace, PeptideAnalysisSnapshot snapshot) : this(workspace, snapshot.DbPeptideAnalysis)
        {
            Merge(snapshot);
        }

        public Peptide Peptide { get; private set; }
        public PeptideFileAnalyses FileAnalyses { get; private set; }
        public ValidationStatus? GetValidationStatus() 
        {
            ValidationStatus? result = null;
            foreach (var peptideFileAnalysis in FileAnalyses.ListChildren())
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
            foreach (var peptideFileAnalysis in FileAnalyses.ListChildren())
            {
                peptideFileAnalysis.ValidationStatus = value.Value;
            }
        }

        protected override IEnumerable<ModelProperty> GetModelProperties()
        {
            foreach (var modelProperty in base.GetModelProperties())
            {
                yield return modelProperty;
            }
            yield return Property<PeptideAnalysis,int>(
                m=>m._minCharge,(m,v)=>m._minCharge = v, e => e.MinCharge, (e,v)=>e.MinCharge = v);
            yield return Property<PeptideAnalysis, int>(
                m=>m._maxCharge,(m,v)=>m._maxCharge = v, e=>e.MaxCharge,(e,v)=>e.MaxCharge = v);
            yield return Property<PeptideAnalysis, byte[]>(
                m => m.ExcludedMzs.ToByteArray(),
                (m, v) => (m).ExcludedMzs.SetByteArray(v),
                e => e.ExcludedMasses ?? new byte[0],
                (e,v)=>e.ExcludedMasses = ArrayConverter.ZeroLengthToNull(v));
            yield return Property<PeptideAnalysis, double?>(
                m => m._massAccuracy, (m, v) => m._massAccuracy = v, 
                e => e.MassAccuracy, (e, v) => e.MassAccuracy = v);
        }

        protected override void Load(DbPeptideAnalysis entity)
        {
            ExcludedMzs = ExcludedMzs ?? new ExcludedMzs();
            Peptide = Workspace.Peptides.GetPeptide(entity.Peptide);
            base.Load(entity);
            _workspaceVersion = Workspace.SavedWorkspaceVersion;
        }

        public override bool IsDirty()
        {
            if (base.IsDirty())
            {
                return true;
            }
            foreach (var fileAnalysis in FileAnalyses.ListChildren())
            {
                if (fileAnalysis.IsDirty())
                {
                    return true;
                }
            }
            return false;
        }

        public void Merge(PeptideAnalysisSnapshot peptideAnalysisSnapshot)
        {
            Load(peptideAnalysisSnapshot.DbPeptideAnalysis);
            foreach (var snapshot in peptideAnalysisSnapshot.FileAnalysisSnapshots.Values)
            {
                var fileAnalysis = FileAnalyses.GetChild(snapshot.Id);
                if (fileAnalysis == null)
                {
                    fileAnalysis = new PeptideFileAnalysis(this, snapshot.DbPeptideFileAnalysis);
                    FileAnalyses.AddChild(fileAnalysis.Id.Value, fileAnalysis);
                }
                fileAnalysis.Merge(snapshot);
            }
            ChromatogramsWereLoaded = peptideAnalysisSnapshot.ChromatogramsWereLoaded;
        }

        void ExcludedMzs_ChangedEvent(ExcludedMzs obj)
        {
            OnChange();
        }

        public double? MassAccuracy
        {
            get
            {
                return _massAccuracy;
            }
            set
            {
                if (_massAccuracy == value)
                {
                    return;
                }
                _massAccuracy = value;
                InvalidateChromatograms();
                OnChange();
            }
        }

        public double GetMassAccuracy()
        {
            return MassAccuracy ?? Workspace.GetMassAccuracy();
        }

        public void SaveDeep(ISession session)
        {
            Save(session);
            foreach (var fileAnalysis in FileAnalyses.ListChildren())
            {
                fileAnalysis.Save(session);
                if (fileAnalysis.Peaks.IsDirty)
                {
                    fileAnalysis.Peaks.Save(session);
                }
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
                return _minCharge;
            }
            set
            {
                using (GetWriteLock())
                {
                    if (_minCharge == value)
                    {
                        return;
                    }
                    _minCharge = value;
                    InvalidateChromatograms();
                    OnChange();
                }
            }
        }

        public void InvalidateChromatograms()
        {
            foreach (var fileAnalysis in FileAnalyses.ListChildren())
            {
                fileAnalysis.InvalidateChromatograms();
            }
        }
        public int MaxCharge 
        { 
            get
            {
                return _maxCharge;
            }
            set
            {
                using (GetWriteLock())
                {
                    if (_maxCharge == value)
                    {
                        return;
                    }
                    _maxCharge = value;
                    InvalidateChromatograms();
                    OnChange();
                }
            } 
        }

        public int GetMassCount()
        {
            return GetTurnoverCalculator().MassCount;
        }

        public ExcludedMzs ExcludedMzs { get; private set; }

        public TurnoverCalculator GetTurnoverCalculator()
        {
            using(GetReadLock())
            {
                var turnoverCalculator = _turnoverCalculator;
                if (turnoverCalculator != null)
                {
                    return turnoverCalculator;
                }
                _turnoverCalculator = turnoverCalculator = new TurnoverCalculator(Workspace, Peptide.Sequence);
                return turnoverCalculator;
            }
        }

        public IList<PeptideFileAnalysis> GetFileAnalyses(bool filterRejects)
        {
            return FileAnalyses.ListPeptideFileAnalyses(filterRejects);
        }
        public PeptideFileAnalysis GetFileAnalysis(long id)
        {
            return FileAnalyses.GetChild(id);
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
        public void IncChromatogramRefCount()
        {
            _chromatogramRefCount++;
        }
        public void DecChromatogramRefCount()
        {
            _chromatogramRefCount--;
        }
        public void SetWorkspaceVersion(WorkspaceVersion newWorkspaceVersion)
        {
            _workspaceVersion = newWorkspaceVersion;
            _turnoverCalculator = null;
            foreach (var peptideFileAnalysis in FileAnalyses.ListChildren())
            {
                peptideFileAnalysis.SetWorkspaceVersion(newWorkspaceVersion);
            }
        }
        public bool ChromatogramsWereLoaded { get; private set; }
    }
    public enum IntensityScaleMode
    {
        none,
        relative_include_all,
        relative_exclude_any_charge,
        relative_total,
    }
}
