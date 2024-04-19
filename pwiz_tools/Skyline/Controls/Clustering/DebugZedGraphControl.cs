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
using pwiz.Skyline.Util;
using System;
using System.Runtime.CompilerServices;
using ZedGraph;

namespace pwiz.Skyline.Controls.Clustering
{
    /// <summary>
    /// Subclass of ZedGraphControl which has extra code to try to track down
    /// "System.InvalidOperationException: Value Dispose() cannot be called while doing CreateHandle()"
    /// failure in "TestClusteredHeatMap" test.
    ///
    /// This class can be removed after that issue is fixed
    /// </summary>
    public class DebugZedGraphControl : ZedGraphControl
    {
        private bool _inCreateHandle;
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        protected override void CreateHandle()
        {
            Assume.IsFalse(_inCreateHandle);
            if (Program.FunctionalTest && Program.MainWindow != null && Program.MainWindow.InvokeRequired)
            {
                throw new ApplicationException(@"DebugZedGraphControl.CreateHandle called on wrong thread");
            }

            try
            {
                _inCreateHandle = true;
                base.CreateHandle();
            }
            catch (Exception e)
            {
                Console.Out.WriteLine(@"Exception in DebugZedGraphControl CreateHandle {0}", e);
                throw new Exception(@"Exception in DebugZedGraphControl", e);
            }
            finally
            {
                _inCreateHandle = false;
            }
        }
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        protected override void Dispose(bool disposing)
        {
            if (_inCreateHandle)
            {
                Console.Out.WriteLine(@"DebugZedGraphControl _inCreateHandle is {0}", _inCreateHandle);
            }
            base.Dispose(disposing);
        }
    }
}
