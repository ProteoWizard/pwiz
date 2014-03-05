/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.Collections;
using pwiz.ProteomeDatabase.Fasta;
using pwiz.Topograph.Model;
using pwiz.Topograph.Model.Data;

namespace pwiz.Topograph.ui.Forms
{
    public partial class UpdateProteinNames : WorkspaceForm
    {
        private const int PeptideIndexLength = 5;
        private bool _running;
        private bool _canceled;
        private String _statusText;
        private int _progress;
        public UpdateProteinNames(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
        }

        private void BtnCancelOnClick(object sender, EventArgs e)
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
                // just do the basic name+description parsing, no regex or web access - we don't use extended metadata here
                var fastaImporter = new WebEnabledFastaImporter(new WebEnabledFastaImporter.FakeWebSearchProvider()); 
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

        private void TimerOnTick(object sender, EventArgs e)
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
            for (int retryCount = 0; ;retryCount++)
            {
                var originalPeptides = Workspace.Data.Peptides;
                var peptides = originalPeptides.ToArray();
                for (int i = 0; i < peptides.Length; i++)
                {
                    string message = string.Format("Peptide {0}/{1}", i, peptides.Length);
                    if (retryCount > 0)
                    {
                        message += string.Format(" (Retry #{0})", retryCount);
                    }
                    if (!UpdateProgress(message, 100 * i / peptides.Length))
                    {
                        return;
                    }
                    var peptide = peptides[i].Value;
                    var proteinSequences = FindPeptides(peptide.Sequence, peptideIndex);
                    if (proteinSequences.Count == 0)
                    {
                        continue;
                    }
                    var names = new SortedDictionary<String, String>();
                    var prevAAs = new List<string>();
                    var nextAAs = new List<string>();
                    foreach (var proteinSequence in proteinSequences)
                    {
                        var proteinData = allProteins[proteinSequence];
                        names[proteinData.GetName()] = proteinData.GetDescription();
                        for (int ichPeptide = proteinSequence.IndexOf(peptide.Sequence, StringComparison.Ordinal);
                             ichPeptide >= 0;
                             ichPeptide = proteinSequence.IndexOf(peptide.Sequence, ichPeptide + 1, StringComparison.Ordinal))
                        {
                            if (ichPeptide == 0)
                            {
                                prevAAs.Add("-");
                            }
                            else
                            {
                                prevAAs.Add(proteinSequence.Substring(ichPeptide - 1, 1));
                            }
                            int ichPeptideEnd = ichPeptide + peptide.Sequence.Length;
                            if (ichPeptideEnd == proteinSequence.Length)
                            {
                                nextAAs.Add("-");
                            }
                            else
                            {
                                nextAAs.Add(proteinSequence.Substring(ichPeptideEnd, 1));
                            }
                        }
                    }
                    string fullSequence;
                    if (prevAAs.Distinct().Count() == 1)
                    {
                        fullSequence = prevAAs[0] + ".";
                    }
                    else
                    {
                        fullSequence = "?.";
                    }
                    fullSequence += peptide.Sequence;
                    if (nextAAs.Distinct().Count() == 1)
                    {
                        fullSequence += "." + nextAAs[0];
                    }
                    else
                    {
                        fullSequence += ".?";
                    }
                    peptide = peptide.SetFullSequence(fullSequence)
                                     .SetProteinName(string.Join("\r\n", names.Keys.ToArray()))
                                     .SetProteinDescription(string.Join("\r\n---\r\n", names.Values.ToArray()));
                    peptides[i] = new KeyValuePair<long, PeptideData>(peptides[i].Key, peptide);
                }
                bool success = Workspace.RunOnEventQueue(() =>
                    {
                        if (!Equals(originalPeptides, Workspace.Data.Peptides))
                        {
                            return false;
                        }
                        Workspace.Data = Workspace.Data.SetPeptides(ImmutableSortedList.FromValues(peptides));
                        return true;
                    });
                if (success)
                {
                    return;
                }
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
                for (int i = 0; i < proteinSequence.Length - PeptideIndexLength; i++)
                {
                    var peptide = proteinSequence.Substring(i, PeptideIndexLength);
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
            if (peptide.Length < PeptideIndexLength)
            {
                return new string[0];
            }
            IList<string> candidates;
            if (!index.TryGetValue(peptide.Substring(0, PeptideIndexLength), out candidates))
            {
                return new string[0];
            }
            var result = new List<string>();
            foreach (var protein in candidates)
            {
                if (protein.IndexOf(peptide, StringComparison.Ordinal) >= 0)
                {
                    result.Add(protein);
                }
            }
            return result;
        }
    }
}
