/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using DigitalRune.Windows.Docking;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Util
{
    public class DockableFormEx : DockableForm, IFormView, IClosingAware,
        Helpers.IModeUIAwareForm  // Can translate "peptide"=>"molecule" etc if desired
    {
        /// <summary>
        /// Flag indicating the form is closing or disposing. Set early (in OnFormClosing)
        /// to give background threads maximum warning before the handle is destroyed.
        /// Used by SafeBeginInvoke to avoid deadlock when BeginInvoke tries to recreate
        /// a handle on a closing form.
        /// </summary>
        private volatile bool _isClosingOrDisposing;

        /// <summary>
        /// Returns true if this form is in the process of closing or disposing.
        /// Background threads should check this before calling BeginInvoke.
        /// </summary>
        public bool IsClosingOrDisposing => _isClosingOrDisposing;

        /// <summary>
        /// Sealed to keep ReSharper happy, because we set it in constructors
        /// </summary>
        public sealed override string Text
        {
            get { return base.Text; }
            set { base.Text = value; }
        }

        private Helpers.ModeUIAwareFormHelper _modeUIHelper;
        public Helpers.ModeUIExtender ModeUIExtender; // Allows UI mode management in Designer
        private Container _components; // For IExtender use

        public DockableFormEx()
        {
            InitializeComponent(); // Required for Windows Form Designer support
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this._components = new System.ComponentModel.Container();
            this.ModeUIExtender = new Helpers.ModeUIExtender(_components);
            this._modeUIHelper = new Helpers.ModeUIAwareFormHelper(ModeUIExtender);
        }
        #endregion

        public Helpers.ModeUIAwareFormHelper GetModeUIHelper() // Method instead of property so it doesn't show up in Designer
        {
            return _modeUIHelper; 
        }

        public string ModeUIAwareStringFormat(string format, params object[] args)
        {
            return _modeUIHelper.Format(format, args);
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (Program.SkylineOffscreen && ParentForm != null)
                FormEx.SetOffscreen(ParentForm);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Potentially replace "peptide" with "molecule" etc in all controls on open, or possibly disable non-proteomic components etc
            GetModeUIHelper().OnLoad(this);

            if (Program.SkylineOffscreen && Parent == null)
                FormEx.SetOffscreen(this);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            // Set the flag after base call in case a handler cancelled the close.
            // This gives background threads early warning before handle destruction,
            // helping SafeBeginInvoke avoid deadlock.
            if (!e.Cancel)
                _isClosingOrDisposing = true;
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _isClosingOrDisposing = true;
            base.OnHandleDestroyed(e);
        }

        protected override void Dispose(bool disposing)
        {
            _isClosingOrDisposing = true;
            base.Dispose(disposing);
        }

        protected override bool ShowWithoutActivation
        {
            get { return Program.FunctionalTest || Program.SkylineOffscreen; }
        }
    }
}
