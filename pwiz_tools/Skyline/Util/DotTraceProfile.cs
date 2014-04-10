/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.IO;
using System.Reflection;
using System.Threading;
// ReSharper disable NonLocalizedString

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// DotTraceProfile turns the dotTrace performance profiler on and off for portions of
    /// code we wish to measure.  It does this using dynamic links to the JetBrains profiler,
    /// so the project won't break if the profiler isn't installed on a particular system.
    /// </summary>
    public class DotTraceProfile : IDisposable
    {
        private const string PROFILER_DLL = @"JetBrains\dotTrace\v5.3\Bin\JetBrains.Profiler.Core.Api.dll";
        private const string PROFILER_TYPE = "JetBrains.Profiler.Core.Api.PerformanceProfiler";

        private static readonly Type PROFILER;
        private static readonly Log LOG = new Log<DotTraceProfile>();
        private static bool _profilerStopped;
        
        // Load JetBrains profiler dynamically so we don't have to statically link with a DLL that may
        // not be installed on some machines.
        static DotTraceProfile()
        {
            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? "";
            var profilerDll = Path.Combine(programFiles, PROFILER_DLL);
            if (File.Exists(profilerDll))
            {
                var profilerAssembly = Assembly.LoadFrom(profilerDll);
                PROFILER = profilerAssembly.GetType(PROFILER_TYPE);
            }
        }

        /// <summary>
        /// Start scoped performance profiling.
        /// </summary>
        public DotTraceProfile()
        {
            Start();
        }

        /// <summary>
        /// Stop scoped performance profiling.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Returns true if the profiler is installed and active.
        /// </summary>
        public static bool IsProfiling
        {
            get
            {
                var isActive = (PROFILER != null) && (bool)PROFILER.GetMethod("get_IsActive").Invoke(null, null);
                return isActive;
            }
        }

        /// <summary>
        /// Start profiling.  Consider "using (new DotTraceProfile())" instead to designate
        /// a scope for profiled code.
        /// </summary>
        public static void Start()
        {
            if (IsProfiling && _profilerStopped)
            {
                _profilerStopped = false;
                LOG.Info("Start profiler");
                PROFILER.GetMethod("Start").Invoke(null, null);
            }
        }

        /// <summary>
        /// Stop profiling.  Consider "using (new DotTraceProfile())" instead to designate
        /// a scope for profiled code.
        /// </summary>
        /// <param name="suppressLog">Optional parameter to suppress log message</param>
        public static void Stop(bool suppressLog = false)
        {
            if (IsProfiling && !_profilerStopped)
            {
                _profilerStopped = true;
                if (!suppressLog)
                    LOG.Info("Stop profiler");
                PROFILER.GetMethod("Stop").Invoke(null, null);
            }
        }

        /// <summary>
        /// Save a profile snapshot.
        /// </summary>
        public static void Save()
        {
            if (IsProfiling)
            {
                LOG.Info("Save profile snapshot");
                PROFILER.GetMethod("EndSave").Invoke(null, null);
                Thread.Sleep(500);  // Sometimes the snapshot doesn't open if there isn't a short delay.
            }
        }
    }
}
