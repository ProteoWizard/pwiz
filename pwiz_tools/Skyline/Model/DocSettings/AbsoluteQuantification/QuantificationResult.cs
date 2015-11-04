/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Collections;
using System.ComponentModel;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class QuantificationResult : Immutable, IComparable
    {
        [Format(Formats.GLOBAL_STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double? NormalizedArea{ get; private set; }

        public QuantificationResult ChangeNormalizedArea(double? intensity)
        {
            return ChangeProp(ImClone(this), im => im.NormalizedArea = intensity);
        }
        [Format(Formats.GLOBAL_STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double? CalculatedConcentration { get; private set; }

        [Format(Formats.CV, NullValue = TextUtil.EXCEL_NA)]
        public double? Accuracy { get; private set; }

        [Browsable(false)]
        public string Units { get; private set; }

        public QuantificationResult ChangeUnits(string units)
        {
            return ChangeProp(ImClone(this), im => im.Units = units);
        }

        public QuantificationResult ChangeCalculatedConcentration(double? calculatedConcentration)
        {
            return ChangeProp(ImClone(this), im => im.CalculatedConcentration = calculatedConcentration);
        }

        public QuantificationResult ChangeAccuracy(double? accuracy)
        {
            return ChangeProp(ImClone(this), im => im.Accuracy = accuracy);
        }

        public int CompareTo(object obj)
        {
            if (null == obj)
            {
                return 1;
            }
            return Comparer.Default.Compare(GetSortKey(), ((QuantificationResult) obj).GetSortKey());
        }

        private Tuple<double?, double?> GetSortKey()
        {
            return new Tuple<double?, double?>(CalculatedConcentration, NormalizedArea);
        }

        public override string ToString()
        {
            if (CalculatedConcentration.HasValue)
            {
                if (Units == null)
                {
                    return CalculatedConcentration.Value.ToString(Formats.CalibrationCurve);
                }
                return TextUtil.SpaceSeparate(CalculatedConcentration.Value.ToString(Formats.Concentration), Units);
            }
            else if (NormalizedArea.HasValue)
            {
                return string.Format(QuantificationStrings.QuantificationResult_ToString_Normalized_Area___0_, 
                    NormalizedArea.Value.ToString(Formats.CalibrationCurve));
            }
            return TextUtil.EXCEL_NA;
        }
    }
}
