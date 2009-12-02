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
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using NHibernate;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class PeptideFileAnalysisFrame : PeptideFileAnalysisForm
    {
        private readonly DockPanel dockPanel;
        private AbstractChromatogramForm _chromatogramForm;
        private PrecursorEnrichmentsForm _precursorEnrichmentsForm;
        private TracerAmountsForm _tracerAmountsForm;
        private TracerChromatogramForm _tracerChromatogramForm;
        private PeptideFileAnalysisFrame(PeptideFileAnalysis peptideFileAnalysis) : base(peptideFileAnalysis)
        {
            InitializeComponent();
            dockPanel = new DockPanel
                            {
                                Dock = DockStyle.Fill
                            };
            panel1.Controls.Add(dockPanel);
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
            return ActivatePeptideDataForm<T>(analysisForm.PeptideAnalysisSummary, peptideFileAnalysis);
        }
        public T ShowForm<T>() where T : PeptideFileAnalysisForm
        {
            Activate();
            foreach (var form in dockPanel.Contents)
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
                newForm = (T)typeof(T).GetConstructor(new[] { typeof(PeptideFileAnalysis) }).Invoke(new object[] { PeptideFileAnalysis });
            }
            catch (TargetInvocationException targetInvocationException)
            {
                Console.Out.WriteLine(targetInvocationException.InnerException);
                return null;
            }

            newForm.Show(dockPanel, DockState.Document);
            return newForm;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (Workspace == null)
            {
                return;
            }
            if (_chromatogramForm == null)
            {
                _chromatogramForm = new ChromatogramForm(PeptideFileAnalysis)
                                        {
                                            CloseButton = false
                                        };
                _chromatogramForm.Show(dockPanel, DockState.Document);
            }
            if (_precursorEnrichmentsForm == null)
            {
                _precursorEnrichmentsForm = new PrecursorEnrichmentsForm(PeptideFileAnalysis)
                                                {
                                                    CloseButton = false

                                                };
                _precursorEnrichmentsForm.Show(dockPanel, DockState.Document);
            }
            if (_tracerAmountsForm == null)
            {
                _tracerAmountsForm = new TracerAmountsForm(PeptideFileAnalysis)
                                         {
                                             CloseButton = false
                                         };
                _tracerAmountsForm.Show(dockPanel, DockState.Document);
            }
            if (_tracerChromatogramForm == null)
            {
                _tracerChromatogramForm = new TracerChromatogramForm(PeptideFileAnalysis)
                                              {
                                                  CloseButton = false
                                              };
                _tracerChromatogramForm.Show(dockPanel, DockState.Document);
            }
            UpdateForm();
        }

        private void UpdateForm()
        {
            Text = PeptideFileAnalysis.GetLabel();
            TabText = PeptideFileAnalysis.MsDataFile.Label;
        }

        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            base.OnWorkspaceEntitiesChanged(args);
            if (args.Contains(PeptideFileAnalysis)
                || args.Contains(PeptideAnalysis)
                || args.Contains(PeptideFileAnalysis.MsDataFile))
            {
                UpdateForm();
            }
        }
    }
}
