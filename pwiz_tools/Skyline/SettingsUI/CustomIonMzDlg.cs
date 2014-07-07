/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class CustomIonMzDlg : FormEx
    {
        public CustomIonMzDlg(MeasuredIon measuredIon)
        {
            Assume.IsTrue(measuredIon.IsCustom);

            InitializeComponent();

            Icon = Resources.Skyline;

            foreach (var charge in measuredIon.Charges)
            {
                double mzMono = SequenceMassCalc.GetMZ(measuredIon.GetMassH(MassType.Monoisotopic), charge);
                double mzAverage = SequenceMassCalc.GetMZ(measuredIon.GetMassH(MassType.Average), charge);
                dataGridMz.Rows.Add(charge, mzMono, mzAverage);
            }
        }
    }
}
