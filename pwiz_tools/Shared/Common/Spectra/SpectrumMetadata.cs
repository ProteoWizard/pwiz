﻿/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
 */using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Spectra
{
    public class SpectrumMetadata : Immutable
    {
        private ImmutableList<ImmutableList<SpectrumPrecursor>> _precursorsByMsLevel =
            ImmutableList<ImmutableList<SpectrumPrecursor>>.EMPTY;

        public SpectrumMetadata(string id, double retentionTime)
        {
            Id = id;
            RetentionTime = retentionTime;
        }

        public string Id { get; private set; }
        public double RetentionTime { get; private set; }
        public int MsLevel
        {
            get { return _precursorsByMsLevel.Count + 1; }
        }

        public int PresetScanConfiguration { get; private set; }

        public SpectrumMetadata ChangePresetScanConfiguration(int presetScanConfiguration)
        {
            return ChangeProp(ImClone(this), im => im.PresetScanConfiguration = presetScanConfiguration);
        }

        public ImmutableList<SpectrumPrecursor> GetPrecursors(int msLevel)
        {
            if (msLevel < 1 || msLevel > _precursorsByMsLevel.Count)
            {
                return ImmutableList<SpectrumPrecursor>.EMPTY;
            }

            return _precursorsByMsLevel[msLevel - 1];
        }

        public SpectrumMetadata ChangePrecursors(IEnumerable<IEnumerable<SpectrumPrecursor>> precursorsByMsLevel)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im._precursorsByMsLevel = ImmutableList.ValueOf(precursorsByMsLevel.Select(ImmutableList.ValueOf));
            });
        }

        public bool NegativeCharge { get; private set; }

        public SpectrumMetadata ChangeNegativeCharge(bool negativeCharge)
        {
            return ChangeProp(ImClone(this), im => im.NegativeCharge = negativeCharge);
        }

        public string ScanDescription { get; private set; }

        public SpectrumMetadata ChangeScanDescription(string scanDescription)
        {
            if (scanDescription == ScanDescription)
            {
                return this;
            }

            return ChangeProp(ImClone(this), im => im.ScanDescription = scanDescription);
        }

        public double? CompensationVoltage { get; private set; }

        public SpectrumMetadata ChangeCompensationVoltage(double? compensationVoltage)
        {
            return ChangeProp(ImClone(this), im => im.CompensationVoltage = compensationVoltage);
        }

        public double? ScanWindowLowerLimit { get; private set; }
        public double? ScanWindowUpperLimit { get; private set; }

        public SpectrumMetadata ChangeScanWindow(double lowerLimit, double upperLimit)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.ScanWindowLowerLimit = ScanWindowLowerLimit;
                im.ScanWindowUpperLimit = ScanWindowUpperLimit;
            });
        }

        public string Analyzer { get; private set; }

        public SpectrumMetadata ChangeAnalyzer(string value)
        {
            return ChangeProp(ImClone(this), im => im.Analyzer = value);
        }

        protected bool Equals(SpectrumMetadata other)
        {
            return _precursorsByMsLevel.Equals(other._precursorsByMsLevel) && Id == other.Id &&
                   RetentionTime.Equals(other.RetentionTime) && NegativeCharge == other.NegativeCharge &&
                   ScanDescription == other.ScanDescription &&
                   Nullable.Equals(ScanWindowLowerLimit, other.ScanWindowLowerLimit) &&
                   Nullable.Equals(ScanWindowUpperLimit, other.ScanWindowUpperLimit) &&
                   Nullable.Equals(CompensationVoltage, other.CompensationVoltage) &&
                   Equals(PresetScanConfiguration, other.PresetScanConfiguration) &&
                   Equals(Analyzer, other.Analyzer);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SpectrumMetadata) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = _precursorsByMsLevel.GetHashCode();
                hashCode = (hashCode * 397) ^ Id.GetHashCode();
                hashCode = (hashCode * 397) ^ RetentionTime.GetHashCode();
                hashCode = (hashCode * 397) ^ NegativeCharge.GetHashCode();
                hashCode = (hashCode * 397) ^ ScanDescription.GetHashCode();
                hashCode = (hashCode * 397) ^ ScanWindowLowerLimit.GetHashCode();
                hashCode = (hashCode * 397) ^ ScanWindowUpperLimit.GetHashCode();
                hashCode = (hashCode * 397) ^ CompensationVoltage.GetHashCode();
                return hashCode;
            }
        }
    }
}
