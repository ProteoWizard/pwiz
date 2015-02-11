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
using System.Threading.Tasks;
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
        private TaskScheduler _taskScheduler;
        private BindingListSource _bindingListSource;
        private SkylineDataSchema _skylineDataSchema;
        private SkylineViewContext _skylineViewContext;


        public FoldChangeBindingSource(GroupComparisonModel groupComparisonModel)
        {
            _container = new Container();
            GroupComparisonModel = groupComparisonModel;
            _taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
        }

        public GroupComparisonModel GroupComparisonModel { get; private set; }

        public void AddRef()
        {
            if (Interlocked.Increment(ref _referenceCount) == 1)
            {
                _skylineDataSchema = new SkylineDataSchema(GroupComparisonModel.DocumentContainer,
                    SkylineDataSchema.GetLocalizedSchemaLocalizer());
                var viewInfo =
                    SkylineViewContext.GetDefaultViewInfo(ColumnDescriptor.RootColumn(_skylineDataSchema,
                        typeof(FoldChangeRow)));
                var rowSourceInfo = new RowSourceInfo(typeof(FoldChangeRow), new FoldChangeRow[0], new[] { viewInfo });
                _skylineViewContext = new SkylineViewContext(_skylineDataSchema, new[] { rowSourceInfo });
                _container = new Container();
                _bindingListSource = new BindingListSource(_container);
                _bindingListSource.SetViewContext(_skylineViewContext, viewInfo);
                GroupComparisonModel.ModelChanged += GroupComparisonModelOnModelChanged;
                GroupComparisonModelOnModelChanged(GroupComparisonModel, new EventArgs());
            }
        }

        private void GroupComparisonModelOnModelChanged(object sender, EventArgs eventArgs)
        {
            if (null != _bindingListSource)
            {
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        UpdateResults();
                    }
                    catch (Exception e)
                    {
                        Program.ReportException(e);
                    }
                }, CancellationToken.None, TaskCreationOptions.None, _taskScheduler);
            }
        }

        private void UpdateResults()
        {
            var results = GroupComparisonModel.Results;
            List<FoldChangeRow> rows = new List<FoldChangeRow>();
            if (null != results)
            {
                var groupComparisonDef = results.GroupComparer.ComparisonDef;
                var adjustedPValues = PValues.AdjustPValues(results.ResultRows.Select(
                    row => row.LinearFitResult.PValue)).ToArray();
                for (int iRow = 0; iRow < results.ResultRows.Count; iRow++)
                {
                    var resultRow = results.ResultRows[iRow];
                    var protein = new Protein(_skylineDataSchema, new IdentityPath(resultRow.PeptideGroup.Id));
                    Model.Databinding.Entities.Peptide peptide = null;
                    if (null != resultRow.Peptide)
                    {
                        peptide = new Model.Databinding.Entities.Peptide(_skylineDataSchema,
                            new IdentityPath(protein.IdentityPath, resultRow.Peptide.Id));
                    }
                    rows.Add(new FoldChangeRow(protein, peptide,
                        new FoldChangeResult(groupComparisonDef.ConfidenceLevel, 
                            adjustedPValues[iRow], resultRow.LinearFitResult)));
                }
            }
            _bindingListSource.RowSource = rows;
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref _referenceCount) == 0)
            {
                _container.Dispose();
                _container = null;
                GroupComparisonModel.ModelChanged -= GroupComparisonModelOnModelChanged;
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
            public FoldChangeRow(Protein protein, Model.Databinding.Entities.Peptide peptide, FoldChangeResult foldChangeResult)
            {
                Protein = protein;
                Peptide = peptide;
                FoldChangeResult = foldChangeResult;
            }
            public Protein Protein { get; private set; }
            public Model.Databinding.Entities.Peptide Peptide { get; private set; }
            public FoldChangeResult FoldChangeResult { get; private set; }
        }

    }
}
