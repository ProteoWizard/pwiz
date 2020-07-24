using System.Collections.Generic;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.ElementLocators.ExportAnnotations
{
    public class ExtractedMetadataResultRow
    {
        public ExtractedMetadataResultRow(object sourceObject)
        {
            SourceObject = sourceObject;
            Values = new Dictionary<ColumnKey, ExtractedMetadataResultColumn>();
            RuleResults = new List<ExtractedMetadataRuleResult>();
        }
        public object SourceObject { get; private set; }
        public IDictionary<ColumnKey, ExtractedMetadataResultColumn> Values { get; private set; }
        public IList<ExtractedMetadataRuleResult> RuleResults { get; private set; }

        public void AddRuleResult(ColumnKey columnKey, ExtractedMetadataRuleResult result)
        {
            if (result == null)
            {
                return;
            }
            RuleResults.Add(result);
            if (columnKey != null && result.Match && !Values.ContainsKey(columnKey))
            {
                Values.Add(columnKey, new ExtractedMetadataResultColumn(result.Rule, columnKey.DisplayName, result.TargetValue, result.ErrorText));
            }
        }

        public sealed class ColumnKey
        {
            public ColumnKey(PropertyPath propertyPath, string displayName)
            {
                PropertyPath = propertyPath;
                DisplayName = displayName;
            }

            public PropertyPath PropertyPath { get; private set; }
            public string DisplayName { get; private set; }

            private bool Equals(ColumnKey other)
            {
                return PropertyPath.Equals(other.PropertyPath);
            }

            public override bool Equals(object obj)
            {
                return ReferenceEquals(this, obj) || obj is ColumnKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return PropertyPath.GetHashCode();
            }

            public override string ToString()
            {
                return DisplayName;
            }
        }
    }

    public class ExtractedMetadataResultColumn : IErrorTextProvider
    {
        public ExtractedMetadataResultColumn(ExtractedMetadataRule rule, string columnName, object value, string errorText)
        {
            Rule = rule;
            ColumnName = columnName;
            Value = value;
            ErrorText = errorText;
        }
        public string ColumnName { get; private set; }
        public object Value { get; private set; }
        public string ErrorText { get; private set; }
        public ExtractedMetadataRule Rule { get; private set; }

        public string GetErrorText(string columnName)
        {
            return ErrorText;
        }
    }

    public class ExtractedMetadataRuleResult : IErrorTextProvider
    {
        public ExtractedMetadataRuleResult(ExtractedMetadataRule rule, string source, bool match, string matchedValue, string replacedValue,
            object target, string errorText)
        {
            Rule = rule;
            Source = source;
            Match = match;
            MatchedValue = matchedValue;
            ReplacedValue = replacedValue;
            TargetValue = target;
            ErrorText = errorText;
        }

        public ExtractedMetadataRule Rule { get; private set; }
        public string Source { get; private set; }
        public bool Match { get; private set; }
        public string MatchedValue { get; private set; }
        public string ReplacedValue { get; private set; }
        public object TargetValue { get; private set; }

        public string ErrorText { get; private set; }

        public string GetErrorText(string columnName)
        {
            if (columnName == nameof(ReplacedValue))
            {
                return ErrorText;
            }

            return null;
        }
    }
}
