/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Menus
{
    /// <summary>
    /// Base class for UserControls which hold items which are tightly coupled to the SkylineWindow class.
    /// </summary>
    public class SkylineControl : UserControl
    {
        private Container _components; // For IExtender use
        protected Helpers.ModeUIExtender modeUIHandler; // Allows UI mode management in Designer
        public SkylineControl()
        {
            InitializeComponent();
        }

        public SkylineControl(SkylineWindow skylineWindow) : this()
        {
            SkylineWindow = skylineWindow;
        }

        public Helpers.ModeUIExtender ModeUiHandler
        {
            get { return modeUIHandler; }
        }

        private void InitializeComponent()
        {
            _components = new Container();
            modeUIHandler = new Helpers.ModeUIExtender(_components);
        }

        public SkylineWindow SkylineWindow { get; private set; }
        public SrmDocument DocumentUI
        {
            get { return SkylineWindow?.DocumentUI; }
        }

        public SrmDocument Document
        {
            get { return SkylineWindow?.Document; }
        }

        public void ModifyDocument(string description, Func<SrmDocument, SrmDocument> act, Func<SrmDocumentPair, AuditLogEntry> logFunc)
        {
            SkylineWindow.ModifyDocument(description, null, act, null, null, logFunc);
        }

        public IdentityPath SelectedPath
        {
            get { return SkylineWindow?.SelectedPath; }
            set { SkylineWindow.SelectedPath = value; }
        }

        public SequenceTree SequenceTree
        {
            get { return SkylineWindow.SequenceTree; }
        }

        public TreeNodeMS SelectedNode
        {
            get { return SkylineWindow.SelectedNode; }
        }

        public int SelectedResultsIndex
        {
            get
            {
                return SkylineWindow.SelectedResultsIndex;
            }
            set
            {
                SkylineWindow.SelectedResultsIndex = value;
            }
        }

        public ChromFileInfoId GetSelectedChromFileId()
        {
            return SkylineWindow.GetSelectedChromFileId();
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing && _components != null)
            {
                _components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
