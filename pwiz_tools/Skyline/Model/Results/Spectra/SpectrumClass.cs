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
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Hibernate;

namespace pwiz.Skyline.Model.Results.Spectra
{
    /// <summary>
    /// Represents a set of properties which a group of spectra might have in common.
    /// </summary>
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

        public double? CollisionEnergy
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
    }
}
