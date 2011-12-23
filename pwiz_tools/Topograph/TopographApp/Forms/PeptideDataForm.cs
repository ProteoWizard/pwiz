using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using NHibernate;
using turnover.Data;
using turnover.Enrichment;

namespace turnover.ui.Forms
{
    public partial class PeptideDataForm : PeptideAnalysisForm
    {
        private readonly DockPanel dockPanel;
        private PeptideDataForm(PeptideAnalysis peptideAnalysis) : base(peptideAnalysis)
        {
            InitializeComponent();
            Workspace = peptideAnalysis.Workspace;
            dockPanel = new DockPanel
                            {
                                Dock = DockStyle.Fill
                            };
            panel1.Controls.Add(dockPanel);
            tbxDataFile.Text = peptideAnalysis.MsDataFileName;
            tbxSequence.Text = peptideAnalysis.ChargedPeptide.ToString();
            String label = PeptideAnalysis.Sequence + "+" + PeptideAnalysis.Charge;
            if (label.Length > 15)
            {
                label = label.Substring(0, 5) + "..." + label.Substring(label.Length - 7, 7);
            }
            Name = TabText = label;
        }

        public static PeptideDataForm ActivatePeptideDataForm(Form sibling, PeptideAnalysis peptideAnalysis)
        {
            PeptideDataForm peptideDataForm;
            foreach (var form in Application.OpenForms)
            {
                if (!(form is PeptideDataForm))
                {
                    continue;
                }
                
                if (((PeptideDataForm)form).PeptideAnalysis != peptideAnalysis)
                {
                    continue;
                }
                peptideDataForm = (PeptideDataForm)form;
                peptideDataForm.Activate();
                return peptideDataForm;
            }
            peptideDataForm = new PeptideDataForm(peptideAnalysis);
            if (sibling is DockableForm)
            {
                DockableForm dockableSibling = (DockableForm)sibling;
                if (dockableSibling.DockPanel != null)
                {
                    peptideDataForm.Show(dockableSibling.DockPanel, dockableSibling.DockState);
                    return peptideDataForm;
                }
            }
            if (sibling != null)
            {
                peptideDataForm.Show(sibling.Parent);
            }
            else
            {
                peptideDataForm.Show(null);
            }
            peptideDataForm.ShowForm<PeptideInfoForm>();
            return peptideDataForm;
        }
        public T ShowForm<T>() where T : PeptideAnalysisForm
        {
            foreach (var form in dockPanel.Contents)
            {
                T tForm = form as T;
                if (tForm == null)
                {
                    continue;
                }
                tForm.Activate();
                return tForm;
            }
            if (!HasChromatograms)
            {
                MessageBox.Show("That form cannot be shown until the chromatograms have been generated.");
                return null;
            }
            T newForm = (T) typeof(T).GetConstructor(new[] {typeof(PeptideAnalysis)}).Invoke(new object[] {PeptideAnalysis});
            newForm.Show(dockPanel, DockState.Document);
            return newForm;
        }

        public bool HasChromatograms { get; private set;}
        public bool WaitingForChromatograms { get; private set;}

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            new PeptideInfoForm(PeptideAnalysis).Show(dockPanel, DockState.Document);
            DbPeptideAnalysis dbPeptideAnalysis;
            List<AnalysisChromatograms> analyses = new List<AnalysisChromatograms>();
            using (ISession session = Workspace.OpenSession())
            {
                dbPeptideAnalysis = session.Get<DbPeptideAnalysis>(PeptideAnalysis.Id);
            }
            if (dbPeptideAnalysis.HasChromatograms)
            {
                HasChromatograms = true;
                return;
            }
            DbMsDataFile msDataFile = TurnoverForm.Instance.EnsureMsDataFile(dbPeptideAnalysis.MsDataFile);
            if (msDataFile == null)
            {
                return;
            }

            WaitingForChromatograms = true;
            tbxStatus.Text = "Generating chromatograms";
            progressBar1.Visible = true;
            new Action<DbMsDataFile>(GenerateChromatogramsBackground).BeginInvoke(msDataFile, null, null);
        }

        private void GenerateChromatogramsBackground(DbMsDataFile msDataFile)
        {
            ChromatogramGenerator chromatogramGenerator = new ChromatogramGenerator(Workspace);
            chromatogramGenerator.GenerateChromatograms(msDataFile, new List<AnalysisChromatograms>{new AnalysisChromatograms(PeptideAnalysis)}, UpdateChromatogramProgress );
            SafeBeginInvoke(delegate
                                {
                                    HasChromatograms = true;
                                    WaitingForChromatograms = false;
                                    progressBar1.Visible = false;
                                    tbxStatus.Text = "";
                                });
            
        }
        private bool UpdateChromatogramProgress(int iProgress)
        {
            if (IsDisposed)
            {
                return false;
            }
            SafeBeginInvoke(delegate { progressBar1.Value = iProgress; });
            return true;
        }
    }
}
