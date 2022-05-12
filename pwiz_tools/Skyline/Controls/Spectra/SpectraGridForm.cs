using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Spectra
{
    public partial class SpectraGridForm : DataboundGridForm
    {
        private SkylineDataSchema _dataSchema;
        private SequenceTree _sequenceTree;
        private HashSet<IdentityPath> _selectedPrecursorPaths = new HashSet<IdentityPath>();
        private List<SpectrumClass> _spectrumClasses;
        private BindingList<SpectrumClass> _bindingList;

        private Dictionary<MsDataFileUri, SpectrumMetadataList> _spectrumLists =
            new Dictionary<MsDataFileUri, SpectrumMetadataList>();
        private readonly SpectrumReader _spectrumReader;
        private List<MsDataFileUri> _dataFileList = new List<MsDataFileUri>();
        private HashSet<MsDataFileUri> _dataFileSet = new HashSet<MsDataFileUri>();

        public SpectraGridForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            SkylineWindow = skylineWindow;
            _dataSchema = new SkylineDataSchema(skylineWindow, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            BindingListSource.QueryLock = _dataSchema.QueryLock;
            _spectrumClasses = new List<SpectrumClass>();
            _bindingList = new BindingList<SpectrumClass>(_spectrumClasses);
            var viewContext = new SkylineViewContext(_dataSchema, MakeRowSourceInfos());
            BindingListSource.SetViewContext(viewContext);
            Text = TabText = "Spectra";
            _spectrumReader = new SpectrumReader(this);
        }

        private IList<RowSourceInfo> MakeRowSourceInfos()
        {
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(SpectrumClass));
            return ImmutableList.Singleton(new RowSourceInfo(BindingListRowSource.Create(_bindingList),
                new ViewInfo(rootColumn, GetDefaultViewSpec())));
        }

        private ViewSpec GetDefaultViewSpec()
        {
            var columns = new List<ColumnSpec>
            {
                new ColumnSpec(PropertyPath.Root.Property(nameof(SpectrumClass.Ms1Precursors)))
            };

            if (HasMultipleValues(spectrum => spectrum.GetPrecursors(2)))
            {
                columns.Add(new ColumnSpec(PropertyPath.Root.Property(nameof(SpectrumClass.Ms2Precursors))));
            }

            if (HasMultipleValues(spectrum => spectrum.ScanDescription))
            {
                columns.Add(new ColumnSpec(PropertyPath.Root.Property(nameof(SpectrumClass.ScanDescription))));
            }

            if (HasMultipleValues(spectrum => spectrum.CollisionEnergy))
            {
                columns.Add(new ColumnSpec(PropertyPath.Root.Property(nameof(SpectrumClass.CollisionEnergy))));
            }
            columns.Add(new ColumnSpec(PropertyPath.Root.Property(nameof(SpectrumClass.Files)).DictionaryValues()));
            return new ViewSpec().SetName("Default").SetColumns(columns);
        }

        private bool HasMultipleValues<T>(Func<SpectrumMetadata, T> getterFunc)
        {
            var tolerance = SkylineWindow.Document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            return _spectrumLists.Values.Any(spectrumList => HasMultipleValues(spectrumList, getterFunc, tolerance));
        }

        private bool HasMultipleValues<T>(SpectrumMetadataList spectra, Func<SpectrumMetadata, T> getterFunc, double tolerance)
        {
            if (spectra.Ms1Spectra.Select(getterFunc).Distinct().Skip(1).Any())
            {
                return true;
            }

            for (int i = 0; i < spectra.SpectraByPrecursor.Count; i++)
            {
                IEnumerable<T> values = spectra.SpectraByPrecursor[i].Value.Select(getterFunc);
                for (int j = i + 1;
                     j < spectra.SpectraByPrecursor.Count && spectra.SpectraByPrecursor.Keys[j] - i <= tolerance * 2;
                     j++)
                {
                    values = values.Concat(spectra.SpectraByPrecursor[j].Value.Select(getterFunc));
                }

                if (values.Distinct().Skip(1).Any())
                {
                    return true;
                }
            }

            return false;
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
            var document = SkylineWindow.DocumentUI;
            var spectrumClasses = new Dictionary<SpectrumClassKey, SpectrumClass>();
            IList<TransitionGroupDocNode> transitionGroupDocNodes = null;
            if (_selectedPrecursorPaths.Count > 0)
            {
                transitionGroupDocNodes = _selectedPrecursorPaths.Select(path => document.FindNode(path))
                    .OfType<TransitionGroupDocNode>().ToList();
            }

            var tolerance = document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            foreach (var msDataFileUri in _dataFileList)
            {
                if (!_spectrumLists.TryGetValue(msDataFileUri, out var spectrumList ))
                {
                    continue;
                }

                IReadOnlyList<SpectrumMetadata> spectra = spectrumList;
                if (transitionGroupDocNodes != null)
                {
                    spectra = ImmutableList.ValueOf(spectra.Where(spectrum =>
                        FindMatchingTransitionGroups(spectrum, transitionGroupDocNodes, tolerance).Any()));
                }

                foreach (var spectrumGroup in spectra.GroupBy(SpectrumClassKey.FromSpectrumMetadata))
                {
                    if (!spectrumClasses.TryGetValue(spectrumGroup.Key, out var spectrumClass))
                    {
                        spectrumClass = new SpectrumClass(_dataSchema, spectrumGroup.Key);
                        spectrumClasses.Add(spectrumGroup.Key, spectrumClass);
                    }
                    spectrumClass.Files.Add(msDataFileUri.GetFileName(), new FileSpectrumInfo(_dataSchema, spectrumGroup.Count()));
                }
            }

            _spectrumClasses.Clear();
            _spectrumClasses.AddRange(spectrumClasses.Values);
            _bindingList.ResetBindings();
            UpdateViewContext();
        }

        private void UpdateViewContext()
        {
            var viewContext = BindingListSource.ViewContext as SkylineViewContext;
            if (viewContext == null)
            {
                return;
            }

            viewContext.SetRowSources(MakeRowSourceInfos());
            if (null != BindingListSource.ViewInfo && null != BindingListSource.ViewInfo.ViewGroup && ViewGroup.BUILT_IN.Id.Equals(BindingListSource.ViewInfo.ViewGroup.Id))
            {
                var viewName = BindingListSource.ViewInfo.ViewGroup.Id.ViewName(BindingListSource.ViewInfo.Name);
                var newViewInfo = viewContext.GetViewInfo(viewName);
                if (null != newViewInfo && !ColumnsEqual(newViewInfo, BindingListSource.ViewInfo))
                {
                    BindingListSource.SetView(newViewInfo, BindingListSource.RowSource);
                }
            }
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
            _spectrumReader.RemoveAll();
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

                foreach (var msDataFileUri in dataSrcDlg.DataSources)
                {
                    if (_dataFileSet.Add(msDataFileUri))
                    {
                        _dataFileList.Add(msDataFileUri);
                        if (!_spectrumLists.ContainsKey(msDataFileUri))
                        {
                            _spectrumReader.AddFile(msDataFileUri);
                            listBoxFiles.Items.Add(msDataFileUri.GetFileName());
                        }
                    }
                }
            }
        }

        public void SetSpectra(MsDataFileUri dataFile, IList<SpectrumMetadata> spectra)
        {
            _spectrumLists[dataFile] = new SpectrumMetadataList(spectra);
            UpdateSpectrumRows();
        }

        public HashSet<IdentityPath> GetSelectedPrecursorPaths()
        {
            return GetPrecursorPathsFromSelectedPaths(SkylineWindow.DocumentUI,
                SkylineWindow.SequenceTree.SelectedPaths);
        }

        public HashSet<IdentityPath> GetPrecursorPathsFromSelectedPaths(SrmDocument document,
            IEnumerable<IdentityPath> selectedPaths)
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
                        result.UnionWith(molecule.TransitionGroups.Select(tg =>
                            new IdentityPath(identityPath, tg.TransitionGroup)));
                    }
                }
                else if (identityPath.Length > 2)
                {
                    result.Add(identityPath.GetPathTo(2));
                }
            }

            return result;
        }

        private void UpdateProgress(string text, int value)
        {
            statusPanel.Visible = true;
            lblStatus.Text = text;
            progressBar1.Value = value;
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

        class SpectrumReader
        {
            private SpectraGridForm _form;
            private List<MsDataFileUri> _files;
            private bool _isRunning;

            public SpectrumReader(SpectraGridForm form)
            {
                _form = form;
                _files = new List<MsDataFileUri>();
            }

            public void AddFile(MsDataFileUri file)
            {
                lock (this)
                {
                    if (_files.Contains(file))
                    {
                        return;
                    }
                    _files.Add(file);
                    if (!_isRunning)
                    {
                        ActionUtil.RunAsync(ReadSpectra, @"Spectrum Reader");
                        _isRunning = true;
                    }
                }
            }

            public void RemoveFile(MsDataFileUri file)
            {
                lock (this)
                {
                    _files.Remove(file);
                }
            }

            public void RemoveAll()
            {
                lock (this)
                {
                    _files.Clear();
                }
            }

            public void Stop()
            {
                lock (this)
                {
                    _files = null;
                    Monitor.PulseAll(this);
                }
            }

            private void ReadSpectra()
            {
                while (true)
                {
                    MsDataFileUri msDataFileUri;
                    lock (this)
                    {
                        if (_files.Count == 0)
                        {
                            CommonActionUtil.SafeBeginInvoke(_form, () => _form.statusPanel.Visible = false);
                            _isRunning = false;
                            return;
                        }

                        msDataFileUri = _files.First();
                    }

                    if (ReadSpectraFromFile(msDataFileUri))
                    {
                        lock (this)
                        {
                            _files.Remove(msDataFileUri);
                        }
                    }
                }
            }

            private bool ReadSpectraFromFile(MsDataFileUri file)
            {
                using (var msDataFile = file.OpenMsDataFile(true, false, false, false, true))
                {
                    var spectra = new List<SpectrumMetadata>();
                    int spectrumCount = msDataFile.SpectrumCount;
                    int lastProgress = -1;
                    for (int i = 0; i < spectrumCount; i++)
                    {
                        lock (this)
                        {
                            if (!Equals(file, _files.FirstOrDefault()))
                            {
                                return false;
                            }
                        }

                        int progress = i * 100 / spectrumCount;
                        if (progress != lastProgress)
                        {
                            lastProgress = progress;
                            string message = string.Format("Reading spectra from {0}", file.GetFileName());
                            CommonActionUtil.SafeBeginInvoke(_form, () => _form.UpdateProgress(message, progress));
                        }

                        var spectrumMetadata = msDataFile.GetSpectrumMetadata(i);
                        if (spectrumMetadata != null)
                        {
                            spectra.Add(spectrumMetadata);
                        }
                    }

                    CommonActionUtil.SafeBeginInvoke(_form, () => _form.SetSpectra(file, spectra));
                    return true;
                }
            }
        }
    }
}
