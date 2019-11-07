/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace pwiz.SkylineCmd
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            var argsList = args.ToList();
            AddBufferWidth(argsList);

            var preferredEncoding = GetPreferredEncoding(argsList);
            using (new EncodingManager(preferredEncoding))
            {
                // ReSharper disable once PossibleNullReferenceException
                return (int)GetMainFunction().Invoke(null, new object[] { argsList.ToArray() });
            }
        }

        private static void AddBufferWidth(List<string> argsList)
        {
            try
            {
                argsList.Insert(0, "--sw=" + (Console.BufferWidth - 1));
            }
            catch
            {
                // Rely on the default width. The command is being run in an invironment without a screen width
            }
        }

        private static Encoding GetPreferredEncoding(List<string> argsList)
        {
            // If forcing culture to be something other than the system settings, then also force UTF8 encoding
            return argsList.FirstOrDefault(arg => arg.StartsWith("--culture=")) != null ? Encoding.UTF8 : null;
        }

        private static MethodInfo GetMainFunction()
        {
            Assembly assembly;
            // SkylineCmd and Skyline must be in the same directory
            string dirPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            try
            {
                assembly = Assembly.LoadFrom(Path.Combine(dirPath, @"Skyline-daily.exe")); // Keep -daily
            }
            catch (Exception e1)
            {
                try
                {
                    assembly = Assembly.LoadFrom(Path.Combine(dirPath, @"Skyline.exe"));
                }
                catch (Exception e2)
                {
                    throw new AggregateException(e1, e2);
                }
            }
            var programClass = assembly.GetType(@"pwiz.Skyline.Program");
            var mainFunction = programClass.GetMethod(@"Main");
            return mainFunction;
        }
    }

    /// <summary>
    /// A class for managing setting the Console.OutputEncoding to UTF-8 and reverting
    /// it back to its original value. Otherwise, this a system change that presists throughout
    /// a console session, similar to the Windows OS function SetConsoleOutputCP().
    /// </summary>
    internal class EncodingManager : IDisposable
    {
        private Encoding _startEncoding;
        private readonly bool _logStatus;

        public EncodingManager(Encoding encodingOverride, bool logStatus = false)
        {
            if (encodingOverride == null && !logStatus)
                return;

            _logStatus = logStatus;
            try
            {
                _startEncoding = Console.OutputEncoding;
                Log(@"Start encoding: " + _startEncoding.EncodingName);
                if (_startEncoding.Equals(encodingOverride))
                    _startEncoding = null;
                else if (encodingOverride != null)
                {
                    Console.OutputEncoding = encodingOverride;
                    Log(@"Using encoding: " + Console.OutputEncoding.EncodingName);
                }
            }
            catch
            {
                // Keep going with the default encoding.
            }
        }

        public void Dispose()
        {
            if (_startEncoding != null)
            {
                try
                {
                    Log(@"Revert encoding: " + _startEncoding.EncodingName);
                    Console.OutputEncoding = _startEncoding;
                }
                catch
                {
                    // Ignore failure
                }

                _startEncoding = null;
            }
        }

        private void Log(string message)
        {
            if (_logStatus)
                Console.WriteLine(message);
        }
    }
}
