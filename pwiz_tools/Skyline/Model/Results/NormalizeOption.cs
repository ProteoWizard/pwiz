using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;

namespace pwiz.Skyline.Model.Results
{
    public abstract class NormalizeOption
    {
        public static readonly NormalizeOption NONE = new Simple(NormalizationMethod.NONE);
        private static Dictionary<string, Special> _specialOptions 
            = new Dictionary<string, Special>();
        public static readonly NormalizeOption MAXIMUM = new Special(@"maximum", ()=>"Maximum");
        public static readonly NormalizeOption TOTAL = new Special(@"total", ()=>"Total");
        public static readonly NormalizeOption DEFAULT = new Special(@"default", ()=>"Default Normalization Method");
        public static readonly NormalizeOption CALIBRATED = new Special(@"calibrated", ()=>"Calibration Curve");

        public static readonly NormalizeOption GLOBAL_STANDARDS =
            FromNormalizationMethod(NormalizationMethod.GLOBAL_STANDARDS);


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

        public class Special : NormalizeOption
        {
            private string _persistedName;
            private Func<string> _getCaptionFunc;
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

        public abstract string PersistedName { get; }

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

        public static NormalizeOption FromPersistedName(string name, SrmSettings srmSettings)
        {
            NormalizeOption normalizeOption;
            if (string.IsNullOrEmpty(name))
            {
                normalizeOption = RatioToFirstStandard(srmSettings);
            }
            else
            {
                normalizeOption = FromPersistedName(name);
            }

            return normalizeOption.Constrain(srmSettings);
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
        /// Note: never includes "None", "Total", or "Maximum".
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        public static IEnumerable<NormalizeOption> AvailableNormalizeOptions(SrmDocument document)
        {
            yield return DEFAULT;
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

        public static NormalizeOption RatioToFirstStandard(SrmSettings settings)
        {
            var firstLabelType = settings.PeptideSettings.Modifications.RatioInternalStandardTypes.FirstOrDefault();
            if (firstLabelType != null)
            {
                return FromNormalizationMethod(new NormalizationMethod.RatioToLabel(firstLabelType));
            }

            return NONE;
        }

        public static NormalizeOption Constrain(SrmSettings settings, NormalizeOption NormalizeOption)
        {
            return (NormalizeOption ?? RatioToFirstStandard(settings)).Constrain(settings);
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

            return this;
        }
    }
}
