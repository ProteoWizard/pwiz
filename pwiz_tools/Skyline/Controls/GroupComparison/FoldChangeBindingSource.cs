/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Threading;
using pwiz.Common.DataAnalysis;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.GroupComparison;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public class FoldChangeBindingSource
    {
        private int _referenceCount;
        private Container _container;
        private EventTaskScheduler _taskScheduler;
        private BindingListSource _bindingListSource;
        private SkylineDataSchema _skylineDataSchema;


        public FoldChangeBindingSource(GroupComparisonModel groupComparisonModel)
        {
            _container = new Container();
            GroupComparisonModel = groupComparisonModel;
            _taskScheduler = new EventTaskScheduler();
        }

        public GroupComparisonModel GroupComparisonModel { get; private set; }
        public GroupComparisonViewContext ViewContext { get; private set; }

        public void AddRef()
        {
            if (Interlocked.Increment(ref _referenceCount) == 1)
            {
                _skylineDataSchema = new SkylineDataSchema(GroupComparisonModel.DocumentContainer,
                    SkylineDataSchema.GetLocalizedSchemaLocalizer());
                var viewInfo = new ViewInfo(_skylineDataSchema, typeof(FoldChangeRow), GetDefaultViewSpec(new FoldChangeRow[0]))
                    .ChangeViewGroup(ViewGroup.BUILT_IN);
                var rowSourceInfo = new RowSourceInfo(typeof(FoldChangeRow), new StaticRowSource(new FoldChangeRow[0]), new[] { viewInfo });
                ViewContext = new GroupComparisonViewContext(_skylineDataSchema, new[]{rowSourceInfo});
                _container = new Container();
                _bindingListSource = new BindingListSource(_container);
                _bindingListSource.SetViewContext(ViewContext, viewInfo);
                GroupComparisonModel.ModelChanged += GroupComparisonModelOnModelChanged;
                GroupComparisonModelOnModelChanged(GroupComparisonModel, new EventArgs());
            }
        }

        private void GroupComparisonModelOnModelChanged(object sender, EventArgs eventArgs)
        {
            if (null != _bindingListSource && 0 < _referenceCount)
            {
                _taskScheduler.Run(() =>
                {
                    try
                    {
                        if (0 < _referenceCount)
                        {
                            UpdateResults();
                        }
                    }
                    catch (Exception e)
                    {
                        Program.ReportException(e);
                    }
                });
            }
        }

        private void UpdateResults()
        {
            var results = GroupComparisonModel.Results;
            var rows = new List<FoldChangeRow>();
            if (null != results)
            {
                Dictionary<int, double> criticalValuesByDegreesOfFreedom = new Dictionary<int, double>();
                var groupComparisonDef = results.GroupComparer.ComparisonDef;
                var adjustedPValues = PValues.AdjustPValues(results.ResultRows.Select(
                    row => row.LinearFitResult.PValue)).ToArray();
                for (int iRow = 0; iRow < results.ResultRows.Count; iRow++)
                {
                    var resultRow = results.ResultRows[iRow];
                    var protein = new Protein(_skylineDataSchema, new IdentityPath(resultRow.Selector.Protein.Id));
                    Model.Databinding.Entities.Peptide peptide = null;
                    if (null != resultRow.Selector.Peptide)
                    {
                        peptide = new Model.Databinding.Entities.Peptide(_skylineDataSchema,
                            new IdentityPath(protein.IdentityPath, resultRow.Selector.Peptide.Id));
                    }
                    double criticalValue;
                    if (!criticalValuesByDegreesOfFreedom.TryGetValue(resultRow.LinearFitResult.DegreesOfFreedom,
                        out criticalValue))
                    {
                        criticalValue = FoldChangeResult.GetCriticalValue(groupComparisonDef.ConfidenceLevel,
                            resultRow.LinearFitResult.DegreesOfFreedom);
                        criticalValuesByDegreesOfFreedom.Add(resultRow.LinearFitResult.DegreesOfFreedom, criticalValue);
                    }
                    FoldChangeResult foldChangeResult = new FoldChangeResult(groupComparisonDef.ConfidenceLevel,
                        adjustedPValues[iRow], resultRow.LinearFitResult, criticalValue);
                    rows.Add(new FoldChangeRow(protein, peptide, resultRow.Selector.LabelType,
                        resultRow.Selector.MsLevel, resultRow.Selector.GroupIdentifier, resultRow.ReplicateCount, foldChangeResult));
                }
            }
            var defaultViewSpec = GetDefaultViewSpec(rows);
            if (!Equals(defaultViewSpec, ViewContext.BuiltInViews.First()))
            {
                var viewInfo = new ViewInfo(_skylineDataSchema, typeof (FoldChangeRow), defaultViewSpec).ChangeViewGroup(ViewGroup.BUILT_IN);
                ViewContext.SetRowSources(new[]
                {
                    new RowSourceInfo(new StaticRowSource(rows), viewInfo)
                });
                if (null != _bindingListSource.ViewSpec && _bindingListSource.ViewSpec.Name == defaultViewSpec.Name &&
                    !_bindingListSource.ViewSpec.Equals(defaultViewSpec))
                {
                    _bindingListSource.SetView(viewInfo, new StaticRowSource(rows));
                }
            }
            _bindingListSource.RowSource = new StaticRowSource(rows);
        }

        private ViewSpec GetDefaultViewSpec(IList<FoldChangeRow> foldChangeRows)
        {
            bool showPeptide;
            bool showLabelType;
            bool showMsLevel;
            bool showGroup;
            if (foldChangeRows.Any())
            {
                showPeptide = foldChangeRows.Any(row => null != row.Peptide);
                showLabelType = foldChangeRows.Select(row => row.IsotopeLabelType).Distinct().Count() > 1;
                showMsLevel = foldChangeRows.Select(row => row.MsLevel).Distinct().Count() > 1;
                showGroup = foldChangeRows.Select(row => row.Group).Distinct().Count() > 1;
            }
            else
            {
                showPeptide = !GroupComparisonModel.GroupComparisonDef.PerProtein;
                showLabelType = false;
                showMsLevel = false;
                showGroup = false;
            }
            // ReSharper disable NonLocalizedString
            var columns = new List<PropertyPath>
            {
                PropertyPath.Root.Property("Protein")
            };
            if (showPeptide)
            {
                columns.Add(PropertyPath.Root.Property("Peptide"));
            }
            if (showMsLevel)
            {
                columns.Add(PropertyPath.Root.Property("MsLevel"));
            }
            if (showLabelType)
            {
                columns.Add(PropertyPath.Root.Property("IsotopeLabelType"));
            }
            if (showGroup)
            {
                columns.Add(PropertyPath.Root.Property("Group"));
            }
            columns.Add(PropertyPath.Root.Property("FoldChangeResult"));
            columns.Add(PropertyPath.Root.Property("FoldChangeResult").Property("AdjustedPValue"));
            // ReSharper restore NonLocalizedString

            var viewSpec = new ViewSpec()
                .SetName(AbstractViewContext.DefaultViewName)
                .SetRowType(typeof (FoldChangeRow))
                .SetColumns(columns.Select(col => new ColumnSpec(col)));
            return viewSpec;
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref _referenceCount) == 0)
            {
                _container.Dispose();
                _container = null;
                GroupComparisonModel.ModelChanged -= GroupComparisonModelOnModelChanged;
                _taskScheduler.Dispose();
            }
        }

        public BindingListSource GetBindingListSource()
        {
            if (_referenceCount <= 0)
            {
                throw new ObjectDisposedException("FoldChangeBindingSource"); // Not L10N
            }
            return _bindingListSource;
        }

        public class FoldChangeRow
        {
            public FoldChangeRow(Protein protein, Model.Databinding.Entities.Peptide peptide, IsotopeLabelType labelType,
                int? msLevel, GroupIdentifier group, int replicateCount, FoldChangeResult foldChangeResult)
            {
                Protein = protein;
                Peptide = peptide;
                IsotopeLabelType = labelType;
                MsLevel = msLevel;
                ReplicateCount = replicateCount;
                FoldChangeResult = foldChangeResult;
                Group = group;
            }

            public Protein Protein { get; private set; }
            public Model.Databinding.Entities.Peptide Peptide { get; private set; }
            public IsotopeLabelType IsotopeLabelType { get; private set; }
            public int? MsLevel { get; private set; }
            public GroupIdentifier Group { get; private set; }
            public int ReplicateCount { get; private set; }
            public FoldChangeResult FoldChangeResult { get; private set; }
        }
    }
}
