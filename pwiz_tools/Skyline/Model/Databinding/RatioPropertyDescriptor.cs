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
                && _componentType.Equals(other._componentType)
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
        /// the specified parts separated by ':'.
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

        private static RatioPropertyDescriptor MakeRatioProperty<TComponent>(string name, string displayName, Func<TComponent, double?> getValueFunc)
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
                new FormatAttribute(Formats.STANDARD_RATIO) {NullValue = TextUtil.EXCEL_NA},
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
                if (parts.Count != 2)
                {
                    return null;
                }
                var labelType = FindLabel(modifications.GetModificationTypes(), parts[0]);
                var standardType = FindLabel(modifications.InternalStandardTypes, parts[1]);
                string displayName;
                Func<PeptideResult, double?> getterFunc;
                if (prefix == RATIO_PREFIX)
                {
                    getterFunc = peptideResult =>
                        RatioValue.GetRatio(FindPeptideLabelRatio(peptideResult, labelType, standardType).Ratio);
                    displayName = string.Format("Ratio{0}To{1}", parts[0], parts[1]); // Not L10N
                }
                else if (prefix == RDOTP_PREFIX)
                {
                    getterFunc = peptideResult =>
                        RatioValue.GetDotProduct(FindPeptideLabelRatio(peptideResult, labelType, standardType).Ratio);
                    displayName = string.Format("DotProduct{0}To{1}", parts[0], parts[1]); // Not L10N
                }
                else
                {
                    return null;
                }
                return MakeRatioProperty(propertyName, displayName, getterFunc);
            }
            if (componentType == typeof (PrecursorResult))
            {
                if (parts.Count != 1)
                {
                    return null;
                }
                int ratioIndex =  IndexOf(modifications.InternalStandardTypes, parts[0]);
                Func<PrecursorResult, double?> getterFunc;
                string displayName;
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
                    displayName = "TotalAreaRatioTo" + parts[0];
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
                    displayName = "DotProductTo" + parts[0];
                }
                else
                {
                    return null;
                }
                return MakeRatioProperty(propertyName, displayName, getterFunc);
            }
            if (componentType == typeof (TransitionResult))
            {
                if (prefix != RATIO_PREFIX || parts.Count != 1)
                {
                    return null;
                }
                int ratioIndex = IndexOf(modifications.InternalStandardTypes, parts[0]);
                Func<TransitionResult, double?> getterFunc = transitionResult =>
                {
                    if (ratioIndex < 0 || ratioIndex >= transitionResult.ChromInfo.Ratios.Count)
                    {
                        return null;
                    }
                    return transitionResult.ChromInfo.Ratios[ratioIndex];
                };
                string displayName = "AreaRatioTo" + parts[0];
                return MakeRatioProperty(propertyName, displayName, getterFunc);
            }
            return null;
        }

        public static IEnumerable<PropertyDescriptor> ListProperties(SrmDocument document, Type componentType)
        {
            var modifications = document.Settings.PeptideSettings.Modifications;
            var propertyNames = new List<string>();
            if (componentType == typeof (PeptideResult))
            {
                var standardTypes = modifications.InternalStandardTypes;
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
            }
            if (componentType == typeof (PrecursorResult) || componentType == typeof (TransitionResult))
            {
                var standardTypes = modifications.InternalStandardTypes;
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
