using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.Spectra;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Spectra
{
    public partial class SpectraGridForm : DataboundGridForm
    {
        private SkylineDataSchema _dataSchema;
        private SpectrumList _spectrumList;
        private SequenceTree _sequenceTree;
        private HashSet<IdentityPath> _selectedPrecursorPaths = new HashSet<IdentityPath>();
        private List<SpectrumClass> _spectrumClasses;
        private BindingList<SpectrumClass> _bindingList;


        public SpectraGridForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            SkylineWindow = skylineWindow;
            _dataSchema = new SkylineDataSchema(skylineWindow, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            BindingListSource.QueryLock = _dataSchema.QueryLock;
            _spectrumClasses = new List<SpectrumClass>();
            _bindingList = new BindingList<SpectrumClass>(_spectrumClasses);
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(SpectrumClass));
            var rowSourceInfo = new RowSourceInfo(BindingListSource.Create())
        }

        public SkylineWindow SkylineWindow { get; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SkylineWindow.DocumentUIChangedEvent += SkylineWindow_DocumentUIChangedEvent;
            OnDocumentChanged();
        }

        private void SkylineWindow_DocumentUIChangedEvent(object sender, DocumentChangedEventArgs e)
        {
            OnDocumentChanged();
        }

        private void OnDocumentChanged()
        {
            SetSequenceTree(SkylineWindow.SequenceTree);
            UpdateSpectrumRows();
        }

        private void SetSequenceTree(SequenceTree sequenceTree)
        {
            if (ReferenceEquals(_sequenceTree, sequenceTree))
            {
                return;
            }
            if (null != _sequenceTree)
            {
                _sequenceTree.AfterSelect -= SequenceTreeOnAfterSelect;
            }
            _sequenceTree = sequenceTree;
            if (null != _sequenceTree)
            {
                _sequenceTree.AfterSelect += SequenceTreeOnAfterSelect;
                SetSelectedPrecursorPaths(GetSelectedPrecursorPaths());
            }
        }

        private void SequenceTreeOnAfterSelect(object sender, TreeViewEventArgs args)
        {
            SetSelectedPrecursorPaths(GetSelectedPrecursorPaths());
        }

        public void SetSelectedPrecursorPaths(HashSet<IdentityPath> paths)
        {
            if (paths.SetEquals(_selectedPrecursorPaths))
            {
                return;
            }

            _selectedPrecursorPaths = paths;
            UpdateSpectrumRows();
        }

        public void UpdateSpectrumRows()
        {
            var spectrumList = _spectrumList ?? SpectrumList.EMPTY;
            var document = SkylineWindow.DocumentUI;
            if (_selectedPrecursorPaths.Count > 0)
            {
                var transitionGroupDocNodes = _selectedPrecursorPaths.Select(path => document.FindNode(path))
                    .OfType<TransitionGroupDocNode>().ToList();
                var spectra = new List<SpectrumMetadata>();
                var tolerance = document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
                foreach (var spectrum in spectrumList.SpectrumMetadatas)
                {
                    if (FindMatchingTransitionGroups(spectrum, transitionGroupDocNodes, tolerance).Any())
                    {
                        spectra.Add(spectrum);
                    }
                }

                spectrumList = new SpectrumList(spectra);
            }

            var precursorGroups = spectrumList.SpectrumMetadatas.GroupBy(spectrum => spectrum.GetPrecursors(2));
            _rowsList.Clear();
            _rowsList.AddRange(precursorGroups.Select(p=>new SpectrumRow()
            {
                Precursors = PrecursorsToString(p.Key),
                SpectrumCount = p.Count()
            }));
            bindingSource1.ResetBindings(false);
        }

        private string PrecursorsToString(IEnumerable<SpectrumPrecursor> precursors)
        {
            return string.Join(TextUtil.GetCsvSeparator(CultureInfo.CurrentCulture).ToString(),
                precursors.OrderBy(p => p.PrecursorMz.RawValue)
                    .Select(p => p.PrecursorMz.ToString(Formats.Mz, CultureInfo.CurrentCulture)));
        }

        private IEnumerable<TransitionGroupDocNode> FindMatchingTransitionGroups(SpectrumMetadata spectrumMetadata,
            IList<TransitionGroupDocNode> transitionGroups, double tolerance)
        {
            foreach (var spectrumPrecursor in spectrumMetadata.GetPrecursors(1))
            {
                foreach (var transitionGroup in transitionGroups)
                {
                    if (0 == transitionGroup.PrecursorMz.CompareTolerant(spectrumPrecursor.PrecursorMz, tolerance))
                    {
                        yield return transitionGroup;
                    }
                }
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            SkylineWindow.DocumentUIChangedEvent -= SkylineWindow_DocumentUIChangedEvent;
            SetSequenceTree(null);
            base.OnHandleDestroyed(e);
        }

        public class SpectrumRow
        {
            public string Precursors { get; set; }
            public int SpectrumCount { get; set; }
            public int[] SpectrumCounts { get; set; }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var dataSrcDlg = new OpenDataSourceDialog(Settings.Default.RemoteAccountList))
            {
                if (!string.IsNullOrEmpty(SkylineWindow.DocumentFilePath))
                {
                    dataSrcDlg.InitialDirectory =
                        new MsDataFilePath(Path.GetDirectoryName(SkylineWindow.DocumentFilePath));
                }
                if (dataSrcDlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                if (dataSrcDlg.DataSource != null)
                {
                    OpenDataFile(dataSrcDlg.DataSource);
                }
            }
        }

        public void OpenDataFile(MsDataFileUri dataFileUri)
        {
            tbxFile.Text = dataFileUri.ToString();
            using (var longWaitDlg = new LongWaitDlg())
            {
                SpectrumList spectrumList = null;
                longWaitDlg.PerformWork(this, 1000, broker =>
                {
                    using (var msDataFileImpl = dataFileUri.OpenMsDataFile(true, false, false, false, true))
                    {
                        int spectrumCount = msDataFileImpl.SpectrumCount;
                        var spectra = new List<SpectrumMetadata>(spectrumCount);
                        for (int i = 0; i < spectrumCount; i++)
                        {
                            broker.ProgressValue = 100 * i / spectrumCount;
                            broker.CancellationToken.ThrowIfCancellationRequested();
                            var spectrum = msDataFileImpl.GetSpectrumMetadata(i);
                            if (spectrum != null)
                            {
                                spectra.Add(spectrum);
                            }
                        }
                        spectrumList = new SpectrumList(spectra);
                    }
                });
                if (spectrumList != null)
                {
                    SetSpectrumList(spectrumList);
                }
            }
        }

        public void SetSpectrumList(SpectrumList spectrumList)
        {
            _spectrumList = spectrumList;
            UpdateSpectrumRows();
        }

        public HashSet<IdentityPath> GetSelectedPrecursorPaths()
        {
            return GetPrecursorPathsFromSelectedPaths(SkylineWindow.DocumentUI,
                SkylineWindow.SequenceTree.SelectedPaths);
        }

        public HashSet<IdentityPath> GetPrecursorPathsFromSelectedPaths(SrmDocument document, IEnumerable<IdentityPath> selectedPaths)
        {
            HashSet<IdentityPath> result = new HashSet<IdentityPath>();
            foreach (var identityPath in selectedPaths)
            {
                if (identityPath.Length == 1)
                {
                    var peptideGroupDocNode = (PeptideGroupDocNode) document.FindNode(identityPath);
                    if (peptideGroupDocNode != null)
                    {
                        result.UnionWith(peptideGroupDocNode.Molecules.SelectMany(molecule =>
                            molecule.TransitionGroups.Select(tg => new IdentityPath(peptideGroupDocNode.PeptideGroup,
                                molecule.Peptide, tg.TransitionGroup))));
                    }
                }
                else if (identityPath.Length == 2)
                {
                    var molecule = (PeptideDocNode) document.FindNode(identityPath);
                    if (molecule != null)
                    {
                        result.UnionWith(molecule.TransitionGroups.Select(tg=>new IdentityPath(identityPath, tg.TransitionGroup)));
                    }
                }
                else if (identityPath.Length > 2)
                {
                    result.Add(identityPath.GetPathTo(2));
                }
            }

            return result;
        }

        class SpectrumIdentifier
        {
            public SpectrumIdentifier(int index, string id)
            {
                Index = index;
                Id = id;
            }

            public int Index { get; }
            public string Id { get; }
        }
    }
}
