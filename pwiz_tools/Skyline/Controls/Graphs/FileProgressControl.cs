/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class FileProgressControl : UserControl
    {
        private int _number;
        private MsDataFileUri _filePath;
        private bool _selected;
        private Color _backColor;
        private int _errorCount;
        private readonly List<string> _errorLog = new List<string>();
        private readonly List<string> _errorExceptions = new List<string>();

        private readonly Color _okColor = Color.FromArgb(180, 230, 180);
        private readonly Color _failColor = Color.FromArgb(250, 170, 170);
        private readonly Color _cancelColor = Color.FromArgb(220, 220, 160);
        private readonly Color _baseColor = SystemColors.Control;

        public event EventHandler Cancel;
        public event EventHandler Retry;
        public event EventHandler ShowGraph;
        public event EventHandler ShowLog;

        public FileProgressControl()
        {
            InitializeComponent();
            labelPercent.Text = string.Empty;
            TabStop = false;

            foreach (Control control in Controls)
            {
                if (control != btnRetry)
                    control.MouseDown += ControlOnMouseDown;
            }

            _backColor = _baseColor;
        }

        private void ControlOnMouseDown(object sender, MouseEventArgs mouseEventArgs)
        {
            if (null != ControlMouseDown)
                ControlMouseDown(this, mouseEventArgs);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            ControlOnMouseDown(this, e);
        }

        public int Number
        {
            get { return _number; }
            set 
            { 
                _number = value;
                if (_filePath != null)
                    labelFileName.Text = string.Format(Resources.FileProgressControl_Number__0____1_, _number, Path.GetFileNameWithoutExtension(_filePath.GetFilePath()));
            }
        }

        public int Progress
        {
            get { return progressBar.Value; }
        }

        public MsDataFileUri FilePath
        {
            get { return _filePath; }
            set
            {
                _filePath = value;
                Number = _number;   // Regenerate label
            }
        }

        public void SetToolTip(ToolTip toolTip, string text)
        {
            toolTip.SetToolTip(labelFileName, text);
        }

        public bool IsCanceled { get; set; }

        public bool IsComplete
        {
            get { return Status != null && Status.IsComplete; }
        }

        public void Reset()
        {
            progressBar.Value = 0;
            Error = null;
            progressBar.Visible = false;
            labelPercent.Visible = false;
            labelStatus.Visible = false;
            btnRetry.Visible = false;
            _backColor = _baseColor;
        }

        public void SetStatus(ChromatogramLoadingStatus status)
        {
            Status = status;
            IsCanceled = false;
            try
            {
                if (status.IsError)
                {
                    if (Error == null)
                    {
                        Error = string.Format(Resources.FileProgressControl_SetStatus_, DateTime.Now.ToShortTimeString(),
                            ExceptionUtil.GetMessage(status.ErrorException));
                        _errorCount++;
                        if (_errorLog.Count == 3)
                        {
                            _errorLog.RemoveAt(2);
                            _errorExceptions.RemoveAt(2);
                        }
                        _errorLog.Insert(0, Error);
                        _errorExceptions.Insert(0, ExceptionUtil.GetStackTraceText(status.ErrorException, null, false));
                        btnRetry.Text = Resources.FileProgressControl_SetStatus_Retry;
                        btnRetry.Visible = true;
                        ShowWarningIcon(Resources.FileProgressControl_SetStatus_failed);
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(status.WarningMessage))
                    {
                        Warning = status.WarningMessage;
                        ShowWarningIcon(Resources.FileProgressControl_SetStatus_warning);
                    }
                    if (status.IsCanceled)
                    {
                        IsCanceled = true;
                        progressBar.Visible = false;
                        labelPercent.Visible = false;
                        labelStatus.Text = Resources.FileProgressControl_SetStatus_canceled;
                        labelStatus.Visible = true;
                        btnRetry.Text = Resources.FileProgressControl_SetStatus_Retry;
                        btnRetry.Visible = true;
                        _backColor = _cancelColor;
                    }
                    else if (status.IsComplete)
                    {
                        Finish();
                    }
                    else if (status.PercentComplete > 0)
                    {
                        progressBar.Visible = true;
                        labelPercent.Visible = true;
                        progressBar.Value = status.PercentComplete;
                        labelPercent.Text = (status.PercentComplete / 100.0).ToString(@"P0");
                        labelStatus.Visible = false;
                        btnRetry.Text = Resources.FileProgressControl_SetStatus_Cancel;
                        btnRetry.Visible = true;
                        _backColor = _okColor;
                    }
                }
                Invalidate();
            }
            catch
            {
                // ignored
            }
        }

        private void ShowWarningIcon(string status)
        {
            warningIcon.Visible = true;
            progressBar.Visible = false;
            labelPercent.Visible = false;
            labelStatus.Text = status;
            labelStatus.Visible = true;
            _backColor = _failColor;
        }

        public ChromatogramLoadingStatus Status { get; private set; }

        public void Finish()
        {
            if (!IsCanceled && Error == null)
            {
                progressBar.Value = 100;
                progressBar.Visible = false;
                labelPercent.Visible = false;
                labelStatus.Text = Resources.FileProgressControl_Finish_imported;
                labelStatus.Visible = true;
                btnRetry.Visible = false;
                _backColor = _okColor;
                if (_errorCount > 0)
                {
                    btnRetry.Text = Resources.FileProgressControl_btnRetry_Click_Log;
                    btnRetry.Visible = true;
                }
            }
        }

        public string Error { get; private set; }
        public string Warning { get; private set; }

        public string GetErrorLog(bool showExceptions)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Warning))
            {
                sb.AppendLine(Warning);
            }
            if (_errorCount > 1)
                sb.AppendFormat(Resources.FileProgressControl_GetErrorLog_, _errorCount);
            if (_errorCount > 3)
                sb.Append(Resources.FileProgressControl_GetErrorLog_2);
            for (int i = 0; i < _errorLog.Count; i++)
            {
                sb.AppendLine().Append(_errorLog[i]);
                if (showExceptions)
                    sb.Append(_errorExceptions[i]);
            }
            sb.AppendLine().AppendLine();
            if (_errorCount > 3)
                sb.AppendLine(@"...");
            return sb.ToString();
        }

        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var backColor = _backColor;
            if (Selected)
            {
                const double blend = 0.5;
                int white = (int) ((1 - blend)*255);
                backColor = Color.FromArgb(
                    (int)(_backColor.R * blend + white),
                    (int)(_backColor.G * blend + white),
                    (int)(_backColor.B * blend + white));
            }
            const double darkBlend = 0.4;
            labelStatus.ForeColor = Color.FromArgb(
                    (int)(_backColor.R * darkBlend),
                    (int)(_backColor.G * darkBlend),
                    (int)(_backColor.B * darkBlend));
            BackColor = backColor;
            base.OnPaint(e);
            CreateGraphics().DrawRectangle(Selected ? Pens.Blue : Pens.Silver, new Rectangle(0, 0, Bounds.Width-1, Bounds.Height-1));
        }

        public event EventHandler ControlMouseDown;

        private void btnRetry_Click(object sender, EventArgs e)
        {
            ButtonClick();
        }

        public void ButtonClick()
        {
            ControlOnMouseDown(this, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
            var buttonText = btnRetry.Text;
            if (buttonText == Resources.FileProgressControl_SetStatus_Retry)
            {
                if (Retry != null)
                    Retry(this, null);
            }
            else if (buttonText == Resources.FileProgressControl_SetStatus_Cancel)
            {
                if (Cancel != null)
                    Cancel(this, null);
            }
            else if (buttonText == Resources.FileProgressControl_btnRetry_Click_Graph)
            {
                if (ShowGraph != null)
                    ShowGraph(this, null);
                btnRetry.Text = Resources.FileProgressControl_btnRetry_Click_Log;
            }
            else if (buttonText == Resources.FileProgressControl_btnRetry_Click_Log)
            {
                if (ShowLog != null)
                    ShowLog(this, null);
                btnRetry.Text = Resources.FileProgressControl_btnRetry_Click_Graph;
            }
        }
    }
}
