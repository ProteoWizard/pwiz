/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Threading;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Tells anyone who is interested that the selection changed, by rewriting one small file. Rewriting it IS the
    /// message: there is no payload, and a listener that wants to know what is selected now asks the service.
    ///
    /// <para>Nobody registers, nobody is sent anything, and Skyline never learns who is listening - which is the
    /// point. A listener that dies, hangs, or never starts costs nothing, where the receiver list this replaces
    /// had Skyline calling each registered tool in turn and reaping the ones that timed out.</para>
    ///
    /// <para>Writes are coalesced: arrowing down the Targets tree rewrites the file once, shortly after the last
    /// change, rather than once per keystroke. That is what a listener wants anyway - where the selection ended up
    /// - and it keeps the writing off the thread the user is typing on.</para>
    /// </summary>
    public class SelectionChangeFile : IDisposable
    {
        // Long enough to swallow a burst of changes, short enough that a listener does not feel it.
        private const int WRITE_DELAY_MILLIS = 200;

        private readonly string _filePath;
        private readonly object _lock = new object();
        private Timer _writeTimer;
        private bool _disposed;

        public SelectionChangeFile(string filePath)
        {
            _filePath = filePath;
        }

        /// <summary>
        /// Says that the selection changed. Cheap enough to call from anywhere, as often as it happens: it only
        /// schedules the write, which then happens on its own thread.
        /// </summary>
        public void SelectionChanged()
        {
            lock (_lock)
            {
                // A timer already waiting is left alone rather than restarted, so a change every 10 ms still gets
                // written every WRITE_DELAY_MILLIS instead of being put off for as long as the changes keep coming.
                if (_disposed || _writeTimer != null)
                {
                    return;
                }
                _writeTimer = new Timer(WriteTimerTick, null, WRITE_DELAY_MILLIS, Timeout.Infinite);
            }
        }

        private void WriteTimerTick(object state)
        {
            lock (_lock)
            {
                _writeTimer?.Dispose();
                _writeTimer = null;
                if (_disposed)
                {
                    return;
                }
            }
            Write();
        }

        /// <summary>
        /// Rewrites the file, and does not care if it cannot: this is a courtesy to whoever may be listening, and
        /// nothing Skyline does depends on it. What is written is only the time of the change, for a person who
        /// opens the file to see what it is; a listener needs nothing out of it.
        /// </summary>
        private void Write()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? string.Empty);
                File.WriteAllText(_filePath, DateTime.UtcNow.ToString(@"o"));
            }
            catch (Exception)
            {
                // A listener that misses this one will be told by the next change.
            }
        }

        /// <summary>
        /// Stops writing and takes the file away, so nothing is left claiming that a Skyline which has exited has
        /// anything to say.
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                _writeTimer?.Dispose();
                _writeTimer = null;
            }
            Delete(_filePath);
        }

        public static void Delete(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception)
            {
                // Ignore cleanup errors
            }
        }
    }
}
