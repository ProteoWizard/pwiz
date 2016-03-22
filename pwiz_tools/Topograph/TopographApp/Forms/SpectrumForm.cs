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
using System.Drawing;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.MSGraph;
using pwiz.ProteowizardWrapper;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using ZedGraph;
using pwiz.Topograph.MsData;

namespace pwiz.Topograph.ui.Forms
{
    public partial class SpectrumForm : WorkspaceForm
    {
        private const string NumberFormat = "#,###";
        private int _scanIndex = -1;
        private int _minCharge = 1;
        private int _maxCharge = 10;
        private string _peptideSequence;
        private double _massAccuracy;
        private ChromatogramData[] _chromatogramDatas;
        private SpectrumData _spectrumData;
        public SpectrumForm(MsDataFile msDataFile) : base(msDataFile.Workspace)
        {
            InitializeComponent();
            MsDataFile = msDataFile;
            tbxScanIndex.Leave += (o, e) => ScanIndex = int.Parse(tbxScanIndex.Text);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Redisplay();
        }

        public int ScanIndex
        {
            get
            {
                return _scanIndex;
            }
            set
            {
                if (value == ScanIndex)
                {
                    return;
                }
                _scanIndex = value;
                _spectrumData = null;
                tbxScanIndex.Text = ScanIndex.ToString();
                Redisplay();
            }
        }
        public MsDataFile MsDataFile
        {
            get;private set;
        }
        public void SetPeptideAnalysis(PeptideAnalysis peptideAnalysis)
        {
            PeptideSequence = peptideAnalysis.Peptide.Sequence;
            MinCharge = peptideAnalysis.MinCharge;
            MaxCharge = peptideAnalysis.MaxCharge;
            MassAccuracy = peptideAnalysis.GetMassAccuracy();
        }
        public int MinCharge
        {
            get
            {
                return _minCharge;
            }
            set
            {
                if (value == MinCharge)
                {
                    return;
                }
                _minCharge = value;
                Redisplay();                
            }
        }

        public int MaxCharge
        {
            get { return _maxCharge; }
            set
            {
                if (value == MaxCharge)
                {
                    return;
                }
                _maxCharge = value;
                Redisplay();
            }
        }
        public double MassAccuracy
        {
            get { return _massAccuracy; }
            set
            {
                if (value == MassAccuracy)
                {
                    return;
                }
                _massAccuracy = value;
                Redisplay();
            }
        }
        public string PeptideSequence
        {
            get { return _peptideSequence; }
            set
            {
                if (value == PeptideSequence)
                {
                    return;
                }
                _peptideSequence = value;
                Redisplay();
            }
        }

