/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;

namespace pwiz.Topograph.Model.Data
{
    public class PeptideFileAnalysisData
    {
        public PeptideFileAnalysisData(PeptideFileAnalysisData peptideFileAnalysisData)
        {
            MsDataFileId = peptideFileAnalysisData.MsDataFileId;
            ValidationStatus = peptideFileAnalysisData.ValidationStatus;
            Note = peptideFileAnalysisData.Note;
            ChromatogramStartTime = peptideFileAnalysisData.ChromatogramStartTime;
            ChromatogramEndTime = peptideFileAnalysisData.ChromatogramEndTime;
            AutoFindPeak = peptideFileAnalysisData.AutoFindPeak;
            Peaks = peptideFileAnalysisData.Peaks;
            ChromatogramSetId = peptideFileAnalysisData.ChromatogramSetId;
            PsmTimes = peptideFileAnalysisData.PsmTimes;
            ChromatogramSet = peptideFileAnalysisData.ChromatogramSet;
        }
        public PeptideFileAnalysisData(DbPeptideFileAnalysis dbPeptideFileAnalysis, IEnumerable<Peak> peaks)
        {
            MsDataFileId = dbPeptideFileAnalysis.MsDataFile.GetId();
            ValidationStatus = dbPeptideFileAnalysis.ValidationStatus;
            Note = dbPeptideFileAnalysis.Note;
            ChromatogramStartTime = dbPeptideFileAnalysis.ChromatogramStartTime;
            ChromatogramEndTime = dbPeptideFileAnalysis.ChromatogramEndTime;
            AutoFindPeak = dbPeptideFileAnalysis.AutoFindPeak;
            Peaks = new PeakSet(dbPeptideFileAnalysis, peaks);
            if (null != dbPeptideFileAnalysis.ChromatogramSet)
            {
                ChromatogramSetId = dbPeptideFileAnalysis.ChromatogramSet.Id;
            }
        }

        public long MsDataFileId { get; private set; }
        public ValidationStatus ValidationStatus { get; private set; }
        public PeptideFileAnalysisData SetValidationStatus(ValidationStatus value)
        {
            return new PeptideFileAnalysisData(this) {ValidationStatus = value};
        }
        public string Note { get; private set; }
        public PeptideFileAnalysisData SetNote(string value)
        {
            return new PeptideFileAnalysisData(this) { Note = Note };
        }
        public double ChromatogramStartTime { get; private set; }
        public double ChromatogramEndTime { get; private set; }
        public bool AutoFindPeak { get; private set; }
        public PeptideFileAnalysisData SetAutoFindPeak(bool value)
        {
            return new PeptideFileAnalysisData(this)
                {
                    AutoFindPeak = value,
                };
        }
        public PeakSet Peaks { get; private set; }
        public PeptideFileAnalysisData SetPeaks(bool autoFindPeak, PeakSet peaks)
        {
            return new PeptideFileAnalysisData(this) {AutoFindPeak = autoFindPeak, Peaks = peaks};
        }
        public long? ChromatogramSetId { get; private set; }
        public PsmTimes PsmTimes { get; private set; }
        public PeptideFileAnalysisData SetPsmTimes(PsmTimes value)
        {
            return new PeptideFileAnalysisData(this){PsmTimes = value};
        }
        public ChromatogramSetData ChromatogramSet { get; private set; }
        public PeptideFileAnalysisData SetChromatogramSet(ChromatogramSetData value)
        {
            return new PeptideFileAnalysisData(this){ChromatogramSet = value};
        }
        public PeptideFileAnalysisData UnloadChromatograms()
        {
            if (PsmTimes == null && ChromatogramSet == null)
            {
                return this;
            }
            return new PeptideFileAnalysisData(this)
                       {
                           PsmTimes = null,
                           ChromatogramSet = null,
                       };
        }

