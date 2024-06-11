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
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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

        public Step ResolveStep(MetadataRule extractedMetadataRule, List<CommonException<StepError>> errors)
        {
            var sourceColumn = ResolveColumn(extractedMetadataRule, nameof(extractedMetadataRule.Source), extractedMetadataRule.Source, errors);
            Regex regex = null;
            if (!string.IsNullOrEmpty(extractedMetadataRule.Pattern))
            {
                try
                {
                    regex = new Regex(extractedMetadataRule.Pattern);
                }
                catch (Exception x)
                {
                    errors?.Add(CommonException.Create(new StepError(
                        extractedMetadataRule,
                        nameof(extractedMetadataRule.Pattern),
                        x.Message), x));
                }
            }

            var targetColumn = ResolveColumn(extractedMetadataRule, nameof(extractedMetadataRule.Target),
                extractedMetadataRule.Target, errors);
            return new Step(extractedMetadataRule, sourceColumn, regex, extractedMetadataRule.Replacement, targetColumn);
        }

        public IEnumerable<TextColumnWrapper> GetSourceColumns()
        {
            return _textColumns.Values.Where(IsSource);
        }

        public IEnumerable<TextColumnWrapper> GetTargetColumns()
        {
            return _textColumns.Values.Where(IsTarget);
        }

        public CultureInfo CultureInfo { get; set; }

        public MetadataStepResult ApplyStep(object sourceObject, Step step)
        {
            if (step.Source == null)
            {
                return null;
            }

            string sourceText = step.Source.GetTextValue(CultureInfo, sourceObject);
            bool isMatch;
            string strMatchedValue;
            string strReplacedValue;

            if (step.Regex!= null)
            {
                var match = step.Regex.Match(sourceText);
                if (match.Success)
                {
                    strMatchedValue = match.ToString();
                    if (!string.IsNullOrEmpty(step.Replacement))
                    {
                        strReplacedValue = match.Result(step.Replacement);
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
                if (string.IsNullOrEmpty(step.Replacement))
                {
                    strReplacedValue = strMatchedValue;
                }
                else
                {
                    strReplacedValue = step.Replacement;
                }
            }

            object targetValue = null;
            string strErrorText = null;

            if (strReplacedValue != null)
            {
                isMatch = true;
                if (step.Target != null)
                {
                    try
                    {
                        targetValue = step.Target.ParseTextValue(CultureInfo, strReplacedValue);
                    }
                    catch (Exception x)
                    {
                        string message = TextUtil.LineSeparate(
                            string.Format(MetadataExtractionResources.MetadataExtractor_ApplyStep_Error_converting___0___to___1___, strReplacedValue,
                                step.Target.DisplayName),
                            x.Message);
                        strErrorText = message;
                    }
                }
            }
            else
            {
                isMatch = false;
            }
            return new MetadataStepResult(step.Def, sourceText, isMatch, strMatchedValue, strReplacedValue, targetValue, strErrorText);
        }

        public TextColumnWrapper FindColumn(PropertyPath propertyPath)
        {
            return ResolveColumn(null, null, propertyPath, null);
        }

        private TextColumnWrapper ResolveColumn(MetadataRule rule, string propertyName, PropertyPath propertyPath, List<CommonException<StepError>> errors)
        {
            if (propertyPath == null)
            {
                return null;
            }

            TextColumnWrapper textColumn;
            if (!_textColumns.TryGetValue(propertyPath, out textColumn))
            {
                errors?.Add(CommonException.Create(new StepError(rule, propertyName, 
                    string.Format(MetadataExtractionResources.MetadataExtractor_ResolveColumn_Unable_to_find_column__0_, propertyPath))));
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

        /// <summary>
        /// Returns true if the column is allowed to be the target of a rule
        /// </summary>
        public static bool IsTarget(TextColumnWrapper column)
        {
            if (column.IsImportable)
            {
                // All columns with the [Importable] attribute are valid targets
                return true;
            }

            if (column.ColumnDescriptor.Parent?.PropertyType == typeof(Replicate) &&
                column.ColumnDescriptor.Name == nameof(Replicate.Name))
            {
                // Allow the "Replicate Name" to be a target even though it cannot be imported
                // with "Import Annotations".
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

        public class StepError
        {
            public StepError(
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

        public class RuleError
        {
            public RuleError(string ruleName, MsDataFileUri msDataFileUri, string message)
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
                return TextUtil.LineSeparate(string.Format(MetadataExtractionResources.RuleError_ToString_An_error_occurred_applying_the_rule___0___, RuleName), Message);
                
            }
        }

        public void ApplyRule(MetadataRuleSet ruleSetSet, HashSet<MsDataFileUri> dataFileUris)
        {
            var skylineDataSchema = (SkylineDataSchema) _rootColumn.DataSchema;
            List<Step> resolvedRules = null;
            foreach (var resultFile in skylineDataSchema.ResultFileList.Values)
            {
                if (dataFileUris != null && !dataFileUris.Contains(resultFile.ChromFileInfo.FilePath))
                {
                    continue;
                }

                if (resolvedRules == null)
                {
                    var ruleErrors = new List<CommonException<StepError>>();
                    resolvedRules = new List<Step>();
                    foreach (var rule in ruleSetSet.Rules)
                    {
                        var resolvedRule = ResolveStep(rule, ruleErrors);
                        if (ruleErrors.Count > 0)
                        {
                            var ruleError = ruleErrors[0];
                            throw CommonException.Create(
                                new RuleError(ruleSetSet.Name, resultFile.ChromFileInfo.FilePath, ruleError.Message),
                                ruleError);

                        }
                        resolvedRules.Add(resolvedRule);
                    }
                }

                var properties = new HashSet<PropertyPath>();
                foreach (var rule in resolvedRules)
                {
                    if (properties.Contains(rule.Def.Target))
                    {
                        continue;
                    }

                    var result = ApplyStep(resultFile, rule);
                    if (result.ErrorText != null)
                    {
                        throw CommonException.Create(new RuleError(ruleSetSet.Name, resultFile.ChromFileInfo.FilePath, result.ErrorText));
                    }
                    if (!result.Match)
                    {
                        continue;
                    }

                    try
                    {
                        properties.Add(rule.Def.Target);
                        rule.Target.SetTextValue(CultureInfo.InvariantCulture, resultFile, result.ReplacedValue);
                    }
                    catch (Exception exception)
                    {
                        throw CommonException.Create(new RuleError(ruleSetSet.Name, resultFile.ChromFileInfo.FilePath,
                            exception.Message), exception);
                    }
                }
            }
        }

        public static SrmDocument ApplyRules(SrmDocument document, HashSet<MsDataFileUri> dataFiles, out CommonException<RuleError> error)
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
                    metadataExtractor.ApplyRule(ruleSet, dataFiles);
                }

                dataSchema.CommitBatchModifyDocument(string.Empty, null);
                return dataSchema.Document;
            }
            catch (CommonException<RuleError> ex)
            {
                error = ex;
                return document;
            }
        }
        public class Step
        {
            public static readonly Step EMPTY = new Step(MetadataRule.EMPTY, null, null, null, null);
            public Step(MetadataRule def, TextColumnWrapper source, Regex regex, string replacement, TextColumnWrapper target)
            {
                Def = def;
                Source = source;
                Regex = regex;
                Replacement = replacement;
                Target = target;
            }

            public MetadataRule Def { get; private set; }
            public TextColumnWrapper Source { get; private set; }

            public string Replacement { get; private set; }
            public Regex Regex { get; private set; }
            public TextColumnWrapper Target { get; private set; }
        }

    }
}
