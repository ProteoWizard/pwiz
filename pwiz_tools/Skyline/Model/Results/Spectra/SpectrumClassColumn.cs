using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumClassColumn
    {
      
        private Func<SpectrumClassKey, object> _getter;
        private Func<SpectrumClassKey, SpectrumClassKey> _eraser;
        private SpectrumClassColumn(string columnName, Func<SpectrumClassKey, object> getter,
            Func<SpectrumClassKey, SpectrumClassKey> eraser)
        {
            ColumnName = columnName;
        }

        public string ColumnName { get; }

        public object GetValue(SpectrumClassKey spectrumClass)
        {
            return _getter(spectrumClass);
        }

        public SpectrumClassKey EraseValue(SpectrumClassKey spectrumClassKey)
        {
            return _eraser(spectrumClassKey);
        }

        public string GetLocalizedColumnName(CultureInfo cultureInfo)
        {
            return ColumnCaptions.ResourceManager.GetString(ColumnName, cultureInfo);
        }

        public static SpectrumClassColumn MakeColumn<T>(string name, Func<SpectrumClassKey, T> getter,
            Func<SpectrumClassKey, SpectrumClassKey> eraser)
        {
            return new SpectrumClassColumn(name, key => getter(key), eraser);
        }
    }
}
