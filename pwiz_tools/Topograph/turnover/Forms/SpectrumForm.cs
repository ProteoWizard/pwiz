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
using MSGraph;
using pwiz.ProteowizardWrapper;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class SpectrumForm : WorkspaceForm
    {
        private int scanIndex;
        private MSGraphControl msGraphControl;
        public SpectrumForm(MsDataFile msDataFile) : base(msDataFile.Workspace)
        {
            InitializeComponent();
            MsDataFile = msDataFile;
            tbxScanIndex.Leave += (o, e) => ScanIndex = int.Parse(tbxScanIndex.Text);
            msGraphControl = new MSGraphControl()
                                 {
                                     Dock = DockStyle.Fill,
                                 };
            Controls.Add(msGraphControl);
        }
        public int ScanIndex
        {
            get
            {
                return scanIndex;
            }
            set
            {
                scanIndex = value;
                tbxScanIndex.Text = scanIndex.ToString();
                Redisplay();
            }
        }
        public MsDataFile MsDataFile
        {
            get;private set;
        }
        public void Redisplay()
        {
            double[] mzArray;
            double[] intensityArray;
            msGraphControl.GraphPane.GraphObjList.Clear();
            msGraphControl.GraphPane.CurveList.Clear();
            using (var msDataFileImpl = new MsDataFileImpl(MsDataFile.Path))
            {
                tbxMsLevel.Text = msDataFileImpl.GetMsLevel(scanIndex).ToString();
                try
                {
                    tbxTime.Text = msDataFileImpl.GetScanTimes()[scanIndex].ToString();
                }
                catch
                {
                    tbxTime.Text = "#Error#";
                }
                if (!msDataFileImpl.IsCentroided(scanIndex))
                {
                    msDataFileImpl.GetSpectrum(scanIndex, out mzArray, out intensityArray);
                    msGraphControl.AddGraphItem(msGraphControl.GraphPane, new SpectrumGraphItem()
                                                                              {
                                                                                  Points =
                                                                                      new PointPairList(mzArray,
                                                                                                        intensityArray),
                                                                                                        GraphItemDrawMethod = MSGraphItemDrawMethod.Line,

                    Color = Color.Blue,
                                                                              });
                }
                msDataFileImpl.GetCentroidedSpectrum(scanIndex, out mzArray, out intensityArray);
                msGraphControl.AddGraphItem(msGraphControl.GraphPane,
                                            new SpectrumGraphItem()
                                                {
                                                    Points = new PointPairList(mzArray, intensityArray),
                                                    GraphItemDrawMethod = MSGraphItemDrawMethod.Stick,
                                                    Color = Color.Black
                                                });
            }
            msGraphControl.AxisChange();
            msGraphControl.Invalidate();
        }
        public void Zoom(double minMz, double maxMz)
        {
            msGraphControl.GraphPane.XAxis.Scale.Min = minMz;
            msGraphControl.GraphPane.XAxis.Scale.Max = maxMz;
            msGraphControl.Invalidate();
        }
    }
    public class SpectrumGraphItem : IMSGraphItemInfo
    {
        public string Title { get; set; }
        public Color Color { get; set; }

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
                return new PointAnnotation(Math.Round(point.X, 4).ToString());
            }
            return new PointAnnotation();
        }

        public void AddAnnotations(MSPointList pointList, GraphObjList annotations)
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
