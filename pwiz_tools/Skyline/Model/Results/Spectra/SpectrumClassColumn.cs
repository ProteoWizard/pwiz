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
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.Spectra;
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

        public static readonly ImmutableList<SpectrumClassColumn> ALL = ImmutableList.ValueOf(new[]
        {
            Ms1Precursors, Ms2Precursors, ScanDescription, CollisionEnergy, ScanWindowWidth, CompensationVoltage,
            PresetScanConfiguration, MsLevel, Analyzer, IsolationWindowWidth
        });

        public static readonly ImmutableList<SpectrumClassColumn> MS1 = ImmutableList.ValueOf(new[]
        {
            ScanDescription, ScanWindowWidth, CompensationVoltage, PresetScanConfiguration, Analyzer
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

        public string GetLocalizedColumnName(CultureInfo cultureInfo)
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
            return ALL.FirstOrDefault(col => Equals(propertyPath, col.PropertyPath));
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
        /// If the spectrum has only one collision energy, then return that collision energy.
        /// Otherwise, return null.
        /// </summary>
        private static double? GetCollisionEnergy(SpectrumMetadata spectrumMetadata)
        {
            var collisionEnergies = spectrumMetadata.GetPrecursors(1).Select(precursor => precursor.CollisionEnergy)
                .OfType<double>().Distinct().ToList();
            if (collisionEnergies.Count == 1)
            {
                return collisionEnergies[0];
            }

            return null;
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
        /// Returns a SpectrumPrecursors with only the m/z values (i.e. ignoring collision energy and isolation window width)
        /// </summary>
        private static SpectrumPrecursors SpectrumPrecursorMzs(IEnumerable<SpectrumPrecursor> precursors)
        {
            return SpectrumPrecursors.FromPrecursors(precursors.Select(precursor =>
                new SpectrumPrecursor(precursor.PrecursorMz)));
        }
    }
}
