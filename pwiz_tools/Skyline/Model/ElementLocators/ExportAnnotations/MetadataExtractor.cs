using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.ElementLocators.ExportAnnotations
{
    public class MetadataExtractor
    {
        private IDictionary<PropertyPath, TextColumnWrapper> _textColumns;
        private ColumnDescriptor _rootColumn;

        public MetadataExtractor(SkylineDataSchema dataSchema, Type sourceObjectType) 
            : this(dataSchema, sourceObjectType, new ExtractedMetadataRuleSet(sourceObjectType.FullName, new ExtractedMetadataRule[0]))
        {

        }
        public MetadataExtractor(SkylineDataSchema dataSchema, Type sourceObjectType, ExtractedMetadataRuleSet ruleSet)
        {
            DataSchema = dataSchema;
            RuleSet = ruleSet;
            _rootColumn = ColumnDescriptor.RootColumn(dataSchema, sourceObjectType);
            _textColumns = GetAllTextColumnWrappers(_rootColumn).ToDictionary(col => col.PropertyPath);
            CultureInfo = CultureInfo.InvariantCulture;

        }

        public SkylineDataSchema DataSchema { get; private set; }
        public ExtractedMetadataRuleSet RuleSet { get; private set; }

        public Rule ResolveRule(ExtractedMetadataRule extractedMetadataRule)
        {
            var sourceColumn = ResolveColumn(nameof(extractedMetadataRule.SourceColumn),
                extractedMetadataRule.SourceColumn);
            Regex regex = null;
            if (!string.IsNullOrEmpty(extractedMetadataRule.MatchRegularExpression))
            {
                try
                {
                    regex = new Regex(extractedMetadataRule.MatchRegularExpression);
                }
                catch (Exception x)
                {
                    throw CommonException.Create(new RuleError(nameof(extractedMetadataRule.MatchRegularExpression),
                        x.Message));
                }
            }

            var targetColumn = ResolveColumn(nameof(extractedMetadataRule.TargetColumn),
                extractedMetadataRule.TargetColumn);
            return new Rule(extractedMetadataRule, sourceColumn, regex, targetColumn);
        }

        public CultureInfo CultureInfo { get; set; }

        public ExtractedMetadataRuleResult ApplyRule(object sourceObject, Rule rule)
        {
            if (rule.Source == null)
            {
                return null;
            }

            string sourceText = rule.Source.GetTextValue(CultureInfo, sourceObject);
            bool isMatch;
            string strExtractedValue;
            if (rule.Regex!= null)
            {
                var match = rule.Regex.Match(sourceText);
                if (match.Success)
                {
                    strExtractedValue = match.Groups[Math.Min(match.Groups.Count - 1, 1)].ToString();
                }
                else
                {
                    strExtractedValue = null;
                }
            }
            else
            {
                strExtractedValue = sourceText;
            }

            object targetValue = null;
            string strErrorText = null;

            if (strExtractedValue != null)
            {
                isMatch = true;
                if (rule.Target != null)
                {
                    try
                    {
                        targetValue = rule.Target.ParseTextValue(CultureInfo, strExtractedValue);
                    }
                    catch (Exception x)
                    {
                        string message = TextUtil.LineSeparate(
                            string.Format("Error converting '{0}' to '{1}':", strExtractedValue,
                                rule.Target.DisplayName),
                            x.Message);
                        strErrorText = message;
                    }
                }
            }
            else
            {
                isMatch = false;
            }
            return new ExtractedMetadataRuleResult(rule.Def, sourceText, isMatch, strExtractedValue, targetValue, strErrorText);
        }

        public TextColumnWrapper FindColumn(string name)
        {
            try
            {
                return ResolveColumn(null, name);
            }
            catch
            {
                return null;
            }
        }

        private TextColumnWrapper ResolveColumn(string propertyName, string strPropertyPath)
        {
            if (string.IsNullOrEmpty(strPropertyPath))
            {
                return null;
            }
            PropertyPath propertyPath;
            try
            {
                propertyPath = PropertyPath.Parse(strPropertyPath);
            }
            catch (Exception x)
            {
                throw CommonException.Create(new RuleError(propertyName,
                    "Invalid column identifier " + strPropertyPath));
            }

            TextColumnWrapper textColumn;
            if (!_textColumns.TryGetValue(propertyPath, out textColumn))
            {
                throw CommonException.Create(new RuleError(propertyName, "Unable to find column " + strPropertyPath));
            }

            return textColumn;
        }


        public static IEnumerable<TextColumnWrapper> GetAllTextColumnWrappers(ColumnDescriptor columnDescriptor)
        {
            var columns = Enumerable.Empty<TextColumnWrapper>();
            if (columnDescriptor.CollectionInfo != null || columnDescriptor.IsAdvanced)
            {
                return columns;
            }

            if (!typeof(SkylineObject).IsAssignableFrom(columnDescriptor.PropertyType)
                && !@"Locator".Equals(columnDescriptor.PropertyPath.Name))
            {
                columns = columns.Append(new TextColumnWrapper(columnDescriptor));
            }

            columns = columns.Concat(columnDescriptor.GetChildColumns().SelectMany(GetAllTextColumnWrappers));

            return columns;
        }

        public static bool IsSource(TextColumnWrapper column)
        {
            return !IsTarget(column);
        }

        public static bool IsTarget(TextColumnWrapper column)
        {
            if (column.IsImportable)
            {
                return true;
            }

            var annotationDef = column.AnnotationDef;
            if (annotationDef != null)
            {
                if (annotationDef.Expression == null)
                {
                    return true;
                }
            }

            return false;
        }

        public class Rule
        {
            public static readonly Rule EMPTY = new Rule(ExtractedMetadataRule.EMPTY, null, null, null);
            public Rule(ExtractedMetadataRule def, TextColumnWrapper source, Regex regex, TextColumnWrapper target)
            {
                Def = def;
                Source = source;
                Regex = regex;
                Target = target;
            }

            public ExtractedMetadataRule Def { get; private set; }
            public TextColumnWrapper Source { get; private set; }

            public Regex Regex { get; private set; }
            public TextColumnWrapper Target { get; private set; }
        }

        public class RuleError
        {
            public RuleError(string property, string message)
            {
                Property = property;
                Message = message;
            }
            public string Property { get; private set; }
            public string Message { get; private set; }
        }
    }
}
