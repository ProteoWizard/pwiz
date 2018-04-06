/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Linq;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class FiguresOfMerit : Immutable
    {
        public static FiguresOfMerit EMPTY = new FiguresOfMerit();

        [Format(Formats.CalibrationCurve)]
        public double? LimitOfDetection { get; private set; }
        [Format(Formats.CalibrationCurve)]
        public double? LimitOfQuantification { get; private set; }
        [Browsable(false)]
        public string Units { get; private set; }

        public FiguresOfMerit ChangeLimitOfDetection(double? limitOfDetection)
        {
            return ChangeProp(ImClone(this), im => im.LimitOfDetection = limitOfDetection);
        }

        public FiguresOfMerit ChangeLimitOfQuantification(double? limitOfQuantification)
        {
            return ChangeProp(ImClone(this), im => im.LimitOfQuantification = limitOfQuantification);
        }

        public FiguresOfMerit ChangeUnits(string units)
        {
            return ChangeProp(ImClone(this), im => im.Units = units);
        }

        public override string ToString()
        {
            var parts = new List<string>();
            string format = string.IsNullOrEmpty(Units) ? Formats.CalibrationCurve : Formats.Concentration;
            if (LimitOfDetection.HasValue)
            {
                parts.Add(QuantificationStrings.FiguresOfMerit_ToString_LOD__ + LimitOfDetection.Value.ToString(format));
            }
            if (LimitOfQuantification.HasValue)
            {
                parts.Add(QuantificationStrings.FiguresOfMerit_ToString_LOQ__ + LimitOfQuantification.Value.ToString(format));
            }
            if (!parts.Any())
            {
                return string.Empty;
            }
            if (!string.IsNullOrEmpty(Units))
            {
                parts.Add(Units);
            }
            return TextUtil.SpaceSeparate(parts);
        }
    }
}
