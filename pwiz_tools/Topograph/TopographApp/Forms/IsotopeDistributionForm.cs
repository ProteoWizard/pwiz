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
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.MSGraph;
using pwiz.Common.Chemistry;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.ui.Controls;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class IsotopeDistributionForm : WorkspaceForm
    {
        private readonly MSGraphControl _msGraphControl;
        
        public IsotopeDistributionForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            _msGraphControl = new MSGraphControlEx {Dock = DockStyle.Fill};
            _msGraphControl.GraphPane.XAxis.Title.Text = "M/Z";
            _msGraphControl.GraphPane.YAxis.Title.Text = "Intensity";
            colMass.DefaultCellStyle.Format = "0.######";
            colIntensity.DefaultCellStyle.Format = "0.######";
            splitContainer1.Panel2.Controls.Add(_msGraphControl);
        }

        public String Sequence {get { return tbxSequence.Text;}}

        private void TbxSequenceOnTextChanged(object sender, EventArgs e)
        {
            UpdateGraph();
        }


        private static bool _inUpdateGraph;
        private void UpdateGraph()
        {
            if (_inUpdateGraph)
            {
                return;
            }
            try
            {
                _inUpdateGraph = true;
                _msGraphControl.GraphPane.GraphObjList.Clear();
                _msGraphControl.GraphPane.CurveList.Clear();
                dataGridView1.Rows.Clear();

                int charge;
                try
                {
                    charge = Convert.ToInt32(tbxCharge.Text);
                    tbxCharge.BackColor = Color.White;
                }
                catch
                {
                    charge = 0;
                    tbxCharge.BackColor = Color.Red;
                }
                double massResolution;
                try
                {
                    massResolution = double.Parse(tbxMassResolution.Text);
                    tbxMassResolution.BackColor = Color.White;
                }
                catch
                {
                    tbxMassResolution.BackColor = Color.Red;
                    massResolution = .01;
                }

                var sequence = tbxSequence.Text;
                MassDistribution spectrum;
                if (string.IsNullOrEmpty(sequence))
                {
                    var formula = tbxFormula.Text;
                    var molecule = Molecule.Parse(formula);
                    if (molecule.Values.Sum() > 10000000)
                    {
                        tbxFormula.BackColor = Color.Red;
                        return;
                    }
                    try
                    {
                        spectrum = Workspace.GetAminoAcidFormulas().GetMassDistribution(molecule, charge);
                        tbxFormula.BackColor = Color.White;
                    }
                    catch
                    {
                        tbxFormula.BackColor = Color.Red;
                        return;
                    }
                }
                else
                {
                    tbxFormula.Text = Workspace.GetAminoAcidFormulas().GetFormula(sequence).ToString();
                    var turnoverCalculator = new TurnoverCalculator(Workspace, sequence);
                    var aminoAcidFormulas = Workspace.GetAminoAcidFormulasWithTracers();
                    aminoAcidFormulas = aminoAcidFormulas.SetMassResolution(massResolution);
                    try
                    {
                        if (cbxTracerPercents.Checked)
                        {
                            var tracerPercentFormula = TracerPercentFormula.Parse(tbxTracerFormula.Text);
                            spectrum = turnoverCalculator.GetAminoAcidFormulas(tracerPercentFormula)
                                .GetMassDistribution(sequence, charge);
                        }
                        else
                        {
                            var tracerFormula = TracerFormula.Parse(tbxTracerFormula.Text);
                            spectrum = aminoAcidFormulas.GetMassDistribution(
                                turnoverCalculator.MoleculeFromTracerFormula(tracerFormula), charge);
                            double massShift = aminoAcidFormulas.GetMassShift(sequence);
                            if (charge > 0)
                            {
                                massShift /= charge;
                            }
                            spectrum = spectrum.OffsetAndDivide(massShift, 1);
                        }
                        tbxTracerFormula.BackColor = Color.White;
                    }
                    catch
                    {
                        tbxTracerFormula.BackColor = Color.Red;
                        spectrum = aminoAcidFormulas.GetMassDistribution(sequence, charge);
                    }
                }
                if (spectrum == null)
                {
                    return;
                }
                var curveItem = _msGraphControl.AddGraphItem(_msGraphControl.GraphPane, new GraphItem
                                                                                            {
                                                                                                Title = "Intensity",
                                                                                                Color = Color.Black,
                                                                                                Points =
                                                                                                    new PointPairList(
                                                                                                    spectrum.Keys.
                                                                                                        ToArray(),
                                                                                                    spectrum.Values.
                                                                                                        ToArray())
                                                                                            });
                curveItem.Label.IsVisible = false;
                _msGraphControl.AxisChange();
                _msGraphControl.Invalidate();
                var entries = spectrum.ToArray();
                dataGridView1.Rows.Add(entries.Length);
                for (int i = 0; i < entries.Count(); i++)
                {
                    var row = dataGridView1.Rows[i];
                    row.Cells[colMass.Index].Value = entries[i].Key;
                    row.Cells[colIntensity.Index].Value = entries[i].Value;
                }
            }
            finally
            {
                _inUpdateGraph = false;
            }
        }

        private void TbxChargeOnTextChanged(object sender, EventArgs e)
        {
            UpdateGraph();
        }

        private void TbxTracerFormulaOnTextChanged(object sender, EventArgs e)
        {
            UpdateGraph();
        }

        private void TbxMassResolutionOnTextChanged(object sender, EventArgs e)
        {
            UpdateGraph();
        }

        private class GraphItem : IMSGraphItemInfo
        {
            public string Title { get; set; }
            public Color Color { get; set; }
            public float LineWidth { get { return LineBase.Default.Width; } }

            void CustomizeAxis(Axis axis)
            {
                axis.Title.FontSpec.Family = "Arial";
                axis.Title.FontSpec.Size = 14;
                axis.Color = axis.Title.FontSpec.FontColor = Color.Black;
                axis.Title.FontSpec.Border.IsVisible = false;
            }

            public PointAnnotation AnnotatePoint(PointPair point)
            {
                return new PointAnnotation(Math.Round(point.X, 4).ToString(CultureInfo.CurrentCulture));
            }

            public void AddAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
            {
            }

            public MSGraphItemType GraphItemType
            {
                get { return MSGraphItemType.chromatogram; }
            }

            public MSGraphItemDrawMethod GraphItemDrawMethod { get {return MSGraphItemDrawMethod.stick;}}

            public void CustomizeXAxis(Axis axis)
            {
                axis.Title.Text = "M/Z";
                CustomizeAxis(axis);
            }

            public void CustomizeYAxis(Axis axis)
            {
                axis.Title.Text = "Intensity";
                CustomizeAxis(axis);
            }

            public IPointList Points
            {
                get;
                set;
            }
        }

        private void CbxTracerPercentsOnCheckedChanged(object sender, EventArgs e)
        {
            UpdateGraph();
        }

        private void TbxFormulaOnTextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(tbxSequence.Text))
            {
                if (tbxFormula.Text != Workspace.GetAminoAcidFormulas().GetFormula(tbxSequence.Text).ToString())
                {
                    tbxSequence.Text = "";
                }
            }
            UpdateGraph();
        }
    }
}
