/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results.ProtoBuf;

namespace pwiz.Skyline.Model.Results.Spectra
{
    /// <summary>
    /// More efficient storage of a list of <see cref="SpectrumMetadata"/> objects
    /// by storing repeated values as <see cref="Factor{T}"/> etc.
    /// </summary>
    public class SpectrumMetadatas : AbstractReadOnlyList<SpectrumMetadata>
    {
        private readonly ImmutableList<double> _retentionTimes;
        private readonly ImmutableList<double?> _totalIonCurrents;
        private readonly ImmutableList<double?> _injectionTimes;
        private readonly ImmutableList<int> _presetScanConfigurations;
        private readonly ImmutableList<ScanId> _scanIds;
        private readonly ImmutableList<string> _scanDescriptions;
        private readonly ImmutableList<string> _analyzers;
        private readonly ImmutableList<Tuple<double, double>> _scanWindows;
        private readonly ImmutableList<ImmutableList<PrecursorWithLevel>> _spectrumPrecursors;
        private readonly ImmutableList<double?> _compensationVoltages;
        private readonly ImmutableList<bool> _negativeCharges;
        private readonly ImmutableList<int> _msLevels;
        private readonly ImmutableList<double?> _constantNeutralLosses;

        public SpectrumMetadatas(IEnumerable<SpectrumMetadata> enumerable)
        {
            var collection = enumerable as ICollection<SpectrumMetadata> ?? enumerable.ToList();
            _retentionTimes = collection.Select(m => m.RetentionTime).ToImmutable();
            _totalIonCurrents = ImmutableListFactory.Nullables(collection.Select(m => m.TotalIonCurrent))
                .MaybeConstant();
            _injectionTimes = collection.Select(m => m.InjectionTime).Nullables().MaybeConstant();
            _presetScanConfigurations =
                IntegerList.FromIntegers(collection.Select(m => m.PresetScanConfiguration)).MaybeConstant();
            _scanIds = collection.Select(m => new ScanId(m.Id)).ToImmutable();
            _scanDescriptions = collection.Select(m => m.ScanDescription).ToFactor().MaybeConstant();
            _analyzers = collection.Select(m => m.Analyzer).ToFactor().MaybeConstant();
            _scanWindows = collection.Select(GetScanWindow).ToFactor().MaybeConstant();
            _spectrumPrecursors = collection.Select(GetPrecursors).ToFactor().MaybeConstant();
            _compensationVoltages = collection.Select(m => m.CompensationVoltage).ToFactor().MaybeConstant();
            _negativeCharges = collection.Select(m => m.NegativeCharge).ToFactor().MaybeConstant();
            _msLevels = collection.Select(m => m.MsLevel).ToFactor().MaybeConstant();
            _constantNeutralLosses = collection.Select(m => m.ConstantNeutralLoss).Nullables().MaybeConstant();
        }

        public SpectrumMetadatas(ResultFileMetaDataProto proto)
        {
            _retentionTimes = proto.Spectra.Select(s => s.RetentionTime).ToImmutable();
            _totalIonCurrents = proto.Spectra.Select(s => s.TotalIonCurrent).Nullables().MaybeConstant();
            _injectionTimes = proto.Spectra.Select(s => s.InjectionTime).Nullables().MaybeConstant();
            _presetScanConfigurations = IntegerList.FromIntegers(proto.Spectra.Select(s => s.PresetScanConfiguration));
            var precursors = new List<PrecursorWithLevel>();
            var valueCache = new ValueCache();
            foreach (var protoPrecursor in proto.Precursors)
            {
                var spectrumPrecursor = new SpectrumPrecursor(new SignedMz(protoPrecursor.TargetMz));
                if (protoPrecursor.CollisionEnergy != 0)
                {
                    spectrumPrecursor = spectrumPrecursor.ChangeCollisionEnergy(protoPrecursor.CollisionEnergy);
                }

                if (!string.IsNullOrEmpty(protoPrecursor.DissociationMethod))
                {
                    spectrumPrecursor = spectrumPrecursor.ChangeDissociationMethod(valueCache.CacheValue(protoPrecursor.DissociationMethod));
                }

                if (protoPrecursor.IsolationWindowLower != 0 || protoPrecursor.IsolationWindowUpper != 0)
                {
                    spectrumPrecursor =
                        spectrumPrecursor.ChangeIsolationWindowWidth(protoPrecursor.IsolationWindowLower,
                            protoPrecursor.IsolationWindowUpper);
                }
                precursors.Add(new PrecursorWithLevel(protoPrecursor.MsLevel, spectrumPrecursor));
            }
            _scanIds = proto.Spectra.Select(spectrum=>new ScanId(spectrum)).ToImmutable();
            _scanDescriptions = ToFactor(proto.ScanDescriptions, proto.Spectra.Select(s => s.ScanDescriptionIndex));
            _analyzers = ToFactor(proto.Analyzers, proto.Spectra.Select(s => s.AnalyzerIndex));
            _scanWindows = ToFactor(proto.ScanWindows.Select(sw => Tuple.Create(sw.LowerLimit, sw.UpperLimit)),
                proto.Spectra.Select(s => s.ScanWindowIndex));
            _compensationVoltages = proto.Spectra.Select(s => s.CompensationVoltage).Nullables().MaybeConstant();
            _negativeCharges = proto.Spectra.Select(s => s.NegativeCharge).Booleans();
            var msLevels = new List<int>();
            var spectrumPrecursorsList = new List<ImmutableList<PrecursorWithLevel>>();
            foreach (var spectrum in proto.Spectra)
            {
                var spectrumPrecursors = spectrum.PrecursorIndex.Select(i => precursors[i - 1]).ToImmutable();
                spectrumPrecursorsList.Add(spectrumPrecursors);
                var msLevel = spectrum.MsLevel;
                if (msLevel == 0)
                {
                    msLevel = spectrumPrecursors.Select(p=>p.MsLevel + 1).Prepend(1).Max();
                }
                msLevels.Add(msLevel);
            }
            _spectrumPrecursors = spectrumPrecursorsList.ToFactor().MaybeConstant();
            _msLevels = IntegerList.FromIntegers(msLevels);
            _constantNeutralLosses = proto.Spectra.Select(s => s.ConstantNeutralLoss).Nullables().MaybeConstant();
        }