        private bool _inRedisplay;
        public void Redisplay()
        {
            if (!IsHandleCreated)
            {
                return;
            }
            if (_inRedisplay)
            {
                return;
            }
            try
            {
                _inRedisplay = true;
                tbxMinCharge.Text = MinCharge.ToString();
                tbxMaxCharge.Text = MaxCharge.ToString();
                tbxPeptideSequence.Text = PeptideSequence;
                cbxShowPeptideMzs.Enabled = !string.IsNullOrEmpty(PeptideSequence);
                tbxMassAccuracy.Text = MassAccuracy.ToString();
                TurnoverCalculator turnoverCalculator = null;
                if (!string.IsNullOrEmpty(PeptideSequence))
                {
                    turnoverCalculator = new TurnoverCalculator(Workspace, PeptideSequence);
                }
                
                msGraphControlEx1.GraphPane.GraphObjList.Clear();
                msGraphControlEx1.GraphPane.CurveList.Clear();
                if (_spectrumData == null || comboChromatogram.SelectedIndex >= 0 && null == _chromatogramDatas[comboChromatogram.SelectedIndex])
                {
                    using (var msDataFileImpl = new MsDataFileImpl(Workspace.GetDataFilePath(MsDataFile.Name)))
                    {
                        if (comboChromatogram.SelectedIndex >= 0 && null == _chromatogramDatas[comboChromatogram.SelectedIndex])
                        {
                            string chromatogramName;
                            float[] timeArray;
                            float[] intensityArray;
                            msDataFileImpl.GetChromatogram(comboChromatogram.SelectedIndex, out chromatogramName, out timeArray, out intensityArray);
                            _chromatogramDatas[comboChromatogram.SelectedIndex] = new ChromatogramData(chromatogramName, timeArray, intensityArray);
                        }
                        _spectrumData = _spectrumData ?? new SpectrumData(msDataFileImpl, ScanIndex);
                    }
                }
                ChromatogramData chromatogram = null;
                if (comboChromatogram.SelectedIndex >= 0)
                {
                    chromatogram = _chromatogramDatas[comboChromatogram.SelectedIndex];
                }
                tbxMsLevel.Text = _spectrumData.MsLevel.ToString();
                tbxTime.Text = _spectrumData.Time.ToString();
                if (chromatogram != null && ScanIndex < chromatogram.TimeArray.Length)
                {
                    tbxChromatogramRetentionTime.Text = chromatogram.TimeArray[ScanIndex].ToString();
                    tbxChromatogramIonCurrent.Text = chromatogram.IntensityArray[ScanIndex].ToString(NumberFormat);
                }
                else
                {
                    tbxChromatogramRetentionTime.Text = tbxChromatogramIonCurrent.Text = @"N/A";
                }

                if (cbxShowProfile.Checked && _spectrumData.ProfileMzs != null)
                {
                    msGraphControlEx1.AddGraphItem(msGraphControlEx1.GraphPane, new SpectrumGraphItem()
                    {
                        Points =
                            new PointPairList(_spectrumData.ProfileMzs,
                                              _spectrumData.ProfileIntensities),
                        GraphItemDrawMethod = MSGraphItemDrawMethod.line,

                        Color = Color.Blue,
                    });
                }

                if (_spectrumData.ProfileIntensities != null)
                {
                    tbxSumOfProfileIntensities.Text = _spectrumData.ProfileIntensities.Sum().ToString(NumberFormat);    
                }
                else
                {
                    tbxSumOfProfileIntensities.Text = "";
                }
                tbxCentroidIntensitySum.Text = _spectrumData.CentroidIntensities.Sum().ToString(NumberFormat);
                

                var spectrum = new SpectrumGraphItem
                                   {
                                       Points = new PointPairList(_spectrumData.CentroidMzs, _spectrumData.CentroidIntensities),
                                       GraphItemDrawMethod = MSGraphItemDrawMethod.stick,
                                       Color = Color.Black
                                   };

                if (turnoverCalculator != null)
                {
                    var mzRanges = new Dictionary<MzRange, String>();
                    double monoisotopicMass = Workspace.GetAminoAcidFormulas().GetMonoisotopicMass(PeptideSequence);
                    double peptideIntensity = 0.0;
                    for (int charge = MinCharge; charge <= MaxCharge; charge ++)
                    {
                        foreach (var mzRange in turnoverCalculator.GetMzs(charge))
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
                            var chromatogramPoint = MsDataFileUtil.GetPoint(mzRange, _spectrumData.CentroidMzs, _spectrumData.CentroidIntensities);
                            peptideIntensity += chromatogramPoint.GetIntensity(mzRange, MassAccuracy);
                        }
                    }
                    spectrum.MassAccuracy = MassAccuracy;
                    spectrum.MzRanges = mzRanges;
                    tbxPeptideIntensity.Text = peptideIntensity.ToString(NumberFormat);
                }
                if (cbxShowCentroids.Checked)
                {
                    msGraphControlEx1.AddGraphItem(msGraphControlEx1.GraphPane, spectrum);
                }


                if (turnoverCalculator != null && cbxShowPeptideMzs.Checked)
                {
                    double massAccuracy = MassAccuracy;
                    for (int charge = MinCharge; charge <= MaxCharge; charge++)
                    {
                        var mzs = turnoverCalculator.GetMzs(charge);
                        var height = int.MaxValue;
                        foreach (var mzRange in mzs)
                        {
                            double min = mzRange.MinWithMassAccuracy(massAccuracy);
                            double max = mzRange.MaxWithMassAccuracy(massAccuracy);

                            msGraphControlEx1.GraphPane.GraphObjList.Add(new BoxObj(min, height, max - min, height, Color.Goldenrod, Color.Goldenrod)
                                                                        {
                                                                            IsClippedToChartRect = true,
                                                                            ZOrder = ZOrder.F_BehindGrid  
                                                                        });
                        }
                    }
                }
                msGraphControlEx1.Invalidate();
            }
            finally
            {
                _inRedisplay = false;
            }
        }
        public void Zoom(double minMz, double maxMz)
        {
            msGraphControlEx1.GraphPane.XAxis.Scale.Min = minMz;
            msGraphControlEx1.GraphPane.XAxis.Scale.Max = maxMz;
            msGraphControlEx1.Invalidate();
        }

        private void CbxShowPeptideMzsOnCheckedChanged(object sender, EventArgs e)
        {
            Redisplay();
        }

        private void BtnPrevPrevScanOnClick(object sender, EventArgs e)
        {
            MoveToNextScan(-1, true);
        }

        private void BtnPrevScanOnClick(object sender, EventArgs e)
        {
            MoveToNextScan(-1, false);
        }

        private void BtnNextScanOnClick(object sender, EventArgs e)
        {
            MoveToNextScan(1, false);
        }

