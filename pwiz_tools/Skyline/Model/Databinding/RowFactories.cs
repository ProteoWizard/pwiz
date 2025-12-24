using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
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
using pwiz.Skyline.Model.Hibernate;

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
            return DataSchema.Document.MoleculeGroups.SelectMany(protein => protein.Molecules.Select(mol =>
                new Model.Databinding.Entities.Peptide(DataSchema,
                    new IdentityPath(protein.PeptideGroup, mol.Peptide))));
        }

        public IEnumerable<Precursor> ListPrecursors()
        {
            return DataSchema.Document.MoleculeGroups.SelectMany(protein => protein.Molecules.SelectMany(mol =>
                mol.TransitionGroups.Select(prec => new Precursor(DataSchema,
                    new IdentityPath(protein.PeptideGroup, mol.Peptide, prec.TransitionGroup)))));
        }

        public IEnumerable<Model.Databinding.Entities.Transition> ListTransitions()
        {
            return DataSchema.Document.MoleculeGroups.SelectMany(protein => protein.Molecules.SelectMany(mol =>
                mol.TransitionGroups.SelectMany(prec => prec.Transitions.Select(tran =>
                    new Model.Databinding.Entities.Transition(DataSchema,
                        new IdentityPath(protein.PeptideGroup, mol.Peptide, prec.TransitionGroup, tran.Transition))
                ))));
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

        public void ExportReport(Stream stream, ViewName viewName, IRowItemExporter rowItemExporter, IProgressMonitor progressMonitor, ref IProgressStatus status)
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
            if (!_factoriesByName.TryGetValue(viewSpec.RowSource, out var factory))
            {
                throw new ArgumentException(string.Format(DatabindingResources.RowFactories_ExportReport_The_row_type___0___cannot_be_exported_,
                    viewSpec.RowSource));
            }

            var layout = viewSpecList.GetViewLayouts(viewName.Name).DefaultLayout;
            var viewInfo = new ViewInfo(DataSchema, factory.ItemType, viewSpec);
            using var bindingListSource = new BindingListSource(CancellationToken);
            bindingListSource.SetView(viewInfo, factory);
            if (layout != null)
            {
                foreach (var column in layout.ColumnFormats)
                {
                    bindingListSource.ColumnFormats.SetFormat(column.Item1, column.Item2);
                }
            }

            var rowItemEnumerator = RowItemEnumerator.FromBindingListSource(bindingListSource);
            rowItemExporter.Export(progressMonitor, ref status, stream, rowItemEnumerator);
            if (!progressMonitor.IsCanceled)
            {
                progressMonitor.UpdateProgress(status = status.Complete());
            }
        }
    }
}
