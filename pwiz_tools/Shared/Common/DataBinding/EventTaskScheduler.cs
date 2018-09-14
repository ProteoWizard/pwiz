/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Class which is able to run actions on the event thread on which it was created.
    /// </summary>
    public class EventTaskScheduler : IDisposable
    {
        private MarshalingControl _marshallingControl;
        public EventTaskScheduler()
        {
            _marshallingControl = new MarshalingControl();
        }
        public void Run(Action action)
        {
            lock (this)
            {
                if (_marshallingControl != null)
                {
                    _marshallingControl.BeginInvoke(action, null);
                }
            }
        }

        public void Dispose()
        {
            lock (this)
            {
                if (_marshallingControl != null)
                {
                    _marshallingControl.Dispose();
                    _marshallingControl = null;
                }
            }
        }

        /// <summary>
        /// Hidden window. This code was copied with small modifications from 
        /// System.Windows.Forms.Application.MarshalingControl
        /// </summary>
        internal sealed class MarshalingControl : Control
        {
            internal MarshalingControl()
            {
                Visible = false;
                SetTopLevel(true);
                CreateControl();
                CreateHandle();
            }

            protected override void OnLayout(LayoutEventArgs levent)
            {
            }

            protected override void OnSizeChanged(EventArgs e)
            {

                // don't do anything here -- small perf game of avoiding layout, etc.
            }
        }

    }
}
