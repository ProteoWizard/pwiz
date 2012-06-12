using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.MSGraph;
using pwiz.Common.Chemistry;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.ui.Controls;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class MercuryForm : WorkspaceForm
    {
        private MSGraphControl _msGraphControl;
        
        public MercuryForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            _msGraphControl = new MSGraphControlEx() {Dock = DockStyle.Fill};
            _msGraphControl.GraphPane.XAxis.Title.Text = "M/Z";
            _msGraphControl.GraphPane.YAxis.Title.Text = "Intensity";
            colMass.DefaultCellStyle.Format = "0.######";
            colIntensity.DefaultCellStyle.Format = "0.######";
            splitContainer1.Panel2.Controls.Add(_msGraphControl);
        }

        public String Sequence {get { return tbxSequence.Text;}}

        private void tbxSequence_TextChanged(object sender, EventArgs e)
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
                var curveItem = _msGraphControl.AddGraphItem(_msGraphControl.GraphPane, new GraphItem()
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

        private void tbxCharge_TextChanged(object sender, EventArgs e)
        {
            UpdateGraph();
        }

        private void tbxTracerFormula_TextChanged(object sender, EventArgs e)
        {
            UpdateGraph();
        }

        private void tbxMassResolution_TextChanged(object sender, EventArgs e)
        {
            UpdateGraph();
        }

        private class GraphItem : IMSGraphItemInfo
        {
            public string Title { get; set; }
            public Color Color { get; set; }

            void CustomizeAxis(Axis axis)
            {
                axis.Title.FontSpec.Family = "Arial";
                axis.Title.FontSpec.Size = 14;
                axis.Color = axis.Title.FontSpec.FontColor = Color.Black;
                axis.Title.FontSpec.Border.IsVisible = false;
            }

            public PointAnnotation AnnotatePoint(PointPair point)
            {
                return new PointAnnotation(Math.Round(point.X, 4).ToString());
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

        private void cbxTracerPercents_CheckedChanged(object sender, EventArgs e)
        {
            UpdateGraph();
        }

        private void tbxFormula_TextChanged(object sender, EventArgs e)
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
