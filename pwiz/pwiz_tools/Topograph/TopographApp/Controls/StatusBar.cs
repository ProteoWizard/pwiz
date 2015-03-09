/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Topograph.Model;
using pwiz.Topograph.ui.Forms;

namespace pwiz.Topograph.ui.Controls
{
    public partial class StatusBar : UserControl
    {
        private PerformanceCounter _availableMBytes;
        private double _totalMb;
        private double _peakMb;
        private double _currentMb;
        private int _totalChromatograms;
        private int _completedChromatograms;
        private int _totalResults;
        private int _possibleResults;
        private int _completedResults;
        public StatusBar()
        {
            InitializeComponent();
            if (DesignMode)
            {
                return;
            }
            _availableMBytes = new PerformanceCounter("Memory", "Available MBytes");
            components.Add(_availableMBytes);
            UpdateTimerTick(this, new EventArgs());
        }

        private void UpdateTimerTick(object sender, EventArgs e)
        {
            _currentMb = Process.GetCurrentProcess().WorkingSet64 / 1048576.0;
            _peakMb = Process.GetCurrentProcess().PeakWorkingSet64 / 1048576.0;
            _totalMb = _availableMBytes.NextValue() + _currentMb;
            if (!Environment.Is64BitProcess)
            {
                _totalMb = Math.Min(_totalMb, 2048);
            }
            lblMemory.Text = string.Format("{0}/{1} MB", Math.Round(_currentMb), Math.Round(_totalMb));
            panelMemory.Invalidate();
            Workspace workspace = null;
            var turnoverForm = ParentForm as TopographForm;
            if (null != turnoverForm)
            {
                workspace = turnoverForm.Workspace;
            }
            if (workspace != null)
            {
                _totalChromatograms = workspace.PeptideAnalyses.Select(peptideAnalysis => peptideAnalysis.FileAnalyses.Count).Sum();
                _completedChromatograms = workspace.PeptideAnalyses
                    .Select(peptideAnalysis => peptideAnalysis.FileAnalyses.Count(
                        fileAnalysis => fileAnalysis.ChromatogramSetId.HasValue))
                    .Sum();
                _totalResults = _totalChromatograms;
                _possibleResults = _completedChromatograms;
                _completedResults = workspace.PeptideAnalyses
                    .Select(peptideAnalysis => peptideAnalysis.FileAnalyses.Count(
                        fileAnalysis => fileAnalysis.PeakData.IsCalculated))
                    .Sum();
            }
            else
            {
                _totalChromatograms = _completedChromatograms = 0;
                _totalResults = _possibleResults = _completedResults = 0;
            }
            lblChromatograms.Text = string.Format("{0}/{1} Chromatograms", _completedChromatograms, _totalChromatograms);
            lblResults.Text = string.Format("{0}/{1} Results", _completedResults, _totalResults);
            panelChromatograms.Invalidate();
        }

        private void LblMemoryClick(object sender, EventArgs e)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        }

        private void PanelMemoryPaint(object sender, PaintEventArgs e)
        {
            FillRect(e.Graphics, panelMemory.Size, _currentMb/_totalMb, _peakMb/_totalMb);
        }

        private void PanelChromatogramsPaint(object sender, PaintEventArgs e)
        {
            FillRect(e.Graphics, panelChromatograms.Size, ((double) _completedChromatograms) / _totalChromatograms, 1.0);
        }

        private void PanelResultsPaint(object sender, PaintEventArgs e)
        {
            FillRect(e.Graphics, panelResults.Size, ((double)_completedResults) / _totalResults, ((double)_possibleResults)/_totalResults);
        }

        private void FillRect(Graphics graphics, Size size, double completedFraction, double availableFraction)
        {
            var rectF = new RectangleF(0, 0, size.Width, size.Height);
            using (var totalBrush = new SolidBrush(SystemColors.ControlLight))
            {
                graphics.FillRectangle(totalBrush, rectF);
            }
            if (!double.IsNaN(availableFraction) && !double.IsInfinity(availableFraction))
            {
                rectF.Width = (float)(size.Width * availableFraction);
                using (var currentBrush = new SolidBrush(SystemColors.Control))
                {
                    graphics.FillRectangle(currentBrush, rectF);
                }
            }
            if (!double.IsNaN(completedFraction) && !double.IsInfinity(completedFraction))
            {
                rectF.Width = (float) (size.Width*completedFraction);
                using (var completedBrush = new SolidBrush(SystemColors.ControlDark))
                {
                    graphics.FillRectangle(completedBrush, rectF);
                }
            }
        }
    }
}
