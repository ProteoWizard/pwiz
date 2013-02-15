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

using System;
using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;

namespace pwiz.Topograph.Model.Data
{
    public class MsDataFileData
    {
        public MsDataFileData(DbMsDataFile dbMsDataFile)
        {
            Name = dbMsDataFile.Name;
            Label = dbMsDataFile.Label;
            Cohort = dbMsDataFile.Cohort;
            Sample = dbMsDataFile.Sample;
            PrecursorPool = PrecursorPoolValue.ParsePersistedString(dbMsDataFile.PrecursorPool);
            TimePoint = dbMsDataFile.TimePoint;
            Times = ImmutableList.ValueOf(dbMsDataFile.Times);
            TotalIonCurrent = ImmutableList.ValueOf(dbMsDataFile.TotalIonCurrent);
            MsLevels = ImmutableList.ValueOf(dbMsDataFile.MsLevels);
        }

        public MsDataFileData(MsDataFileData msDataFileData)
        {
            Name = msDataFileData.Name;
            Label = msDataFileData.Label;
            Cohort = msDataFileData.Cohort;
            Sample = msDataFileData.Sample;
            PrecursorPool = msDataFileData.PrecursorPool;
            TimePoint = msDataFileData.TimePoint;
            Times = msDataFileData.Times;
            TotalIonCurrent = msDataFileData.TotalIonCurrent;
            MsLevels = msDataFileData.MsLevels;
            RetentionTimesByModifiedSequence = msDataFileData.RetentionTimesByModifiedSequence;
        }

        protected bool Equals(MsDataFileData other)
        {
            return !CheckDirty(other) && EqualsBinary(other);
        }

        public bool CheckDirty(MsDataFileData other)
        {
            return !string.Equals(Name, other.Name)
                   || !string.Equals(Label, other.Label)
                   || !string.Equals(Cohort, other.Cohort)
                   || !string.Equals(Sample, other.Sample)
                   || !PrecursorPool.Equals(other.PrecursorPool)
                   || !TimePoint.Equals(other.TimePoint);
        }
        public bool EqualsBinary(MsDataFileData other)
        {
            return Equals(Times, other.Times) 
                && Equals(TotalIonCurrent, other.TotalIonCurrent)
                && Equals(MsLevels, other.MsLevels)
                && Equals(RetentionTimesByModifiedSequence, other.RetentionTimesByModifiedSequence);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((MsDataFileData) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Label != null ? Label.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Cohort != null ? Cohort.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Sample != null ? Sample.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (PrecursorPool != null ? PrecursorPool.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ TimePoint.GetHashCode();
                hashCode = (hashCode*397) ^ (Times != null ? Times.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (TotalIonCurrent != null ? TotalIonCurrent.GetHashCode() : 0);
                return hashCode;
            }
        }

        public string Name { get; private set; }
        public string Label { get; private set; }
        public MsDataFileData SetLabel(string value)
        {
            return new MsDataFileData(this){Label = value};
        }
        public string Cohort { get; private set; }
        public MsDataFileData SetCohort(string value)
        {
            return new MsDataFileData(this){Cohort = value};
        }
        public string Sample { get; private set; }
        public MsDataFileData SetSample(string value)
        {
            return new MsDataFileData(this){Sample = value};
        }
        public PrecursorPoolValue? PrecursorPool { get; private set; }
        public MsDataFileData SetPrecursorPool(PrecursorPoolValue? value)
        {
            return new MsDataFileData(this) {PrecursorPool = value};
        }
        public double? TimePoint { get; private set; }
        public MsDataFileData SetTimePoint(double? value)
        {
            return new MsDataFileData(this){TimePoint = value};
        }
        public IList<double> Times { get; private set; }
        public MsDataFileData SetTimes(IList<double> times)
        {
            return new MsDataFileData(this){Times = ImmutableList.ValueOf(times)};
        }
        public IList<double> TotalIonCurrent { get; private set; }
        public MsDataFileData SetTotalIonCurrent(IList<double> totalIonCurrent)
        {
            return new MsDataFileData(this){TotalIonCurrent = ImmutableList.ValueOf(totalIonCurrent)};
        }
        public IList<byte> MsLevels { get; private set; }
        public MsDataFileData SetMsLevels(IList<byte> msLevels)
        {
            return new MsDataFileData(this){MsLevels = ImmutableList.ValueOf(msLevels)};
        }
        public ImmutableSortedList<string, double> RetentionTimesByModifiedSequence { get; private set; }
        public MsDataFileData SetRetentionTimesByModifiedSequence(IEnumerable<KeyValuePair<string, double>> times)
        {
            return new MsDataFileData(this)
                       {
                           RetentionTimesByModifiedSequence = ImmutableSortedList.FromValues(times, StringComparer.Ordinal),
                       };
        }
        public ImmutableSortedList<long, RetentionTimeAlignment> RetentionTimeAlignments { get; private set; }
    }
}
