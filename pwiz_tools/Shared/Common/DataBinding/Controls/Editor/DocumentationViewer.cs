/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Controls.Editor
{
    public partial class DocumentationViewer : CommonFormEx
    {
        public DocumentationViewer(bool showInTaskBar)
        {
            InitializeComponent();

            // WINDOWS 10 UPDATE HACK: Because Windows 10 update version 1803 causes unparented non-ShowInTaskbar windows to leak GDI and User handles
            ShowInTaskbar = showInTaskBar;
        }

        public String DocumentationHtml
        {
            get { return webBrowser1.DocumentText; } 
            set { webBrowser1.DocumentText = value; }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            Dispose();
        }
    }
}
