/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditPeakScoringModelDlg : FormEx
    {
        private MProphetPeakScoringModel _peakScoringModel;
        private readonly IEnumerable<IPeakScoringModel> _existing; 
        private readonly PeakCalculatorGridViewDriver _gridViewDriver;

        public EditPeakScoringModelDlg(MProphetPeakScoringModel model, IEnumerable<IPeakScoringModel> existing)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _gridViewDriver = new PeakCalculatorGridViewDriver(gridPeakCalculators, bindingPeakCalculators,
                                                            new SortableBindingList<PeakCalculatorWeight>());
            PeakScoringModel = model;
            _existing = existing;
        }

        public MProphetPeakScoringModel PeakScoringModel
        {
            get { return _peakScoringModel; }

            set
            {
                _peakScoringModel = value;

                if (_peakScoringModel == null)
                {
                    textName.Text = string.Empty;
                    textMean.Text = string.Empty;
                    textStdev.Text = string.Empty;
                }
                else
                {
                    textName.Text = _peakScoringModel.Name;
                    textMean.Text = _peakScoringModel.DecoyMean.ToString(CultureInfo.CurrentCulture);
                    textStdev.Text = _peakScoringModel.DecoyStdev.ToString(CultureInfo.CurrentCulture);
                }

                InitializeCalculatorGrid();
            }
        }

        private void InitializeCalculatorGrid()
        {
            // Create list of calculators and their corresponding weights.  If no scoring model
            // exists yet, the weights will be null.
            PeakCalculatorWeights.Clear();
            foreach (var calculator in PeakFeatureCalculator.Calculators)
            {
                double? weight = null;
                if (_peakScoringModel != null)
                {
                    // Find the weight for the matching calculator in the model.
                    for (int i = 0; i < _peakScoringModel.PeakFeatureCalculators.Count; i++)
                    {
                        if (calculator.GetType() == _peakScoringModel.PeakFeatureCalculators[i])
                        {
                            weight = _peakScoringModel.Weights[i];
                            break;
                        }
                    }
                }
                PeakCalculatorWeights.Add(new PeakCalculatorWeight(calculator.Name, weight));
            }
        }

        private SortableBindingList<PeakCalculatorWeight> PeakCalculatorWeights { get { return _gridViewDriver.Items; } }

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(e, textName, out name))
                return;

            if (_existing != null && _existing.Contains(r => !ReferenceEquals(_peakScoringModel, r) && Equals(name, r.Name)))
            {
                helper.ShowTextBoxError(textName, Resources.EditPeakScoringModelDlg_OkDialog_The_peak_scoring_model__0__already_exists, name);
                e.Cancel = true;
                return;
            }

            double decoyMean;
            if (!helper.ValidateDecimalTextBox(e, textMean, out decoyMean))
                return;

            double decoyStdev;
            if (!helper.ValidateDecimalTextBox(e, textStdev, out decoyStdev))
                return;

            var peakScoringModel = new MProphetPeakScoringModel(
                name, 
                GetTypes(PeakFeatureCalculator.Calculators).ToList(),
                GetWeightsFromGrid(),
                decoyMean,
                decoyStdev);

            _peakScoringModel = peakScoringModel;

            DialogResult = DialogResult.OK;
        }

        private IEnumerable<Type> GetTypes(IEnumerable<object> objects)
        {
            return objects.Select(o => o.GetType());
        }

        private double[] GetWeightsFromGrid()
        {
            var calculators = PeakFeatureCalculator.Calculators.ToArray();
            var weights = new double[calculators.Length];
            for (int i = 0; i < calculators.Length; i++)
            {
                var calculator = calculators[i];
                foreach (var t in PeakCalculatorWeights)
                {
                    if (t.Matches(calculator.Name))
                    {
                        weights[i] = t.Weight ?? 0.0;
                        break;
                    }
                }
            }
            return weights;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnUseCurrent_Click(object sender, EventArgs e)
        {
            AddResults();
        }

        public void AddResults()
        {
        }

        private void btnShowGraph_Click(object sender, EventArgs e)
        {
            ShowGraph();
        }

        public void ShowGraph()
        {
        }


        #region Functional test support

        public string PeakScoringModelName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public string Mean
        {
            get { return textMean.Text; }
            set { textMean.Text = value; }
        }

        public string Stdev
        {
            get { return textStdev.Text; }
            set { textStdev.Text = value; }
        }

        public SimpleGridViewDriver<PeakCalculatorWeight> PeakCalculatorsGrid
        {
            get { return _gridViewDriver; }
        }

        #endregion


        private class PeakCalculatorGridViewDriver : SimpleGridViewDriver<PeakCalculatorWeight>
        {
            public PeakCalculatorGridViewDriver(DataGridViewEx gridView,
                                             BindingSource bindingSource,
                                             SortableBindingList<PeakCalculatorWeight> items)
                : base(gridView, bindingSource, items)
            {
                GridView.DataError += GridView_DataError;
                foreach (var calculator in PeakFeatureCalculator.Calculators)
                    Items.Add(new PeakCalculatorWeight(calculator.Name, null));
            }

            private void GridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
            {
                SelectCell(e.ColumnIndex, e.RowIndex);
                MessageDlg.Show(MessageParent, string.Format(Resources.GridViewDriver_GridView_DataError__0__must_be_a_valid_number, GetHeaderText(e.ColumnIndex)));
                EditCell();     // Edit bad data
            }

            private string GetHeaderText(int index)
            {
                return GridView.Columns[index].HeaderText;
            }

            protected override void DoPaste()
            {
                var calculatorWeightsNew = new List<PeakCalculatorWeight>();
                if (!GridView.DoPaste(MessageParent, ValidateRow, (values, lineNum) =>
                    calculatorWeightsNew.Add(values.Length == 1
                        ? new PeakCalculatorWeight(Items[lineNum-1].Name, double.Parse(values[0]))
                        : new PeakCalculatorWeight(values[0], double.Parse(values[1])))))
                    return;

                // Special case for pasting only a single number.
                if (calculatorWeightsNew.Count == 1)
                {
                    if (calculatorWeightsNew[0].Weight.HasValue)
                    {
                        SetCellValue(calculatorWeightsNew[0].Weight.Value);
                    }
                    return;
                }

                SetCalculatorWeights(calculatorWeightsNew);
            }

            private bool ValidateRow(object[] columns, int lineNumber)
            {
                double value;
                string weight;

                if (columns.Length == 1)
                {
                    // Assume we have one column of numbers.  They will be paired with names in current display order.
                    weight = columns[0] as string;
                    return (!string.IsNullOrEmpty(weight) &&
                        double.TryParse(weight, out value));
                }

                string name = columns[0] as string;
                weight = columns[1] as string;
                if (string.IsNullOrEmpty(name) ||
                    string.IsNullOrEmpty(weight) ||
                    !double.TryParse(weight, out value))
                    return false;

                try
                {
// ReSharper disable ObjectCreationAsStatement
                    new PeakCalculatorWeight(name, value);
// ReSharper restore ObjectCreationAsStatement
                }
                catch (Exception x)
                {
                    MessageDlg.Show(MessageParent, 
                        string.Format(Resources.PeakCalculatorGridViewDriver_ValidateRow_On_line__0____1_, lineNumber, x.Message));
                    return false;
                }

                return true;
            }

            private void SetCalculatorWeights(IEnumerable<PeakCalculatorWeight> calculatorWeights)
            {
                foreach (var calculatorWeight in calculatorWeights)
                {
                    for (int i = 0; i < Items.Count; i++)
                    {
                        if (Items[i].Matches(calculatorWeight.Name))
                        {
                            Items[i] = new PeakCalculatorWeight(Items[i].Name, calculatorWeight.Weight);
                            break;
                        }
                    }
                }
            }
        }
    }

    public class PeakCalculatorWeight
    {
        public string Name { get; private set; }
        public double? Weight { get; set; }

        public PeakCalculatorWeight(string name, double? weight)
        {
            Name = name;
            Weight = weight;
            Validate();
        }

        public void Validate()
        {
            if (!PeakFeatureCalculator.Calculators.Any(calculator => Matches(calculator.Name)))
                throw new InvalidDataException(string.Format(Resources.PeakCalculatorWeight_Validate___0___is_not_a_known_name_for_a_peak_feature_calculator, Name));
        }

        public bool Matches(string name)
        {
            return (String.Compare(name, Name, StringComparison.OrdinalIgnoreCase) == 0);
        }
    }
}
