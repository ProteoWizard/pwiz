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
using System.Globalization;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class QuantificationResult : Immutable, IComparable
    {
        [Format(Formats.GLOBAL_STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        [ChildDisplayName("NormalizedArea{0}")]
        public AnnotatedDouble NormalizedArea { get; private set; }

        public QuantificationResult ChangeNormalizedArea(AnnotatedDouble annotatedValue)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.NormalizedArea = annotatedValue;
            });
        }

        [Format(Formats.GLOBAL_STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        [ChildDisplayName("CalculatedConcentration{0}")]
        public AnnotatedDouble CalculatedConcentration { get; private set; }

        [Format(Formats.CV, NullValue = TextUtil.EXCEL_NA)]
        [ChildDisplayName("Accuracy{0}")]
        public double? Accuracy { get; private set; }

        [Browsable(false)]
        public string Units { get; private set; }

        public QuantificationResult ChangeUnits(string units)
        {
            return ChangeProp(ImClone(this), im => im.Units = units);
        }

        public QuantificationResult ChangeCalculatedConcentration(AnnotatedDouble calculatedConcentration)
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

        private Tuple<AnnotatedDouble, AnnotatedDouble> GetSortKey()
        {
            return Tuple.Create(CalculatedConcentration, NormalizedArea);
        }

        public override string ToString()
        {
            if (CalculatedConcentration != null)
            {
                return FormatCalculatedConcentration(CalculatedConcentration, Units);
            }
            else if (NormalizedArea != null)
            {
                return string.Format(QuantificationStrings.QuantificationResult_ToString_Normalized_Area___0_, 
                    AppendMessage(NormalizedArea.Raw.ToString(Formats.CalibrationCurve), NormalizedArea.Message));
            }
            return TextUtil.EXCEL_NA;
        }

        public static string FormatCalculatedConcentration(double calculatedConcentration, string units)
        {
            if (units == null)
            {
                return calculatedConcentration.ToString(Formats.CalibrationCurve, CultureInfo.CurrentCulture);
            }

            return TextUtil.SpaceSeparate(calculatedConcentration.ToString(Formats.Concentration, CultureInfo.CurrentCulture), units);
        }

        public static string FormatCalculatedConcentration(AnnotatedDouble calculatedConcentration, string units)
        {
            return AppendMessage(FormatCalculatedConcentration(calculatedConcentration.Raw, units), calculatedConcentration.Message);
        }

        private static string AppendMessage(string value, string message)
        {
            if (message == null)
            {
                return value;
            }

            return value + @" (" + message + @")";
        }
    }

    public class PrecursorQuantificationResult : QuantificationResult
    {
        #region duplicate properties from base class to control the order they appear in Report Editor
        [Format(Formats.GLOBAL_STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        [InvariantDisplayName("PrecursorNormalizedArea")]
        [ChildDisplayName("PrecursorNormalizedArea{0}")]
        public new AnnotatedDouble NormalizedArea
        {
            get { return base.NormalizedArea; }
        }

        [Format(Formats.GLOBAL_STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        [InvariantDisplayName("PrecursorCalculatedConcentration")]
        [ChildDisplayName("PrecursorCalculatedConcentration{0}")]
        public new AnnotatedDouble CalculatedConcentration
        {
            get { return base.CalculatedConcentration; }
        }

        [Format(Formats.CV, NullValue = TextUtil.EXCEL_NA)]
        [InvariantDisplayName("PrecursorAccuracy")]
        [ChildDisplayName("PrecursorAccuracy{0}")]
        public new double? Accuracy
        {
            get { return base.Accuracy; }
        }
        #endregion

        [Format(Formats.STANDARD_RATIO)]
        public double? QualitativeIonRatio { get; private set; }
        public ValueStatus QualitativeIonRatioStatus { get; private set; }
        [InvariantDisplayName("BatchTargetQualitativeIonRatio")]
        [Format(Formats.STANDARD_RATIO)]
        public double? TargetQualitativeIonRatio { get; private set; }
        public PrecursorQuantificationResult ChangeIonRatio(double? targetIonRatio, double? ionRatio, ValueStatus ionRatioStatus)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.TargetQualitativeIonRatio = targetIonRatio;
                im.QualitativeIonRatio = ionRatio;
                im.QualitativeIonRatioStatus = ionRatioStatus;
            });
        }
    }
}
