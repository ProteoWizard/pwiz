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
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Web;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.GroupComparison
{
    public abstract class NormalizationMethod : LabeledValues<string>
    {
        private const string ratio_prefix = "ratio_to_";
        private const string surrogate_prefix = "surrogate_";

        private NormalizationMethod(string name, Func<string> getLabelFunc) : base(name, getLabelFunc)
        {
        }

        public override string ToString()
        {
            return Label;
        }

        public string NormalizationMethodCaption
        {
            get
            {
                return Label;
            }
        }

        public virtual string NormalizeToCaption
        {
            get
            {
                return Label;
            }
        }

        public abstract string GetAxisTitle(string plottedValue);

        public static NormalizationMethod FromName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            if (name.StartsWith(ratio_prefix))
            {
                string isotopeLabelTypeName = name.Substring(ratio_prefix.Length);
                var isotopeLabelType = new IsotopeLabelType(isotopeLabelTypeName, 0);
                return new RatioToLabel(isotopeLabelType);
            }
            RatioToSurrogate ratioToSurrogate = RatioToSurrogate.ParseRatioToSurrogate(name);
            if (ratioToSurrogate != null)
            {
                return ratioToSurrogate;
            }
            foreach (var normalizationMethod in new[] {EQUALIZE_MEDIANS, GLOBAL_STANDARDS, TIC})
            {
                if (Equals(normalizationMethod.Name, name))
                {
                    return normalizationMethod;
                }
            }
            return NONE;
        }

        public static RatioToLabel FromIsotopeLabelTypeName(string name)
        {
            return (RatioToLabel) FromName(ratio_prefix + name);
        }

        public static readonly NormalizationMethod NONE
            = new SingletonNormalizationMethod(@"none", () => GroupComparisonStrings.NormalizationMethod_NONE_None, value=>value);
        public static readonly NormalizationMethod EQUALIZE_MEDIANS 
            = new SingletonNormalizationMethod(@"equalize_medians", 
                () => GroupComparisonStrings.NormalizationMethod_EQUALIZE_MEDIANS_Equalize_Medians, value=>string.Format(GroupComparisonStrings.NormalizationMethod_EQUALIZE_MEDIANS_Median_Normalized__0_, value));

        public static readonly NormalizationMethod GLOBAL_STANDARDS
            = new SingletonNormalizationMethod(@"global_standards",
                () => GroupComparisonStrings.NormalizationMethod_GLOBAL_STANDARDS_Ratio_to_Global_Standards,
                () => Resources.AreaCVToolbar_UpdateUI_Global_standards,
                value=>string.Format(GroupComparisonStrings.NormalizationMethod_GLOBAL_STANDARDS__0__Ratio_to_Global_Standards, value));
        public static readonly NormalizationMethod TIC
            = new SingletonNormalizationMethod(@"tic",
                () => GroupComparisonStrings.NormalizationMethod_TIC_Total_Ion_Current,
                value=>string.Format(GroupComparisonStrings.NormalizationMethod_TIC_TIC_Normalized__0_, value));

        public static RatioToLabel GetNormalizationMethod(IsotopeLabelType isotopeLabelType)
        {
            return (RatioToLabel) FromName(ratio_prefix + isotopeLabelType.Name);
        }

        private bool Equals(NormalizationMethod other)
        {
            if (null == other)
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return Name.Equals(other.Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is NormalizationMethod && Equals((NormalizationMethod) obj);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public static IList<NormalizationMethod> ListNormalizationMethods(SrmDocument document)
        {
            var result = new List<NormalizationMethod>
            {
                NONE,
                EQUALIZE_MEDIANS
            };
            if (document.Settings.HasGlobalStandardArea)
            {
                result.Add(GLOBAL_STANDARDS);
            }

            if (document.Settings.HasTicArea)
            {
                result.Add(TIC);
            }

            if (document.Settings.PeptideSettings.Modifications.HasHeavyModifications)
            {
                foreach (var isotopeLabelType in document.Settings.PeptideSettings.Modifications.RatioInternalStandardTypes)
                {
                    result.Add(GetNormalizationMethod(isotopeLabelType));
                }
            }
            return result.AsReadOnly();
        }

        public class RatioToLabel : NormalizationMethod
        {
            private readonly IsotopeLabelType _isotopeLabelType;

            public RatioToLabel(IsotopeLabelType isotopeLabelType) : base(ratio_prefix + isotopeLabelType.Name, null)
            {
                _isotopeLabelType = new IsotopeLabelType(isotopeLabelType.Name, isotopeLabelType.SortOrder);
            }

            public override string Label
            {
                get
                {
                    return string.Format(GroupComparisonStrings.NormalizationMethod_FromName_Ratio_to__0_,
                        _isotopeLabelType.Title);
                }
            }

            public override string GetAxisTitle(string plottedValue)
            {
                return string.Format(QuantificationStrings.RatioToLabel_GetAxisTitle__0__Ratio_To__1_, plottedValue, _isotopeLabelType.Title);
            }

            public override string NormalizeToCaption
            {
                get
                {
                    return _isotopeLabelType.Title;
                }
            }

            public override string ToString()
            {
                return Label;
            }

            public string IsotopeLabelTypeName { get { return _isotopeLabelType.Name; } }

            public bool Matches(IsotopeLabelType labelType)
            {
                return Equals(_isotopeLabelType.Name, labelType?.Name);
            }

            public static bool Matches(NormalizationMethod normalizationMethod, IsotopeLabelType isotopeLabelType)
            {
                return (normalizationMethod as RatioToLabel)?.Matches(isotopeLabelType) ?? false;
            }

            public IsotopeLabelType FindIsotopeLabelType(SrmSettings settings)
            {
                return settings.PeptideSettings.Modifications.HeavyModifications
                    .FirstOrDefault(mods => Matches(mods.LabelType))?.LabelType ?? _isotopeLabelType;
            }
        }

        public class RatioToSurrogate : NormalizationMethod
        {
            private readonly IsotopeLabelType _isotopeLabelType;
            private readonly string _surrogateName;
            private const string LABEL_ARG = "label";

            public RatioToSurrogate(string surrogateName, IsotopeLabelType isotopeLabelType)
                : base(surrogate_prefix + Uri.EscapeUriString(surrogateName) + '?' + LABEL_ARG + '=' + Uri.EscapeUriString(isotopeLabelType.Name), null)
            {
                _surrogateName = surrogateName;
                _isotopeLabelType = isotopeLabelType;
            }

            public override string AuditLogText
            {
                get { return Label; }
            }

            public override string Label
            {
                get
                {
                    if (_isotopeLabelType == null)
                    {
                        return string.Format(Resources.RatioToSurrogate_ToString_Ratio_to_surrogate__0_, _surrogateName);
                    }
                    return string.Format(Resources.RatioToSurrogate_ToString_Ratio_to_surrogate__0____1__, _surrogateName, _isotopeLabelType.Title);
                }
            }

            public override string NormalizeToCaption
            {
                get
                {
                    if (_isotopeLabelType == null)
                    {
                        return string.Format(QuantificationStrings.RatioToSurrogate_NormalizeToCaption_Surrogate__0_, _surrogateName);
                    }

                    return string.Format(QuantificationStrings.RatioToSurrogate_NormalizeToCaption_Surrogate__0____1__, _surrogateName, _isotopeLabelType.Title);
                }
            }

            public RatioToSurrogate(string surrogateName) : base(surrogate_prefix + Uri.EscapeUriString(surrogateName), null)
            {
                _surrogateName = surrogateName;
            }

            public String SurrogateName { get { return _surrogateName; } }

            public string IsotopeLabelName { get { return _isotopeLabelType == null ? null : _isotopeLabelType.Name; } }

            public override string ToString()
            {
                return Label;
            }

            public override string GetAxisTitle(string plottedValue)
            {
                return string.Format(QuantificationStrings.RatioToLabel_GetAxisTitle__0__Ratio_To__1_, plottedValue, NormalizeToCaption);
            }

            public static RatioToSurrogate ParseRatioToSurrogate(string name)
            {
                if (!name.StartsWith(surrogate_prefix))
                {
                    return null;
                }
                string[] parts = name.Substring(surrogate_prefix.Length).Split(new []{'?'}, 2);
                string surrogateName = Uri.UnescapeDataString(parts[0]);
                string labelName = null;

                if (parts.Length > 1)
                {
                    NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(parts[1]);
                    labelName = nameValueCollection.Get(LABEL_ARG);
                }
                if (labelName == null)
                {
                    return new RatioToSurrogate(surrogateName);
                }
                return new RatioToSurrogate(surrogateName, new IsotopeLabelType(labelName, 0));
            }

            public static IEnumerable<RatioToSurrogate> ListSurrogateNormalizationMethods(SrmDocument srmDocument)
            {
                var surrogatesByName = srmDocument.Settings.GetPeptideStandards(StandardType.SURROGATE_STANDARD).ToLookup(mol => mol.ModifiedTarget);
                foreach (var grouping in surrogatesByName)
                {
                    yield return new RatioToSurrogate(grouping.Key.InvariantName);
                    var labelTypes = grouping.SelectMany(
                        mol => mol.TransitionGroups.Select(transitionGroup => transitionGroup.TransitionGroup.LabelType))
                            .Distinct()
                            .ToArray();
                    if (labelTypes.Length > 1)
                    {
                        Array.Sort(labelTypes);
                        foreach (var label in labelTypes)
                        {
                            yield return new RatioToSurrogate(grouping.Key.InvariantName, label);
                        }
                    }
                }
            }

            public double GetStandardArea(SrmSettings settings, int resultsIndex, ChromFileInfoId fileId)
            {
                double globalStandardArea = 0;
                var peptideStandards = settings.GetPeptideStandards(StandardType.SURROGATE_STANDARD);
                if (peptideStandards == null)
                {
                    return 0;
                }
                foreach (var peptideDocNode in peptideStandards)
                {
                    if (peptideDocNode.ModifiedTarget.InvariantName != SurrogateName)
                    {
                        continue;
                    }
                    foreach (var nodeGroup in peptideDocNode.TransitionGroups)
                    {
                        if (null != _isotopeLabelType &&
                            _isotopeLabelType.Name != nodeGroup.TransitionGroup.LabelType.Name)
                        {
                            continue;
                        }
                        var chromInfos = nodeGroup.GetSafeChromInfo(resultsIndex);
                        if (chromInfos.IsEmpty)
                            continue;
                        foreach (var groupChromInfo in chromInfos)
                        {
                            if (ReferenceEquals(fileId, groupChromInfo.FileId) &&
                                    groupChromInfo.OptimizationStep == 0 &&
                                    groupChromInfo.Area.HasValue)
                                globalStandardArea += groupChromInfo.Area.Value;
                        }
                    }
                }
                return globalStandardArea;
            }
        }

        private class SingletonNormalizationMethod : NormalizationMethod
        {
            private Func<string> _getNormalizeToCaptionFunc;
            private Func<string, string> _getAxisTitleFunc;
            public SingletonNormalizationMethod(string name, Func<string> getNormalizationMethodCaptionFunc, Func<string, string> getAxisTitleFunc) : this(
                name, getNormalizationMethodCaptionFunc, getNormalizationMethodCaptionFunc, getAxisTitleFunc)
            {

            }

            public SingletonNormalizationMethod(string name, Func<string> getNormalizationMethodCaptionFunc, Func<string> getNormalizeToCaptionFunc, Func<string, string> getAxisTitleFunc) 
                : base(name, getNormalizationMethodCaptionFunc)
            {
                _getNormalizeToCaptionFunc = getNormalizeToCaptionFunc;
                _getAxisTitleFunc = getAxisTitleFunc;
            }

            public override string NormalizeToCaption
            {
                get { return _getNormalizeToCaptionFunc(); }
            }

            public override string GetAxisTitle(string plottedValue)
            {
                return _getAxisTitleFunc(plottedValue);
            }
        }

        public class PropertyFormatter : IPropertyFormatter
        {
            public string FormatValue(CultureInfo cultureInfo, object value)
            {
                return ((NormalizationMethod) value).Name;
            }

            public object ParseValue(CultureInfo cultureInfo, string text)
            {
                return FromName(text);
            }
        }

        /// <summary>
        /// Given a list of IdentityPaths, returns the unique set of NormalizationMethods that the molecules
        /// or peptides use.
        /// </summary>
        public static HashSet<NormalizationMethod> GetMoleculeNormalizationMethods(SrmDocument document,
            IEnumerable<IdentityPath> identityPaths)
        {
            var defaultNormalizationMethod = document.Settings.PeptideSettings.Quantification.NormalizationMethod;
            var normalizationMethods = new HashSet<NormalizationMethod>();
            foreach (var path in identityPaths.Select(path => path.GetPathTo(Math.Min(1, path.Depth))).Distinct())
            {
                var docNode = document.FindNode(path);
                IEnumerable<PeptideDocNode> molecules;
                if (docNode is PeptideDocNode molecule)
                {
                    molecules = new[] { molecule };
                }
                else if (docNode is PeptideGroupDocNode peptideGroupDocNode)
                {
                    molecules = peptideGroupDocNode.Molecules;
                }
                else
                {
                    continue;
                }

                normalizationMethods.UnionWith(molecules.Select(mol =>
                    mol.NormalizationMethod ?? defaultNormalizationMethod));
            }

            return normalizationMethods;
        }
    }
}
