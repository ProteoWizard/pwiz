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
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Common.Chemistry;
using pwiz.MSGraph;
using pwiz.ProteowizardWrapper;
using pwiz.Topograph.Controls;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.MsData;
using ZedGraph;
using Label=System.Windows.Forms.Label;

namespace pwiz.Topograph.ui.Forms
{
    public partial class SpectrumForm : WorkspaceForm
    {
        private int _scanIndex;
        private readonly MSGraphControl _msGraphControl;
        private PeptideAnalysis _peptideAnalysis;
        public SpectrumForm(MsDataFile msDataFile) : base(msDataFile.Workspace)
        {
            InitializeComponent();
            MsDataFile = msDataFile;
            tbxScanIndex.Leave += (o, e) => ScanIndex = int.Parse(tbxScanIndex.Text);
            _msGraphControl = new MSGraphControl()
                                 {
                                     Dock = DockStyle.Fill,
                                 };
            _msGraphControl.ContextMenuBuilder += _msGraphControl_ContextMenuBuilder;
            Controls.Add(_msGraphControl);
        }

        void _msGraphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            menuStrip.Items.Insert(0, new CopyEmfToolStripMenuItem(sender));
        }

        public int ScanIndex
        {
            get
            {
                return _scanIndex;
            }
            set
            {
                _scanIndex = value;
                tbxScanIndex.Text = _scanIndex.ToString();
                Redisplay();
            }
        }
        public MsDataFile MsDataFile
        {
            get;private set;
        }
        public PeptideAnalysis PeptideAnalysis
        {
            get
            {
                return _peptideAnalysis;
            }
            set
            {
                _peptideAnalysis = value;
                Redisplay();
            }
        }
        
        public void Redisplay()
        {
            cbxShowPeptideMzs.Visible = PeptideAnalysis != null;
            double[] mzArray;
            double[] intensityArray;
            _msGraphControl.GraphPane.GraphObjList.Clear();
            _msGraphControl.GraphPane.CurveList.Clear();

            using (var msDataFileImpl = new MsDataFileImpl(Workspace.GetDataFilePath(MsDataFile.Name)))
            {
                tbxMsLevel.Text = MsDataFile.GetMsLevel(_scanIndex, msDataFileImpl).ToString();
                try
                {
                    tbxTime.Text = MsDataFile.GetTime(_scanIndex).ToString();
                }
                catch
                {
                    tbxTime.Text = "#Error#";
                }
                msDataFileImpl.GetSpectrum(_scanIndex, out mzArray, out intensityArray);
                if (!msDataFileImpl.IsCentroided(_scanIndex))
                {

                    _msGraphControl.AddGraphItem(_msGraphControl.GraphPane, new SpectrumGraphItem()
                    {
                        Points =
                            new PointPairList(mzArray,
                                              intensityArray),
                        GraphItemDrawMethod = MSGraphItemDrawMethod.Line,

                        Color = Color.Blue,
                    });
                    var centroider = new Centroider(mzArray, intensityArray);
                    centroider.GetCentroidedData(out mzArray, out intensityArray);
                }

                var spectrum = new SpectrumGraphItem
                                   {
                                       Points = new PointPairList(mzArray, intensityArray),
                                       GraphItemDrawMethod = MSGraphItemDrawMethod.Stick,
                                       Color = Color.Black
                                   };
                if (PeptideAnalysis != null)
                {
                    var mzRanges = new Dictionary<MzRange, String>();
                    double monoisotopicMass = Workspace.GetAminoAcidFormulas().GetMonoisotopicMass(PeptideAnalysis.Peptide.Sequence);
                    for (int charge = PeptideAnalysis.MinCharge; charge <= PeptideAnalysis.MaxCharge; charge ++)
                    {
                        foreach (var mzRange in PeptideAnalysis.GetMzs()[charge])
                        {
                            double mass = (mzRange.Center - AminoAcidFormulas.ProtonMass)* charge;
                            double massDifference = mass - monoisotopicMass;
                            var label = massDifference.ToString("0.#");
                            if (label[0] != '-')
                            {
                                label = "+" + label;
                            }
                            label = "M" + label;
                            label += new string('+', charge);
                            mzRanges.Add(mzRange, label);
                        }
                    }
                    spectrum.MassAccuracy = PeptideAnalysis.GetMassAccuracy();
                    spectrum.MzRanges = mzRanges;
                }
                _msGraphControl.AddGraphItem(_msGraphControl.GraphPane, spectrum);

            }
            if (PeptideAnalysis != null && cbxShowPeptideMzs.Checked)
            {
                double massAccuracy = PeptideAnalysis.GetMassAccuracy();
                for (int charge = PeptideAnalysis.MinCharge; charge <= PeptideAnalysis.MaxCharge; charge++)
                {
                    var mzs = PeptideAnalysis.GetMzs()[charge];
                    var height = int.MaxValue;
                    foreach (var mzRange in mzs)
                    {
                        double min = mzRange.MinWithMassAccuracy(massAccuracy);
                        double max = mzRange.MaxWithMassAccuracy(massAccuracy);

                        _msGraphControl.GraphPane.GraphObjList.Add(new BoxObj(min, height, max - min, height, Color.Goldenrod, Color.Goldenrod)
                                                                    {
                                                                        IsClippedToChartRect = true,
                                                                        ZOrder = ZOrder.F_BehindGrid  
                                                                    });
                    }
                }
            }
            //msGraphControl.AxisChange();
            _msGraphControl.Invalidate();
        }
        public void Zoom(double minMz, double maxMz)
        {
            _msGraphControl.GraphPane.XAxis.Scale.Min = minMz;
            _msGraphControl.GraphPane.XAxis.Scale.Max = maxMz;
            _msGraphControl.Invalidate();
        }

        private void cbxShowPeptideMzs_CheckedChanged(object sender, EventArgs e)
        {
            Redisplay();
        }
    }
    public class SpectrumGraphItem : IMSGraphItemInfo
    {
        public string Title { get; set; }
        public Color Color { get; set; }
        
        public Dictionary<MzRange, String> MzRanges { get; set; }
        public double MassAccuracy { get; set; }

        public void CustomizeAxis(Axis axis)
        {
            axis.Title.FontSpec.Family = "Arial";
            axis.Title.FontSpec.Size = 14;
            axis.Color = axis.Title.FontSpec.FontColor = Color.Black;
            axis.Title.FontSpec.Border.IsVisible = false;
        }

        public PointAnnotation AnnotatePoint(PointPair point)
        {
            if (GraphItemDrawMethod == MSGraphItemDrawMethod.Stick)
            {
                var text = point.X.ToString("0.####");
                if (MzRanges != null)
                {
                    foreach (var entry in MzRanges)
                    {
                        if (entry.Key.ContainsWithMassAccuracy(point.X, MassAccuracy))
                        {
                            text = text + "\n" + entry.Value;
                            break;
                        }
                    }
                }
                
                return new PointAnnotation(text);
            }
            return new PointAnnotation();
        }

        public void AddAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
        {
        }

        public MSGraphItemType GraphItemType
        {
            get { return MSGraphItemType.Chromatogram; }
        }

        public MSGraphItemDrawMethod GraphItemDrawMethod
        {
            get; set;
        }

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

}
