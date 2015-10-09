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
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class QuantificationResult : Immutable, IComparable
    {
        [Format(Formats.GLOBAL_STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double? NormalizedIntensity { get; private set; }

        public QuantificationResult ChangeNormalizedIntensity(double? intensity)
        {
            return ChangeProp(ImClone(this), im => im.NormalizedIntensity = intensity);
        }
        [Format(Formats.GLOBAL_STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double? CalculatedConcentration { get; private set; }

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
        public LinkValue<CalibrationCurve> CalibrationCurve { get; private set; }

        public QuantificationResult ChangeCalibrationCurve(LinkValue<CalibrationCurve> calibrationCurve)
        {
            return ChangeProp(ImClone(this), im => im.CalibrationCurve = calibrationCurve);
        }

        public int CompareTo(object obj)
        {
            if (null == obj)
            {
                return 1;
            }
            return Comparer.Default.Compare(CalculatedConcentration,
                ((QuantificationResult) obj).CalculatedConcentration);
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
            else
            {
                return string.Empty;
            }
        }
    }
}
