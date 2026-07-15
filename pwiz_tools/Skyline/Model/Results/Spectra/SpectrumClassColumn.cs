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
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Filtering;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Spectra
{
    /// <summary>
    /// List of the columns on which one can apply a <see cref="SpectrumClassFilter"/>.
    /// Each of these columns corresponds to a property in the class <see cref="SpectrumClass"/>.
    /// </summary>
    public abstract class SpectrumClassColumn
    {
        public static readonly SpectrumClassColumn Ms1Precursors =
            new PrecursorsColumn(nameof(SpectrumClass.Ms1Precursors),
                spectrum => SpectrumPrecursorMzs(spectrum.GetPrecursors(1)),
                () => SpectraResources.SpectrumClassColumn_Ms1Precursors_MS1);

        public static readonly SpectrumClassColumn Ms2Precursors =
            new PrecursorsColumn(nameof(SpectrumClass.Ms2Precursors),
                spectrum => SpectrumPrecursorMzs(spectrum.GetPrecursors(2)),
                () => SpectraResources.SpectrumClassColumn_Ms2Precursors_MS2);

        public static readonly SpectrumClassColumn ScanDescription =
            MakeColumn(nameof(SpectrumClass.ScanDescription), spectrum => spectrum.ScanDescription);

        public static readonly SpectrumClassColumn CollisionEnergy =
            MakeColumn(nameof(SpectrumClass.CollisionEnergy), GetCollisionEnergy);

        public static readonly SpectrumClassColumn ScanWindowWidth =
            MakeColumn(nameof(SpectrumClass.ScanWindowWidth),
                spectrum => spectrum.ScanWindowUpperLimit - spectrum.ScanWindowLowerLimit);

        public static readonly SpectrumClassColumn CompensationVoltage =
            MakeColumn(nameof(SpectrumClass.CompensationVoltage),
                spectrum => spectrum.CompensationVoltage);

        public static readonly SpectrumClassColumn PresetScanConfiguration =
            MakeColumn(nameof(SpectrumClass.PresetScanConfiguration),
                spectrum => spectrum.PresetScanConfiguration);

        public static readonly SpectrumClassColumn MsLevel =
            MakeColumn(nameof(SpectrumClass.MsLevel), spectrum => spectrum.MsLevel);

        public static readonly SpectrumClassColumn Analyzer =
            MakeColumn(nameof(SpectrumClass.Analyzer), spectrum => spectrum.Analyzer);

        public static readonly SpectrumClassColumn IsolationWindowWidth = MakeColumn(
            nameof(SpectrumClass.IsolationWindowWidth),
            spectrum => GetIsolationWindowWidth(spectrum.GetPrecursors(1)));

        public static readonly SpectrumClassColumn DissociationMethod = MakeColumn(
            nameof(SpectrumClass.DissociationMethod), GetDissociationMethod);

        public static readonly SpectrumClassColumn ConstantNeutralLoss = MakeColumn(
            nameof(SpectrumClass.ConstantNeutralLoss), spectrum => spectrum.ConstantNeutralLoss);

        public static readonly SpectrumClassColumn SourceOffsetVoltage = MakeColumn(
            nameof(SpectrumClass.SourceOffsetVoltage), spectrum => spectrum.SourceOffsetVoltage);

        public static readonly ImmutableList<SpectrumClassColumn> ALL = ImmutableList.ValueOf(new[]
        {
            Ms1Precursors, Ms2Precursors, ScanDescription, CollisionEnergy, ScanWindowWidth, CompensationVoltage,
            PresetScanConfiguration, MsLevel, Analyzer, IsolationWindowWidth, DissociationMethod, ConstantNeutralLoss, SourceOffsetVoltage
        });

        public static readonly ImmutableList<SpectrumClassColumn> MS1 = ImmutableList.ValueOf(new[]
        {
            ScanDescription, ScanWindowWidth, CompensationVoltage, PresetScanConfiguration, Analyzer, SourceOffsetVoltage
        });

        /// <summary>
        /// This method gets used by System.Windows.Forms.Formatter.InvokeStringParseMethod
        /// </summary>
        public static SpectrumClassColumn Parse(string value, IFormatProvider formatProvider)
        {
            var cultureInfo = (formatProvider as CultureInfo) ?? CultureInfo.CurrentUICulture;
            return ALL.FirstOrDefault(column => value == column.GetLocalizedColumnName(cultureInfo));
        }

        public abstract Type ValueType { get; }

        public abstract string ColumnName { get; }

        public PropertyPath PropertyPath
        {
            get
            {
                return PropertyPath.Root.Property(ColumnName);
            }
        }

        public abstract object GetValue(SpectrumMetadata spectrumMetadata);

        public abstract object GetValue(SpectrumClass spectrumClass);

        public abstract void SetValue(SpectrumClass spectrumClass, object value);

        public virtual string GetLocalizedColumnName(CultureInfo cultureInfo)
        {
            return ColumnCaptions.ResourceManager.GetString(ColumnName, cultureInfo) ?? ColumnName;
        }

        public virtual string GetAbbreviatedColumnName()
        {
            return GetLocalizedColumnName(CultureInfo.CurrentUICulture);
        }


        public virtual string FormatAbbreviatedValue(object value)
        {
            return (value ?? string.Empty).ToString();
        }

        public override string ToString()
        {
            return GetLocalizedColumnName(CultureInfo.CurrentUICulture);
        }

        public static SpectrumClassColumn FindColumn(PropertyPath propertyPath)
        {
            var column = ALL.FirstOrDefault(col => Equals(propertyPath, col.PropertyPath));
            if (column != null)
            {
                return column;
            }

            // A dynamic mzML CV/user-parameter column is not in ALL; reconstruct it from its
            // encoded path so saved filters resolve (and validate) before any file is imported.
            if (propertyPath.IsProperty && propertyPath.Parent.IsRoot &&
                TryDecodeCvParamColumnName(propertyPath.Name, out var accession))
            {
                return new CvParamColumn(accession, null, false);
            }

            return null;
        }

        // Encoded-column-name prefixes identifying a dynamic mzML CV/user-parameter column. Both keep
        // the whole encoded name a single alphanumeric identifier, so a PropertyPath and the
        // filter-string serializer round-trip it without quoting. A controlled-vocabulary term
        // ("MS:1000505") encodes readably as "cvid" + accession without the colon ("cvidMS1000505"); a
        // vendor userParam (an arbitrary name, no CVID) encodes as "cvup" + the name's UTF-8 hex.
        private const string CV_ID_PREFIX = @"cvid";
        private const string CV_USERPARAM_PREFIX = @"cvup";

        /// <summary>
        /// Builds a dynamic column for the uninterpreted mzML CV/user parameter identified by
        /// <paramref name="accession"/> (a CV accession such as "MS:1000505", or a userParam name).
        /// <paramref name="name"/> is the friendly display name (null when the column is reconstructed
        /// from a saved filter). <paramref name="isNumeric"/> drives the column type offered in the
        /// filter editor; the extraction predicate infers numeric vs. string from the operator and
        /// operand independently (see <see cref="SpectrumClassFilter"/>).
        /// </summary>
        public static SpectrumClassColumn CvParam(string accession, string name, bool isNumeric)
        {
            return new CvParamColumn(accession, name, isNumeric);
        }

        /// <summary>
        /// True if <paramref name="column"/> is a dynamic mzML CV/user-parameter column.
        /// </summary>
        public static bool IsCvParamColumn(SpectrumClassColumn column)
        {
            return column is CvParamColumn;
        }

        /// <summary>
        /// Builds the dynamic CV/user-parameter columns present across the given spectra: one per
        /// distinct accession (the term's CV accession or userParam name). A column is typed numeric when
        /// every value seen for it parses as an invariant number and at least one value was seen, else
        /// string (the runtime type inference the design calls for, since these terms carry no declared
        /// type and no unit is available). The friendly name comes from the term. Ordered by display
        /// name for the UI.
        /// </summary>
        public static IList<SpectrumClassColumn> DiscoverCvColumns(IEnumerable<SpectrumMetadata> spectra)
        {
            var discovered = new Dictionary<string, CvColumnDiscovery>();
            foreach (var spectrum in spectra)
            {
                foreach (var term in spectrum.OtherParams)
                {
                    if (!discovered.TryGetValue(term.Accession, out var info))
                    {
                        info = new CvColumnDiscovery(term.Accession);
                        discovered.Add(term.Accession, info);
                    }
                    info.Add(term);
                }
            }

            return discovered.Values
                .Select(info => (SpectrumClassColumn)new CvParamColumn(info.Accession, info.Name, info.IsNumeric))
                .OrderBy(column => column.GetLocalizedColumnName(CultureInfo.CurrentCulture), StringComparer.CurrentCulture)
                .ToList();
        }

        /// <summary>
        /// The dynamic CV/user-parameter columns discovered from a document's imported results (the
        /// per-file metadata persisted in the chromatogram cache). Empty when nothing has been imported.
        /// </summary>
        public static IList<SpectrumClassColumn> DiscoverCvColumns(SrmDocument document)
        {
            var measuredResults = document.Settings.MeasuredResults;
            if (measuredResults == null)
            {
                return Array.Empty<SpectrumClassColumn>();
            }

            var spectra = measuredResults.Chromatograms.SelectMany(chromatogramSet => chromatogramSet.MSDataFilePaths)
                .Distinct()
                .Select(path => measuredResults.GetResultFileMetaData(path))
                .Where(metadata => metadata != null)
                .SelectMany(metadata => metadata.SpectrumMetadatas);
            return DiscoverCvColumns(spectra);
        }

        /// <summary>
        /// The catalog of uninterpreted mzML CV terms that can appear on a spectrum, as dynamic columns,
        /// so the filter editor can offer them before any data is imported. Typeless: the editor offers
        /// every operator and the predicate infers numeric vs. string from the operator and operand.
        /// </summary>
        public static IList<SpectrumClassColumn> GetCvColumnCatalog()
        {
            return MsDataFileImpl.GetSpectrumCvTermCatalog()
                .Select(term => (SpectrumClassColumn)new CvParamColumn(term.Accession, term.Name, false))
                .ToList();
        }

        /// <summary>
        /// The CV/user-parameter columns to offer in the filter editor for <paramref name="document"/>:
        /// the ontology catalog (always available) plus any discovered from imported data (vendor
        /// userParams, and CV terms typed from their observed values, which take precedence over the
        /// typeless catalog entry with the same accession).
        /// </summary>
        public static IList<SpectrumClassColumn> GetEditorCvColumns(SrmDocument document)
        {
            var byColumnName = new Dictionary<string, SpectrumClassColumn>();
            foreach (var column in GetCvColumnCatalog())
            {
                byColumnName[column.ColumnName] = column;
            }
            foreach (var column in DiscoverCvColumns(document))
            {
                byColumnName[column.ColumnName] = column;
            }
            return byColumnName.Values
                .OrderBy(column => column.GetLocalizedColumnName(CultureInfo.CurrentCulture), StringComparer.CurrentCulture)
                .ToList();
        }

        /// <summary>
        /// Accumulates, across the spectra that carry one term (keyed by accession), its friendly name
        /// and whether every value seen parses as a number (so the discovered column can be typed).
        /// </summary>
        private class CvColumnDiscovery
        {
            private bool _sawValue;
            private bool _allNumeric = true;

            public CvColumnDiscovery(string accession)
            {
                Accession = accession;
            }

            public string Accession { get; }
            public string Name { get; private set; }
            public bool IsNumeric => _sawValue && _allNumeric;

            public void Add(SpectrumMetadataTerm term)
            {
                if (string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(term.Name))
                {
                    Name = term.Name;
                }

                if (!string.IsNullOrEmpty(term.Value))
                {
                    _sawValue = true;
                    if (!double.TryParse(term.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        _allNumeric = false;
                    }
                }
            }
        }

        private static string EncodeCvParamColumnName(string accession)
        {
            if (TrySplitCvid(accession, out var prefix, out var number))
            {
                return CV_ID_PREFIX + prefix + number;
            }

            var payload = Encoding.UTF8.GetBytes(accession);
            var hex = new StringBuilder(CV_USERPARAM_PREFIX, CV_USERPARAM_PREFIX.Length + payload.Length * 2);
            foreach (var b in payload)
            {
                hex.Append(b.ToString(@"x2", CultureInfo.InvariantCulture));
            }
            return hex.ToString();
        }

        private static bool TryDecodeCvParamColumnName(string columnName, out string accession)
        {
            accession = null;
            if (columnName == null)
            {
                return false;
            }

            if (columnName.StartsWith(CV_ID_PREFIX))
            {
                var rest = columnName.Substring(CV_ID_PREFIX.Length);
                int split = 0;
                while (split < rest.Length && char.IsLetter(rest[split]))
                {
                    split++;
                }
                if (split == 0 || split == rest.Length || !rest.Skip(split).All(char.IsDigit))
                {
                    return false;
                }
                accession = rest.Substring(0, split) + @":" + rest.Substring(split);
                return true;
            }

            if (columnName.StartsWith(CV_USERPARAM_PREFIX))
            {
                var hex = columnName.Substring(CV_USERPARAM_PREFIX.Length);
                if (hex.Length == 0 || hex.Length % 2 != 0)
                {
                    return false;
                }
                var bytes = new byte[hex.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                {
                    if (!byte.TryParse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                            out bytes[i]))
                    {
                        return false;
                    }
                }
                accession = Encoding.UTF8.GetString(bytes);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Splits a controlled-vocabulary accession ("MS:1000505") into its letter prefix ("MS") and
        /// digit number ("1000505"). Returns false for anything that is not a CV accession (e.g. a vendor
        /// userParam name), which the caller then encodes verbatim instead.
        /// </summary>
        private static bool TrySplitCvid(string accession, out string prefix, out string number)
        {
            prefix = null;
            number = null;
            if (string.IsNullOrEmpty(accession))
            {
                return false;
            }
            int colon = accession.IndexOf(':');
            if (colon <= 0 || colon == accession.Length - 1)
            {
                return false;
            }
            prefix = accession.Substring(0, colon);
            number = accession.Substring(colon + 1);
            return prefix.All(char.IsLetter) && number.All(char.IsDigit);
        }

        private static bool TypesMatch(Type propertyType, Type valueType)
        {
            if (propertyType == valueType)
            {
                return true;
            }

            if (valueType.IsValueType && propertyType == typeof(Nullable<>).MakeGenericType(valueType))
            {
                return true;
            }

            return false;
        }


        private abstract class Column<T> : SpectrumClassColumn
        {
            private PropertyInfo _propertyInfo;
            public Column(string columnName)
            {
                ColumnName = columnName;
                _propertyInfo = typeof(SpectrumClass).GetProperty(columnName);
                if (_propertyInfo == null)
                {
                    throw new ArgumentException(string.Format(@"No such property {0}", columnName), nameof(columnName));
                }
                if (!TypesMatch(_propertyInfo.PropertyType, typeof(T)))
                {
                    throw new ArgumentException(string.Format(@"Property {0} has type {1} instead of {2}", columnName,
                        _propertyInfo.PropertyType, typeof(T)));
                }
            }

            public override string ColumnName { get; }

            public override object GetValue(SpectrumMetadata spectrumMetadata)
            {
                return GetValueFromMetadata(spectrumMetadata);
            }

            protected abstract T GetValueFromMetadata(SpectrumMetadata spectrumMetadata);

            public override object GetValue(SpectrumClass spectrumClass)
            {
                return _propertyInfo.GetValue(spectrumClass);
            }

            public override void SetValue(SpectrumClass spectrumClass, object value)
            {
                _propertyInfo.SetValue(spectrumClass, value);
            }

            public override Type ValueType
            {
                get { return typeof(T); }
            }
        }

        private class SimpleColumn<T> : Column<T>
        {
            private Func<SpectrumMetadata, T> _getter;
            public SimpleColumn(string columnName, Func<SpectrumMetadata, T> getter) : base(columnName)
            {
                _getter = getter;
            }

            protected override T GetValueFromMetadata(SpectrumMetadata spectrumMetadata)
            {
                return _getter(spectrumMetadata);
            }
        }

        private static SimpleColumn<T> MakeColumn<T>(string name, Func<SpectrumMetadata, T> getter)
        {
            return new SimpleColumn<T>(name, key => getter(key));
        }

        private class PrecursorsColumn : SimpleColumn<SpectrumPrecursors>
        {
            private Func<string> _getAbbreviatedColumName;
            public PrecursorsColumn(string columnName, Func<SpectrumMetadata, SpectrumPrecursors> getter, Func<string> getAbbreviatedColumnName) : base(columnName, getter)
            {
                _getAbbreviatedColumName = getAbbreviatedColumnName;
            }

            public override string FormatAbbreviatedValue(object value)
            {
                if (value is SpectrumPrecursors spectrumPrecursors)
                {
                    return spectrumPrecursors.ToString(@"0", CultureInfo.CurrentCulture);
                }
                return base.FormatAbbreviatedValue(value);
            }

            public override string GetAbbreviatedColumnName()
            {
                return _getAbbreviatedColumName();
            }
        }

        /// <summary>
        /// A dynamic column for one uninterpreted mzML CV/user parameter, identified by its
        /// <see cref="Accession"/> (a CV accession such as "MS:1000505", or a userParam name). Unlike the
        /// static columns it is not bound to a <see cref="SpectrumClass"/> property: it reads its value
        /// straight from <see cref="SpectrumMetadata.OtherParams"/>. The spectrum-filter predicate reads
        /// it via <see cref="GetValue(SpectrumMetadata)"/>; the <see cref="SpectrumClass"/> POCO has no
        /// such property, so the <see cref="SpectrumClass"/> accessors are not supported.
        /// </summary>
        private class CvParamColumn : SpectrumClassColumn
        {
            private readonly string _accession;
            private readonly string _name;
            private readonly bool _isNumeric;

            public CvParamColumn(string accession, string name, bool isNumeric)
            {
                _accession = accession;
                _name = name;
                _isNumeric = isNumeric;
                ColumnName = EncodeCvParamColumnName(accession);
            }

            public string Accession
            {
                get { return _accession; }
            }

            public override string ColumnName { get; }

            public override Type ValueType
            {
                get { return _isNumeric ? typeof(double) : typeof(string); }
            }

            public override object GetValue(SpectrumMetadata spectrumMetadata)
            {
                foreach (var term in spectrumMetadata.OtherParams)
                {
                    if (Equals(term.Accession, _accession))
                    {
                        return term.Value;
                    }
                }
                return null;
            }

            public override object GetValue(SpectrumClass spectrumClass)
            {
                throw new NotSupportedException();
            }

            public override void SetValue(SpectrumClass spectrumClass, object value)
            {
                throw new NotSupportedException();
            }

            public override string GetLocalizedColumnName(CultureInfo cultureInfo)
            {
                if (string.IsNullOrEmpty(_name))
                {
                    return _accession;
                }
                // A controlled-vocabulary term shows its friendly name with the accession as the precise
                // cue, e.g. "base peak intensity (MS:1000505)"; a userParam (name == accession) shows just
                // its name.
                return Equals(_name, _accession)
                    ? _name
                    : string.Format(CultureInfo.CurrentCulture, @"{0} ({1})", _name, _accession);
            }

            public override bool Equals(object obj)
            {
                return obj is CvParamColumn other && Equals(ColumnName, other.ColumnName);
            }

            public override int GetHashCode()
            {
                return ColumnName.GetHashCode();
            }
        }

        /// <summary>
        /// Returns the collision energies found across the spectrum's precursor levels as a list (one
        /// entry per level, so the same value can repeat), or null if the spectrum reports none.
        /// </summary>
        private static FormattableList<PositiveNumber> GetCollisionEnergy(SpectrumMetadata spectrumMetadata)
        {
            var collisionEnergies = GetMsLevelValues(spectrumMetadata, precursor => precursor.CollisionEnergy)
                .OfType<double>().Select(ce => new PositiveNumber(ce)).ToList();
            if (collisionEnergies.Count == 0)
            {
                return null;
            }

            return new FormattableList<PositiveNumber>(collisionEnergies);
        }

        private static double? GetIsolationWindowWidth(IEnumerable<SpectrumPrecursor> precursors)
        {
            double totalWidth = 0;
            Tuple<double, double> currentRange = null;
            foreach (var precursor in precursors.OrderBy(precursor=>precursor.PrecursorMz - precursor.IsolationWindowLowerWidth))
            {
                double? lowerMz = precursor.PrecursorMz - precursor.IsolationWindowLowerWidth;
                double? upperMz = precursor.PrecursorMz + precursor.IsolationWindowUpperWidth;
                if (!lowerMz.HasValue || !upperMz.HasValue)
                {
                    continue;
                }
                if (lowerMz <= currentRange?.Item2)
                {
                    currentRange = Tuple.Create(currentRange.Item1,
                        Math.Max(currentRange.Item2, upperMz.Value));
                }
                else
                {
                    if (currentRange != null)
                    {
                        totalWidth += currentRange.Item2 - currentRange.Item1;
                    }

                    currentRange = Tuple.Create(lowerMz.Value, upperMz.Value);
                }
            }

            if (currentRange != null)
            {
                return totalWidth + currentRange.Item2 - currentRange.Item1;
            }
            Assume.AreEqual(0.0, totalWidth);
            return null;
        }
        
        /// <summary>
        /// Returns a list of the dissociation methods for the spectrum. This will typically be one dissociation method per MS Level.
        /// So, if the MS1 dissociation method was CID and the MS2 dissociation method was HCD, this would return ["CID", "HCD"]
        /// If the MS1 and MS2 levels both had "CID" dissociation method, then this would return ["CID", "CID"].
        /// In the rare situation where a particular MS Level had more than one dissociation method, the list returned would be a flattened list
        /// of the unique dissociation methods found at each level.
        /// </summary>
        private static ListColumnValue<string> GetDissociationMethod(SpectrumMetadata spectrumMetadata)
        {
            if (spectrumMetadata.MsLevel <= 1)
            {
                return null;
            }

            var dissociationMethods = GetMsLevelValues(spectrumMetadata, precursor => precursor.DissociationMethod);
            if (dissociationMethods.Count == 0)
            {
                return null;
            }
            return ListColumnValue.FromItems(dissociationMethods);
        }


        /// <summary>
        /// Returns a list of the unique values of a property at each MS Level.
        /// </summary>
        private static IList<T> GetMsLevelValues<T>(SpectrumMetadata spectrumMetadata,
            Func<SpectrumPrecursor, T> getValueFunc)
        {
            return Enumerable.Range(1, spectrumMetadata.MsLevel - 1)
                .Select(level =>
                    spectrumMetadata.GetPrecursors(level).Select(getValueFunc).Where(value =>
                            value is string str ? !string.IsNullOrEmpty(str) : value != null)
                        .Distinct())
                .SelectMany(list => list)
                .ToList();
        }

        /// <summary>
        /// Returns a SpectrumPrecursors with only the m/z values (i.e. ignoring collision energy and isolation window width)
        /// </summary>
        private static SpectrumPrecursors SpectrumPrecursorMzs(IEnumerable<SpectrumPrecursor> precursors)
        {
            return SpectrumPrecursors.FromPrecursors(precursors.Select(precursor =>
                new SpectrumPrecursor(precursor.PrecursorMz)));
        }
    }
}
