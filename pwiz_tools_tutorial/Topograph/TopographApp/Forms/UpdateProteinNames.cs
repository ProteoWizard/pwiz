using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using pwiz.ProteomeDatabase.Fasta;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class UpdateProteinNames : WorkspaceForm
    {
        private const int PEPTIDE_INDEX_LENGTH = 5;
        private bool _running;
        private bool _canceled;
        private String _statusText;
        private int _progress;
        public UpdateProteinNames(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _canceled = true;
            while (true)
            {
                lock(this)
                {
                    if (!_running)
                    {
                        return;
                    }
                    Monitor.Wait(this);
                }
            }
        }

        protected override void OnShown(EventArgs e)
        {
            _running = true;
            new Action(DoWork).BeginInvoke(null, null);
        }

        public IList<string> FastaFilePaths { get; set; }

        private void DoWork()
        {
            try
            {
                var fastaImporter = new FastaImporter();
                var proteins = new Dictionary<string, ProteinData>();
                foreach (var path in FastaFilePaths)
                {
                    var statusText = "Reading FASTA file " + Path.GetFileName(path);
                    var fileInfo = new FileInfo(path);
                    var reader = File.OpenText(path);
                    foreach (var protein in fastaImporter.Import(File.OpenText(path)))
                    {
                        if (!UpdateProgress(statusText, (int) (reader.BaseStream.Position * 100 / fileInfo.Length)))
                        {
                            return;
                        }
                        ProteinData proteinData;
                        if (!proteins.TryGetValue(protein.Sequence, out proteinData))
                        {
                            proteinData = new ProteinData(protein.Sequence);
                            proteins.Add(protein.Sequence, proteinData);
                        }
                        foreach (var name in protein.Names)
                        {
                            proteinData.AddName(name.Name, name.Description);
                        }
                    }
                }
                UpdatePeptides(proteins);

                if (!IsDisposed)
                {
                    BeginInvoke(new Action(Close));
                }
            }
            finally
            {
                lock(this)
                {
                    _running = false;
                    Monitor.PulseAll(this);
                }
            }
        }

        private bool UpdateProgress(String status, int progress)
        {
            _statusText = status;
            _progress = progress;
            return !_canceled;
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            tbxStatus.Text = _statusText;
            progressBar.Value = _progress;
        }

        private void UpdatePeptides(Dictionary<string, ProteinData> allProteins)
        {
            var peptideIndex = CreatePeptideIndex(allProteins.Keys);
            if (peptideIndex == null)
            {
                return;
            }
            var peptides = Workspace.Peptides.ListChildren();
            for (int i = 0; i < peptides.Count(); i++)
            {
                if (!UpdateProgress("Peptide " + i + "/" + peptides.Count, 100 * i / peptides.Count))
                {
                    return;
                }
                var peptide = peptides[i];
                var proteinSequences = FindPeptides(peptide.Sequence, peptideIndex);
                if (proteinSequences.Count == 0)
                {
                    continue;
                }
                var names = new SortedDictionary<String, String>();
                foreach (var proteinSequence in proteinSequences)
                {
                    var proteinData = allProteins[proteinSequence];
                    names[proteinData.GetName()] = proteinData.GetDescription();
                }
                peptide.UpdateProtein(string.Join("\r\n", names.Keys.ToArray()), string.Join("\r\n---\r\n", names.Values.ToArray()));
            }

        }

        class ProteinData
        {
            public List<string> Names { get; private set; }
            public List<string> Descriptions { get; private set; }
            public string Sequence { get; private set; }
            public ProteinData(string sequence)
            {
                Names = new List<string>();
                Descriptions = new List<string>();
                Sequence = sequence;
            }
            public String GetName()
            {
                return string.Join(" ", Names.ToArray());
            }
            public String GetDescription()
            {
                return string.Join("\r\n", Descriptions.ToArray());
            }
            public void AddName(string name, string description)
            {
                if (!Names.Contains(name))
                {
                    Names.Add(name);
                }
                if (!Descriptions.Contains(description))
                {
                    Descriptions.Add(description);
                }
            }
        }

        private IDictionary<string, IList<string>> CreatePeptideIndex(ICollection<string> proteinsCollection)
        {
            var result = new Dictionary<string, IList<string>>();
            IList<string> proteinsArray = proteinsCollection.ToArray();
            for (int proteinIndex = 0; proteinIndex < proteinsArray.Count; proteinIndex++ )
            {
                if (!UpdateProgress("Indexing protein sequences", 100 * proteinIndex / proteinsArray.Count))
                {
                    return null;
                }
                var proteinSequence = proteinsArray[proteinIndex];
                var peptides = new HashSet<string>();
                for (int i = 0; i < proteinSequence.Length - PEPTIDE_INDEX_LENGTH; i++)
                {
                    var peptide = proteinSequence.Substring(i, PEPTIDE_INDEX_LENGTH);
                    if (peptides.Contains(peptide))
                    {
                        continue;
                    }
                    IList<string> list;
                    if (!result.TryGetValue(peptide, out list))
                    {
                        list = new List<string>();
                        result.Add(peptide, list);
                    }
                    list.Add(proteinSequence);
                    peptides.Add(peptide);
                }
            }
            return result;
        }
        IList<string> FindPeptides(string peptide, IDictionary<string, IList<string>> index)
        {
            if (peptide.Length < PEPTIDE_INDEX_LENGTH)
            {
                return new string[0];
            }
            IList<string> candidates;
            if (!index.TryGetValue(peptide.Substring(0, PEPTIDE_INDEX_LENGTH), out candidates))
            {
                return new string[0];
            }
            var result = new List<string>();
            foreach (var protein in candidates)
            {
                if (protein.IndexOf(peptide) >= 0)
                {
                    result.Add(protein);
                }
            }
            return result;
        }
    }
}
