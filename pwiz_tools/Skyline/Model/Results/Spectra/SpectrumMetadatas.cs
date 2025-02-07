using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results.ProtoBuf;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumMetadatas : AbstractReadOnlyList<SpectrumMetadata>
    {
        private ImmutableList<double> _retentionTimes;
        private ImmutableList<double?> _totalIonCurrents;
        private ImmutableList<double?> _injectionTimes;
        private ImmutableList<int> _presetScanConfigurations;
        private ImmutableList<int[]> _scanIdInts;
        private ImmutableList<string> _scanIdStrings;
        private ImmutableList<string> _scanDescriptions;
        private ImmutableList<string> _analyzers;
        private ImmutableList<Tuple<double, double>> _scanWindows;
        private ImmutableList<ImmutableList<PrecursorWithLevel>> _spectrumPrecursors;
        private ImmutableList<double?> _compensationVoltages;
        private ImmutableList<bool> _negativeCharges;
        private ImmutableList<int> _msLevels;

        public SpectrumMetadatas(IEnumerable<SpectrumMetadata> enumerable)
        {
            var collection = enumerable as ICollection<SpectrumMetadata> ?? enumerable.ToList();
            _retentionTimes = collection.Select(m => m.RetentionTime).ToImmutable();
            _totalIonCurrents = ImmutableOptimizations.Nullables(collection.Select(m => m.TotalIonCurrent))
                .MaybeConstant();
            _injectionTimes = collection.Select(m => m.InjectionTime).Nullables().MaybeConstant();
            _presetScanConfigurations =
                IntList.ValueOf(collection.Select(m => m.PresetScanConfiguration)).MaybeConstant();
            _scanIdInts = ImmutableList.ValueOf(collection.Select(m=>GetScanIdParts(m.Id)?.ToArray()));
            if (_scanIdInts.Contains(null))
            {
                _scanIdInts = null;
                _scanIdStrings = collection.Select(m => m.Id).ToImmutable();
            }
            _scanDescriptions = collection.Select(m => m.ScanDescription).ToFactor().MaybeConstant();
            _analyzers = collection.Select(m => m.Analyzer).ToFactor().MaybeConstant();
            _scanWindows = collection.Select(GetScanWindow).ToFactor().MaybeConstant();
            _spectrumPrecursors = collection.Select(GetPrecursors).ToFactor().MaybeConstant();
            _compensationVoltages = collection.Select(m => m.CompensationVoltage).ToFactor().MaybeConstant();
            _negativeCharges = collection.Select(m => m.NegativeCharge).ToFactor().MaybeConstant();
            _msLevels = collection.Select(m => m.MsLevel).ToFactor().MaybeConstant();
        }

        public SpectrumMetadatas(ResultFileMetaDataProto proto)
        {
            _retentionTimes = proto.Spectra.Select(s => s.RetentionTime).ToImmutable();
            _totalIonCurrents = proto.Spectra.Select(s => s.TotalIonCurrent).Nullables().MaybeConstant();
            _injectionTimes = proto.Spectra.Select(s => s.InjectionTime).Nullables().MaybeConstant();
            _presetScanConfigurations = IntList.ValueOf(proto.Spectra.Select(s => s.PresetScanConfiguration));
            var scanIdPartsList = new List<int[]>();
            var precursors = new List<PrecursorWithLevel>();
            foreach (var protoPrecursor in proto.Precursors)
            {
                var spectrumPrecursor = new SpectrumPrecursor(new SignedMz(protoPrecursor.TargetMz));
                if (protoPrecursor.CollisionEnergy != 0)
                {
                    spectrumPrecursor = spectrumPrecursor.ChangeCollisionEnergy(protoPrecursor.CollisionEnergy);
                }

                if (protoPrecursor.IsolationWindowLower != 0 || protoPrecursor.IsolationWindowUpper != 0)
                {
                    spectrumPrecursor =
                        spectrumPrecursor.ChangeIsolationWindowWidth(protoPrecursor.IsolationWindowLower,
                            protoPrecursor.IsolationWindowUpper);
                }
                precursors.Add(new PrecursorWithLevel(protoPrecursor.MsLevel, spectrumPrecursor));
            }
            foreach (var spectrum in proto.Spectra)
            {
                if (!string.IsNullOrEmpty(spectrum.ScanIdText))
                {
                    break;
                }
                scanIdPartsList.Add(spectrum.ScanIdParts.ToArray());
            }

            if (scanIdPartsList.Count == proto.Spectra.Count)
            {
                _scanIdInts = scanIdPartsList.ToImmutable();
            }
            else
            {
                var scanIdStrings = new List<string>();
                foreach (var spectrum in proto.Spectra)
                {
                    if (string.IsNullOrEmpty(spectrum.ScanIdText))
                    {
                        scanIdStrings.Add(MakeScanId(spectrum.ScanIdParts));
                    }
                    else
                    {
                        scanIdStrings.Add(spectrum.ScanIdText);
                    }
                }
                _scanIdStrings = scanIdStrings.ToImmutable();
            }

            _scanDescriptions = ToFactor(proto.ScanDescriptions, proto.Spectra.Select(s => s.ScanDescriptionIndex));
            _analyzers = ToFactor(proto.Analyzers, proto.Spectra.Select(s => s.AnalyzerIndex));
            _scanWindows = ToFactor(proto.ScanWindows.Select(sw => Tuple.Create(sw.LowerLimit, sw.UpperLimit)),
                proto.Spectra.Select(s => s.ScanWindowIndex));
            _compensationVoltages = proto.Spectra.Select(s => s.CompensationVoltage).Nullables().MaybeConstant();
            _negativeCharges = proto.Spectra.Select(s => s.NegativeCharge).Booleans();
            var msLevels = new List<int>();
            var spectrumPrecursors = new List<ImmutableList<PrecursorWithLevel>>();
            foreach (var spectrum in proto.Spectra)
            {
                spectrumPrecursors.Add(spectrum.PrecursorIndex.Select(i => precursors[i]).ToImmutable());
                var msLevel = spectrum.MsLevel;
                if (msLevel == 0)
                {
                    msLevel = spectrum.PrecursorIndex.Select(p => proto.Precursors[p].MsLevel + 1).Prepend(1).Max();
                }
                msLevels.Add(msLevel);
            }
            _spectrumPrecursors = spectrumPrecursors.ToFactor().MaybeConstant();
            _msLevels = IntList.ValueOf(msLevels);
        }

        public override SpectrumMetadata this[int index]
        {
            get
            {
                var scanId = _scanIdStrings?[index] ?? MakeScanId(_scanIdInts[index]);
                var spectrumMetadata = new SpectrumMetadata(scanId, _retentionTimes[index])
                    .ChangeAnalyzer(_analyzers[index])
                    .ChangeCompensationVoltage(_compensationVoltages[index])
                    .ChangeInjectionTime(_injectionTimes[index])
                    .ChangeNegativeCharge(_negativeCharges[index])
                    .ChangePresetScanConfiguration(_presetScanConfigurations[index])
                    .ChangeScanDescription(_scanDescriptions[index])
                    .ChangeTotalIonCurrent(_totalIonCurrents[index])
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
            var analyzerFactor = ToFactorIncludeNull(_analyzers);
            proto.Analyzers.AddRange(analyzerFactor.Levels.Skip(1));
            var precursors = ToDistinctList(_spectrumPrecursors.SelectMany(l => l));
            proto.Precursors.AddRange(precursors.Select(p => new ResultFileMetaDataProto.Types.Precursor
            {
                MsLevel = p.MsLevel,
                CollisionEnergy = p.Precursor.CollisionEnergy ?? 0,
                IsolationWindowLower = p.Precursor.IsolationWindowLowerWidth ?? 0,
                IsolationWindowUpper = p.Precursor.IsolationWindowUpperWidth ?? 0,
                TargetMz = p.Precursor.PrecursorMz
            }));
            var scanDescriptions = ToFactorIncludeNull(_scanDescriptions);
            proto.ScanDescriptions.AddRange(scanDescriptions.Levels.Skip(1));
            var scanWindows = ToFactorIncludeNull(_scanWindows);
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
                    PrecursorIndex = { _spectrumPrecursors[index].Select(precursors.IndexOf) },
                    ScanDescriptionIndex = scanDescriptions.LevelIndices[index],
                    ScanWindowIndex = scanWindows.LevelIndices[index],
                    ScanIdText = _scanIdStrings?.ElementAtOrDefault(index) ?? string.Empty,
                    ScanIdParts = { _scanIdInts[index] ?? Array.Empty<int>() },
                    CompensationVoltage = _compensationVoltages[index]
                };
                proto.Spectra.Add(spectrum);
            }

            return proto;
        }

        private IEnumerable<int> GetScanIdParts(string scanId)
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

        private string MakeScanId(IEnumerable<int> parts)
        {
            return string.Join(@".", parts);
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

        private static Factor<T> ToFactorIncludeNull<T>(ImmutableList<T> list) where T : class
        {
            return Factor.ToFactorIncludingLevels(list, ImmutableList.Singleton((T)null));
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
            var levels = zeroBasedLevels.Prepend(null).ToImmutable();
            var indexes = IntList.ValueOf(oneBasedIndexes);
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

        struct PrecursorWithLevel
        {
            public PrecursorWithLevel(int msLevel, SpectrumPrecursor spectrumPrecursor)
            {
                MsLevel = msLevel;
                Precursor = spectrumPrecursor;
            }

            public int MsLevel { get; }
            public SpectrumPrecursor Precursor { get; }
        }
    }
}