        public override SpectrumMetadata this[int index]
        {
            get
            {
                var spectrumMetadata = new SpectrumMetadata(_scanIds[index].ToString(), _retentionTimes[index])
                    .ChangeAnalyzer(_analyzers[index])
                    .ChangeCompensationVoltage(_compensationVoltages[index])
                    .ChangeInjectionTime(_injectionTimes[index])
                    .ChangeNegativeCharge(_negativeCharges[index])
                    .ChangePresetScanConfiguration(_presetScanConfigurations[index])
                    .ChangeScanDescription(_scanDescriptions[index])
                    .ChangeTotalIonCurrent(_totalIonCurrents[index])
                    .ChangeConstantNeutralLoss(_constantNeutralLosses[index])
                    .ChangeNegativeCharge(_negativeCharges[index]);
                var precursorsByLevelLookup = _spectrumPrecursors[index].ToLookup(t=>t.MsLevel, t=>t.Precursor);
                var precursors = Enumerable.Range(1, _msLevels[index] - 1)
                    .Select(level => precursorsByLevelLookup[level]);
                spectrumMetadata = spectrumMetadata.ChangePrecursors(precursors);
                var scanWindow = _scanWindows[index];
                if (scanWindow != null)
                {
                    spectrumMetadata = spectrumMetadata.ChangeScanWindow(scanWindow.Item1, scanWindow.Item2);
                }

                return spectrumMetadata;
            }
        }

        public override int Count
        {
            get { return _retentionTimes.Count; }
        }

        public ResultFileMetaDataProto ToProto()
        {
            var proto = new ResultFileMetaDataProto();
            var analyzerFactor = ToFactorWithNull(_analyzers);
            proto.Analyzers.AddRange(analyzerFactor.Levels.Skip(1));
            var precursors = ToDistinctList(_spectrumPrecursors.SelectMany(l => l));
            proto.Precursors.AddRange(precursors.Select(p => new ResultFileMetaDataProto.Types.Precursor
            {
                MsLevel = p.MsLevel,
                CollisionEnergy = p.Precursor.CollisionEnergy ?? 0,
                IsolationWindowLower = p.Precursor.IsolationWindowLowerWidth ?? 0,
                IsolationWindowUpper = p.Precursor.IsolationWindowUpperWidth ?? 0,
                TargetMz = p.Precursor.PrecursorMz,
                DissociationMethod = p.Precursor.DissociationMethod ?? string.Empty
            }));
            var scanDescriptions = ToFactorWithNull(_scanDescriptions);
            proto.ScanDescriptions.AddRange(scanDescriptions.Levels.Skip(1));
            var scanWindows = ToFactorWithNull(_scanWindows);
            foreach (var scanWindow in scanWindows.Levels.Skip(1))
            {
                proto.ScanWindows.Add(new ResultFileMetaDataProto.Types.ScanWindow
                {
                    LowerLimit = scanWindow.Item1,
                    UpperLimit = scanWindow.Item2,
                });
            }

            for (int index = 0; index < Count; index++)
            {
                var spectrum = new ResultFileMetaDataProto.Types.SpectrumMetadata
                {
                    RetentionTime = _retentionTimes[index],
                    TotalIonCurrent = _totalIonCurrents[index],
                    InjectionTime = _injectionTimes[index],
                    PresetScanConfiguration = _presetScanConfigurations[index],
                    AnalyzerIndex = analyzerFactor.LevelIndices[index],
                    PrecursorIndex = { _spectrumPrecursors[index].Select(p=>precursors.IndexOf(p) + 1) },
                    ScanDescriptionIndex = scanDescriptions.LevelIndices[index],
                    ScanWindowIndex = scanWindows.LevelIndices[index],
                    CompensationVoltage = _compensationVoltages[index]
                };
                _scanIds[index].SetInProto(spectrum);
                proto.Spectra.Add(spectrum);
            }

            return proto;
        }


