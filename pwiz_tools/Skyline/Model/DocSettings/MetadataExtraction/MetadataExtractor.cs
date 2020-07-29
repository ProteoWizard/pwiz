using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DocSettings.MetadataExtraction
{
    public class MetadataExtractor
    {
        private IDictionary<PropertyPath, TextColumnWrapper> _textColumns;
        private ColumnDescriptor _rootColumn;

        public MetadataExtractor(SkylineDataSchema dataSchema, Type sourceObjectType)
        {
            DataSchema = dataSchema;
            _rootColumn = ColumnDescriptor.RootColumn(dataSchema, sourceObjectType);
            _textColumns = GetAllTextColumnWrappers(_rootColumn).ToDictionary(col => col.PropertyPath);
            CultureInfo = CultureInfo.InvariantCulture;

        }

        public SkylineDataSchema DataSchema { get; private set; }
        public MetadataRuleSet RuleSet { get; private set; }

        public ResolvedMetadataRule ResolveRule(MetadataRule extractedMetadataRule)
        {
            var sourceColumn = ResolveColumn(extractedMetadataRule, nameof(extractedMetadataRule.Source), extractedMetadataRule.Source);
            Regex regex = null;
            if (!string.IsNullOrEmpty(extractedMetadataRule.Pattern))
            {
                try
                {
                    regex = new Regex(extractedMetadataRule.Pattern);
                }
                catch (Exception x)
                {
                    throw CommonException.Create(new RuleError(
                        extractedMetadataRule,
                        nameof(extractedMetadataRule.Pattern),
                        x.Message));
                }
            }

            var targetColumn = ResolveColumn(extractedMetadataRule, nameof(extractedMetadataRule.Target),
                extractedMetadataRule.Target);
            return new ResolvedMetadataRule(extractedMetadataRule, sourceColumn, regex, extractedMetadataRule.Replacement, targetColumn);
        }

        public CultureInfo CultureInfo { get; set; }

        public ExtractedMetadataRuleResult ApplyRule(object sourceObject, ResolvedMetadataRule metdataRule)
        {
            if (metdataRule.Source == null)
            {
                return null;
            }

            string sourceText = metdataRule.Source.GetTextValue(CultureInfo, sourceObject);
            bool isMatch;
            string strMatchedValue;
            string strReplacedValue;

            if (metdataRule.Regex!= null)
            {
                var match = metdataRule.Regex.Match(sourceText);
                if (match.Success)
                {
                    strMatchedValue = match.ToString();
                    if (!string.IsNullOrEmpty(metdataRule.Replacement))
                    {
                        strReplacedValue = match.Result(metdataRule.Replacement);
                    }
                    else
                    {
                        strReplacedValue = strMatchedValue;
                    }
                }
                else
                {
                    strReplacedValue = strMatchedValue = null;
                }
            }
            else
            {
                strMatchedValue = sourceText;
                if (string.IsNullOrEmpty(metdataRule.Replacement))
                {
                    strReplacedValue = strMatchedValue;
                }
                else
                {
                    strReplacedValue = metdataRule.Replacement;
                }
            }

            object targetValue = null;
            string strErrorText = null;

            if (strReplacedValue != null)
            {
                isMatch = true;
                if (metdataRule.Target != null)
                {
                    try
                    {
                        targetValue = metdataRule.Target.ParseTextValue(CultureInfo, strReplacedValue);
                    }
                    catch (Exception x)
                    {
                        string message = TextUtil.LineSeparate(
                            string.Format("Error converting '{0}' to '{1}':", strReplacedValue,
                                metdataRule.Target.DisplayName),
                            x.Message);
                        strErrorText = message;
                    }
                }
            }
            else
            {
                isMatch = false;
            }
            return new ExtractedMetadataRuleResult(metdataRule.Def, sourceText, isMatch, strMatchedValue, strReplacedValue, targetValue, strErrorText);
        }

        public TextColumnWrapper FindColumn(PropertyPath propertyPath)
        {
            try
            {
                return ResolveColumn(null, null, propertyPath);
            }
            catch
            {
                return null;
            }
        }

        private TextColumnWrapper ResolveColumn(MetadataRule rule, string propertyName, PropertyPath propertyPath)
        {
            if (propertyPath == null)
            {
                return null;
            }

            TextColumnWrapper textColumn;
            if (!_textColumns.TryGetValue(propertyPath, out textColumn))
            {
                throw CommonException.Create(new RuleError(rule, propertyName, "Unable to find column " + propertyPath));
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

        public class RuleError
        {
            public RuleError(
                MetadataRule rule,
                string property, string message)
            {
                Rule = rule;
                Property = property;
                Message = message;
            }

            public MetadataRule Rule
            {
                get;
                private set;
            }

            public string Property { get; private set; }
            public string Message
            {
                get;
                private set;
            }
        }

        public class RuleSetError
        {
            public RuleSetError(string ruleName, MsDataFileUri msDataFileUri, string message)
            {
                RuleName = ruleName;
                MsDataFileUri = msDataFileUri;
                Message = message;
            }

            public string RuleName { get; private set; }

            public MsDataFileUri MsDataFileUri { get; private set; }
            public string Message { get; private set; }

            public override string ToString()
            {
                return TextUtil.LineSeparate(string.Format("An error occurred applying the rule '{0}':", RuleName), Message);
                
            }
        }

        public void ApplyRules(MetadataRuleSet ruleSet, HashSet<MsDataFileUri> dataFileUris)
        {
            var skylineDataSchema = (SkylineDataSchema) _rootColumn.DataSchema;
            List<ResolvedMetadataRule> resolvedRules = null;
            foreach (var resultFile in skylineDataSchema.ResultFileList.Values)
            {
                if (dataFileUris != null && !dataFileUris.Contains(resultFile.ChromFileInfo.FilePath))
                {
                    continue;
                }

                if (resolvedRules == null)
                {
                    try
                    {
                        resolvedRules = ruleSet.Rules.Select(ResolveRule).ToList();
                    }
                    catch (CommonException<RuleError> ruleError)
                    {
                        throw CommonException.Create(new RuleSetError(ruleSet.Name, resultFile.ChromFileInfo.FilePath, ruleError.Message));
                    }
                }

                var properties = new HashSet<PropertyPath>();
                foreach (var rule in resolvedRules)
                {
                    if (properties.Contains(rule.Def.Target))
                    {
                        continue;
                    }

                    var result = ApplyRule(resultFile, rule);
                    if (result.ErrorText != null)
                    {
                        throw CommonException.Create(new RuleSetError(ruleSet.Name, resultFile.ChromFileInfo.FilePath, result.ErrorText));
                    }
                    if (!result.Match)
                    {
                        continue;
                    }

                    properties.Add(rule.Def.Target);
                    rule.Target.SetTextValue(CultureInfo.InvariantCulture, resultFile, result.ReplacedValue);
                }
            }
        }

        public static SrmDocument ApplyRules(SrmDocument document, HashSet<MsDataFileUri> dataFiles, out CommonException<RuleSetError> error)
        {
            error = null;
            if (!document.Settings.DataSettings.MetadataRuleSets.Any())
            {
                return document;
            }
            try
            {
                var dataSchema = SkylineDataSchema.MemoryDataSchema(document, DataSchemaLocalizer.INVARIANT);
                dataSchema.BeginBatchModifyDocument();
                foreach (var ruleSet in document.Settings.DataSettings.MetadataRuleSets)
                {
                    var metadataExtractor = new MetadataExtractor(dataSchema, typeof(ResultFile));
                    metadataExtractor.ApplyRules(ruleSet, dataFiles);
                }

                dataSchema.CommitBatchModifyDocument(string.Empty, null);
                return dataSchema.Document;
            }
            catch (CommonException<RuleSetError> ex)
            {
                error = ex;
                return document;
            }
        }
    }
}
