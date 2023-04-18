using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI.OptimizeTransitions
{
    public partial class OptimizeDocumentTransitionsForm : DataboundGridForm
    {
        private SkylineDataSchema _dataSchema;
        private List<Row> _rowList = new List<Row>();
        private BindingList<Row> _bindingList;
        private SrmDocument _originalDocument;
        private SrmDocument _optimizedDocument;

        public OptimizeDocumentTransitionsForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            SkylineWindow = skylineWindow;
            _dataSchema = new SkylineWindowDataSchema(skylineWindow, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            BindingListSource.QueryLock = _dataSchema.QueryLock;
            _bindingList = new BindingList<Row>(_rowList);
            UpdateViewContext();
            Text = TabText = Resources.OptimizeDocumentTransitionsForm_OptimizeDocumentTransitionsForm_Optimize_Document_Transitions;
            Icon = Resources.Skyline;
        }

        public SkylineWindow SkylineWindow { get; }

        private IList<RowSourceInfo> MakeRowSourceInfos()
        {
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(Row));
            return ImmutableList.Singleton(new RowSourceInfo(BindingListRowSource.Create(_bindingList),
                new ViewInfo(rootColumn, GetDefaultViewSpec())));
        }

        private ViewSpec GetDefaultViewSpec()
        {
            var propertyPaths = new List<PropertyPath>
            {
                PropertyPath.Root.Property(nameof(Row.Molecule)),
            };
            foreach (string fom in new[] {nameof(Row.OriginalFiguresOfMerit), nameof(Row.OptimizedFiguresOfMerit)})
            {
                var ppFom = PropertyPath.Root.Property(fom);
                if (OptimizeType == OptimizeType.LOD)
                {
                    propertyPaths.Add(ppFom.Property(nameof(FiguresOfMerit.LimitOfDetection)));
                }
                else
                {
                    propertyPaths.Add(ppFom.Property(nameof(FiguresOfMerit.LimitOfQuantification)));
                }
            }

            if (OptimizeType == OptimizeType.LOD)
            {
                propertyPaths.Add(PropertyPath.Root.Property(nameof(Row.LodImprovement)));
            }
            else
            {
                propertyPaths.Add(PropertyPath.Root.Property(nameof(Row.LloqImprovement)));
            }

            return new ViewSpec().SetRowType(typeof(Row)).SetColumns(propertyPaths.Select(pp => new ColumnSpec(pp)));
        }

        public class Row : SkylineObject
        {
            private SrmDocument _originalDocument;
            private SrmDocument _optimizedDocument;
            private Lazy<FiguresOfMerit> _originalFiguresOfMerit;
            private Lazy<FiguresOfMerit> _optimizedFiguresOfMerit;
            private BilinearTransitionOptimizer _bilinearTransitionOptimizer;
            public Row(Model.Databinding.Entities.Peptide molecule, SrmDocument originalDocument, SrmDocument optimizedDocument, BilinearTransitionOptimizer bilinearTransitionOptimizer) 
            {
                Molecule = molecule;
                _originalDocument = originalDocument;
                _optimizedDocument = optimizedDocument;
                _bilinearTransitionOptimizer = bilinearTransitionOptimizer;
                _originalFiguresOfMerit = new Lazy<FiguresOfMerit>(GetOriginalFiguresOfMerit);
                _optimizedFiguresOfMerit = new Lazy<FiguresOfMerit>(GetOptimizedFiguresOfMerit);
                if (optimizedDocument != null)
                {
                    var peptideQuantifier = GetPeptideQuantifier(null, optimizedDocument, molecule.IdentityPath, bilinearTransitionOptimizer.OptimizeTransitionSettings);
                    int countQuantitative = 0;
                    int countNonQuantitative = 0;
                    foreach (var tg in peptideQuantifier.PeptideDocNode.TransitionGroups)
                    {
                        if (!peptideQuantifier.SkipTransitionGroup(tg))
                        {
                            countQuantitative += tg.Transitions.Count(t => t.ExplicitQuantitative);
                            countNonQuantitative += tg.Transitions.Count(t => !t.ExplicitQuantitative);
                        }
                    }

                    CountQuantitative = countQuantitative;
                    CountNonQuantitative = countNonQuantitative;
                }
            }

            protected override SkylineDataSchema GetDataSchema()
            {
                return Molecule.DataSchema;
            }

            [InvariantDisplayName("Peptide", InUiMode = UiModes.PROTEOMIC)]
            public Model.Databinding.Entities.Peptide Molecule { get; private set; }

            public int? CountQuantitative
            {
                get; internal set;
            }

            public int? CountNonQuantitative 
            {
                get; internal set;
            }

            [ChildDisplayName("Original{0}")]
            public FiguresOfMerit OriginalFiguresOfMerit
            {
                get { return _originalFiguresOfMerit.Value;}
            }

            [ChildDisplayName("Optimized{0}")]
            public FiguresOfMerit OptimizedFiguresOfMerit
            {
                get { return _optimizedFiguresOfMerit.Value; }
            }

            private FiguresOfMerit GetOriginalFiguresOfMerit()
            {
                return GetFiguresOfMerit(_originalDocument, _bilinearTransitionOptimizer.OptimizeTransitionSettings);
            }

            private FiguresOfMerit GetOptimizedFiguresOfMerit()
            {
                return GetFiguresOfMerit(_optimizedDocument, _bilinearTransitionOptimizer.OptimizeTransitionSettings);
            }

            private FiguresOfMerit GetFiguresOfMerit(SrmDocument document, OptimizeTransitionSettings optimizeTransitionSettings)
            {
                if (document == null)
                {
                    return null;
                }

                var peptideQuantifier = GetPeptideQuantifier(null, document, Molecule.IdentityPath, optimizeTransitionSettings);
                if (peptideQuantifier == null)
                {
                    return null;
                }

                var calibrationCurveFitter =
                    _bilinearTransitionOptimizer.OptimizeTransitionSettings.GetCalibrationCurveFitter(peptideQuantifier,
                        document.Settings);
                return MakeFiguresOfMerit(_bilinearTransitionOptimizer.ComputeQuantLimits(calibrationCurveFitter),
                    document.Settings);
            }

            [Format(Formats.CalibrationCurve)]
            public double? LloqImprovement
            {
                get
                {
                    return _originalFiguresOfMerit.Value?.LimitOfQuantification -
                           _optimizedFiguresOfMerit.Value?.LimitOfQuantification;
                }
            }

            [Format(Formats.CalibrationCurve)]
            public double? LodImprovement
            {
                get
                {
                    return _optimizedFiguresOfMerit.Value?.LimitOfDetection -
                           _optimizedFiguresOfMerit.Value?.LimitOfDetection;
                }
            }
        }

        public SrmDocument OptimizeTransitions(ILongWaitBroker longWaitBroker, SrmDocument document, BilinearTransitionOptimizer bilinearTransitionOptimizer)
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
                var peptideQuantifier = GetPeptideQuantifier(normalizationData, document,
                    new IdentityPath(moleculeList.PeptideGroup, molecule.Peptide), bilinearTransitionOptimizer.OptimizeTransitionSettings);
                if (!bilinearTransitionOptimizer.OptimizeTransitionSettings.PreserveNonQuantitative)
                {
                    peptideQuantifier = peptideQuantifier.MakeAllTransitionsQuantitative();
                }

                var calibrationCurveFitter =
                    bilinearTransitionOptimizer.OptimizeTransitionSettings.GetCalibrationCurveFitter(peptideQuantifier,
                        document.Settings);
                var optimizedMolecule =
                    bilinearTransitionOptimizer.OptimizeTransitions(calibrationCurveFitter, null);
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

        private void btnPreview_Click(object sender, EventArgs e)
        {
            Preview();
        }

        public void Preview()
        {
            var originalDocument = SkylineWindow.Document;
            var optimizedDocument = GetOptimizedDocument(originalDocument);
            if (optimizedDocument != null)
            {
                _originalDocument = originalDocument;
                _optimizedDocument = optimizedDocument;
                var newRows = MakeRows().ToList();
                _rowList.Clear();
                _rowList.AddRange(newRows);
                _bindingList.ResetBindings();
            }
        }

        private BilinearTransitionOptimizer GetBilinearCurveFitter(CancellationToken cancellationToken)
        {
            var settings = optimizeTransitionsSettingsControl1.CurrentSettings;
            if (settings == null)
            {
                return null;
            }
            return new BilinearTransitionOptimizer
            {
                CancellationToken = cancellationToken,
                OptimizeTransitionSettings = settings
            };
        }

        public SrmDocument GetOptimizedDocument(SrmDocument document)
        {
            SrmDocument newDocument = null;
            using (var longWaitDlg = new LongWaitDlg())
            {
                var bilinearCurveFitter = GetBilinearCurveFitter(longWaitDlg.CancellationToken);
                if (bilinearCurveFitter == null)
                {
                    return null;
                }

                longWaitDlg.PerformWork(this, 1000, broker =>
                {
                    newDocument = OptimizeTransitions(broker, document, bilinearCurveFitter);
                });
            }

            return newDocument;
        }

        public IEnumerable<Row> MakeRows()
        {
            UpdateViewContext();
            var currentDocument = SkylineWindow.Document;
            var bilinearCurveFitter = GetBilinearCurveFitter(CancellationToken.None);
            foreach (var moleculeList in currentDocument.MoleculeGroups)
            {
                foreach (var molecule in moleculeList.Molecules)
                {
                    if (molecule.IsDecoy || null != molecule.GlobalStandardType)
                    {
                        continue;
                    }

                    var peptideIdentityPath = new IdentityPath(moleculeList.PeptideGroup, molecule.Peptide);
                    var row = new Row(new Model.Databinding.Entities.Peptide(_dataSchema, peptideIdentityPath), _originalDocument, _optimizedDocument, bilinearCurveFitter);
                    yield return row;
                }
            }
        }

        private static FiguresOfMerit MakeFiguresOfMerit(QuantLimit quantLimit, SrmSettings settings)
        {
            if (quantLimit == null)
            {
                return null;
            }

            return FiguresOfMerit.EMPTY.ChangeLimitOfDetection(quantLimit.Lod)
                .ChangeLimitOfQuantification(quantLimit.Loq)
                .ChangeUnits(settings.PeptideSettings.Quantification.Units);
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            Apply();
        }

        public void Apply()
        {
            lock (SkylineWindow.GetDocumentChangeLock())
            {
                SkylineWindow.ModifyDocument("Optimize transitions", doc =>
                    {
                        if (null != _optimizedDocument && ReferenceEquals(doc, _originalDocument))
                        {
                            return _optimizedDocument;
                        }

                        var optimizedDoc = GetOptimizedDocument(doc);
                        if (optimizedDoc != null)
                        {
                            _originalDocument = doc;
                            _optimizedDocument = optimizedDoc;
                            return optimizedDoc;
                        }

                        return doc;
                    },
                    docPair => AuditLogEntry.DiffDocNodes(MessageType.changed_quantitative, docPair, "Unknown"));
            }
        }

        public static PeptideQuantifier GetPeptideQuantifier(NormalizationData normalizationData, SrmDocument document, IdentityPath peptideIdentityPath, OptimizeTransitionSettings optimizeTransitionSettings)
        {
            var moleculeList = (PeptideGroupDocNode)
                document.FindNode(peptideIdentityPath.GetIdentity((int) SrmDocument.Level.MoleculeGroups));
            var molecule = (PeptideDocNode) moleculeList?.FindNode(peptideIdentityPath.GetIdentity((int) SrmDocument.Level.Molecules));
            if (molecule == null)
            {
                return null;
            }

            var quantificationSettings = optimizeTransitionSettings.GetQuantificationSettings(document.Settings);
            if (normalizationData == null)
            {
                return PeptideQuantifier.GetPeptideQuantifier(document, moleculeList, molecule)
                    .WithQuantificationSettings(quantificationSettings);
            }
            return new PeptideQuantifier(() => normalizationData, moleculeList, molecule,
                quantificationSettings);
        }

        public OptimizeType OptimizeType
        {
            get
            {
                return optimizeTransitionsSettingsControl1.OptimizeType;
            }
            set
            {
                optimizeTransitionsSettingsControl1.OptimizeType = value;
            }
        }

        private void UpdateViewContext()
        {
            var viewContext = new SkylineViewContext(_dataSchema, MakeRowSourceInfos());
            if (BindingListSource.ViewInfo == null ||
                Equals(BindingListSource.ViewInfo.ViewGroup.Id, ViewGroup.BUILT_IN.Id))
            {
                BindingListSource.SetViewContext(viewContext, viewContext.GetViewInfo(
                    ViewGroup.BUILT_IN,
                    viewContext.GetViewSpecList(ViewGroup.BUILT_IN.Id).ViewSpecs.FirstOrDefault()));
            }
            else
            {
                BindingListSource.SetViewContext(viewContext);
            }
        }
    }
}
