/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Threading;
using System.Windows.Forms;
#if NET472
using Microsoft.ConcurrencyVisualizer.Instrumentation;
#endif

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Helper class for use with Microsoft's Concurrency Visualizer (optional download for Visual Studio).
    /// On net8 the underlying Microsoft.ConcurrencyVisualizer.Markers assembly is not available, so
    /// the methods become no-ops — VS profiling instrumentation only matters on the legacy build path.
    /// </summary>
    public static class ConcurrencyVisualizer
    {
#if NET472
        private static readonly MarkerSeries _series = Markers.DefaultWriter.CreateMarkerSeries(@"events");
#endif
        private static Control _control;

        /// <summary>
        /// Add markers to annotate the threads graph with our thread names.
        /// </summary>
        public static void AddThreadName()
        {
            if (string.IsNullOrEmpty(Thread.CurrentThread.Name))
                return;
#if NET472
            MarkerSeries flagSeries = Markers.DefaultWriter.CreateMarkerSeries(Thread.CurrentThread.Name);
            flagSeries.WriteFlag(Thread.CurrentThread.Name);
#endif
        }

        /// <summary>
        /// We will put all events on the main thread so a developer can see them in one place in the threads view.
        /// </summary>
        public static void StartEvents(Control control)
        {
            _control = control;
        }

        /// <summary>
        /// Add an event annotation to the threads view.
        /// </summary>
        public static void CreateEvent(string name)
        {
#if NET472
            _control.Invoke(new Action(() => _series.WriteFlag(name)));
#else
            _ = name; // no-op on net8 — ConcurrencyVisualizer is VS-tooling-only
#endif
        }
    }
}