        private Tuple<double, double> GetScanWindow(SpectrumMetadata spectrumMetadata)
        {
            if (spectrumMetadata.ScanWindowLowerLimit.HasValue && spectrumMetadata.ScanWindowUpperLimit.HasValue)
            {
                return Tuple.Create(spectrumMetadata.ScanWindowLowerLimit.Value,
                    spectrumMetadata.ScanWindowUpperLimit.Value);
            }

            return null;
        }

        private ImmutableList<PrecursorWithLevel> GetPrecursors(
            SpectrumMetadata spectrumMetadata)
        {
            return Enumerable.Range(1, spectrumMetadata.MsLevel - 1).SelectMany(msLevel =>
                spectrumMetadata.GetPrecursors(msLevel).Select(p=>new PrecursorWithLevel(msLevel, p))).ToImmutable();
        }

        public static SpectrumMetadatas FromProto(ResultFileMetaDataProto proto)
        {
            return new SpectrumMetadatas(proto);
        }

        private static Factor<T> ToFactorWithNull<T>(ImmutableList<T> list) where T : class
        {
            return Factor<T>.FromItemsWithLevels(list, ImmutableList.Singleton((T)null));
        }

        private DistinctList<T> ToDistinctList<T>(IEnumerable<T> items)
        {
            var distinctList = new DistinctList<T>();
            foreach (var item in items)
            {
                distinctList.Add(item);
            }

            return distinctList;
        }

        private ImmutableList<T> ToFactor<T>(IEnumerable<T> zeroBasedLevels, IEnumerable<int> oneBasedIndexes)
            where T : class
        {
            var levels = zeroBasedLevels
                .Prepend(null)
                .ToImmutable();
            var indexes = IntegerList.FromIntegers(oneBasedIndexes);
            return new Factor<T>(levels, indexes).MaybeConstant();
        }

        protected bool Equals(SpectrumMetadatas other)
        {
            if (Count != other.Count)
            {
                return false;
            }

            for (int i = 0; i < Count; i++)
            {
                var thisSpectrum = this[i];
                var otherSpectrum = other[i];
                if (!Equals(thisSpectrum, otherSpectrum))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((SpectrumMetadatas)obj);
        }

        public override int GetHashCode()
        {
            return CollectionUtil.GetHashCodeDeep(this);
        }

        private readonly struct PrecursorWithLevel : IEquatable<PrecursorWithLevel>
        {
            public PrecursorWithLevel(int msLevel, SpectrumPrecursor spectrumPrecursor)
            {
                MsLevel = msLevel;
                Precursor = spectrumPrecursor;
            }

            public int MsLevel { get; }
            public SpectrumPrecursor Precursor { get; }

            public bool Equals(PrecursorWithLevel other)
            {
                return MsLevel == other.MsLevel && Precursor.Equals(other.Precursor);
            }

            public override bool Equals(object obj)
            {
                return obj is PrecursorWithLevel other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (MsLevel * 397) ^ Precursor.GetHashCode();
                }
            }
        }

        private struct ScanId
        {
            private object _value; // either string or IList<int>

            public ScanId(string s)
            {
                var parts = GetScanIdParts(s);
                if (parts == null)
                {
                    _value = s;
                }
                else
                {
                    _value = IntegerList.FromIntegers(parts);
                }
            }

            public ScanId(ResultFileMetaDataProto.Types.SpectrumMetadata spectrumMetadataProto)
            {
                if (spectrumMetadataProto.ScanIdParts.Count > 0)
                {
                    _value = IntegerList.FromIntegers(spectrumMetadataProto.ScanIdParts);
                }
                else
                {
                    _value = spectrumMetadataProto.ScanIdText;
                }
            }

            public override string ToString()
            {
                if (_value is IList<int> parts)
                {
                    return string.Join(@".", parts);
                }

                return _value as string ?? string.Empty;
            }

            public void SetInProto(ResultFileMetaDataProto.Types.SpectrumMetadata spectrumMetadataProto)
            {
                if (_value is IList<int> parts)
                {
                    spectrumMetadataProto.ScanIdParts.AddRange(parts);
                }
                else
                {
                    spectrumMetadataProto.ScanIdText = _value as string ?? string.Empty;
                }
            }

            private static IList<int> GetScanIdParts(string scanId)
            {
                var parts = scanId.Split('.');
                var intParts = new List<int>();
                foreach (var part in parts)
                {
                    if (!int.TryParse(part, out int intPart))
                    {
                        return null;
                    }

                    if (!Equals(part, intPart.ToString(CultureInfo.InvariantCulture)))
                    {
                        return null;
                    }

                    intParts.Add(intPart);
                }

                return intParts;
            }

        }
    }
}