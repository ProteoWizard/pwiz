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
using System.Collections.Generic;

namespace pwiz.Skyline.Model.GroupComparison
{
    public sealed class NormalizationMethod
    {
        private const string ratio_prefix = "ratio_to_"; // Not L10N
        private readonly Func<string> _getLabelFunc;
        private readonly string _name;
        private NormalizationMethod(string name, Func<string> getLabelFunc)
        {
            _getLabelFunc = getLabelFunc;
            _name = name;
        }

        public override string ToString()
        {
            return _getLabelFunc();
        }

        public string Name
        {
            get
            {
                return _name;
            }
        }

        public static NormalizationMethod FromName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return NONE;
            }
            if (name.StartsWith(ratio_prefix))
            {
                string isotopeLabelTypeName = name.Substring(ratio_prefix.Length);
                var isotopeLabelType = new IsotopeLabelType(isotopeLabelTypeName, 0);
                return new NormalizationMethod(name, () => string.Format(GroupComparisonStrings.NormalizationMethod_FromName_Ratio_to__0_, isotopeLabelType.Title))
                {
                    IsotopeLabelTypeName = isotopeLabelType.Name
                };
            }
            foreach (var normalizationMethod in new[] {EQUALIZE_MEDIANS, QUANTILE, GLOBAL_STANDARDS})
            {
                if (Equals(normalizationMethod.Name, name))
                {
                    return normalizationMethod;
                }
            }
            return NONE;
        }

        public string IsotopeLabelTypeName { get; private set; }
        public bool AllowTruncatedTransitions { get { return !string.IsNullOrEmpty(IsotopeLabelTypeName); } }

        // ReSharper disable NonLocalizedString
        public static readonly NormalizationMethod NONE = new NormalizationMethod("none", ()=>GroupComparisonStrings.NormalizationMethod_NONE_None);
        public static readonly NormalizationMethod EQUALIZE_MEDIANS = new NormalizationMethod("equalize_medians", ()=>GroupComparisonStrings.NormalizationMethod_EQUALIZE_MEDIANS_Equalize_Medians);
        public static readonly NormalizationMethod QUANTILE = new NormalizationMethod("quantile", ()=>GroupComparisonStrings.NormalizationMethod_QUANTILE_Quantile);
        public static readonly NormalizationMethod GLOBAL_STANDARDS = new NormalizationMethod("global_standards", ()=>GroupComparisonStrings.NormalizationMethod_GLOBAL_STANDARDS_Ratio_to_Global_Standards);
        // ReSharper restore NonLocalizedString

        public static NormalizationMethod GetNormalizationMethod(IsotopeLabelType isotopeLabelType)
        {
            return FromName(ratio_prefix + isotopeLabelType.Name);
        }

        private bool Equals(NormalizationMethod other)
        {
            if (null == IsotopeLabelTypeName)
            {
                return ReferenceEquals(this, other);
            }
            return Equals(IsotopeLabelTypeName, other.IsotopeLabelTypeName);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is NormalizationMethod && Equals((NormalizationMethod) obj);
        }

        public override int GetHashCode()
        {
            if (IsotopeLabelTypeName == null)
            {
                // ReSharper disable BaseObjectGetHashCodeCallInGetHashCode
                return base.GetHashCode();
                // ReSharper restore BaseObjectGetHashCodeCallInGetHashCode
            }
            return IsotopeLabelTypeName.GetHashCode();
        }

        public static IList<NormalizationMethod> ListNormalizationMethods(SrmDocument document)
        {
            var result = new List<NormalizationMethod>
            {
                NONE
            };
            if (document.Settings.HasGlobalStandardArea)
            {
                result.Add(GLOBAL_STANDARDS);
            }
            foreach (var isotopeLabelType in document.Settings.PeptideSettings.Modifications.InternalStandardTypes)
            {
                result.Add(GetNormalizationMethod(isotopeLabelType));
            }
            return result.AsReadOnly();
        }
    }
}
