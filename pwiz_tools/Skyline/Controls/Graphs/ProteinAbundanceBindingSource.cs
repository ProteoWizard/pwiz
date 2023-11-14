using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Graphs
{
    public class ProteinAbundanceBindingSource
    {
        private int _referenceCount;
        private Container _container;
        private EventTaskScheduler _taskScheduler;
        private BindingListSource _bindingListSource;
        private SkylineDataSchema _skylineDataSchema;
        private int _updatingCount;
        public const string CLUSTERED_VIEW_NAME = "Clustered";


        public ProteinAbundanceBindingSource(GroupComparisonModel groupComparisonModel)
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
                var rowSourceInfos = CreateRowSourceInfos(new ProteinAbundanceRow[0], new ProteinAbundanceDetailRow[0]);
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
            var rows = new List<ProteinAbundanceRow>();
            if (null != results)
            {
                var controlGroupIdentifier =
                    GroupComparisonModel.GroupComparisonDef.GetControlGroupIdentifier(_skylineDataSchema.Document
                        .Settings);
                for (int iRow = 0; iRow < results.ResultRows.Count; iRow++)
                {
                    var resultRow = results.ResultRows[iRow];
                    var protein = new Protein(_skylineDataSchema, new IdentityPath(resultRow.Selector.Protein.Id));
                    var runAbundances = new Dictionary<Replicate, ReplicateRow>();
                    var abundances = protein.GetProteinAbundances();
                    var abundance = abundances[1].Abundance;
                    ProteinAbundanceResult result = new ProteinAbundanceResult(abundance);

                    foreach (var runAbundance in resultRow.RunAbundances)
                    {
                        Replicate replicate = new Replicate(_skylineDataSchema, runAbundance.ReplicateIndex);
                        runAbundances.Add(replicate, new ReplicateRow(replicate, runAbundance.Control ?
                                controlGroupIdentifier : resultRow.Selector.GroupIdentifier
                            , runAbundance.BioReplicate, Math.Pow(2, runAbundance.Log2Abundance)));
                    }
                    rows.Add(new ProteinAbundanceRow(protein, resultRow.Selector.GroupIdentifier, resultRow.ReplicateCount, result, runAbundances));
                }
            }

            var detailRows = new List<ProteinAbundanceDetailRow>();
            foreach (var grouping in rows.ToLookup(row =>
                Tuple.Create(row.Protein)))
            {
                var proteinAbundanceResults = grouping.ToDictionary(row => row.Group, row => row.ProteinAbundanceResult);
                var runAbundances = new Dictionary<Replicate, ReplicateRow>();
                foreach (var abundance in grouping.SelectMany(row => row.ReplicateAbundances))
                {
                    runAbundances[abundance.Key] = abundance.Value;
                }
                detailRows.Add(new ProteinAbundanceDetailRow(grouping.Key.Item1, proteinAbundanceResults, runAbundances));
            }
            SetRowSourceInfos(CreateRowSourceInfos(rows, detailRows));
        }

        private IList<RowSourceInfo> CreateRowSourceInfos(IList<ProteinAbundanceRow> foldChangeRows, IList<ProteinAbundanceDetailRow> detailRows)
        {
            var defaultViewSpec = GetDefaultViewSpec(foldChangeRows);
            var clusteredViewSpec = GetClusteredViewSpec(defaultViewSpec);


            var rowSourceInfos = new List<RowSourceInfo>()
            {
                new RowSourceInfo(new FixedSkylineObjectList<ProteinAbundanceRow>(_skylineDataSchema, foldChangeRows),
                    new ViewInfo(_skylineDataSchema, typeof(ProteinAbundanceRow), defaultViewSpec).ChangeViewGroup(ViewGroup.BUILT_IN)),
                new RowSourceInfo(new FixedSkylineObjectList<ProteinAbundanceDetailRow>(_skylineDataSchema, detailRows),
                    new ViewInfo(_skylineDataSchema, typeof(ProteinAbundanceDetailRow), clusteredViewSpec).ChangeViewGroup(ViewGroup.BUILT_IN))
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

        private ViewSpec GetDefaultViewSpec(IList<ProteinAbundanceRow> foldChangeRows)
        {
            bool showGroup;
            if (foldChangeRows.Any())
            {
                showGroup = foldChangeRows.Select(row => row.Group).Distinct().Count() > 1;
            }
            else
            {
                showGroup = false;
            }
            // ReSharper disable LocalizableElement
            var columns = new List<PropertyPath>
            {
                PropertyPath.Root.Property("Protein")
            };
            
            if (showGroup)
            {
                columns.Add(PropertyPath.Root.Property("Group"));
            }
            columns.Add(PropertyPath.Root.Property("FoldChangeResult"));
            columns.Add(PropertyPath.Root.Property("FoldChangeResult").Property("AdjustedPValue"));
            // ReSharper restore LocalizableElement

            var viewSpec = new ViewSpec()
                .SetName(AbstractViewContext.DefaultViewName)
                .SetRowType(typeof(ProteinAbundanceRow))
                .SetColumns(columns.Select(col => new ColumnSpec(col)));
            return viewSpec;
        }

        private ViewSpec GetClusteredViewSpec(ViewSpec defaultViewSpec)
        {
            var clusteredViewSpec = defaultViewSpec.SetName(CLUSTERED_VIEW_NAME).SetRowType(typeof(ProteinAbundanceDetailRow));

            PropertyPath ppRunAbundance = PropertyPath.Root.Property(nameof(ProteinAbundanceDetailRow.ReplicateAbundances)).DictionaryValues();
            PropertyPath ppFoldChange = PropertyPath.Root.Property(nameof(ProteinAbundanceDetailRow.ProteinAbundanceResults))
                .DictionaryValues();
            var columnsToAdd = new List<PropertyPath>();
            var columnPrefixesToRemove = new List<PropertyPath>()
            {
                PropertyPath.Root.Property(nameof(ProteinAbundanceRow.ProteinAbundanceResult)),
                PropertyPath.Root.Property(nameof(ProteinAbundanceRow.Group))
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
                .Where(col => !columnPrefixesToRemove.Any(prefix => col.PropertyPath.StartsWith(prefix)))
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

        public abstract class AbstractProteinAbundanceRow
        {
            public AbstractProteinAbundanceRow(Protein protein, 
                IDictionary<Replicate, ReplicateRow> replicateResults)
            {
                Protein = protein;
                ReplicateAbundances = replicateResults;
            }

            public Protein Protein { get; private set; }

            [OneToMany(IndexDisplayName = "Replicate")]
            public IDictionary<Replicate, ReplicateRow> ReplicateAbundances { get; private set; }

            public abstract IEnumerable<ProteinAbundanceRow> GetProteinAbundanceRows();
        }

        public class ProteinAbundanceRow : AbstractProteinAbundanceRow
        {
            public ProteinAbundanceRow(Protein protein, GroupIdentifier group, int replicateCount, ProteinAbundanceResult proteinAbundanceResult, IDictionary<Replicate, ReplicateRow> replicateResults)
                : base(protein, replicateResults)
            {
                ReplicateCount = replicateCount;
                ProteinAbundanceResult = proteinAbundanceResult;
                Group = group;
            }

            public GroupIdentifier Group { get; private set; }
            public int ReplicateCount { get; private set; }
            public ProteinAbundanceResult ProteinAbundanceResult { get; private set; }
            public override IEnumerable<ProteinAbundanceRow> GetProteinAbundanceRows()
            {
                yield return this;
            }
        }

        public struct ProteinAbundanceResult : IComparable
        {
            public ProteinAbundanceResult(double abundance) : this()
            {
                Abundance = abundance;
            }
            public double Abundance;
            public int CompareTo(object obj)
            {
                if (null == obj)
                {
                    return 1;
                }
                var that = (ProteinAbundanceResult)obj;
                return ((IComparable)Abundance).CompareTo(that.Abundance);
            }
        }
        public class ProteinAbundanceDetailRow : AbstractProteinAbundanceRow
        {
            public ProteinAbundanceDetailRow(Protein protein, 
                Dictionary<GroupIdentifier, ProteinAbundanceResult> proteinAbundanceResults,
                IDictionary<Replicate, ReplicateRow> replicateResult) : base(protein, replicateResult)
            {
                ProteinAbundanceResults = proteinAbundanceResults;
            }

            [OneToMany(ItemDisplayName = "FoldChange", IndexDisplayName = "GroupIdentifier")]
            public IDictionary<GroupIdentifier, ProteinAbundanceResult> ProteinAbundanceResults { get; private set; }

            public override IEnumerable<ProteinAbundanceRow> GetProteinAbundanceRows()
            {
                return ProteinAbundanceResults.Select(kvp =>
                    new ProteinAbundanceRow(Protein, kvp.Key, 0, kvp.Value, ReplicateAbundances));
            }
        }

        [InvariantDisplayName("ReplicateAbundance")]
        public class ReplicateRow : IReplicateValue
        {
            public ReplicateRow(Replicate replicate, GroupIdentifier groupIdentifier, String identity, double? abundance)
            {
                Replicate = replicate;
                ReplicateGroup = groupIdentifier;
                ReplicateSampleIdentity = identity;
                Abundance = abundance;
            }
            public Replicate Replicate { get; private set; }
            [Format(Formats.CalibrationCurve)]
            public double? Abundance { get; private set; }
            public string ReplicateSampleIdentity { get; private set; }
            public GroupIdentifier ReplicateGroup { get; private set; }

            Replicate IReplicateValue.GetReplicate()
            {
                return Replicate;
            }

            public override string ToString()
            {
                var parts = new List<string> { Replicate.ToString() };
                if (Abundance.HasValue)
                {
                    parts.Add(Abundance.Value.ToString(Formats.CalibrationCurve));
                }

                return TextUtil.SpaceSeparate(parts);
            }
        }
    }
}
