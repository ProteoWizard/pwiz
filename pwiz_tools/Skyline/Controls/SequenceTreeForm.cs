/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    public partial class SequenceTreeForm : DockableFormEx
    {
        public SequenceTreeForm(IDocumentUIContainer documentContainer, bool restoringState)
        {
            InitializeComponent();
            _defaultTabText = TabText;
            sequenceTree.LockDefaultExpansion = restoringState;
            sequenceTree.InitializeTree(documentContainer);
            sequenceTree.LockDefaultExpansion = false;
            if (documentContainer.DocumentUI != null)
                UpdateResultsUI(documentContainer.DocumentUI.Settings, null);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            SequenceTree.HideEffects();
            base.OnClosing(e);
        }

        protected override string GetPersistentString()
        {
            return base.GetPersistentString() + "|" + SequenceTree.GetPersistentString(); // Not L10N
        } 

        public SequenceTree SequenceTree { get { return sequenceTree; } }
        public ToolStripComboBox ComboResults { get { return comboResults; } }

        public sealed class LockDoc : IDisposable
        {
            private readonly SequenceTreeForm _sequenceTreeForm;

            public LockDoc(SequenceTreeForm sequenceTreeForm)
            {
                _sequenceTreeForm = sequenceTreeForm;
                if (_sequenceTreeForm != null)
                    _sequenceTreeForm.BeginUpdateDoc();
            }

            public void Dispose()
            {
                if (_sequenceTreeForm != null && !_sequenceTreeForm.IsDisposed)
                    _sequenceTreeForm.EndUpdateDoc();
            }
        }

        private readonly string _defaultTabText;

        public void UpdateTitle()
        {
            // update the form's title bar to indicate display mode
            string newTitle = null;
            switch (SequenceTree.ProteinsDisplayMode)
            {
                case ProteinDisplayMode.ByName:
                    newTitle = _defaultTabText;
                    break;
                case ProteinDisplayMode.ByAccession:
                    newTitle = Resources.SequenceTreeForm_UpdateTitle_Targets_by_Accession;
                    break;
                case ProteinDisplayMode.ByPreferredName:
                    newTitle = Resources.SequenceTreeForm_UpdateTitle_Targets_by_Preferred_Name;
                    break;
                case ProteinDisplayMode.ByGene:
                    newTitle = Resources.SequenceTreeForm_UpdateTitle_Targets_by_Gene;
                    break;
            }
            TabText = newTitle ?? _defaultTabText;
        }

        private int _updateLockCountDoc;
        private SrmDocument _updateDocPrevious;

        public bool IsInUpdateDoc { get { return _updateLockCountDoc > 0; } }

        public void BeginUpdateDoc()
        {
            _updateLockCountDoc++;
            _updateDocPrevious = SequenceTree.DocumentContainer.Document;
            SequenceTree.BeginUpdateDoc();
        }

        public void EndUpdateDoc()
        {
            if (_updateLockCountDoc == 0)
                return;
            SequenceTree.EndUpdateDoc();
            if (--_updateLockCountDoc == 0 && !ReferenceEquals(_updateDocPrevious, SequenceTree.DocumentContainer.Document))
            {
                var settingsPrevious = _updateDocPrevious != null ? _updateDocPrevious.Settings : null;
                UpdateResultsUI(SequenceTree.DocumentContainer.Document.Settings, settingsPrevious);
                _updateDocPrevious = null;
            }
        }

        private void toolBarResults_Resize(object sender, EventArgs e)
        {
            EnsureResultsComboSize();
        }

        private void EnsureResultsComboSize()
        {
            comboResults.Width = toolBarResults.Width - labelResults.Width - 6;
            ComboHelper.AutoSizeDropDown(comboResults);
        }

        public void UpdateResultsUI(SrmSettings settingsNew, SrmSettings settingsOld)
        {
            if (_updateLockCountDoc > 0)
                return;

            var results = settingsNew.MeasuredResults;
            if (settingsOld == null || !ReferenceEquals(results, settingsOld.MeasuredResults))
            {
                if (results == null || results.Chromatograms.Count < 2)
                {
                    if (toolBarResults.Visible)
                        toolBarResults.Visible = false;

                    // Make sure the combo contains the results name, if one is
                    // available, because graph handling depends on this.
                    if (results != null && results.Chromatograms.Count > 0)
                    {
                        string resultsName = results.Chromatograms[0].Name;
                        if (ComboResults.Items.Count != 1 || !Equals(ComboResults.SelectedItem, resultsName))
                        {
                            ComboResults.Items.Clear();
                            ComboResults.Items.Add(resultsName);
                            ComboResults.SelectedIndex = 0;
                        }
                    }
                }
                else
                {
                    // Check to see if the list of files has changed.
                    var listNames = new List<string>();
                    foreach (var chromSet in results.Chromatograms)
                        listNames.Add(chromSet.Name);
                    var listExisting = new List<string>();
                    foreach (var item in ComboResults.Items)
                        listExisting.Add(item.ToString());
                    if (!ArrayUtil.EqualsDeep(listNames, listExisting))
                    {
                        // If it has, update the list, trying to maintain selection, if possible.
                        object selected = ComboResults.SelectedItem;
                        ComboResults.Items.Clear();
                        foreach (string name in listNames)
                            ComboResults.Items.Add(name);
                        if (selected == null || ComboResults.Items.IndexOf(selected) == -1)
                            ComboResults.SelectedIndex = 0;
                        else
                            ComboResults.SelectedItem = selected;
                        ComboHelper.AutoSizeDropDown(ComboResults);
                    }

                    // Show the toolbar after updating the files
                    if (!toolBarResults.Visible)
                    {
                        toolBarResults.Visible = true;
                        EnsureResultsComboSize();
                    }
                }
            }
        }
    }
}
