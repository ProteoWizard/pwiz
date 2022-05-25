using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.Spectra;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumClassColumn
    {
        public static readonly SpectrumClassColumn Ms1Precursors =
            MakeColumn(nameof(SpectrumClass.Ms1Precursors), spectrum => new SpectrumPrecursors(spectrum.GetPrecursors(1)));

        public static readonly SpectrumClassColumn Ms2Precursors =
            MakeColumn(nameof(SpectrumClass.Ms2Precursors),
                spectrum => new SpectrumPrecursors(spectrum.GetPrecursors(2)));

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

        public static readonly ImmutableList<SpectrumClassColumn> ALL = ImmutableList.ValueOf(new[]
        {
            Ms1Precursors, Ms2Precursors, ScanDescription, CollisionEnergy, ScanWindowWidth, CompensationVoltage
        });

        private Func<SpectrumMetadata, object> _getter;
        private PropertyInfo _propertyInfo;
        private SpectrumClassColumn(string columnName, Func<SpectrumMetadata, object> getter, Type valueType)
        {
            ColumnName = columnName;
            _getter = getter;
            _propertyInfo = typeof(SpectrumClass).GetProperty(columnName);
            ValueType = valueType;
            if (_propertyInfo == null)
            {
                throw new ArgumentException(string.Format(@"No such property {0}", columnName), nameof(columnName));
            }
            if (!TypesMatch(_propertyInfo.PropertyType, valueType))
            {
                throw new ArgumentException(string.Format(@"Property {0} has type {1} instead of {2}", columnName,
                    _propertyInfo.PropertyType, valueType));
            }
        }

        public Type ValueType { get; }

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

        public string ColumnName { get; }

        public PropertyPath PropertyPath
        {
            get
            {
                return PropertyPath.Root.Property(ColumnName);
            }
        }

        public object GetValue(SpectrumMetadata spectrumMetadata)
        {
            return _getter(spectrumMetadata);
        }

        public object GetValue(SpectrumClass spectrumClass)
        {
            return _propertyInfo.GetValue(spectrumClass);
        }

        public void SetValue(SpectrumClass spectrumClass, object value)
        {
            _propertyInfo.SetValue(spectrumClass, value);
        }

        public string GetLocalizedColumnName(CultureInfo cultureInfo)
        {
            return ColumnCaptions.ResourceManager.GetString(ColumnName, cultureInfo) ?? ColumnName;
        }

        public override string ToString()
        {
            return GetLocalizedColumnName(CultureInfo.CurrentUICulture);
        }

        public static SpectrumClassColumn MakeColumn<T>(string name, Func<SpectrumMetadata, T> getter)
        {
            return new SpectrumClassColumn(name, key => getter(key), typeof(T));
        }

        public static SpectrumClassColumn FindColumn(PropertyPath propertyPath)
        {
            return ALL.FirstOrDefault(col => Equals(propertyPath, col.PropertyPath));
        }
    }
}
