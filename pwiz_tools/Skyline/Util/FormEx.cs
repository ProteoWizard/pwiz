﻿/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
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
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Util
{
    public class FormEx : CommonFormEx,
                     Helpers.IModeUIAwareForm // Can translate "peptide"=>"molecule" etc if desired
    {
        private const int STATE_CREATINGHANDLE = 0x00040000;

        private Helpers.ModeUIAwareFormHelper _modeUIHelper;
        public Helpers.ModeUIExtender modeUIHandler; // Allows UI mode management in Designer
        private Container _components; // For IExtender use


        public FormEx()
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
            this.modeUIHandler = new Helpers.ModeUIExtender(_components);
            this._modeUIHelper = new Helpers.ModeUIAwareFormHelper(modeUIHandler);
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

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _modeUIHelper.Dispose();
            base.OnHandleDestroyed(e);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SrmDocument.DOCUMENT_TYPE ModeUI
        {
            get { return _modeUIHelper.ModeUI; }
        }

        protected void SetModeUIToolStripButtons(ToolStripDropDownButton toolStripDropDownButton, bool setButtonImage = false)
        {
            _modeUIHelper.SetModeUIToolStripButtons(toolStripDropDownButton, SetUIMode);
            // Choosing the active button may be delayed until later, e.g. if document will be opened
            if (setButtonImage)
                _modeUIHelper.UpdateButtonImageForModeUI();
        }

        public virtual void SetUIMode(SrmDocument.DOCUMENT_TYPE mode)
        {
            _modeUIHelper.ModeUI = mode == SrmDocument.DOCUMENT_TYPE.none ? SrmDocument.DOCUMENT_TYPE.proteomic : mode;
            _modeUIHelper.AttemptChangeModeUI(mode);
        }

        protected void EnsureUIModeSet()
        {
            // If user has never selected a default UI mode, ask for it now
            if (string.IsNullOrEmpty(Settings.Default.UIMode))
            {
                if (!string.IsNullOrEmpty(Program.DefaultUiMode))
                    Settings.Default.UIMode = Program.DefaultUiMode;
                else
                {
                    using (var noModeUIDlg = new NoModeUIDlg())
                    {
                        noModeUIDlg.ShowDialog(this);
                        SetUIMode(noModeUIDlg.SelectedDocumentType);
                    }
                }
            }
        }

        private bool IsCreatingHandle()
        {
            var GetState = GetType().GetMethod(@"GetState", BindingFlags.NonPublic | BindingFlags.Instance);
            Assume.IsNotNull(GetState);

            // ReSharper disable once PossibleNullReferenceException
            return (bool) GetState.Invoke(this, new object[] { STATE_CREATINGHANDLE });
        }

        /// <summary>
        /// Sealed to keep ReSharper happy, because we set it in constructors
        /// </summary>
        public sealed override string Text
        {
            get { return base.Text; }
            set { base.Text = value; }
        }

        public new void Show()
        {
            // If you really need this, then you have to use ShowParentless (not yet created), or windows may leak handles.
            throw new InvalidOperationException(@"Not supported.");
        }

        public new DialogResult ShowDialog()
        {
            // If you really need this, then you have to use ShowParentlessDialog, or windows may leak handles.
            throw new InvalidOperationException(@"Not supported.");
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // For unit testing, move window offscreen.
            if (Program.SkylineOffscreen)
                SetOffscreen(this);

            if (Program.FunctionalTest)
            {
                // Track undisposed forms.
            }

            // Potentially replace "peptide" with "molecule" etc in all controls on open, or possibly disable non-proteomic components etc
            GetModeUIHelper().OnLoad(this);

            if (ShowFormNames)
            {
                string textAppend = @"  (" + GetType().Name + @")";
                Text += textAppend;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return Program.FunctionalTest || Program.SkylineOffscreen; }
        }

        [Localizable(false)]
        protected override void Dispose(bool disposing)
        {
            if (Program.FunctionalTest && IsCreatingHandle())
            {
                // We might be in a stack unwind at this point, so we print out some information
                // and return so that we don't call base.Dispose and maybe get to find out what
                // the "current exception" is
                Program.Log?.Invoke(string.Format(
                    "\r\n[WARNING] Attempting to dispose form of type '{0}' during handle creation. StackTrace:\r\n{1}\r\n",
                    GetType(), Environment.StackTrace));

                var exceptionPtrs = ExceptionPointers.Current;
                if (exceptionPtrs == null)
                {
                    Program.Log?.Invoke("ExceptionPointers is null\r\n\r\n");
                }
                else
                {
                    Program.Log?.Invoke(string.Format("ExceptionPointers: {0}\r\nModule List:{1}\r\n\r\n",
                        exceptionPtrs, ExceptionPointers.GetModuleList()));
                }

                return;
            }

            try
            {
                base.Dispose(disposing);
            }
            catch (InvalidOperationException x)
            {
                var message = TextUtil.LineSeparate(
                    string.Format("Exception thrown attempting to dispose {0}", GetType()),
                    x.Message,
                    "Exception caught at: " + new StackTrace());
                throw new InvalidOperationException(message, x);
            }
        }

        public static Form GetParentForm(Control control)
        {
            for (; ; )
            {
                var parent = control.Parent;
                if (parent == null)
                    return FormUtil.FindTopLevelOpenForm();
                var parentForm = parent as Form;
                if (parentForm != null)
                    return parentForm;
                control = parent;
            }
        }

        public void ForceOnScreen()
        {
            ForceOnScreen(this);
        }

        public static void ForceOnScreen(Form form)
        {
            var location = form.Location;
            location.X = Math.Max(GetScreen(form.Left, form.Top).WorkingArea.Left,
                Math.Min(location.X, GetScreen(form.Right, form.Top).WorkingArea.Right - form.Size.Width));
            location.Y = Math.Max(GetScreen(form.Left, form.Top).WorkingArea.Top,
                Math.Min(location.Y, GetScreen(form.Left, form.Bottom).WorkingArea.Bottom - form.Size.Height));
            form.Location = location;
        }

        private static Screen GetScreen(int x, int y)
        {
            return Screen.FromPoint(new Point(x, y));
        }

        public virtual void CancelDialog()
        {
            CancelButton.PerformClick();
        }

        public static Control GetFocused(Control.ControlCollection controls)
        {
            foreach (Control c in controls)
            {
                if (c.Focused)
                {
                    // Return the focused control
                    return c;
                }
                if (c.ContainsFocus)
                {
                    // If the focus is contained inside a control's children
                    // return the child
                    return GetFocused(c.Controls);
                }
            }
            // No control on the form has focus
            return null;
        }
        public new static void SetOffscreen(Form form)
        {
            CommonFormEx.SetOffscreen(form);
        }

        public new static Point GetOffscreenPoint()
        {
            return CommonFormEx.GetOffscreenPoint();
        }
    }
}
