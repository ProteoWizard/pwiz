/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Linq;
using System.Runtime.InteropServices;
using NHibernate.Criterion;
using pwiz.Common.DataBinding;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.MsData;
using pwiz.Topograph.Util;
using pwiz.Topograph.ui.Properties;

namespace pwiz.Topograph.ui.Forms
{
    public partial class HalfLivesForm : WorkspaceForm
    {
        private IViewContext _viewContext;
        public HalfLivesForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            var tracerDef = workspace.GetTracerDefs()[0];
            tbxInitialTracerPercent.Text = tracerDef.InitialApe.ToString();
            tbxFinalTracerPercent.Text = tracerDef.FinalApe.ToString();
            foreach (var evviesFilter in Enum.GetValues(typeof(EvviesFilterEnum)))
            {
                comboEvviesFilter.Items.Add(evviesFilter);
            }
            Settings.Default.Reload();
            HalfLifeSettings = Settings.Default.HalfLifeSettings;
            UpdateTimePoints();
            navBar1.ViewContext = _viewContext = new TopographViewContext(workspace, typeof (ResultRow), new[] {GetDefaultViewSpec(false)});
        }

        private ViewSpec GetDefaultViewSpec(bool byProtein)
        {
            List<ColumnSpec> columnSpecs = new List<ColumnSpec>();
            if (!byProtein)
            {
                columnSpecs.Add(new ColumnSpec().SetName("Peptide"));
            }
            columnSpecs.Add(new ColumnSpec().SetName("ProteinName"));
            columnSpecs.Add(new ColumnSpec().SetName("ProteinDescription"));
            columnSpecs.Add(new ColumnSpec().SetName("HalfLives.[].Value"));
            return new ViewSpec().SetName("default").SetColumns(columnSpecs);
        }

        public double MinScore
        {
            get
            {
                if (string.IsNullOrEmpty(tbxMinScore.Text))
                {
                    return 0;
                }
                try
                {
                    return double.Parse(tbxMinScore.Text);
                }
                catch
                {
                    return 0;
                }
            }
        }

        public HalfLifeSettings HalfLifeSettings
        {
            get
            {
                return new HalfLifeSettings
                           {
                               ByProtein = cbxByProtein.Checked,
                               BySample = cbxBySample.Checked,
                               EvviesFilter = (EvviesFilterEnum) comboEvviesFilter.SelectedIndex,
                               HalfLifeCalculationType = (HalfLifeCalculationType) comboCalculationType.SelectedIndex,
                               HoldInitialTracerPercentConstant = cbxFixYIntercept.Checked,
                               MinimumAuc = HalfLifeSettings.TryParseDouble(tbxMinAuc.Text, 0),
                               MinimumDeconvolutionScore = HalfLifeSettings.TryParseDouble(tbxMinScore.Text, 0),
                               MinimumTurnoverScore = HalfLifeSettings.TryParseDouble(tbxMinTurnoverScore.Text, 0),
                           };
            }
            set 
            { 
                cbxByProtein.Checked = value.ByProtein;
                cbxBySample.Checked = value.BySample;
                comboEvviesFilter.SelectedIndex = (int) value.EvviesFilter;
                comboCalculationType.SelectedIndex = (int) value.HalfLifeCalculationType;
                cbxFixYIntercept.Checked = value.HoldInitialTracerPercentConstant;
                tbxMinAuc.Text = value.MinimumAuc.ToString();
                tbxMinScore.Text = value.MinimumDeconvolutionScore.ToString();
                tbxMinTurnoverScore.Text = value.MinimumTurnoverScore.ToString();
            }
        }

        public double MinTurnoverScore
        {
            get
            {
                if (string.IsNullOrEmpty(tbxMinTurnoverScore.Text))
                {
                    return 0;
                }
                try
                {
                    return double.Parse(tbxMinTurnoverScore.Text);
                }
                catch
                {
                    return 0;
                }
            }
        }

        public double MinAuc
        {
            get
            {
                if (string.IsNullOrEmpty(tbxMinAuc.Text))
                {
                    return 0;
                }
                try
                {
                    return double.Parse(tbxMinAuc.Text);
                }
                catch
                {
                    return 0;
                }
            }
            set { tbxMinAuc.Text = value.ToString(); }
        }

