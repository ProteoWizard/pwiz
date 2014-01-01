/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public abstract class AbstractDiaExporter
    {
        public IsolationScheme IsolationScheme { get; private set; }
        public string ExportString { get; set; }
        public int CalculationTime { get; set; }
        public bool DebugCycles { get; set; }
        private readonly int? _maxInstrumentWindows;

        protected AbstractDiaExporter(IsolationScheme isolationScheme, int? maxInstrumentWindows)
        {
            IsolationScheme = isolationScheme;
            _maxInstrumentWindows = maxInstrumentWindows;
        }

        /// <summary>
        /// Initialize isolation scheme export.
        /// </summary>
        protected bool InitExport(string fileName, IProgressMonitor progressMonitor)
        {
            if (progressMonitor.IsCanceled)
                return false;

            // First export transition lists to map in memory
            Export(null, progressMonitor);

            // If filename is null, then no more work needs to be done.
            if (fileName == null)
            {
                progressMonitor.UpdateProgress(new ProgressStatus(string.Empty).Complete());
                return false;
            }

            return true;
        }

        /// <summary>
        /// Export to a isolation scheme to a file or to memory.
        /// </summary>
        /// <param name="fileName">A file on disk to export to, or null to export to memory.</param>
        /// <param name="progressMonitor">progress monitor</param>
        public void Export(string fileName, IProgressMonitor progressMonitor)
        {
            if (ExportString == null)
            {
                StringWriter writer = new StringWriter();
                if (HasHeaders)
                    WriteHeaders(writer);
                if (IsolationScheme.WindowsPerScan.HasValue)
                    WriteMultiplexedWindows(writer, IsolationScheme.WindowsPerScan.Value, progressMonitor);
                else
                    WriteWindows(writer);
                ExportString = writer.ToString();
            }

            if (fileName != null)
            {
                var saver = new FileSaver(fileName);
                if (!saver.CanSave())
                    throw new IOException(string.Format(Resources.AbstractDiaExporter_Export_Cannot_save_to__0__, fileName));

                var writer = new StreamWriter(saver.SafeName);
                writer.Write(ExportString);
                writer.Close();
                saver.Commit();
            }
        }

        private void WriteWindows(TextWriter writer)
        {
            foreach (var window in IsolationScheme.PrespecifiedIsolationWindows)
            {
                WriteIsolationWindow(writer, window);
            }
        }

        /// <summary>
        /// Generate an isolation list containing multiplexed windows, attempting to minimize the number
        /// and frequency of repeated window pairings within each scan.
        /// </summary>
        /// <param name="writer">writer to write results</param>
        /// <param name="windowsPerScan">how many windows are contained in each scan</param>
        /// <param name="progressMonitor">progress monitor</param>
        private void WriteMultiplexedWindows(TextWriter writer, int windowsPerScan, IProgressMonitor progressMonitor)
        {
            int maxInstrumentWindows = Assume.Value(_maxInstrumentWindows);
            int windowCount = IsolationScheme.PrespecifiedIsolationWindows.Count;
            int cycleCount = maxInstrumentWindows / windowCount;
            double totalScore = 0.0;

            // Prepare to generate the best isolation list possible within the given time limit.
            var startTime = DateTime.Now;
            var cycle = new Cycle(windowCount, windowsPerScan);
            int cyclesGenerated = 0;
            ProgressStatus status = new ProgressStatus(Resources.AbstractDiaExporter_WriteMultiplexedWindows_Exporting_Isolation_List);
            progressMonitor.UpdateProgress(status);

            // Generate each cycle.
            for (int cycleNumber = 1; cycleNumber <= cycleCount; cycleNumber++)
            {
                // Update status.
                if (progressMonitor.IsCanceled)
                    return;
                progressMonitor.UpdateProgress(status.ChangePercentComplete(
                    (int) (DateTime.Now - startTime).TotalSeconds*100/CalculationTime).ChangeMessage(
                        string.Format(Resources.AbstractDiaExporter_WriteMultiplexedWindows_Exporting_Isolation_List__0__cycles_out_of__1__,
                            cycleNumber - 1, cycleCount)));

                double secondsRemaining = CalculationTime - (DateTime.Now - startTime).TotalSeconds;
                double secondsPerCycle = secondsRemaining / (cycleCount - cycleNumber + 1);
                var endTime = DateTime.Now.AddSeconds(secondsPerCycle);

                Cycle bestCycle = null;
                do
                {
                    // Generate a bunch of cycles, looking for one with the lowest score.
                    const int attemptCount = 50;
                    for (int i = 0; i < attemptCount; i++)
                    {
                        cycle.Generate(cycleNumber);
                        if (bestCycle == null || bestCycle.CycleScore > cycle.CycleScore)
                        {
                            bestCycle = new Cycle(cycle);
                            if (bestCycle.CycleScore == 0.0)
                            {
                                cyclesGenerated += i + 1 - attemptCount;
                                endTime = DateTime.Now; // Break outer loop.
                                break;
                            }
                        }
                    }
                    cyclesGenerated += attemptCount;
                } while (DateTime.Now < endTime);

                // ReSharper disable PossibleNullReferenceException
                totalScore += bestCycle.CycleScore;
                WriteCycle(writer, bestCycle, cycleNumber);
                WriteCycleInfo(bestCycle, cycleNumber, cyclesGenerated, startTime);
                // ReSharper restore PossibleNullReferenceException

            }

            WriteTotalScore(totalScore);

            // Show 100% in the wait dialog.
            progressMonitor.UpdateProgress(status.ChangePercentComplete(100).ChangeMessage(
                string.Format(Resources.AbstractDiaExporter_WriteMultiplexedWindows_Exporting_Isolation_List__0__cycles_out_of__0__,
                              cycleCount)));
        }

// ReSharper disable LocalizableElement
        // For debugging...
        private void WriteTotalScore(double totalScore)
        {
            if (!DebugCycles)
                return;
            Console.WriteLine("Total score = {0:0.00}", totalScore); // Not L10N
        }

        // For debugging...
        private void WriteCycleInfo(Cycle cycle, int cycleNumber, int cyclesGenerated, DateTime startTime)
        {
            if (!DebugCycles)
                return;
            if (cycle.Repeats > 0)
                Console.WriteLine("Cycle {0}: score {1:0.00}, repeats {2}, minDistance {3}, at iteration {4}, {5:0.00} seconds", // Not L10N
                    cycleNumber, cycle.CycleScore, cycle.Repeats, cycle.MinDistance, cyclesGenerated, (DateTime.Now - startTime).TotalSeconds);
            else
                Console.WriteLine("Cycle {0}: score {1:0.00}, at iteration {2}, {3:0.00} seconds", // Not L10N
                    cycleNumber, cycle.CycleScore, cyclesGenerated, (DateTime.Now - startTime).TotalSeconds);
        }
// ReSharper restore LocalizableElement

        private void WriteCycle(TextWriter writer, Cycle cycle, int cycleNumber)
        {
            // Record window pairing in this cycle.
            cycle.Commit(cycleNumber);

            foreach (int window in cycle)
            {
                WriteIsolationWindow(writer, IsolationScheme.PrespecifiedIsolationWindows[window]);
            }
        }

        public virtual bool HasHeaders { get { return false; } }

        protected virtual void WriteHeaders(TextWriter writer) { /* No headers by default */ }

        protected virtual void WriteIsolationWindow(TextWriter writer, IsolationWindow isolationWindow)
        {
            double target = isolationWindow.Target ?? isolationWindow.MethodCenter;
            writer.WriteLine(SequenceMassCalc.PersistentMZ(target).ToString(CultureInfo.InvariantCulture));
        }

        // Cycle holds the scans containing all the windows to be sampled.
        private class Cycle
        {
            public double CycleScore { get; private set; }
            public int Repeats { get; private set; }
            public int MinDistance { get; private set; }

            private readonly int[] _ordering;
            private readonly int[] _windows;
            private readonly int[] _scans;
            private readonly int[] _nextWindowInScan;
            private readonly int _windowsPerScan;
            private readonly PairingHistory _pairingHistory;
            private readonly Random _random;

            public Cycle(int windowCount, int windowsPerScan)
            {
                CycleScore = double.MaxValue;
                _ordering = new int[windowCount];
                _windows = new int[windowCount];
                int scansPerCycle = windowCount / windowsPerScan;
                _scans = new int[scansPerCycle];
                _nextWindowInScan = new int[scansPerCycle];
                _windowsPerScan = windowsPerScan;
                _pairingHistory = new PairingHistory(windowCount);
                _random = new Random(0);    // Fixed seed for repeatable results.

                for (int i = 0; i < _ordering.Length; i++)
                {
                    _ordering[i] = i;
                }
            }

            public Cycle(Cycle other)
            {
                CycleScore = other.CycleScore;
                Repeats = other.Repeats;
                MinDistance = other.MinDistance;
                _windows = new int[other._windows.Length];
                other._windows.CopyTo(_windows, 0);
                _windowsPerScan = other._windowsPerScan;
                _pairingHistory = other._pairingHistory;    // Copy shares PairingHistory with other.
            }

            public void Generate(int cycleNumber)
            {
                // Initialize output information.
                CycleScore = 0.0;
                Repeats = 0;
                MinDistance = int.MaxValue;

                // Initialize scan indexing arrays.
                int openScans;
                for (openScans = 0; openScans < _scans.Length; openScans++)
                {
                    _scans[openScans] = _nextWindowInScan[openScans] = openScans * _windowsPerScan;
                }

                // Randomize order.
                for (int i = 0; i < _ordering.Length; i++)
                {
                    Helpers.Swap(ref _ordering[i], ref _ordering[_random.Next(_ordering.Length)]);
                }

                // Place each window in this cycle.
                foreach (int window in _ordering)
                {
                    int bestScan = -1;

                    // Look for an acceptable spot in each scan.
                    for (int i = 0; i < openScans && bestScan < 0; i++)
                    {
                        bestScan = i;
                        int nextWindow = _nextWindowInScan[i];

                        // Does the window create a repeated pair in this scan?
                        for (int j = _scans[i]; j < nextWindow; j++)
                        {
                            if (_pairingHistory.GetLastPairCycle(_windows[j], window) > 0)
                            {
                                bestScan = -1;  // No unpaired scan was found.
                                break;
                            }
                        }
                    }

                    if (bestScan < 0)
                    {
                        double minScore = double.MaxValue;
                        int bestRepeats = 0;
                        int bestDistance = 0;

                        // Find the scan which generates the lowest score with this window added.
                        for (int i = 0; i < openScans; i++)
                        {
                            double score = 0.0;
                            int repeats = 0;
                            int nextWindow = _nextWindowInScan[i];
                            int minDistance = int.MaxValue;

                            // Does the window create a repeated pair in this scan?
                            for (int j = _scans[i]; j < nextWindow; j++)
                            {
                                int lastPairCycle = _pairingHistory.GetLastPairCycle(_windows[j], window);
                                if (lastPairCycle > 0)
                                {
                                    // Compute score for this repeated pair.
                                    repeats++;
                                    int distance = cycleNumber - lastPairCycle;
                                    minDistance = Math.Min(distance, minDistance);
                                    score += 1.0 / (distance * distance);
                                }
                            }

                            // Remember the window placement with the best score.
                            if (minScore <= score)
                                continue;
                            minScore = score;
                            bestScan = i;
                            bestRepeats = repeats;
                            bestDistance = minDistance;
                        }

                        CycleScore += minScore;
                        Repeats += bestRepeats;
                        MinDistance = Math.Min(bestDistance, MinDistance);
                    }

                    // Add this window to the scan.
                    _windows[_nextWindowInScan[bestScan]++] = window;

                    // Compact scan list if this scan is full.
                    if (_nextWindowInScan[bestScan] == _scans[bestScan] + _windowsPerScan)
                    {
                        openScans--;
                        _nextWindowInScan[bestScan] = _nextWindowInScan[openScans];
                        _scans[bestScan] = _scans[openScans];
                    }
                }
            }

            public void Commit(int cycleNumber)
            {
                for (int i = 0; i < _windows.Length; i += _windowsPerScan)
                {
                    int nextScan = i + _windowsPerScan;
                    for (int j = i; j < nextScan; j++)
                    {
                        for (int k = j + 1; k < nextScan; k++)
                        {
                            _pairingHistory.RecordPair(_windows[j], _windows[k], cycleNumber);
                        }
                    }
                }
            }

            public System.Collections.IEnumerator GetEnumerator()
            {
                return _windows.GetEnumerator();
            }

            /// <summary>
            /// PairingHistory records which step each window pairing occurred in.  It can
            /// then compute a score used to select the best cycle among many to minimize
            /// repeated window pairings within each scan.
            /// </summary>
            private class PairingHistory
            {
                private readonly int[] _pairs; // Triangular array of pairs.
                private readonly int _windowCount;

                public PairingHistory(int windowCount)
                {
                    _windowCount = windowCount;

                    // Allocate triangular array.
                    _pairs = new int[(int)Math.Pow(_windowCount - 1, 2) - Sum(_windowCount - 2)];
                }

                public void RecordPair(int index1, int index2, int cycleNumber)
                {
                    _pairs[GetIndex(index1, index2)] = cycleNumber;
                }

                public int GetLastPairCycle(int index1, int index2)
                {
                    return _pairs[GetIndex(index1, index2)];
                }

                // Get index into triangular array.
                private int GetIndex(int window1, int window2)
                {
                    if (window1 > window2)
                        Helpers.Swap(ref window1, ref window2);
                    var index = window1 * (_windowCount - 1) + window2 - 1 - (window1 + 1) * window1 / 2;
                    return index;
                }

                // Return the sum of integers 1..x
                private static int Sum(int x)
                {
                    return (x + 1) * x / 2;
                }
            }
        }
    }
}
