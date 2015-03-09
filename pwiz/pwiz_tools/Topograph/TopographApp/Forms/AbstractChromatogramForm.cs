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
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using pwiz.MSGraph;
using pwiz.Topograph.ui.Controls;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.ui.Properties;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class AbstractChromatogramForm : PeptideFileAnalysisForm
    {
        // ReSharper restore InconsistentNaming
        private Point _ptClick;
        private bool _updatePending;
        private bool _smooth;
        private AbstractChromatogramForm() : base(null)
        {
            
        }
        protected AbstractChromatogramForm(PeptideFileAnalysis peptideFileAnalysis) : base(peptideFileAnalysis)
        {
            InitializeComponent();
            MsGraphControl = new MSGraphControlEx
                                 {
                                     Dock = DockStyle.Fill
                                 };
            MsGraphControl.DoubleClickEvent += MsGraphControlOnDoubleClickEvent;
            MsGraphControl.MouseMoveEvent += MsGraphControlOnMouseMoveEvent;
            MsGraphControl.MouseDownEvent += MsGraphControlOnMouseDownEvent;
            MsGraphControl.MouseUpEvent += MsGraphControlOnMouseUpEvent;
            MsGraphControl.ContextMenuBuilder += MsGraphControlOnContextMenuBuilder;
            Smooth = Settings.Default.SmoothChromatograms;
        }
        protected MSGraphControl MsGraphControl { get; private set; }
        protected IList<double> Times { get; set; }

        void MsGraphControlOnContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            _ptClick = mousePt;
            menuStrip.Items.Insert(0, toolStripMenuItemShowSpectrum);
            menuStrip.Items.Insert(0, toolStripMenuItemSmooth);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateUi();
        }

        protected virtual bool MsGraphControlOnMouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            return false;
        }

        protected virtual bool MsGraphControlOnMouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            return false;
        }

        protected virtual bool MsGraphControlOnMouseUpEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            return false;
        }

        protected virtual bool MsGraphControlOnDoubleClickEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            return false;
        }

        protected virtual ICollection<int> GetSelectedCharges()
        {
            var result = new List<int>();
            for (int i = PeptideAnalysis.MinCharge; i <= PeptideAnalysis.MaxCharge; i++)
            {
                result.Add(i);
            }
            return result;
        }

        protected virtual ICollection<int> GetSelectedMasses()
        {
            var result = new List<int>();
            int massCount = PeptideAnalysis.GetMassCount();
            for (int i = 0; i < massCount; i++)
            {
                result.Add(i);
            }
            return result;
        }

        void DisplaySpectrum(int scanIndex)
        {
            if (!TopographForm.Instance.EnsureMsDataFile(PeptideFileAnalysis.MsDataFile, true))
            {
                return;
            }
            SpectrumForm spectrumForm;
            spectrumForm = new SpectrumForm(PeptideFileAnalysis.MsDataFile)
            {
                ScanIndex = scanIndex,
            };
            spectrumForm.SetPeptideAnalysis(PeptideAnalysis);
            spectrumForm.Show(TopLevelControl);
            double minMz = 2000;
            double maxMz = 0;
            var selectedCharges = GetSelectedCharges();
            var selectedMasses = GetSelectedMasses();
            for (int charge = PeptideAnalysis.MinCharge; charge <= PeptideAnalysis.MaxCharge; charge++)
            {
                if (selectedCharges.Count > 0 && !selectedCharges.Contains(charge))
                {
                    continue;
                }
                for (int iMass = 0; iMass < PeptideAnalysis.GetMassCount(); iMass++)
                {
                    if (selectedMasses.Count > 0 && !selectedMasses.Contains(iMass))
                    {
                        continue;
                    }
                    var mzRange = PeptideAnalysis.GetMzs()[charge][iMass];
                    minMz = Math.Min(minMz, mzRange.Min);
                    maxMz = Math.Max(maxMz, mzRange.Max);
                }
            }
            if (minMz <= maxMz)
            {
                spectrumForm.Zoom(minMz - 1, maxMz + 1);
            }
        }


        protected bool Smooth
        {
            get
            {
                return _smooth;
            }
            set
            {
                if (_smooth == value)
                {
                    return;
                }
                _smooth = value;
                toolStripMenuItemSmooth.Checked = _smooth;
                UpdateUi();
            }
        }

        protected void UpdateUi()
        {
            if (!IsHandleCreated)
            {
                return;
            }
            if (_updatePending)
            {
                return;
            }
            _updatePending = true;
            BeginInvoke(new Action(UpdateUiNow));
        }
        
        private void UpdateUiNow()
        {
            try
            {
                if (PeptideFileAnalysis == null)
                {
                    return;
                }
                if (PeptideFileAnalysis.GetChromatograms() == null)
                {
                    return;
                }
                PeptideFileAnalysis.PeptideAnalysis.EnsurePeaksCalculated();
                Times = PeptideFileAnalysis.ChromatogramSet.Times ?? new double[0];
                Recalc();
            }
            finally
            {
                _updatePending = false;
            }
        }

        protected override void WorkspaceOnChange(object sender, WorkspaceChangeArgs args)
        {
            base.WorkspaceOnChange(sender, args);
            if (args.HasSettingChange)
            {
                UpdateUi();
            }
        }

        protected override void EntityChanged()
        {
            base.EntityChanged();
            UpdateUi();
        }

        protected virtual void Recalc()
        {
            
        }

