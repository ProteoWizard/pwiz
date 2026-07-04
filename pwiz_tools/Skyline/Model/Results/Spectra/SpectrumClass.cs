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

using System.Collections.Generic;
using JetBrains.Annotations;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.DataBinding.Filtering;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Spectra
{
    /// <summary>
    /// Represents a set of properties which a group of spectra might have in common.
    /// </summary>
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public class SpectrumClass
    {
        public SpectrumClass(SpectrumClassKey classKey)
        {
            for (int i = 0; i < classKey.Columns.Count; i++)
            {
                var value = classKey.Values[i];
                if (value != null)
                {
                    classKey.Columns[i].SetValue(this, value);
                }
            }
        }

        /// <summary>
        /// Values of the dynamic mzML CV/user-parameter columns for this class, keyed by the column's
        /// encoded name. These columns are not fixed properties of this POCO (they are only known at
        /// runtime), so they are carried here and read back by the dynamic column's
        /// <see cref="SpectrumClassColumn.GetValue(SpectrumClass)"/>; the grid binds them as lookups into
        /// this dictionary.
        /// </summary>
        public IDictionary<string, object> CvValues { get; } = new Dictionary<string, object>();

        [Format(Formats.Mz)]
        public SpectrumPrecursors Ms1Precursors
        {
            get; private set;
        }

        [Format(Formats.Mz)]
        public SpectrumPrecursors Ms2Precursors
        {
            get; private set;
        }

        public string ScanDescription
        {
            get; private set;
        }

        public FormattableList<PositiveNumber> CollisionEnergy
        {
            get; private set;
        }

        public double? CompensationVoltage
        {
            get;
            private set;
        }

        [Format(Formats.Mz)]
        public double? ScanWindowWidth { get; private set; }

        public int PresetScanConfiguration { get; private set; }

        public int MsLevel { get; private set; }

        public string Analyzer { get; private set; }

        [Format(Formats.Mz)]
        public double? IsolationWindowWidth { get; private set; }
        
        public ListColumnValue<string> DissociationMethod { get; private set; }
        
        [Format(Formats.Mz)]
        public double? ConstantNeutralLoss { get; private set; } // Negative value means neutral gain

        public double? SourceOffsetVoltage { get; private set; }
    }
}
