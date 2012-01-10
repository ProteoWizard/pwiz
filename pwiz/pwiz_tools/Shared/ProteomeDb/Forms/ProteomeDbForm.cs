using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ProteomeDb.API;

namespace ProteomeDb.Forms
{
    public partial class ProteomeDbForm : Form
    {
        public const string FILTER = "Proteome Databases (*.protdb)|*.protdb"
                                     + "|All Files|*.*";

        public const string FASTA_FILTER = "Fasta Files (*.fasta)|*.fasta"
                                           + "|All Files|*.*";

        private ProteomeDb.API.ProteomeDb proteomeDb;

        public ProteomeDbForm()
        {
            InitializeComponent();
            foreach (String enzyme in Enzymes.AllEnzymes().Keys)
            {
                lbxDigestion.Items.Add(enzyme);
            }
            lbxDigestion.SelectedIndex = 0;
        }
        private void RefreshOrganisms()
        {
            lbxOrganisms.Items.Clear();
            if (ProteomeDb != null)
            {
                foreach (Organism organism in ProteomeDb.ListOrganisms())
                {
                    lbxOrganisms.Items.Add(organism.Name);
                }
                if (lbxOrganisms.Items.Count > 0)
                {
                    lbxOrganisms.SelectedIndex = 0;
                    btnDigest.Enabled = true;
                }
                else
                {
                    btnDigest.Enabled = false;
                }
                btnAddOrganism.Enabled = true;
            }
            else
            {
                btnDigest.Enabled = false;
                btnAddOrganism.Enabled = false;
            }
        }
        private void btnCreate_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
                                        {
                                            Filter = FILTER
                                        };
            dialog.ShowDialog(this);
            String filename = dialog.FileName;
            if (String.IsNullOrEmpty(filename))
            {
                return;
            }
            ProteomeDb 
                = global::ProteomeDb.API.ProteomeDb.CreateProteomeDb(filename);
        }

        public ProteomeDb.API.ProteomeDb ProteomeDb
        {
            get
            {
                return proteomeDb;
            }
            set
            {
                proteomeDb = value;
                RefreshOrganisms();
            }
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
                                        {
                                            Filter = FILTER,
                                        };
            dialog.ShowDialog(this);
            String filename = dialog.FileName;
            if (String.IsNullOrEmpty(filename))
            {
                return;
            }
            ProteomeDb = global::ProteomeDb.API.ProteomeDb.OpenProteomeDb(filename);
            
        }

        private void btnAddOrganism_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
                                        {
                                            Filter = FASTA_FILTER
                                        };
            dialog.ShowDialog(this);
            String filename = dialog.FileName;
            if (String.IsNullOrEmpty(filename))
            {
                return;
            }
            var reader = File.OpenText(filename);
            RunBackground(()=>
                              {
                                  ProteomeDb.CreateOrganism(Path.GetFileNameWithoutExtension(filename), null, reader, UpdateProgress);
                                  if (!IsDisposed)
                                  {
                                      BeginInvoke(new Action(() =>
                                      {
                                          RefreshOrganisms();
                                          lbxOrganisms.SelectedItem = Path.GetFileNameWithoutExtension(filename);
                                      }));
                                      
                                  }
                              });
        }

        private void btnDigest_Click(object sender, EventArgs e)
        {
            String enzymeName = lbxDigestion.SelectedItem.ToString();
            IProtease protease = Enzymes.AllEnzymes()[enzymeName];
            String organism = lbxOrganisms.SelectedItem.ToString();
            RunBackground(() =>
                       proteomeDb.GetOrganism(organism).Digest(protease, enzymeName, null, UpdateProgress));
            
        }
        private bool UpdateProgress(String task, int progress)
        {
            BeginInvoke(new Action(() =>
                                       {
                                           tbxTask.Text = task;
                                           progressBar.Value = progress;
                                       }));
            return true;
        }
        private void RunBackground(Action action)
        {
            try
            {
                action.BeginInvoke(null, null);
            }
            catch (Exception e)
            {
                if (!IsDisposed)
                {
                    BeginInvoke(new Action(() => MessageBox.Show("ProteomeDB Unhandled Exception:" + e)));
                }
            }
        }

    }
}
