using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Util;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

namespace pwiz.Skyline.EditUI
{
    public partial class OptimizeTransitionsDlg : DataboundGridForm
    {
        private SkylineDataSchema _dataSchema;
        private List<Row> _rowList = new List<Row>();
        private BindingList<Row> _bindingList;

        public OptimizeTransitionsDlg(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            SkylineWindow = skylineWindow;
            _dataSchema = new SkylineDataSchema(skylineWindow, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            BindingListSource.QueryLock = _dataSchema.QueryLock;
            _bindingList = new BindingList<Row>(_rowList);
            var viewContext = new SkylineViewContext(_dataSchema, MakeRowSourceInfos());
            BindingListSource.SetViewContext(viewContext);
            Text = TabText = "Optimize Transitions";
        }

        public SkylineWindow SkylineWindow { get; }

        private IList<RowSourceInfo> MakeRowSourceInfos()
        {
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(Row));
            return ImmutableList.Singleton(new RowSourceInfo(BindingListRowSource.Create(_bindingList),
                SkylineViewContext.GetDefaultViewInfo(rootColumn)));
        }

        public class Row : SkylineObject
        {
            public Row(Peptide molecule) : base(molecule.DataSchema)
            {
                Molecule = molecule;
            }

            [InvariantDisplayName("Peptide", InUiMode = UiModes.PROTEOMIC)]
            public Peptide Molecule { get; private set; }

            public int CountQuantitative
            {
                get; internal set;
            }

            public int CountNonQuantitative 
            {
                get; internal set;
            }
        }

        public SrmDocument OptimizeTransitions(ILongWaitBroker longWaitBroker, SrmDocument document, BilinearCurveFitter bilinearCurveFitter, OptimizeType optimizeType)
        {
            longWaitBroker.ProgressValue = 0;
            var newMoleculeArrays = new List<PeptideDocNode[]>();
            var moleculeListMoleculesIndexes = new List<Tuple<int, int>>();
            for (int iMoleculeList = 0; iMoleculeList < document.Children.Count; iMoleculeList++)
            {
                var moleculeList = (PeptideGroupDocNode) document.Children[iMoleculeList];
                newMoleculeArrays.Add(new PeptideDocNode[moleculeList.Children.Count]);
                for (int iMolecule = 0; iMolecule < moleculeList.Children.Count; iMolecule++)
                {
                    var molecule = (PeptideDocNode) moleculeList.Children[iMolecule];
                    if (molecule.IsDecoy || null != molecule.GlobalStandardType)
                    {
                        newMoleculeArrays[iMoleculeList][iMolecule] = molecule;
                    }
                    else
                    {
                        moleculeListMoleculesIndexes.Add(Tuple.Create(iMoleculeList, iMolecule));
                    }
                }
            }

            if (moleculeListMoleculesIndexes.Count == 0)
            {
                return document;
            }

            int processedMoleculeCount = 0;
            var normalizationData = NormalizationData.GetNormalizationData(document, false, null);

            ParallelEx.ForEach(moleculeListMoleculesIndexes, moleculeListMoleculeIndex =>
            {
                var moleculeList = (PeptideGroupDocNode) document.Children[moleculeListMoleculeIndex.Item1];
                var molecule = (PeptideDocNode) moleculeList.Children[moleculeListMoleculeIndex.Item2];
                longWaitBroker.CancellationToken.ThrowIfCancellationRequested();
                var peptideQuantifier = new PeptideQuantifier(() => normalizationData, moleculeList, molecule,
                    document.Settings.PeptideSettings.Quantification);
                var calibrationCurveFitter = new CalibrationCurveFitter(peptideQuantifier, document.Settings);
                var optimizedMolecule =
                    bilinearCurveFitter.OptimizeTransitions(optimizeType, calibrationCurveFitter);
                newMoleculeArrays[moleculeListMoleculeIndex.Item1][moleculeListMoleculeIndex.Item2] = optimizedMolecule;
                Interlocked.Increment(ref processedMoleculeCount);
                longWaitBroker.ProgressValue = processedMoleculeCount * 100 / moleculeListMoleculesIndexes.Count;
            });
            var newMoleculeLists = new List<PeptideGroupDocNode>();
            for (int iMoleculeList = 0; iMoleculeList < document.Children.Count; iMoleculeList++)
            {
                var moleculeList = (PeptideGroupDocNode) document.Children[iMoleculeList];
                moleculeList = (PeptideGroupDocNode) moleculeList.ChangeChildren(newMoleculeArrays[iMoleculeList]);
                newMoleculeLists.Add(moleculeList);
            }
            return (SrmDocument) document.ChangeChildren(newMoleculeLists.ToArray());
        }

        private void btnPreview_Click(object sender, System.EventArgs e)
        {
            var optimizedDocument = GetOptimizedDocument(SkylineWindow.Document);
            if (optimizedDocument != null)
            {
                var newRows = MakeRows(optimizedDocument).ToList();
                _rowList.Clear();
                _rowList.AddRange(newRows);
                _bindingList.ResetBindings();
            }
        }

        public SrmDocument GetOptimizedDocument(SrmDocument document)
        {
            SrmDocument newDocument = null;
            int minNumTransitions = (int) tbxMinTransitions.Value;
            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.PerformWork(this, 1000, broker =>
                {
                    var bilinearCurveFitter = new BilinearCurveFitter()
                    {
                        MinNumTransitions = minNumTransitions,
                        CancellationToken = broker.CancellationToken,
                    };
                    newDocument = OptimizeTransitions(broker, document, bilinearCurveFitter, OptimizeType.LOQ);
                });
            }

            return newDocument;
        }

        public IEnumerable<Row> MakeRows(SrmDocument document)
        {
            
            foreach (var moleculeList in document.MoleculeGroups)
            {
                foreach (var molecule in moleculeList.Molecules)
                {
                    if (molecule.IsDecoy || null != molecule.GlobalStandardType)
                    {
                        continue;
                    }
                    var peptideQuantifier = PeptideQuantifier.GetPeptideQuantifier(document, moleculeList, molecule);
                    int countQuantifiable = 0;
                    int countUnquantifiable = 0;
                    foreach (var transitionGroupDocNode in molecule.TransitionGroups)
                    {
                        if (peptideQuantifier.SkipTransitionGroup(transitionGroupDocNode))
                        {
                            continue;
                        }

                        foreach (var transitionDocNode in transitionGroupDocNode.Transitions)
                        {
                            if (transitionDocNode.ExplicitQuantitative)
                            {
                                countQuantifiable++;
                            }
                            else
                            {
                                countUnquantifiable++;
                            }
                        }
                    }

                    var identityPath = new IdentityPath(moleculeList.PeptideGroup, molecule.Peptide);
                    var peptide = new Peptide(_dataSchema, identityPath);
                    yield return new Row(peptide) {CountQuantitative = countQuantifiable, CountNonQuantitative = countUnquantifiable};
                }
            }
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            lock (SkylineWindow.GetDocumentChangeLock())
            {
                SkylineWindow.ModifyDocument("Optimize transitions", doc=>GetOptimizedDocument(doc) ?? doc,
                    docPair=>AuditLogEntry.DiffDocNodes(MessageType.changed_quantitative, docPair, "Unknown"));
            }
        }
    }
}