//        public static ChromatogramForm Show(DockableForm sibling, Workspace workspace, long peptideDataId)
//        {
//            ChromatogramForm form;
//            if (chromatogramForms.TryGetValue(peptideDataId, out form))
//            {
//                form.Activate();
//                return form;
//            }
//            ChromatogramGenerator chromatogramGenerator = new ChromatogramGenerator(workspace);
//            DbPeptideSearchResult peptideData;
//            DbMsDataFile msDataFile;
//            List<ChromatogramGenerator.Chromatogram> chromatograms;
//            using (ISession session = workspace.OpenSession())
//            {
//                peptideData = session.Get<DbPeptideSearchResult>(peptideDataId);
//                msDataFile = peptideData.MsDataFile;
//                msDataFile = session.Get<DbMsDataFile>(msDataFile.Id);
//                chromatograms = chromatogramGenerator.GetRequiredChromatograms(peptideData);
//            }
//
//            if (chromatograms.Count != 0)
//            {
//                if (!TurnoverForm.Instance.EnsureMsDataFile(msDataFile))
//                {
//                    return null;
//                }
//            }
//            chromatogramGenerator.GenerateChromatograms(peptideData.MsDataFile, chromatograms, i => true);
//            return Show(sibling, new PeptideAnalysis(workspace, peptideData));
//        }

        private void ToolStripMenuItemShowSpectrumOnClick(object sender, EventArgs e)
        {
            double x, y;
            MsGraphControl.GraphPane.ReverseTransform(_ptClick, out x, out y);
            int scanIndex = PeptideFileAnalysis.ChromatogramSet.ScanIndexFromTime(x);
            DisplaySpectrum(scanIndex);
        }
        protected void SetAutoFindPeak(bool autoFindPeak)
        {
            if (autoFindPeak == PeptideFileAnalysis.AutoFindPeak)
            {
                return;
            }
            if (autoFindPeak)
            {
                PeptideFileAnalysis.SetAutoFindPeak();
            }
            else
            {
                PeptideAnalysis.EnsurePeaksCalculated();
                var newPeaks = PeptideFileAnalysis.CalculatedPeaks.ChangeBasePeak(PeptideFileAnalysis.CalculatedPeaks.BasePeakKey);
                Debug.Assert(!newPeaks.AutoFindPeak);
                PeptideFileAnalysis.SetCalculatedPeaks(newPeaks);
            }

        }

        private void ToolStripMenuItemSmoothOnClick(object sender, EventArgs e)
        {
            Smooth = toolStripMenuItemSmooth.Checked;
        }
        protected PeptideFileAnalysis UpdateDataFileCombo(ComboBox comboBox)
        {
            var selectedItem = comboBox.SelectedItem as OverlayFileAnalysisItem;
            PeptideFileAnalysis selectedFileAnalysis = null;
            if (selectedItem != null)
            {
                selectedFileAnalysis = selectedItem.PeptideFileAnalysis;
            }
            comboBox.Items.Clear();
            comboBox.Items.Add("");
            PeptideFileAnalysis selectedPeptideFileAnalysis = null;
            foreach (var peptideFileAnalysis in PeptideAnalysis.FileAnalyses)
            {
                if (peptideFileAnalysis.Equals(PeptideFileAnalysis))
                {
                    continue;
                }
                comboBox.Items.Add(new OverlayFileAnalysisItem(peptideFileAnalysis));
                if (peptideFileAnalysis.Equals(selectedFileAnalysis))
                {
                    selectedPeptideFileAnalysis = peptideFileAnalysis;
                    comboBox.SelectedIndex = comboBox.Items.Count - 1;
                }
            }
            return selectedPeptideFileAnalysis;
        }

        protected class OverlayFileAnalysisItem
        {
            public OverlayFileAnalysisItem(PeptideFileAnalysis peptideFileAnalysis)
            {
                PeptideFileAnalysis = peptideFileAnalysis;
            }

            public PeptideFileAnalysis PeptideFileAnalysis { get; private set; }
            public override string ToString()
            {
                if (PeptideFileAnalysis.ValidationStatus == ValidationStatus.reject || 0 == PeptideFileAnalysis.PsmCount)
                {
                    return "(" + PeptideFileAnalysis.MsDataFile.Label + ")";
                }
                return PeptideFileAnalysis.MsDataFile.Label;
            }
        }
    }

    public class ChromatogramGraphItem : IMSGraphItemInfo
    {
        public string Title { get; set; }
        public Color Color { get; set; }
        public float LineWidth { get { return LineBase.Default.Width; } }

        public void CustomizeAxis(Axis axis)
        {
            axis.Title.FontSpec.Family = "Arial";
            axis.Title.FontSpec.Size = 14;
            axis.Color = axis.Title.FontSpec.FontColor = Color.Black;
            axis.Title.FontSpec.Border.IsVisible = false;
        }

        public PointAnnotation AnnotatePoint(PointPair point)
        {
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
            get { return MSGraphItemDrawMethod.line; }
        }

        public void CustomizeXAxis(Axis axis)
        {
            axis.Title.Text = "Time";
            CustomizeAxis(axis);
        }

        public void CustomizeYAxis(Axis axis)
        {
            axis.Title.Text = "Intensity";
            axis.Scale.MaxAuto = true;
            CustomizeAxis(axis);
        }

        public IPointList Points
        {
            get;
            set;
        }
    }
}
