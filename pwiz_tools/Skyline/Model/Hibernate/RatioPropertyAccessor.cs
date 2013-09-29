using System;
using System.Collections;
using System.Reflection;
using NHibernate.Engine;
using NHibernate.Properties;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Hibernate
{
    public class RatioPropertyAccessor : IPropertyAccessor
    {
        public enum RatioTarget
        {
            peptide_result, precursor_result, transition_result
        }

        private const string RATIO_PREFIX = "ratio_"; // Not L10N
        private const string RDOTP_PREFIX = "rdotp_";

        public static RatioPropertyName PeptideRatioProperty(IsotopeLabelType labelType, IsotopeLabelType standardType)
        {
            string propertyKey = string.Format("Ratio{0}To{1}",
                Helpers.MakeId(labelType.Name, true),
                Helpers.MakeId(standardType.Name, true));
            string headerText = string.Format(
                Resources.RatioPropertyAccessor_PeptideProperty_Ratio__0__To__1_,
                labelType.Title, standardType.Title);

            return new RatioPropertyName(RATIO_PREFIX, propertyKey, headerText);
        }

        public static RatioPropertyName PrecursorRatioProperty(IsotopeLabelType standardType)
        {
            string key = "TotalAreaRatioTo" + Helpers.MakeId(standardType.Name, true);
            string header = TextUtil.SpaceSeparate(Resources.RatioPropertyAccessor_PrecursorProperty_Total,
                TextUtil.SpaceSeparate(Resources.RatioPropertyAccessor_GetTransitionResultsHeader_Area_Ratio_To, standardType.Title));
            return new RatioPropertyName(RATIO_PREFIX, key, header);
        }

        public static RatioPropertyName TransitionRatioProperty(IsotopeLabelType standardType)
        {
            string key = "AreaRatioTo" + Helpers.MakeId(standardType.Name, true);
            string header = TextUtil.SpaceSeparate(Resources.RatioPropertyAccessor_GetTransitionResultsHeader_Area_Ratio_To,
                    standardType.Title);
            return new RatioPropertyName(RATIO_PREFIX, key, header);
        }

        public static RatioPropertyName PeptideRdotpProperty(IsotopeLabelType labelType, IsotopeLabelType standardType)
        {
            string key = string.Format("DotProduct{0}To{1}",
                Helpers.MakeId(labelType.Name, true),
                Helpers.MakeId(standardType.Name, true));
            string headerText = string.Format(Resources.RDotPPropertyAccessor_PeptideProperty_Dot_Product__0__To__1_,
                labelType.Title, standardType.Title);
            return new RatioPropertyName(RDOTP_PREFIX, key, headerText);
        }

        public static RatioPropertyName PrecursorRdotpProperty(IsotopeLabelType standardType)
        {
            string key = string.Format("DotProductTo{0}", Helpers.MakeId(standardType.Name, true));
            string headerText = string.Format(Resources.RDotPPropertyAccessor_PrecursorProperty_Dot_Product_To__0_, standardType.Title);
            return new RatioPropertyName(RDOTP_PREFIX, key, headerText);
        }

        public static string KeyFromPropertyName(string propertyName)
        {
            Helpers.Assume(IsRatioOrRdotpProperty(propertyName));
            return propertyName.Substring(RATIO_PREFIX.Length);
        }

        public static bool IsRatioOrRdotpProperty(string propertyName)
        {
            return IsRatioProperty(propertyName) || IsRdotpProperty(propertyName);
        }

        public static bool IsRatioProperty(string propertyName)
        {
            return propertyName.StartsWith(RATIO_PREFIX);
        }

        public static bool IsRdotpProperty(string propertyName)
        {
            return propertyName.StartsWith(RDOTP_PREFIX);
        }

        public static string GetDisplayName(string propertyName)
        {
            if (propertyName.StartsWith(RATIO_PREFIX))
            {
                return propertyName.Substring(RATIO_PREFIX.Length);
            }
            if (propertyName.StartsWith(RDOTP_PREFIX))
            {
                return propertyName.Substring(RDOTP_PREFIX.Length);
            }
            throw new ArgumentException(string.Format("Invalid ratio column '{0}'", propertyName));
        }

        public IGetter GetGetter(Type theClass, string propertyName)
        {
            return new Getter(propertyName);
        }

        public ISetter GetSetter(Type theClass, string propertyName)
        {
            return new Setter(propertyName);
        }

        public bool CanAccessThroughReflectionOptimizer
        {
            get { return false; }
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

        private class Getter : IGetter
        {
            private readonly string _name;

            public Getter(String name)
            {
                _name = name;
            }

            public object Get(object target)
            {
                double value;
                if (((DbRatioResult) target).LabelRatios.TryGetValue(_name, out value))
                {
                    return value;
                }
                return null;
            }

            public object GetForInsert(object owner, IDictionary mergeMap, ISessionImplementor session)
            {
                return Get(owner);
            }

            public Type ReturnType
            {
                get { return typeof(double?); }
            }

            public string PropertyName
            {
                get { return null; }
            }

            public MethodInfo Method
            {
                get { return null; }
            }
        }

        private class Setter : ISetter
        {
            private readonly string _name;

            public Setter(String name)
            {
                _name = name;
            }

            public void Set(object target, object value)
            {
                double? doubleValue = value as double?;
                if (doubleValue.HasValue)
                {
                    ((DbRatioResult)target).LabelRatios[_name] = doubleValue.Value;
                }
                else
                {
                    ((DbRatioResult)target).LabelRatios.Remove(_name);
                }
            }

            public string PropertyName
            {
                get { return null; }
            }

            public MethodInfo Method
            {
                get { return null; }
            }
        }
    }
}