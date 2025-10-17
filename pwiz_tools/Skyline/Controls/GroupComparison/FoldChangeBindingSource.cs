﻿/*
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
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
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
        private int _updatingCount;
        public const string CLUSTERED_VIEW_NAME = "Clustered";


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
                _skylineDataSchema = SkylineWindowDataSchema.FromDocumentContainer(GroupComparisonModel.DocumentContainer);
                var rowSourceInfos = CreateRowSourceInfos(new FoldChangeRow[0], new FoldChangeDetailRow[0]);
                ViewContext = new GroupComparisonViewContext(_skylineDataSchema, rowSourceInfos);
                _container = new Container();
                _bindingListSource = new BindingListSource(_container);
                _bindingListSource.SetViewContext(ViewContext, rowSourceInfos[0].Views[0]);
                GroupComparisonModel.ModelChanged += GroupComparisonModelOnModelChanged;
                GroupComparisonModelOnModelChanged(GroupComparisonModel, new EventArgs());
            }
        }

        private void GroupComparisonModelOnModelChanged(object sender, EventArgs eventArgs)
        {
            if (null != _bindingListSource && 0 < _referenceCount)
            {
                Interlocked.Increment(ref _updatingCount);
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
                    Interlocked.Decrement(ref _updatingCount);
                });
            }
        }

        private void UpdateResults()
        {
            var results = GroupComparisonModel.Results;
            var factory = new FoldChangeRowFactory(_skylineDataSchema);
            var rows = ImmutableList.ValueOf(factory.GetFoldChangeRows(results));
            var detailRows = ImmutableList.ValueOf(factory.GetFoldChangeDetailRows(rows));
            SetRowSourceInfos(CreateRowSourceInfos(rows, detailRows));
        }

        private IList<RowSourceInfo> CreateRowSourceInfos(IList<FoldChangeRow> foldChangeRows, IList<FoldChangeDetailRow> detailRows)
        {
            var defaultViewSpec = GetDefaultViewSpec(foldChangeRows);
            var clusteredViewSpec = GetClusteredViewSpec(defaultViewSpec);


            var rowSourceInfos = new List<RowSourceInfo>()
            {
                new RowSourceInfo(new FixedSkylineObjectList<FoldChangeRow>(_skylineDataSchema, foldChangeRows),
                    new ViewInfo(_skylineDataSchema, typeof(FoldChangeRow), defaultViewSpec).ChangeViewGroup(ViewGroup.BUILT_IN)),
                new RowSourceInfo(new FixedSkylineObjectList<FoldChangeDetailRow>(_skylineDataSchema, detailRows),
                    new ViewInfo(_skylineDataSchema, typeof(FoldChangeDetailRow), clusteredViewSpec).ChangeViewGroup(ViewGroup.BUILT_IN))
            };
            return rowSourceInfos;
        }

        private void SetRowSourceInfos(IList<RowSourceInfo> rowSourceInfos)
        {
            var oldBuiltInViews = ViewContext.BuiltInViews.ToList();
            ViewContext.SetRowSources(rowSourceInfos);
            foreach (var rowSourceInfo in rowSourceInfos)
            {
                if (_bindingListSource.ViewSpec?.RowSource != rowSourceInfo.Name)
                {
                    continue;
                }
                var newViewSpecs = rowSourceInfo.Views.Select(viewInfo => viewInfo.GetViewSpec());
                var oldViewSpecs = oldBuiltInViews.Where(view => view.RowSource == rowSourceInfo.Name);
                ViewInfo newViewInfo = null;
                if (!newViewSpecs.SequenceEqual(oldViewSpecs))
                {
                    if (ViewGroup.BUILT_IN.Id.Equals(_bindingListSource.ViewInfo?.ViewGroup.Id))
                    {
                        newViewInfo =
                            rowSourceInfo.Views.FirstOrDefault(view => view.Name == _bindingListSource.ViewInfo.Name);
                    }
                }

                if (newViewInfo == null)
                {
                    _bindingListSource.RowSource = rowSourceInfo.Rows;
                }
                else
                {
                    _bindingListSource.SetView(newViewInfo, rowSourceInfo.Rows);
                }
            }
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
            // ReSharper disable LocalizableElement
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
            // ReSharper restore LocalizableElement

            var viewSpec = new ViewSpec()
                .SetName(AbstractViewContext.DefaultViewName)
                .SetRowSource(FoldChangeRow.ROW_SOURCE_NAME)
                .SetColumns(columns.Select(col => new ColumnSpec(col)));
            return viewSpec;
        }

        private ViewSpec GetClusteredViewSpec(ViewSpec defaultViewSpec)
        {
            var clusteredViewSpec = defaultViewSpec.SetName(CLUSTERED_VIEW_NAME).SetRowSource(FoldChangeDetailRow.ROW_SOURCE_NAME);

            PropertyPath ppRunAbundance = PropertyPath.Root.Property(nameof(FoldChangeDetailRow.ReplicateAbundances)).DictionaryValues();
            PropertyPath ppFoldChange = PropertyPath.Root.Property(nameof(FoldChangeDetailRow.FoldChangeResults))
                .DictionaryValues();
            var columnsToAdd = new List<PropertyPath>();
            var columnPrefixesToRemove = new List<PropertyPath>()
            {
                PropertyPath.Root.Property(nameof(FoldChangeRow.FoldChangeResult)),
                PropertyPath.Root.Property(nameof(FoldChangeRow.Group))
            };
            columnsToAdd.Add(ppFoldChange);
            columnsToAdd.Add(ppFoldChange.Property(nameof(FoldChangeResult.AdjustedPValue)));
            if (!string.IsNullOrEmpty(GroupComparisonModel.GroupComparisonDef.IdentityAnnotation))
            {
                columnsToAdd.Add(ppRunAbundance.Property(nameof(ReplicateRow.ReplicateSampleIdentity)));
            }
            columnsToAdd.Add(ppRunAbundance.Property(nameof(ReplicateRow.ReplicateGroup)));
            columnsToAdd.Add(ppRunAbundance.Property(nameof(ReplicateRow.Abundance)));
            clusteredViewSpec = clusteredViewSpec.SetColumns(clusteredViewSpec.Columns
                .Where(col => !columnPrefixesToRemove.Any(prefix=>col.PropertyPath.StartsWith(prefix)))
                .Concat(columnsToAdd.Select(col => new ColumnSpec(col))));
            return clusteredViewSpec;
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

        public bool IsComplete
        {
            get
            {
                if (_updatingCount > 0)
                {
                    return false;
                }
                if (GroupComparisonModel.PercentComplete < 100)
                {
                    return false;
                }
                if (!_bindingListSource.IsComplete)
                {
                    return false;
                }
                return _skylineDataSchema.IsDocumentUpToDate();
            }
        }

        public BindingListSource GetBindingListSource()
        {
            if (_referenceCount <= 0)
            {
                throw new ObjectDisposedException(@"FoldChangeBindingSource");
            }
            return _bindingListSource;
        }
    }
}
