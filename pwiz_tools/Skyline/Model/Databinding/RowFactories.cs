/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace pwiz.Skyline.Model.Databinding
{
    public class RowFactories
    {
        private Dictionary<string, Factory> _factoriesByName = new Dictionary<string, Factory>();
        public RowFactories(CancellationToken cancellationToken, SkylineDataSchema dataSchema)
        {
            CancellationToken = cancellationToken;
            DataSchema = dataSchema;
        }
        
        public SkylineDataSchema DataSchema { get; }
        public CancellationToken CancellationToken
        {
            get;
        }

        public IEnumerable<Protein> ListProteins()
        {
            return DataSchema.Document.MoleculeGroups.Select(node =>
                new Protein(DataSchema, new IdentityPath(node.PeptideGroup)));
        }

        public IEnumerable<Model.Databinding.Entities.Peptide> ListPeptides()
        {
            return ListProteins().SelectMany(protein => protein.Peptides);
        }

        public IEnumerable<Precursor> ListPrecursors()
        {
            return ListPeptides().SelectMany(peptide => peptide.Precursors);
        }

        public IEnumerable<Model.Databinding.Entities.Transition> ListTransitions()
        {
            return ListPrecursors().SelectMany(precursor => precursor.Transitions);
        }

        public IEnumerable<Replicate> ListReplicates()
        {
            return DataSchema.ReplicateList.Values;
        }

        public IEnumerable<PeptideResult> ListPeptideResults()
        {
            return ListPeptides().SelectMany(peptide => peptide.Results.Values);
        }

        public IEnumerable<PrecursorResult> ListPrecursorResults()
        {
            return ListPrecursors().SelectMany(precursor => precursor.Results.Values);
        }

        public IEnumerable<TransitionResult> ListTransitionResults()
        {
            return ListTransitions().SelectMany(transition => transition.Results.Values);
        }

        public void RegisterFactory<T>(Func<IEnumerable<T>> listItemsFunc)
        {
            RegisterFactory(ViewSpec.GetRowSourceName(typeof(T)), listItemsFunc);
        }

        public void RegisterFactory<T>(string name, Func<IEnumerable<T>> listItemsFunc)
        {
            var factory = new Factory(name, typeof(T), listItemsFunc);
            _factoriesByName.Add(name, factory);
        }

        public bool HasFactory(string rowSourceName)
        {
            return _factoriesByName.ContainsKey(rowSourceName);
        }

        private class Factory : IRowSource
        {
            public Factory(string rowSourceName, Type itemType, Func<IEnumerable> listItemsFunc)
            {
                RowSourceName = rowSourceName;
                ItemType = itemType;
                ListItemsFunc = listItemsFunc;
            }
            public string RowSourceName { get; }
            public Type ItemType { get; }
            public Func<IEnumerable> ListItemsFunc { get; }

            public IEnumerable GetItems()
            {
                return ListItemsFunc();
            }

            event Action IRowSource.RowSourceChanged
            {
                add {}

                remove { }
            }
        }

        public void RegisterAllFactories()
        {
            var document = DataSchema.Document;
            RegisterFactory(ListProteins);
            RegisterFactory(ListPeptides);
            RegisterFactory(ListPrecursors);
            RegisterFactory(ListTransitions);
            RegisterFactory(ListReplicates);
            RegisterFactory(ListPeptideResults);
            RegisterFactory(ListPrecursorResults);
            RegisterFactory(ListTransitionResults);
            var foldChangeRowFactory = new FoldChangeRowFactory(CancellationToken, DataSchema);
            RegisterFactory(foldChangeRowFactory.GetAllFoldChangeRows);
            RegisterFactory(foldChangeRowFactory.GetAllFoldChangeDetailRows);
            var candidatePeakGroupFactory = new CandidatePeakGroupFactory(CancellationToken, DataSchema);
            RegisterFactory(candidatePeakGroupFactory.GetAllCandidatePeakGroups);
        }

        public static RowFactories GetRowFactories(CancellationToken cancellationToken, SkylineDataSchema dataSchema)
        {
            var rowFactories = new RowFactories(cancellationToken, dataSchema);
            rowFactories.RegisterAllFactories();
            return rowFactories;
        }

        public void ExportReport(Stream stream, ViewName viewName, IReportExporter rowItemExporter, IProgressMonitor progressMonitor, ref IProgressStatus status)
        {
            if (Equals(viewName.GroupId, ViewGroup.BUILT_IN.Id))
            {
                throw new ArgumentException(DatabindingResources.RowFactories_ExportReport_Built_in_reports_cannot_be_exported_here);
            }
            var viewSpecList = Settings.Default.PersistedViews.GetViewSpecList(viewName.GroupId);
            var viewSpec = viewSpecList.GetView(viewName.Name);
            if (viewSpec == null)
            {
                throw new ArgumentException(string.Format(DatabindingResources.RowFactories_ExportReport_There_is_no_report_named___0___in_the_group___1__, viewName.Name, viewName.GroupId));
            }
            var layout = viewSpecList.GetViewLayouts(viewName.Name).DefaultLayout;
            ExportReport(stream, viewSpec, layout, rowItemExporter, progressMonitor, ref status);
        }

        public void ExportReport(Stream stream, ViewSpec viewSpec, ViewLayout layout, IReportExporter rowItemExporter,
            IProgressMonitor progressMonitor, ref IProgressStatus status)
        {
            if (!_factoriesByName.TryGetValue(viewSpec.RowSource, out var factory))
            {
                throw new ArgumentException(string.Format(DatabindingResources.RowFactories_ExportReport_The_row_type___0___cannot_be_exported_,
                    viewSpec.RowSource));
            }

            var viewInfo = new ViewInfo(DataSchema, factory.ItemType, viewSpec);
            ExportReport(CancellationToken, stream, viewInfo, layout, factory, rowItemExporter, progressMonitor, ref status);

        }

        public static void ExportReport(CancellationToken cancellationToken, Stream stream, ViewInfo viewInfo, ViewLayout layout, IRowSource rowSource,
            IReportExporter rowItemExporter, IProgressMonitor progressMonitor, ref IProgressStatus status)
        {
            RowItemEnumerator rowItemEnumerator = null;
            if (layout == null || layout.RowTransforms.Count == 0)
            {

                rowItemEnumerator = viewInfo.GetStreamingRowItemEnumerator(cancellationToken, rowSource);
            }

            if (rowItemEnumerator == null)
            {
                using var bindingListSource = new BindingListSource(cancellationToken);
                bindingListSource.SetView(viewInfo, rowSource);
                layout?.ApplyFormats(bindingListSource.ColumnFormats);
                rowItemEnumerator = RowItemList.FromBindingListSource(bindingListSource);
            }
            rowItemEnumerator.SetProgressMonitor(progressMonitor, status);
            layout?.ApplyFormats(rowItemEnumerator.ColumnFormats);



            rowItemExporter.Export(stream, rowItemEnumerator);
            status = rowItemEnumerator.Status;
            if (!progressMonitor.IsCanceled)
            {
                progressMonitor.UpdateProgress(status = status.Complete());
            }
        }
    }
}