        private void btnRequery_Click(object sender, EventArgs e)
        {
            var halfLifeSettings = HalfLifeSettings;
            Settings.Default.Reload();
            Settings.Default.HalfLifeSettings = halfLifeSettings;
            Settings.Default.Save();
            var calculator = new HalfLifeCalculator(Workspace, halfLifeSettings)
                                 {
                                     InitialPercent = double.Parse(tbxInitialTracerPercent.Text),
                                     FinalPercent = double.Parse(tbxFinalTracerPercent.Text),
                                     ExcludedTimePoints = UpdateTimePoints(),
                                 };
            using (var longWaitDialog = new LongWaitDialog(TopLevelControl, "Calculating Half Lives"))
            {
                var longOperationBroker = new LongOperationBroker(calculator, longWaitDialog);
                if (!longOperationBroker.LaunchJob())
                {
                    return;
                }
            }
            var viewInfo = dataGridView1.BindingListView.ViewInfo;
            var rows = calculator.ResultRows.Select(row => new ResultRow(this, row)).ToArray();
            if (viewInfo == null || "default" == viewInfo.Name)
            {
                viewInfo = new ViewInfo(_viewContext.ParentColumn, GetDefaultViewSpec(calculator.ByProtein));
            }
            dataGridView1.BindingListView.ViewInfo = viewInfo;
            dataGridView1.BindingListView.RowSource = rows;
        }

