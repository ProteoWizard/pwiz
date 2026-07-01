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
                TryDecodeCvParamColumnName(propertyPath.Name, out var accession, out var unit))
            {
                return new CvParamColumn(accession, null, unit, false);
            }

            return null;
        }

        /// <summary>
        /// Encoded-column-name prefix identifying a dynamic mzML CV/user-parameter column.
        /// Letters only, so the whole encoded name is a single alphanumeric identifier that a
        /// <see cref="PropertyPath"/> and the filter-string serializer round-trip without quoting.
        /// </summary>
        private const string CV_PARAM_PREFIX = @"cvparam";

        // Separates accession from unit inside the encoded payload. A control character that never
        // appears in a CV accession or unit name, so decoding can split on it unambiguously.
        private const char CV_PARAM_SEPARATOR = '\u001F';

        /// <summary>
        /// Builds a dynamic column for the uninterpreted mzML CV/user parameter identified by
        /// <paramref name="accession"/> and <paramref name="unit"/> (the "split by unit" identity).
        /// <paramref name="name"/> is the friendly display name (from the term; null when the column
        /// is reconstructed from a saved filter). <paramref name="isNumeric"/> drives the column type
        /// offered in the filter editor; the extraction predicate infers numeric vs. string from the
        /// operator and operand independently (see <see cref="SpectrumClassFilter"/>).
        /// </summary>
        public static SpectrumClassColumn CvParam(string accession, string name, string unit, bool isNumeric)
        {
            return new CvParamColumn(accession, name, unit, isNumeric);
        }

        /// <summary>
        /// True if <paramref name="column"/> is a dynamic mzML CV/user-parameter column.
        /// </summary>
        public static bool IsCvParamColumn(SpectrumClassColumn column)
        {
            return column is CvParamColumn;
        }

        private static string EncodeCvParamColumnName(string accession, string unit)
        {
            var payload = Encoding.UTF8.GetBytes(accession + CV_PARAM_SEPARATOR + (unit ?? string.Empty));
            var hex = new StringBuilder(CV_PARAM_PREFIX, CV_PARAM_PREFIX.Length + payload.Length * 2);
            foreach (var b in payload)
            {
                hex.Append(b.ToString(@"x2", CultureInfo.InvariantCulture));
            }
            return hex.ToString();
        }

        private static bool TryDecodeCvParamColumnName(string columnName, out string accession, out string unit)
        {
            accession = null;
            unit = null;
            if (columnName == null || !columnName.StartsWith(CV_PARAM_PREFIX))
            {
                return false;
            }

            var hex = columnName.Substring(CV_PARAM_PREFIX.Length);
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

            var payload = Encoding.UTF8.GetString(bytes);
            int sep = payload.IndexOf(CV_PARAM_SEPARATOR);
            if (sep < 0)
            {
                return false;
            }

            accession = payload.Substring(0, sep);
            var unitText = payload.Substring(sep + 1);
            unit = unitText.Length == 0 ? null : unitText;
            return true;
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
        /// A dynamic column for one uninterpreted mzML CV/user parameter, identified by
        /// <see cref="Accession"/> and <see cref="Unit"/> (the "split by unit" identity). Unlike the
        /// static columns it is not bound to a <see cref="SpectrumClass"/> property: it reads its value
        /// straight from <see cref="SpectrumMetadata.OtherParams"/>. The spectrum-filter predicate reads
        /// it via <see cref="GetValue(SpectrumMetadata)"/>; the <see cref="SpectrumClass"/> POCO
        /// projection does not carry it, so the <see cref="SpectrumClass"/> accessors throw.
        /// </summary>
        private class CvParamColumn : SpectrumClassColumn
        {
            private readonly string _accession;
            private readonly string _name;
            private readonly string _unit;
            private readonly bool _isNumeric;

            public CvParamColumn(string accession, string name, string unit, bool isNumeric)
            {
                _accession = accession;
                _name = name;
                _unit = unit;
                _isNumeric = isNumeric;
                ColumnName = EncodeCvParamColumnName(accession, unit);
            }

            public string Accession
            {
                get { return _accession; }
            }

            public string Unit
            {
                get { return _unit; }
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
                    if (Equals(term.Accession, _accession) &&
                        Equals(term.Unit ?? string.Empty, _unit ?? string.Empty))
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
                var displayName = string.IsNullOrEmpty(_name) ? _accession : _name;
                return string.IsNullOrEmpty(_unit)
                    ? displayName
                    : string.Format(CultureInfo.CurrentCulture, @"{0} ({1})", displayName, _unit);
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
