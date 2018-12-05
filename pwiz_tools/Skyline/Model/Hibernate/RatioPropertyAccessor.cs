/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Hibernate
{
    public static class RatioPropertyAccessor
    {
        public enum RatioTarget
        {
            peptide_result, precursor_result, transition_result
        }

        private const string RATIO_PREFIX = "ratio_";
        private const string RATIO_GS_PREFIX = "ratio_gs_";
        private const string RDOTP_PREFIX = "rdotp_";

        public static RatioPropertyName PeptideRatioProperty(IsotopeLabelType labelType, IsotopeLabelType standardType)
        {
            string prefix, propertyKey, headerText;
            if (standardType == null)
            {
                prefix = RATIO_GS_PREFIX;
                if (labelType.IsLight)
                {
                    propertyKey = @"RatioToGlobalStandards";
                    headerText = Resources.RatioPropertyAccessor_PeptideRatioProperty_Ratio_To_Global_Standards;
                }
                else
                {
                    propertyKey = string.Format(@"Ratio{0}ToGlobalStandards", Helpers.MakeId(labelType.Name, true));
                    headerText = string.Format(Resources.RatioPropertyAccessor_PeptideRatioProperty_Ratio__0__To_Global_Standards, labelType.Title);
                }
            }
            else
            {
                prefix = RATIO_PREFIX;
                propertyKey = string.Format(@"Ratio{0}To{1}",
                    Helpers.MakeId(labelType.Name, true),
                    Helpers.MakeId(standardType.Name, true));
                headerText = string.Format(Resources.RatioPropertyAccessor_PeptideProperty_Ratio__0__To__1_,
                    labelType.Title, standardType.Title);
            }

            return new RatioPropertyName(prefix, propertyKey, headerText);
        }

        public static RatioPropertyName PrecursorRatioProperty(IsotopeLabelType standardType)
        {
            string prefix, key, header;
            if (standardType == null)
            {
                prefix = RATIO_GS_PREFIX;
                key = @"TotalAreaRatioToGlobalStandards";
                header = Resources.RatioPropertyAccessor_PrecursorRatioProperty_Total_Area_Ratio_To_Global_Standards;
            }
            else
            {
                prefix = RATIO_PREFIX;
                key = @"TotalAreaRatioTo" + Helpers.MakeId(standardType.Name, true);
                header = string.Format(Resources.RatioPropertyAccessor_PrecursorRatioProperty_Total_Area_Ratio_To__0_, standardType.Title);
            }
            return new RatioPropertyName(prefix, key, header);
        }

        public static RatioPropertyName TransitionRatioProperty(IsotopeLabelType standardType)
        {
            string prefix, key, header;
            if (standardType == null)
            {
                prefix = RATIO_GS_PREFIX;
                key = @"AreaRatioToGlobalStandards";
                header = Resources.RatioPropertyAccessor_TransitionRatioProperty_Area_Ratio_To_Global_Standards;
            }
            else
            {
                prefix = RATIO_PREFIX;
                key = @"AreaRatioTo" + Helpers.MakeId(standardType.Name, true);
                header = string.Format(Resources.RatioPropertyAccessor_TransitionRatioProperty_Area_Ratio_To__0_, standardType.Title);
            }
            return new RatioPropertyName(prefix, key, header);
        }

        public static RatioPropertyName PeptideRdotpProperty(IsotopeLabelType labelType, IsotopeLabelType standardType)
        {
            string key = string.Format(@"DotProduct{0}To{1}",
                Helpers.MakeId(labelType.Name, true),
                Helpers.MakeId(standardType.Name, true));
            string headerText = string.Format(Resources.RDotPPropertyAccessor_PeptideProperty_Dot_Product__0__To__1_,
                labelType.Title, standardType.Title);
            return new RatioPropertyName(RDOTP_PREFIX, key, headerText);
        }

        public static RatioPropertyName PrecursorRdotpProperty(IsotopeLabelType standardType)
        {
            string key = string.Format(@"DotProductTo{0}", Helpers.MakeId(standardType.Name, true));
            string headerText = string.Format(Resources.RDotPPropertyAccessor_PrecursorProperty_Dot_Product_To__0_, standardType.Title);
            return new RatioPropertyName(RDOTP_PREFIX, key, headerText);
        }

        public static string KeyFromPropertyName(string propertyName)
        {
            Assume.IsTrue(IsRatioOrRdotpProperty(propertyName));
            if (propertyName.StartsWith(RATIO_GS_PREFIX))
                return propertyName.Substring(RATIO_GS_PREFIX.Length);
            else if (propertyName.StartsWith(RATIO_PREFIX))
                return propertyName.Substring(RATIO_PREFIX.Length);
            else
                return propertyName.Substring(RDOTP_PREFIX.Length);
        }

        public static bool IsRatioOrRdotpProperty(string propertyName)
        {
            return IsRatioProperty(propertyName) || IsRdotpProperty(propertyName);
        }

        public static bool IsRatioProperty(string propertyName)
        {
            return propertyName.StartsWith(RATIO_PREFIX);
        }

        public static bool IsRatioGsProperty(string propertyName)
        {
            return propertyName.StartsWith(RATIO_GS_PREFIX);
        }

        public static bool IsRdotpProperty(string propertyName)
        {
            return propertyName.StartsWith(RDOTP_PREFIX);
        }

        public static string GetDisplayName(string propertyName)
        {
            if (propertyName.StartsWith(RATIO_GS_PREFIX))
                return propertyName.Substring(RATIO_GS_PREFIX.Length);
            if (propertyName.StartsWith(RATIO_PREFIX))
                return propertyName.Substring(RATIO_PREFIX.Length);
            if (propertyName.StartsWith(RDOTP_PREFIX))
                return propertyName.Substring(RDOTP_PREFIX.Length);
            throw new ArgumentException(string.Format(@"Invalid ratio column '{0}'", propertyName)); // CONSIDER: localize? Does user see this?
        }

        public class RatioPropertyName
        {
            public RatioPropertyName(string prefix, string displayName, string headerText)
            {
                Prefix = prefix;
                DisplayName = displayName;
                HeaderText = headerText;
            }

            public string Prefix { get; private set; }
            /// <summary>
            /// Unique name of the column.  Used as the key in <see cref="DbRatioResult.LabelRatios"/>, 
            /// and as the NHibernate column/property name.
            /// </summary>
            public string ColumnName { get { return Prefix + DisplayName; } }
            /// <summary>
            /// Title of the column as displayed in custom reports.
            /// </summary>
            public string DisplayName { get; private set; }
            /// <summary>
            /// Title of the column in the results grid.
            /// </summary>
            public string HeaderText { get; private set; }
        }
    }
}
