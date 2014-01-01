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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class PeptideFileAnalysisFrame : PeptideFileAnalysisForm
    {
        private readonly DockPanel _dockPanel;
        private ChromatogramForm _chromatogramForm;
        private TracerChromatogramForm _tracerChromatogramForm;
        private PrecursorPoolForm _precursorPoolForm;
        private PeptideFileAnalysisFrame(PeptideFileAnalysis peptideFileAnalysis) : base(peptideFileAnalysis)
        {
            InitializeComponent();
            // TODO(nicksh): Move this into .designer file.
            _dockPanel = new DockPanel
                            {
                                Dock = DockStyle.Fill
                            };
            panel1.Controls.Add(_dockPanel);
            tbxDataFile.Text = peptideFileAnalysis.MsDataFile.Name;
            tbxSequence.Text = peptideFileAnalysis.PeptideAnalysis.Peptide.Sequence;
        }

        public static PeptideFileAnalysisFrame ActivatePeptideDataForm<T>(Form sibling, PeptideFileAnalysis peptideFileAnalysis) where T:PeptideFileAnalysisForm
        {
            PeptideFileAnalysisFrame peptideFileAnalysisFrame = null;
            foreach (var form in Application.OpenForms)
            {
                if (!(form is PeptideFileAnalysisFrame))
                {
                    continue;
                }
                
                if (((PeptideFileAnalysisFrame)form).PeptideFileAnalysis != peptideFileAnalysis)
                {
                    continue;
                }
                peptideFileAnalysisFrame = (PeptideFileAnalysisFrame)form;
                break;
            }
            if (peptideFileAnalysisFrame == null)
            {
                peptideFileAnalysisFrame = new PeptideFileAnalysisFrame(peptideFileAnalysis);
                if (sibling is DockableForm)
                {
                    DockableForm dockableSibling = (DockableForm)sibling;
                    if (dockableSibling.DockPanel != null)
                    {
                        peptideFileAnalysisFrame.Show(dockableSibling.DockPanel, dockableSibling.DockState);
                    }
                }
                else
                {
                    if (sibling != null)
                    {
                        peptideFileAnalysisFrame.Show(sibling.Parent);
                    }
                    else
                    {
                        peptideFileAnalysisFrame.Show(null);
                    }
                }
            }
            peptideFileAnalysisFrame.ShowForm<T>();
            return peptideFileAnalysisFrame;
        }
        public static PeptideFileAnalysisFrame ShowFileAnalysisForm<T>(PeptideFileAnalysis peptideFileAnalysis) where T : PeptideFileAnalysisForm
        {
            var analysisForm = PeptideAnalysisFrame.ShowPeptideAnalysis(peptideFileAnalysis.PeptideAnalysis);
            if (null == analysisForm)
            {
                return null;
            }
            return ActivatePeptideDataForm<T>(analysisForm.PeptideAnalysisSummary, peptideFileAnalysis);
        }

        public static PeptideFileAnalysisFrame ShowPeptideFileAnalysis(Workspace workspace, long? peptideFileAnalysisId)
        {
            if (peptideFileAnalysisId == null)
            {
                return null;
            }
            DbPeptideFileAnalysis dbPeptideFileAnalysis;
            using (var session = workspace.OpenSession())
            {
                dbPeptideFileAnalysis = session.Get<DbPeptideFileAnalysis>(peptideFileAnalysisId);
                if (dbPeptideFileAnalysis == null)
                {
                    return null;
                }
            }
            PeptideAnalysis peptideAnalysis;
            workspace.PeptideAnalyses.TryGetValue(dbPeptideFileAnalysis.PeptideAnalysis.Id.GetValueOrDefault(), out peptideAnalysis);
            if (peptideAnalysis == null)
            {
                return null;
            }
            PeptideFileAnalysis peptideFileAnalysis;
            if (!peptideAnalysis.FileAnalyses.TryGetValue(dbPeptideFileAnalysis.GetId(), out peptideFileAnalysis))
            {
                return null;
            }
            return ShowFileAnalysisForm<TracerChromatogramForm>(peptideFileAnalysis);
        }


        public T ShowForm<T>() where T : PeptideFileAnalysisForm
        {
            Activate();
            foreach (var form in _dockPanel.Contents)
            {
                T tForm = form as T;
                if (tForm == null)
                {
                    continue;
                }
                tForm.Activate();
                return tForm;
            }
            T newForm;
            try
            {
                var constructor = typeof (T).GetConstructor(new[] {typeof (PeptideFileAnalysis)});
                Debug.Assert(null != constructor);
                newForm = (T)constructor.Invoke(new object[] { PeptideFileAnalysis });
            }
            catch (TargetInvocationException targetInvocationException)
            {
                Console.Out.WriteLine(targetInvocationException.InnerException);
                return null;
            }

            newForm.Show(_dockPanel, DockState.Document);
            return newForm;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (Workspace == null)
            {
                return;
            }
            if (_tracerChromatogramForm == null)
            {
                _tracerChromatogramForm = new TracerChromatogramForm(PeptideFileAnalysis)
                {
                    CloseButton = false
                };
                _tracerChromatogramForm.Show(_dockPanel, DockState.Document);
            }
            if (_chromatogramForm == null)
            {
                _chromatogramForm = new ChromatogramForm(PeptideFileAnalysis)
                                        {
                                            CloseButton = false
                                        };
                _chromatogramForm.Show(_dockPanel, DockState.Document);
            }
            if (_precursorPoolForm == null)
            {
                _precursorPoolForm = new PrecursorPoolForm(PeptideFileAnalysis)
                                         {
                                             CloseButton = false,
                                         };
                _precursorPoolForm.Show(_dockPanel, DockState.Document);
            }
            UpdateForm();
        }

        private void UpdateForm()
        {
            Text = PeptideFileAnalysis.GetLabel();
            TabText = PeptideFileAnalysis.MsDataFile.Label;
        }

        protected override void WorkspaceOnChange(object sender, WorkspaceChangeArgs args)
        {
            base.WorkspaceOnChange(sender, args);
            UpdateForm();
        }
        /// <summary>
        /// The docking windows have a problem where resizing doesn't update heavily nested docking windows.
        /// In the resize event, we undock and re-dock the window, which causes some flicker, but eventually
        /// gets everything to resize correctly.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PeptideFileAnalysisFrameOnResize(object sender, EventArgs e)
        {
            if (!IsHandleCreated)
            {
                return;
            }
            try
            {
                var controls = Controls.Cast<Control>().Where(control => control.Dock == DockStyle.Fill).ToList();
                if (controls.Count > 0)
                {
                    BeginInvoke(new Action(() =>
                    {
                        foreach (var c in controls)
                        {
                            c.Dock = DockStyle.Fill;
                        }
                    }));
                    foreach (var c in controls)
                    {
                        c.Dock = DockStyle.None;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.LogException("PeptideFileAnalysisFrame", "Exception while resizing form", ex);
            }
        }
    }
}