        private void comboCalculationType_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (HalfLifeCalculationType)
            {
                default:
                    tbxInitialTracerPercent.Enabled = false;
                    tbxFinalTracerPercent.Enabled = false;
                    break;
                case HalfLifeCalculationType.TracerPercent:
                    tbxInitialTracerPercent.Enabled = true;
                    tbxFinalTracerPercent.Enabled = true;
                    break;
            }
        }

        /// <summary>
        /// Updates the list of time points in the "time points" checked list box.
        /// Returns the set of time points that are unchecked (and therefore excluded).
        /// </summary>
        /// <returns></returns>
        private ICollection<double> UpdateTimePoints()
        {
            var existingListItems = new double[checkedListBoxTimePoints.Items.Count];
            for (int i = 0; i < checkedListBoxTimePoints.Items.Count; i++)
            {
                existingListItems[i] = (double) checkedListBoxTimePoints.Items[i];
            }            
            var allTimePoints = new HashSet<double>(Workspace.MsDataFiles.ListChildren()
                                                        .Where(d => d.TimePoint.HasValue)
                                                        .Select(d => d.TimePoint.Value))
                                                        .ToArray();
            Array.Sort(allTimePoints);
            var excludedTimePoints =
                existingListItems.Where((d, index) => !checkedListBoxTimePoints.GetItemChecked(index)).ToArray();
            if (!Lists.EqualsDeep(existingListItems, allTimePoints))
            {
                checkedListBoxTimePoints.Items.Clear();
                foreach (var time in allTimePoints)
                {
                    checkedListBoxTimePoints.Items.Add(time);
                    checkedListBoxTimePoints
                        .SetItemChecked(checkedListBoxTimePoints.Items.Count - 1, !excludedTimePoints.Contains(time));
                }
            }
            return new HashSet<double>(excludedTimePoints.Where(t => allTimePoints.Contains(t)));
        }

        public HalfLifeCalculationType HalfLifeCalculationType
        {
            get
            {
                return (HalfLifeCalculationType)comboCalculationType.SelectedIndex;
            }
            set { comboCalculationType.SelectedIndex = (int) value; }
        }

        public EvviesFilterEnum EvviesFilter
        {
            get
            {
                return (EvviesFilterEnum) comboEvviesFilter.SelectedIndex;
            }
            set
            {
                comboEvviesFilter.SelectedIndex = (int) value;
            }
        }

        public class ResultRow
        {
            private HalfLivesForm _form;
            private HalfLifeCalculator.ResultRow _halfLifeResultRow;
            public ResultRow(HalfLivesForm form, HalfLifeCalculator.ResultRow halfLifeResultRow)
            {
                _form = form;
                _halfLifeResultRow = halfLifeResultRow;
                HalfLives = new Dictionary<string, LinkValue<HalfLifeCalculator.ResultData>>();
                foreach (var resultDataEntry in _halfLifeResultRow.HalfLives)
                {
                    var cohort = resultDataEntry.Key;
                    HalfLives.Add(resultDataEntry.Key, new LinkValue<HalfLifeCalculator.ResultData>(resultDataEntry.Value, 
                        (sender, args)=>ShowHalfLifeForm(_halfLifeResultRow.Peptide, _halfLifeResultRow.ProteinName, cohort)
                        ));
                }
            }
            public LinkValue<Peptide> Peptide
            {
                get
                {
                    return new LinkValue<Peptide>(_halfLifeResultRow.Peptide, PeptideClickHandler);
                }
            }
            public LinkValue<String> ProteinName
            {
                get
                {
                    return new LinkValue<string>(_halfLifeResultRow.ProteinName, ProteinClickHandler);
                }
            }
            public string ProteinKey
            {
                get
                {
                    return _form.Workspace.GetProteinKey(_halfLifeResultRow.ProteinName, _halfLifeResultRow.ProteinDescription);
                }
            }
            public string ProteinDescription
            {
                get
                {
                    return _halfLifeResultRow.ProteinDescription;
                }
            }
            [OneToMany(IndexDisplayName = "Cohort", ItemDisplayName = "Half Life")]
            public IDictionary<string, LinkValue<HalfLifeCalculator.ResultData>> HalfLives
            {
                get; private set;
            }

            private void PeptideClickHandler(object sender, EventArgs eventArgs)
            {
                var peptide = _halfLifeResultRow.Peptide;
                if (peptide == null)
                {
                    return;
                }
                DbPeptideAnalysis dbPeptideAnalysis;
                using (var session = _form.Workspace.OpenSession())
                {
                    dbPeptideAnalysis = (DbPeptideAnalysis) session.CreateCriteria(typeof (DbPeptideAnalysis))
                        .Add(Restrictions.Eq("Peptide", session.Load<DbPeptide>(peptide.Id)))
                        .UniqueResult();
                    if (dbPeptideAnalysis == null)
                    {
                        return;
                    }
                    var peptideAnalysis = TurnoverForm.Instance.LoadPeptideAnalysis(dbPeptideAnalysis.Id.Value);
                    if (peptideAnalysis == null)
                    {
                        return;
                    }
                    var form = Program.FindOpenEntityForm<PeptideAnalysisFrame>(peptideAnalysis);
                    if (form != null)
                    {
                        form.Activate();
                        return;
                    }
                    form = new PeptideAnalysisFrame(peptideAnalysis);
                    form.Show(_form.DockPanel, _form.DockState);
                    return;
                }
            }
            private void ProteinClickHandler(object sender, EventArgs eventArgs)
            {
                ShowHalfLifeForm(null, _halfLifeResultRow.ProteinName, "");
            }
            [DllImport("user32.dll")]
            static extern short GetKeyState(int nVirtKey);

            private void ShowHalfLifeForm(Peptide peptide, string proteinName, string cohort)
            {
                if (0 != (GetKeyState(0x10) & 0x8000))
                {
                    var halfLifeRowDataForm = new HalfLifeRowDataForm(_form.Workspace)
                                                  {
                                                      Protein = proteinName,
                                                  };
                    if (peptide != null)
                    {
                        halfLifeRowDataForm.Peptide = peptide.ToString();
                    }
                    var resultData = HalfLives[cohort].Value;
                    halfLifeRowDataForm.RowDatas = resultData.RowDatas;
                    halfLifeRowDataForm.Show(_form.DockPanel, _form.DockState);
                    return;
                }
                var halfLifeForm = new HalfLifeForm(_form.Workspace)
                                       {
                                           Peptide = peptide == null ? "" : peptide.Sequence,
                                           ProteinName = proteinName,
                                           Cohort = cohort,
                                           InitialPercent = double.Parse(_form.tbxInitialTracerPercent.Text),
                                           FinalPercent = double.Parse(_form.tbxFinalTracerPercent.Text),
                                       };
                halfLifeForm.SetHalfLifeSettings(_form.HalfLifeSettings);
                for (int i = 0; i < _form.checkedListBoxTimePoints.Items.Count; i++)
                {
                    halfLifeForm.SetTimePointExcluded((double)_form.checkedListBoxTimePoints.Items[i],
                                                      !_form.checkedListBoxTimePoints.GetItemChecked(i));
                }
                halfLifeForm.Show(_form.DockPanel, _form.DockState);
            }
        }
    }
}