        public struct PeakSet
        {
            private IList<Peak> _peaks; 
            public PeakSet(DbPeptideFileAnalysis dbPeptideFileAnalysis, IEnumerable<Peak> peaks) : this()
            {
                TracerPercent = dbPeptideFileAnalysis.TracerPercent;
                DeconvolutionScore = dbPeptideFileAnalysis.DeconvolutionScore;
                PrecursorEnrichment = dbPeptideFileAnalysis.PrecursorEnrichment;
                if (dbPeptideFileAnalysis.PrecursorEnrichmentFormula != null)
                {
                    PrecursorEnrichmentFormula = TracerPercentFormula.Parse(dbPeptideFileAnalysis.PrecursorEnrichmentFormula);
                }
                Turnover = dbPeptideFileAnalysis.Turnover;
                TurnoverScore = dbPeptideFileAnalysis.TurnoverScore;
                IntegrationNote = IntegrationNote.Parse(dbPeptideFileAnalysis.IntegrationNote);
                if (peaks != null)
                {
                    _peaks = ImmutableList.ValueOf(peaks);
                }
            }
            public double? TracerPercent { get; set; }
            public double? DeconvolutionScore { get; set; }
            public double? PrecursorEnrichment { get; set; }
            public TracerPercentFormula PrecursorEnrichmentFormula { get; set; }
            public double? Turnover { get; set; }
            public double? TurnoverScore { get; set; }
            public IntegrationNote IntegrationNote { get; set; }
            public IList<Peak> Peaks
            {
                get { return _peaks; }
                set { _peaks = ImmutableList.ValueOf(value); }
            }
            public bool IsCalculated { get { return TracerPercent.HasValue && Peaks.Count > 0; } }
        }

        public struct Peak
        {
            public Peak(DbPeak dbPeak) : this()
            {
                StartTime = dbPeak.StartTime;
                EndTime = dbPeak.EndTime;
                Area = dbPeak.Area;
            }
            public double StartTime { get; set; }
            public double EndTime { get; set; }
            public double Area { get; set; }
        }

        protected bool Equals(PeptideFileAnalysisData other)
        {
            return MsDataFileId == other.MsDataFileId 
                && ChromatogramStartTime.Equals(other.ChromatogramStartTime) 
                && ChromatogramEndTime.Equals(other.ChromatogramEndTime) 
                && AutoFindPeak.Equals(other.AutoFindPeak) 
                && Peaks.Equals(other.Peaks) 
                && ChromatogramSetId == other.ChromatogramSetId
                && Equals(PsmTimes, other.PsmTimes)
                && Equals(ChromatogramSet, other.ChromatogramSet)
                && Equals(ValidationStatus, other.ValidationStatus)
                && Equals(Note, other.Note);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PeptideFileAnalysisData) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = MsDataFileId.GetHashCode();
                hashCode = (hashCode*397) ^ ChromatogramStartTime.GetHashCode();
                hashCode = (hashCode*397) ^ ChromatogramEndTime.GetHashCode();
                hashCode = (hashCode*397) ^ AutoFindPeak.GetHashCode();
                hashCode = (hashCode*397) ^ Peaks.GetHashCode();
                hashCode = (hashCode*397) ^ ChromatogramSetId.GetHashCode();
                hashCode = (hashCode*397) ^ (null == PsmTimes ? 0 : PsmTimes.GetHashCode());
                hashCode = (hashCode*397) ^ (null == ChromatogramSet ? 0 : ChromatogramSet.GetHashCode());
                return hashCode;
            }
        }
        public bool CheckDirty(PeptideFileAnalysisData that)
        {
            if (ReferenceEquals(this, that))
            {
                return false;
            }
            if (!Equals(MsDataFileId, that.MsDataFileId)
                || !Equals(ChromatogramStartTime, that.ChromatogramStartTime)
                || !Equals(ChromatogramEndTime, that.ChromatogramEndTime)
                || !Equals(AutoFindPeak, that.AutoFindPeak)
                || !Equals(ValidationStatus, that.ValidationStatus)
                || !Equals(Note, that.Note))
            {
                return true;
            }
            if (!AutoFindPeak && !Equals(Peaks, that.Peaks))
            {
                return true;
            }
            return false;
        }
        public bool CheckRecalculatePeaks(PeptideFileAnalysisData that)
        {
            if (!Equals(AutoFindPeak, that.AutoFindPeak))
            {
                return true;
            }
            return false;
        }

        public static readonly IList<DataProperty<PeptideFileAnalysisData>> MergeableProperties = ImmutableList.ValueOf(
            new DataProperty<PeptideFileAnalysisData>[]
                {
                    new DataProperty<PeptideFileAnalysisData, ValidationStatus>(data=>data.ValidationStatus, (data, value)=>data.SetValidationStatus(value)),
                    new DataProperty<PeptideFileAnalysisData, string>(data=>data.Note, (data, value)=>data.SetNote(value)),
                });

    }
}
