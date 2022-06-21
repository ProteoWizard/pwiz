using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Web;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.Spectra;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public abstract class SpectrumClassColumn
    {
        public static readonly SpectrumClassColumn Ms1Precursors =
            new PrecursorsColumn(nameof(SpectrumClass.Ms1Precursors), spectrum => SpectrumPrecursors.FromPrecursors(spectrum.GetPrecursors(1)), () => "MS1");

        public static readonly SpectrumClassColumn Ms2Precursors =
            new PrecursorsColumn(nameof(SpectrumClass.Ms2Precursors), 
                spectrum => SpectrumPrecursors.FromPrecursors(spectrum.GetPrecursors(2)), ()=>"MS2");

        public static readonly SpectrumClassColumn ScanDescription =
            MakeColumn(nameof(SpectrumClass.ScanDescription), spectrum => spectrum.ScanDescription);

        public static readonly SpectrumClassColumn CollisionEnergy =
            MakeColumn(nameof(SpectrumClass.CollisionEnergy), spectrum => spectrum.CollisionEnergy);

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

        public static readonly ImmutableList<SpectrumClassColumn> ALL = ImmutableList.ValueOf(new[]
        {
            Ms1Precursors, Ms2Precursors, ScanDescription, CollisionEnergy, ScanWindowWidth, CompensationVoltage, PresetScanConfiguration, MsLevel
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
    }
}
