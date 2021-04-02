/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Threading;

namespace SortRESX
{
    /// <summary>
    /// This code was copied from:
    /// https://www.codeproject.com/Articles/37022/Solving-the-resx-Merge-Problem
    ///
    /// It was subsequently modified to use FileSaver and modify .resx files in place
    /// </summary>
    //
    // 0 command line parameters ==> input is from stdin and output is stdout.
    // 1 command line parameter  ==> input is a source .resx file (arg[0]) and output is the same file.
    // The program reads the source and writes a sorted version of it to the output.
    //
    class Program
    {
        static void Main(string[] args)
        {
            List<string> startingFolders = new List<string>();
            if (args.Length == 0)
            {
                startingFolders.Add(Directory.GetCurrentDirectory());
            }
            else
            {
                foreach (var arg in args)
                {
                    startingFolders.Add(Path.GetFullPath(arg));
                }
            }
            Console.Error.WriteLine("Sorting resx files in {0}", string.Join(",", startingFolders));
            var resxSorter = new ResxSorter();
            RecurseDirectories(resxSorter.ProcessFile, startingFolders.ToArray());
        }

        static void RecurseDirectories(Func<string, bool> fileAction, params string[] startingDirectories)
        {
            int startedCount = 0;
            int completedCount = 0;
            int directoryCount = 0;
            int changeCount = 0;
            var startingTime = DateTime.UtcNow;
            Stack<string> dirs = new Stack<string>(startingDirectories);
            while (dirs.Count > 0)
            {
                var directory = dirs.Pop();
                directoryCount++;
                foreach (var file in Directory.GetFiles(directory))
                {
                    startedCount++;
                    new Action(() =>
                    {
                        bool result = fileAction(file);
                        if (result)
                        {
                            changeCount++;
                            Console.Error.WriteLine("Modified file {0}", file);
                        }
                    }).BeginInvoke(ar =>
                    {
                        Interlocked.Increment(ref completedCount);
                    }, null);
                }
                foreach (var subdirectory in Directory.GetDirectories(directory))
                {
                    dirs.Push(subdirectory);
                }
            }
            while (completedCount < startedCount)
            {
                Thread.Sleep(100);
            }

            var endingTime = DateTime.UtcNow;
            var duration = endingTime.Subtract(startingTime);
            Console.Error.WriteLine("Modified {0} files in {1} directories in {2}", changeCount, directoryCount, duration);
        }
    }
}
