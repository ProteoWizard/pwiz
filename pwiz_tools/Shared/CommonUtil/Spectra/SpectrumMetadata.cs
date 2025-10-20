/*
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
        [Flags]
        private enum Flags
        {
            HasCompensationVoltage = 1,
            HasScanWindow = 2,
            HasTotalIonCurrent = 4,
            HasInjectionTime = 8,
            HasConstantNeutralLoss = 16,
        }
        private ImmutableList<ImmutableList<SpectrumPrecursor>> _precursorsByMsLevel =
            ImmutableList<ImmutableList<SpectrumPrecursor>>.EMPTY;

        private Flags _flags;

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

        private double _compensationVoltage;
        public double? CompensationVoltage
        {
            get
            {
                return GetFlag(Flags.HasCompensationVoltage) ? _compensationVoltage : (double?) null;
            }
            private set
            {
                SetFlag(Flags.HasCompensationVoltage, value.HasValue);
                _compensationVoltage = value.GetValueOrDefault();
            }
        }

        public SpectrumMetadata ChangeCompensationVoltage(double? compensationVoltage)
        {
            return ChangeProp(ImClone(this), im => im.CompensationVoltage = compensationVoltage);
        }

        private double _scanWindowLowerLimit;
        private double _scanWindowUpperLimit;
        public double? ScanWindowLowerLimit
        {
            get { return GetFlag(Flags.HasScanWindow) ? _scanWindowLowerLimit : (double?)null; }
        }

        public double? ScanWindowUpperLimit
        {
            get
            {
                return GetFlag(Flags.HasScanWindow) ? _scanWindowUpperLimit : (double?)null;
            }
        }

        public SpectrumMetadata ChangeScanWindow(double lowerLimit, double upperLimit)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.SetFlag(Flags.HasScanWindow, true);
                im._scanWindowLowerLimit = lowerLimit;
                im._scanWindowUpperLimit = upperLimit;
            });
        }

        public string Analyzer { get; private set; }

        public SpectrumMetadata ChangeAnalyzer(string value)
        {
            return ChangeProp(ImClone(this), im => im.Analyzer = value);
        }

        private double _totalIonCurrent;

        public double? TotalIonCurrent
        {
            get
            {
                return GetFlag(Flags.HasTotalIonCurrent) ? _totalIonCurrent : (double?)null;
            }
            private set
            {
                SetFlag(Flags.HasTotalIonCurrent, value.HasValue);
                _totalIonCurrent = value.GetValueOrDefault();
            }
        }

        private double _constantNeutralLoss;

        public double? ConstantNeutralLoss // As found in Constant Neutral Loss scans, where Q1 peaks are only reported if there's a Q3 peak with this mz offset. Positive value implies loss, negative value implies gain
        {
            get
            {
                return GetFlag(Flags.HasConstantNeutralLoss) ? _constantNeutralLoss : (double?)null;
            }
            private set
            {
                _constantNeutralLoss = value.GetValueOrDefault();
                SetFlag(Flags.HasConstantNeutralLoss, _constantNeutralLoss != 0);
            }
        }
        
        public SpectrumMetadata ChangeConstantNeutralLoss(double? value)
        {
            return ChangeProp(ImClone(this), im => im.ConstantNeutralLoss = value);
        }


        public SpectrumMetadata ChangeTotalIonCurrent(double? value)
        {
            return ChangeProp(ImClone(this), im => im.TotalIonCurrent = value);
        }

        private double _injectionTime;

        public double? InjectionTime
        {
            get
            {
                return GetFlag(Flags.HasInjectionTime) ? _injectionTime : (double?)null;
            }
            set
            {
                SetFlag(Flags.HasInjectionTime, value.HasValue);
                _injectionTime = value.GetValueOrDefault();
            }
        }

        public SpectrumMetadata ChangeInjectionTime(double? value)
        {
            return ChangeProp(ImClone(this), im => im.InjectionTime = value);
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
                   Nullable.Equals(TotalIonCurrent, other.TotalIonCurrent) &&
                   Nullable.Equals(InjectionTime, other.InjectionTime) &&
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
                hashCode = (hashCode * 397) ^ TotalIonCurrent.GetHashCode();
                hashCode = (hashCode * 397) ^ InjectionTime.GetHashCode();
                return hashCode;
            }
        }

        private bool GetFlag(Flags flag)
        {
            return 0 != (_flags & flag);
        }

        private void SetFlag(Flags flag, bool value)
        {
            if (value)
            {
                _flags |= flag;
            }
            else
            {
                _flags &= ~flag;
            }
        }
    }
}
