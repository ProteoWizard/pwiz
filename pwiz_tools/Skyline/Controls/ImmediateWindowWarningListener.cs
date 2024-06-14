/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.IO;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    public class ImmediateWindowWarningListener : IDisposable
    {
        private StringBuilder _stringBuilder;
        private TextWriter _synchronizedTextWriter;
        private Timer _timer;
        private TraceListener _listener;
        private bool _immediateWindowShown;
        public ImmediateWindowWarningListener(SkylineWindow skylineWindow)
        {
            _stringBuilder = new StringBuilder();
            _synchronizedTextWriter = TextWriter.Synchronized(new StringWriter(_stringBuilder));
            _listener = new TraceWarningListener(_synchronizedTextWriter);
            Trace.Listeners.Add(_listener);
            
            SkylineWindow = skylineWindow;
            _timer = new Timer
            {
                Interval = 100,
            };
            _timer.Start();
            _timer.Tick += TimerTick;
        }

        private void TimerTick(object sender, EventArgs e)
        {
            string text;
            lock (_synchronizedTextWriter)
            {
                text = _stringBuilder.ToString();
                _stringBuilder.Clear();
            }

            if (text.Length > 0)
            {
                if (!_immediateWindowShown || SkylineWindow.ImmediateWindow == null)
                {
                    _immediateWindowShown = true;
                    SkylineWindow.ShowImmediateWindow();
                }
                var immediateWindow = SkylineWindow.ImmediateWindow;
                if (immediateWindow == null)
                {
                    // Should not be possible
                    MessageDlg.Show(SkylineWindow, text, true);
                }
                else
                {
                    immediateWindow.Write(text);
                }
            }
        }

        public SkylineWindow SkylineWindow { get; }
        
        public void Dispose()
        {
            Trace.Listeners.Remove(_listener);
            _timer.Dispose();
        }
    }
}
