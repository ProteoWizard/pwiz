//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using DigitalRune.Windows.Docking;

namespace IDPicker.Forms
{
    public partial class LogForm : DockableForm
    {
        NotifyingStringWriter log;

        public TextWriter LogWriter { get { return log; } }

        public LogForm ()
        {
            InitializeComponent();

            HideOnClose = true;
            buffer = new StringBuilder(2000);

            log = new NotifyingStringWriter();
            log.Wrote += new EventHandler<NotifyingStringWriter.WroteEventArgs>(log_Wrote);
        }

        StringBuilder buffer;
        void log_Wrote (object sender, NotifyingStringWriter.WroteEventArgs e)
        {
            int size;
            lock (buffer)
            {
                buffer.Append(e.Text);
                size = buffer.Length;
            }

            if(size < 1000)
                return;

            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker) (() => log_Wrote(sender, e)));
                return;
            }

            lock (buffer)
            {
                textBox1.AppendText(buffer.ToString());
                buffer = new StringBuilder(2000);
            }
        }
    }

    /// <summary>
    /// A wrapper for StringWriter that sends an event when it is written to.
    /// </summary>
    public class NotifyingStringWriter : StringWriter
    {
        public class WroteEventArgs : EventArgs { public string Text { get; set; } }

        public event EventHandler<WroteEventArgs> Wrote;

        public override void Write (char value)
        {
            base.Write(value);
            OnWrote(value.ToString());
        }

        public override void Write (char[] buffer, int index, int count)
        {
            base.Write(buffer, index, count);
            OnWrote(new string(buffer, index, count));
        }

        public override void Write (string value)
        {
            base.Write(value);
            OnWrote(value);
        }

        protected void OnWrote (string text)
        {
            if (Wrote != null)
                Wrote(this, new WroteEventArgs() {Text = text});
        }
    }
}
