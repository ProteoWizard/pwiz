﻿/*
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
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;

namespace pwiz.Skyline.Menus
{
    /// <summary>
    /// Base class for UserControls which hold items which are tightly coupled to the SkylineWindow class.
    /// </summary>
    public class SkylineControl : UserControl
    {
        public SkylineControl()
        {
        }

        public SkylineControl(SkylineWindow skylineWindow)
        {
            SkylineWindow = skylineWindow;
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
    }
}
