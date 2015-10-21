/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding
{
    /// <summary>
    /// Base class for properties for ratios to IsotopeLabelTypes other than the standard
    /// one that is known about at compile time.
    /// </summary>
    public class RatioPropertyDescriptor : PropertyDescriptor
    {
        public const string RATIO_PREFIX = "ratio_"; // Not L10N
        public const string RATIO_GS_PREFIX = "ratio_gs_"; // Not L10N
        public const string RDOTP_PREFIX = "rdotp_"; // Not L10N

        private readonly Type _componentType;
        private readonly Func<object, double?> _getterFunc;
        private RatioPropertyDescriptor(string name, Type componentType, Func<object, double?> getterFunc, Attribute[] attributes) 
            : base(name, attributes)
        {
            _componentType = componentType;
            _getterFunc = getterFunc;
        }

        public override bool CanResetValue(object component)
        {
            return false;
        }

        public override void ResetValue(object component)
        {
            throw new InvalidOperationException();
        }

        public override void SetValue(object component, object value)
        {
            throw new InvalidOperationException();
        }

        public override bool ShouldSerializeValue(object component)
        {
            return false;
        }

        public override bool IsReadOnly
        {
            get { return true; }
        }

        public override object GetValue(object component)
        {
            return _getterFunc(component);
        }

        public override Type ComponentType
        {
            get { return _componentType; }
        }

        public override Type PropertyType
        {
            get { return typeof(double?); }
        }

        #region Equality members

        protected bool Equals(RatioPropertyDescriptor other)
        {
            return base.Equals(other)
                && _componentType == other._componentType
                && _getterFunc.Equals(other._getterFunc);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((RatioPropertyDescriptor) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode*397) ^ _componentType.GetHashCode();
                hashCode = (hashCode*397) ^ _getterFunc.GetHashCode();
                return hashCode;
            }
        }

        #endregion

        public static bool TryParseProperty(string propertyName, out string prefix, out IList<string> parts)
        {
            if (propertyName.StartsWith(RATIO_GS_PREFIX))
            {
                prefix = RATIO_GS_PREFIX;
                string labelType = propertyName.Substring(RATIO_GS_PREFIX.Length);
                parts = labelType.Length > 0 ? ParsePropertyParts(propertyName, prefix) : new string[0];
                return true;
            }
            if (propertyName.StartsWith(RATIO_PREFIX))
            {
                prefix = RATIO_PREFIX;
                parts = ParsePropertyParts(propertyName, prefix);
                return null != parts;
            }
            if (propertyName.StartsWith(RDOTP_PREFIX))
            {
                prefix = RDOTP_PREFIX;
                parts = ParsePropertyParts(propertyName, prefix);
                return null != parts;
            }
            prefix = null;
            parts = null;
            return false;
        }

        /// <summary>
        /// Create a property name by starting it with the given prefix, and following that with
        /// the specified parts separated by ':'.  Unless to the Global Standards, in which case
        /// there will be only one part and no colon.
        /// </summary>
        public static string MakePropertyName(string prefix, params string[] parts)
        {
            return prefix + string.Join(":", parts.Select(EscapePart).ToArray()); // Not L10N
        }

        public static string MakePropertyName(string prefix, params IsotopeLabelType[] parts)
        {
            return MakePropertyName(prefix, parts.Select(part => NormalizePart(part.Name)).ToArray());
        }

        /// <summary>
        /// If propertyName was created by MakePropertyName with the given Prefix, then returns
        /// the list of parts.  Returns null if the propertyName is invalid.
        /// </summary>
        public static IList<string> ParsePropertyParts(string propertyName, string prefix)
        {
            if (!propertyName.StartsWith(prefix))
            {
                return null;
            }
            var names = new List<string>();
            int lastIndex = prefix.Length;
            bool atUnderscore = false;
            for (int ich = prefix.Length; ich < propertyName.Length; ich++)
            {
                switch (propertyName[ich])
                {
                    case '_':
                        atUnderscore = !atUnderscore;
                        break;
                    case ':':
                        if (atUnderscore)
                        {
                            atUnderscore = false;
                        }
                        else
                        {
                            names.Add(UnescapePart(propertyName.Substring(lastIndex, ich - lastIndex)));
                            lastIndex = ich + 1;
                        }
                        break;
                    default:
                        atUnderscore = false;
                        break;
                }
            }
            if (atUnderscore)
            {
                return null;
            }
            names.Add(UnescapePart(propertyName.Substring(lastIndex, propertyName.Length - lastIndex)));
            return names;
        }

        private static string EscapePart(string s)
        {
            return s.Replace("_", "__").Replace(":", "_:"); // Not L10N
        }

        private static string UnescapePart(string s)
        {
            return s.Replace("_:", ":").Replace("__", "_"); // Not L10N
        }

        private static string NormalizePart(string s)
        {
            return Helpers.MakeId(s, true);
        }

        private static RatioPropertyDescriptor MakeRatioProperty<TComponent>(string name, string displayName, string format, Func<TComponent, double?> getValueFunc)
        {
            Func<object, double?> getterFunc = (component =>
            {
                if (!(component is TComponent))
                {
                    return null;
                }
                return getValueFunc((TComponent) component);
            });
            var attributes = new Attribute[]
            {
                new DisplayNameAttribute(displayName),
                new FormatAttribute(format) {NullValue = TextUtil.EXCEL_NA},
            };
            return new RatioPropertyDescriptor(name, typeof(TComponent), getterFunc, attributes);
        }

        public static RatioPropertyDescriptor GetProperty(SrmDocument document, Type componentType, string propertyName)
        {
            string prefix;
            IList<string> parts;
            if (!TryParseProperty(propertyName, out prefix, out parts))
            {
                return null;
            }
            var modifications = document.Settings.PeptideSettings.Modifications;
            if (componentType == typeof(PeptideResult))
            {
                string displayName;
                Func<PeptideResult, double?> getterFunc;
                if (parts.Count == 0)
                {
                    return null;
                }
                string format;
                string labelColumnPart = parts[0];
                var labelType = FindLabel(modifications.GetModificationTypes(), labelColumnPart);
                if (parts.Count == 1)
                {
                    getterFunc = peptideResult =>
                        RatioValue.GetRatio(FindPeptideLabelRatio(peptideResult, labelType, null).Ratio);
                    displayName = string.Format("Ratio{0}ToGlobalStandards", labelColumnPart); // Not L10N
                    format = Formats.GLOBAL_STANDARD_RATIO;
                }
                else if (parts.Count != 2)
                {
                    return null;
                }
                else
                {
                    string standardColumnPart = parts[1];
                    var standardType = FindLabel(modifications.RatioInternalStandardTypes, standardColumnPart);
                    if (prefix == RATIO_PREFIX)
                    {
                        getterFunc = peptideResult =>
                            RatioValue.GetRatio(FindPeptideLabelRatio(peptideResult, labelType, standardType).Ratio);
                        displayName = string.Format("Ratio{0}To{1}", labelColumnPart, standardColumnPart); // Not L10N
                        format = Formats.STANDARD_RATIO;
                    }
                    else if (prefix == RDOTP_PREFIX)
                    {
                        getterFunc = peptideResult =>
                            RatioValue.GetDotProduct(FindPeptideLabelRatio(peptideResult, labelType, standardType).Ratio);
                        displayName = string.Format("DotProduct{0}To{1}", labelColumnPart, standardColumnPart); // Not L10N
                        format = Formats.STANDARD_RATIO;
                    }
                    else
                    {
                        return null;
                    }
                }
                return MakeRatioProperty(propertyName, displayName, format, getterFunc);
            }
            if (componentType == typeof (PrecursorResult))
            {
                string format;
                Func<PrecursorResult, double?> getterFunc;
                string displayName;
                if (parts.Count == 0)
                {
                    if (prefix != RATIO_GS_PREFIX)
                    {
                        return null;
                    }
                    getterFunc = precursorResult => RatioValue.GetRatio(precursorResult.ChromInfo.Ratios[precursorResult.ChromInfo.Ratios.Count - 1]);
                    displayName = "TotalAreaRatioToGlobalStandards"; // Not L10N
                    format = Formats.GLOBAL_STANDARD_RATIO;
                }
                else if (parts.Count != 1)
                {
                    return null;
                }
                else
                {
                    format = Formats.STANDARD_RATIO;
                    string labelColumnPart = parts[0];
                    int ratioIndex = IndexOf(modifications.RatioInternalStandardTypes, labelColumnPart);
                    if (prefix == RATIO_PREFIX)
                    {
                        getterFunc = precursorResult =>
                        {
                            if (ratioIndex < 0 || ratioIndex >= precursorResult.ChromInfo.Ratios.Count)
                            {
                                return null;
                            }
                            return RatioValue.GetRatio(precursorResult.ChromInfo.Ratios[ratioIndex]);
                        };
                        displayName = string.Format("TotalAreaRatioTo{0}", labelColumnPart); // Not L10N
                    }
                    else if (prefix == RDOTP_PREFIX)
                    {
                        getterFunc = precursorResult =>
                        {
                            if (ratioIndex < 0 || ratioIndex >= precursorResult.ChromInfo.Ratios.Count)
                            {
                                return null;
                            }
                            return RatioValue.GetDotProduct(precursorResult.ChromInfo.Ratios[ratioIndex]);
                        };
                        displayName = string.Format("DotProductTo{0}", labelColumnPart); // Not L10N
                    }
                    else
                    {
                        return null;
                    }
                }
                return MakeRatioProperty(propertyName, displayName, format, getterFunc);
            }
            if (componentType == typeof (TransitionResult))
            {
                string format;
                Func<TransitionResult, double?> getterFunc;
                string displayName;
                if (parts.Count == 0)
                {
                    if (prefix != RATIO_GS_PREFIX)
                    {
                        return null;
                    }

                    getterFunc = transitionResult => transitionResult.ChromInfo.Ratios[transitionResult.ChromInfo.Ratios.Count - 1];
                    displayName = "AreaRatioToGlobalStandards"; // Not L10N
                    format = Formats.GLOBAL_STANDARD_RATIO;
                }
                else if (prefix != RATIO_PREFIX || parts.Count != 1)
                {
                    return null;
                }
                else
                {
                    format = Formats.STANDARD_RATIO;
                    string labelColumnPart = parts[0];
                    int ratioIndex = IndexOf(modifications.RatioInternalStandardTypes, labelColumnPart);
                    getterFunc = transitionResult =>
                    {
                        if (ratioIndex < 0 || ratioIndex >= transitionResult.ChromInfo.Ratios.Count)
                        {
                            return null;
                        }
                        return transitionResult.ChromInfo.Ratios[ratioIndex];
                    };
                    displayName = string.Format("AreaRatioTo{0}", labelColumnPart); // Not L10N
                }
                return MakeRatioProperty(propertyName, displayName, format, getterFunc);
            }
            return null;
        }

        public static IEnumerable<PropertyDescriptor> ListProperties(SrmDocument document, Type componentType)
        {
            var modifications = document.Settings.PeptideSettings.Modifications;
            var propertyNames = new List<string>();
            if (componentType == typeof (PeptideResult))
            {
                var standardTypes = modifications.RatioInternalStandardTypes;
                var labelTypes = modifications.GetModificationTypes().ToArray();
                foreach (var standardType in standardTypes)
                {
                    foreach (var labelType in labelTypes)
                    {
                        if (ReferenceEquals(labelType, standardType))
                        {
                            continue;
                        }
                        propertyNames.Add(MakePropertyName(RATIO_PREFIX, labelType, standardType));
                        propertyNames.Add(MakePropertyName(RDOTP_PREFIX, labelType, standardType));
                    }
                }

                if (document.Settings.HasGlobalStandardArea)
                {
                    foreach (var labelType in labelTypes)
                    {
                        propertyNames.Add(MakePropertyName(RATIO_GS_PREFIX, labelType));
                    }
                }
            }
            if (componentType == typeof (PrecursorResult) || componentType == typeof (TransitionResult))
            {
                var standardTypes = modifications.RatioInternalStandardTypes;
                if (standardTypes.Count > 1)
                {
                    foreach (var standardType in standardTypes)
                    {
                        propertyNames.Add(MakePropertyName(RATIO_PREFIX, standardType));
                        if (componentType == typeof (PrecursorResult))
                        {
                            propertyNames.Add(MakePropertyName(RDOTP_PREFIX, standardType));
                        }
                    }
                }

                if (document.Settings.HasGlobalStandardArea)
                {
                    propertyNames.Add(RATIO_GS_PREFIX);
                }
            }
            return propertyNames.Distinct().Select(name => GetProperty(document, componentType, name));
        }

        private static IsotopeLabelType FindLabel(IEnumerable<IsotopeLabelType> labelTypes, string name)
        {
            return labelTypes.FirstOrDefault(labelType => Matches(labelType, name));
        }

        private static int IndexOf(IList<IsotopeLabelType> labelTypes, string name)
        {
            for (int i = 0; i < labelTypes.Count; i++)
            {
                var labelType = labelTypes[i];
                if (Matches(labelType, name))
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool Matches(IsotopeLabelType labelType, string name)
        {
            return String.Equals(name, NormalizePart(labelType.Name), StringComparison.InvariantCultureIgnoreCase);
        }

        private static PeptideLabelRatio FindPeptideLabelRatio(PeptideResult peptideResult, IsotopeLabelType labelType, IsotopeLabelType standardType)
        {
            return peptideResult.ChromInfo.LabelRatios.FirstOrDefault(
                    labelRatio => ReferenceEquals(labelType, labelRatio.LabelType) &&
                        ReferenceEquals(standardType, labelRatio.StandardType));
        }
    }
}