        private void BtnNextNextScanOnClick(object sender, EventArgs e)
        {
            MoveToNextScan(1, true);
        }

        private bool MoveToNextScan(int direction, bool onlyMs1)
        {
            using (var msDataFileImpl = new MsDataFileImpl(Workspace.GetDataFilePath(MsDataFile.Name)))
            {
                int scanIndex = Math.Min(msDataFileImpl.SpectrumCount - 1, Math.Max(0, ScanIndex));
                if (scanIndex < 0)
                {
                    return false;
                }
                while (true)
                {
                    scanIndex += direction;
                    if (scanIndex < 0 || scanIndex >= msDataFileImpl.SpectrumCount)
                    {
                        return false;
                    }
                    if (!onlyMs1 || msDataFileImpl.GetMsLevel(scanIndex) == 1)
                    {
                        break;
                    }
                }
                ScanIndex = scanIndex;
                return true;
            }
        }

        private void CbxShowCentroidsOnCheckedChanged(object sender, EventArgs e)
        {
            Redisplay();
        }

        private void TbxPeptideSequenceOnTextChanged(object sender, EventArgs e)
        {
            PeptideSequence = tbxPeptideSequence.Text;
        }

        private void ComboChromatogramOnSelectedIndexChanged(object sender, EventArgs e)
        {
            Redisplay();
        }
        class ChromatogramData
        {
            public ChromatogramData(string name, float[] times, float[] intensities)
            {
                Name = name;
                TimeArray = times;
                IntensityArray = intensities;
            }

            public string Name { get; private set; }
            public float[] TimeArray { get; private set; }
            public float[] IntensityArray { get; private set; }
            public override string ToString()
            {
                return Name;
            }
        }

        public class SpectrumGraphItem : IMSGraphItemInfo
        {
            public string Title { get; set; }
            public Color Color { get; set; }
            public float LineWidth { get { return LineBase.Default.Width; } }
            
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
                if (GraphItemDrawMethod == MSGraphItemDrawMethod.stick)
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
                get { return MSGraphItemType.chromatogram; }
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

        class SpectrumData
        {
            public SpectrumData(MsDataFileImpl msDataFileImpl, int scanIndex)
            {
                ScanIndex = scanIndex;
                var spectrum = msDataFileImpl.GetSpectrum(scanIndex);
                if (spectrum.Centroided)
                {
                    CentroidMzs = spectrum.Mzs;
                    CentroidIntensities = spectrum.Intensities;
                }
                else
                {
                    ProfileMzs = spectrum.Mzs;
                    ProfileIntensities = spectrum.Intensities;
                    var centroider = new Centroider(ProfileMzs, ProfileIntensities);
                    double[] centroidMzs, centroidIntensities;
                    centroider.GetCentroidedData(out centroidMzs, out centroidIntensities);
                    CentroidMzs = centroidMzs;
                    CentroidIntensities = centroidIntensities;
                }
                Time = spectrum.RetentionTime;
                MsLevel = spectrum.Level;
            }
            public int ScanIndex { get; private set; }
            public int MsLevel { get; private set; }
            public double? Time { get; private set; }
            public double[] ProfileMzs { get; private set; }
            public double[] ProfileIntensities { get; private set; }
            public double[] CentroidMzs { get; private set; }
            public double[] CentroidIntensities { get; private set; }
        }

        private void CbxShowProfileOnCheckedChanged(object sender, EventArgs e)
        {
            Redisplay();
        }

        private void TbxMassAccuracyOnLeave(object sender, EventArgs e)
        {
            MassAccuracy = double.Parse(tbxMassAccuracy.Text);
        }

        private void TbxPeptideSequenceOnLeave(object sender, EventArgs e)
        {
            PeptideSequence = tbxPeptideSequence.Text;
        }

        private void TbxMinChargeOnLeave(object sender, EventArgs e)
        {
            MinCharge = int.Parse(tbxMinCharge.Text);
        }

        private void TbxMaxChargeOnLeave(object sender, EventArgs e)
        {
            MaxCharge = int.Parse(tbxMaxCharge.Text);
        }

        private void ComboChromatogramOnDropDown(object sender, EventArgs e)
        {
            if (_chromatogramDatas != null)
            {
                return;
            }
            comboChromatogram.Items.Clear();
            using (var msDataFileImpl = new MsDataFileImpl(Workspace.GetDataFilePath(MsDataFile.Name)))
            {
                _chromatogramDatas = new ChromatogramData[msDataFileImpl.ChromatogramCount];
                for (int i = 0; i < _chromatogramDatas.Length; i++)
                {
                    int indexId;
                    comboChromatogram.Items.Add(msDataFileImpl.GetChromatogramId(i, out indexId));
                }
            }
        }
    }

}
