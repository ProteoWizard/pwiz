/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Wrapper around <see cref="NormalizationMethod"/>, but also includes special values such as:
    /// MAXIMUM and TOTAL: only available only in Peak Area Replicate Comparison graph
    /// DEFAULT: whatever the normalization method for the currently selected peptide is
    /// CALIBRATED: use the calibration curve
    /// </summary>
    public abstract class NormalizeOption
    {
        public static readonly NormalizeOption NONE = new Simple(NormalizationMethod.NONE);
        private static Dictionary<string, Special> _specialOptions 
            = new Dictionary<string, Special>();
        public static readonly NormalizeOption MAXIMUM = new Special(@"maximum", ()=>QuantificationStrings.NormalizeOption_MAXIMUM_Maximum);
        public static readonly NormalizeOption TOTAL = new Special(@"total", ()=>QuantificationStrings.NormalizeOption_TOTAL_Total);
        public static readonly NormalizeOption DEFAULT = new Special(@"default", ()=>QuantificationStrings.NormalizeOption_DEFAULT_Default_Normalization_Method);
        public static readonly NormalizeOption CALIBRATED = new Special(@"calibrated", ()=>QuantificationStrings.NormalizeOption_CALIBRATED_Calibration_Curve);

        public static readonly NormalizeOption GLOBAL_STANDARDS =
            FromNormalizationMethod(NormalizationMethod.GLOBAL_STANDARDS);


        public abstract string PersistedName { get; }

        /// <summary>
        /// Returns the string that should be displayed in a "Normalize To" list.
        /// </summary>
        public abstract string Caption { get; }

        public abstract NormalizationMethod NormalizationMethod { get; }

        public bool Is(NormalizationMethod normalizationMethod)
        {
            return Equals(NormalizationMethod, normalizationMethod);
        }

        public bool IsRatioToLabel
        {
            get { return NormalizationMethod is NormalizationMethod.RatioToLabel; }
        }

        public override string ToString()
        {
            return Caption;
        }

        public static NormalizeOption FromPersistedName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return FromNormalizationMethod(NormalizationMethod.NONE);
            }

            if (_specialOptions.TryGetValue(name, out Special specialNormalizeOption))
            {
                return specialNormalizeOption;
            }

            return FromNormalizationMethod(NormalizationMethod.FromName(name));
        }

        public static NormalizeOption FromNormalizationMethod(NormalizationMethod normalizationMethod)
        {
            return new Simple(normalizationMethod);
        }

        public static NormalizeOption FromIsotopeLabelType(IsotopeLabelType isotopeLabelType)
        {
            return new Simple(new NormalizationMethod.RatioToLabel(isotopeLabelType));
        }

        /// <summary>
        /// Returns the normalization options that are appropriate for the document.
        /// Note: never includes "Default", "None", "Total", or "Maximum".
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        public static IEnumerable<NormalizeOption> AvailableNormalizeOptions(SrmDocument document)
        {
            foreach (var normalizationMethod in NormalizationMethod.ListNormalizationMethods(document)
                .OrderBy(method=>method is NormalizationMethod.RatioToLabel ? 0 : 1))
            {
                if (Equals(normalizationMethod, NormalizationMethod.NONE))
                {
                    continue;
                }
                yield return FromNormalizationMethod(normalizationMethod);
            }

            if (document.Settings.PeptideSettings.Quantification.RegressionFit != RegressionFit.NONE)
            {
                yield return CALIBRATED;
            }
        }

        #region Equality Members
        protected bool Equals(NormalizeOption other)
        {
            return PersistedName == other.PersistedName;
        }

        public override bool Equals(object obj)
        {
            return obj is NormalizeOption other && Equals(other);
        }

        public override int GetHashCode()
        {
            return PersistedName.GetHashCode();
        }

        public static bool operator ==(NormalizeOption left, NormalizeOption right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(NormalizeOption left, NormalizeOption right)
        {
            return !Equals(left, right);
        }
        #endregion

        public static NormalizeOption RatioToFirstStandard(SrmSettings settings)
        {
            var firstLabelType = settings.PeptideSettings.Modifications.RatioInternalStandardTypes.FirstOrDefault();
            if (firstLabelType != null)
            {
                return FromNormalizationMethod(new NormalizationMethod.RatioToLabel(firstLabelType));
            }

            return NONE;
        }

        public NormalizeOption Constrain(SrmSettings settings)
        {
            var normalizationMethod = (this as Simple)?.NormalizationMethod;
            if (normalizationMethod is NormalizationMethod.RatioToLabel ratioToLabel)
            {
                if (settings.PeptideSettings.Modifications.RatioInternalStandardTypes
                    .All(item => item.Name != ratioToLabel.IsotopeLabelTypeName))
                {
                    return RatioToFirstStandard(settings);
                }
            }

            if (Equals(normalizationMethod, NormalizationMethod.GLOBAL_STANDARDS) && !settings.HasGlobalStandardArea)
            {
                return RatioToFirstStandard(settings);
            }

            if (Equals(normalizationMethod, NormalizationMethod.TIC) && !settings.HasTicArea)
                return FromNormalizationMethod(NormalizationMethod.NONE);

            return this;
        }

        public static NormalizeOption Constrain(SrmSettings settings, NormalizeOption currentNormalizeOption)
        {
            return (currentNormalizeOption ?? RatioToFirstStandard(settings)).Constrain(settings);
        }

        /// <summary>
        /// NormalizeOptions which are wrappers around NormalizationMethod values
        /// </summary>
        public class Simple : NormalizeOption
        {
            public Simple(NormalizationMethod normalizationMethod)
            {
                NormalizationMethod = normalizationMethod;
            }

            public override NormalizationMethod NormalizationMethod { get; }

            public override string PersistedName => NormalizationMethod.Name;

            public override string Caption => NormalizationMethod.NormalizeToCaption;
        }

        /// <summary>
        /// NormalizeOptions which are different than a simple wrapper around a NormalizationMethod
        /// </summary>
        public class Special : NormalizeOption
        {
            private readonly string _persistedName;
            private readonly Func<string> _getCaptionFunc;
            public Special(string persistedName, Func<string> getCaptionFunc)
            {
                _specialOptions.Add(persistedName, this);
                _persistedName = persistedName;
                _getCaptionFunc = getCaptionFunc;
            }

            public override string PersistedName => _persistedName;

            public override string Caption => _getCaptionFunc();

            public override NormalizationMethod NormalizationMethod => null;
        }
    }
}
