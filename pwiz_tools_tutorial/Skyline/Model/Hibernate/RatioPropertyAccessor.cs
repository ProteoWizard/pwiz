using System;
using System.Collections;
using System.Reflection;
using NHibernate.Engine;
using NHibernate.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Hibernate
{
    public class RatioPropertyAccessor : IPropertyAccessor
    {
        public enum RatioTarget
        {
            peptide_result, precursor_result, transition_result
        }

        private const string RATIO_PREFIX = "ratio_";

        public static string GetPeptideResultsHeader(IsotopeLabelType labelType, IsotopeLabelType standardType)
        {
            return string.Format("Ratio {0} To {1}", labelType.Title, standardType.Title);
        }

        public static string GetPeptideKey(IsotopeLabelType labelType, IsotopeLabelType standardType)
        {
            return string.Format("Ratio{0}To{1}",
                Helpers.MakeId(labelType.ToString(), true),
                Helpers.MakeId(standardType.ToString(), true));
        }

        public static string GetPeptideColumnName(IsotopeLabelType labelType, IsotopeLabelType standardType)
        {
            return RATIO_PREFIX + GetPeptideKey(labelType, standardType);
        }

        public static string GetPrecursorResultsHeader(IsotopeLabelType standardType)
        {
            return "Total " + GetTransitionResultsHeader(standardType);
        }

        public static string GetPrecursorKey(IsotopeLabelType standardType)
        {
            return "Total" + GetTransitionKey(standardType);
        }

        public static string GetPrecursorColumnName(IsotopeLabelType standardType)
        {
            return RATIO_PREFIX + GetPrecursorKey(standardType);
        }

        public static string GetTransitionResultsHeader(IsotopeLabelType standardType)
        {
            return "Area Ratio To " + standardType.Title;
        }

        public static string GetTransitionKey(IsotopeLabelType standardType)
        {
            return "AreaRatioTo" + Helpers.MakeId(standardType.Name, true);
        }

        public static string GetTransitionColumnName(IsotopeLabelType standardType)
        {
            return RATIO_PREFIX + GetTransitionKey(standardType);
        }

        public static string GetDisplayName(string propertyName)
        {
            return propertyName.Substring(RATIO_PREFIX.Length);
        }

        public static bool IsRatioProperty(string propertyName)
        {
            return propertyName.StartsWith(RATIO_PREFIX);
        }

        public IGetter GetGetter(Type theClass, string propertyName)
        {
            return new Getter(GetDisplayName(propertyName));
        }

        public ISetter GetSetter(Type theClass, string propertyName)
        {
            return new Setter(GetDisplayName(propertyName));
        }

        public bool CanAccessThroughReflectionOptimizer
        {
            get { return false; }
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
                double? value;
                ((DbRatioResult)target).LabelRatios.TryGetValue(_name, out value);
                return value;
            }

            public object GetForInsert(object owner, IDictionary mergeMap, ISessionImplementor session)
            {
                return Get(owner);
            }

            public Type ReturnType
            {
                get { return typeof(bool); }
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
                ((DbRatioResult)target).LabelRatios[_name] = (double?) value;
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