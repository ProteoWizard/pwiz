/*
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Windows.Forms;

namespace pwiz.Skyline.Controls.FilesTree
{
    /// <summary>
    /// Wrapper for .NET's <see cref="FileSystemWatcher"/> (FSW) that can be paused and restarted. FileSystemWatchers are
    /// finicky and subject to failure. They also stop working when the directory they're monitoring becomes
    /// unavailable - because a local directory was deleted, a network share becomes unavailable, or a thumb drive
    /// was removed.
    ///
    /// When an FSW raises an error for those (or other) reasons, the FSW instance is paused and an event is raised.
    /// Callers can subscribe to that event to handle a directory becoming unavailable and to resume watching
    /// a directory when it becomes available again.
    /// </summary>
    internal class ManagedFileSystemWatcher : IDisposable
    {
        internal ManagedFileSystemWatcher(string path, Control synchronizingObject)
        {
            Path = path;
            SynchronizingObject = synchronizingObject;

            CreateWatcher();
        }

        internal string Path { get; }

        private Control SynchronizingObject { get; }
        private FileSystemWatcher FileSystemWatcher { get; set; }
        internal bool IsPaused { get; private set; }

        internal event FileSystemEventHandler Created;
        internal event FileSystemEventHandler Deleted;
        internal event RenamedEventHandler Renamed;
        internal event ErrorEventHandler Error;

        internal void CreateWatcher()
        {
            FileSystemWatcher = new FileSystemWatcher();
            FileSystemWatcher.Path = Path;
            FileSystemWatcher.SynchronizingObject = SynchronizingObject;
            FileSystemWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;

            // FileSystemService does not recursively monitor subdirectories. Instead, FileSystemWatcher instances are
            // started for each directory containing files Skyline wants to monitor. This approach is easier to 
            // reason about and avoids the downsides of inadvertently monitoring a large tree of files - for example,
            // if a replicate sample file happens to be imported from C:\ or from the root of a slow network drive.
            FileSystemWatcher.IncludeSubdirectories = false;

            FileSystemWatcher.Created += (sender, eventArgs) => Created?.Invoke(this, eventArgs);
            FileSystemWatcher.Deleted += (sender, eventArgs) => Deleted?.Invoke(this, eventArgs);
            FileSystemWatcher.Renamed += (sender, eventArgs) => Renamed?.Invoke(this, eventArgs);
            FileSystemWatcher.Error += (sender, EventArgs) => Error?.Invoke(this, EventArgs);

            Error += ErrorHandler;
        }

        internal void Start()
        {
            if (!IsPaused)
            {
                CreateWatcher();
                FileSystemWatcher.EnableRaisingEvents = true;
            }
        }

        internal void Pause()
        {
            if (!IsPaused)
            {
                IsPaused = true;
                DisposeWatcher();
            }
        }

        internal void Resume()
        {
            if (IsPaused)
            {
                IsPaused = false;
                CreateWatcher();
                FileSystemWatcher.EnableRaisingEvents = true;
            }
        }

        internal void ErrorHandler(object sender, EventArgs eventArgs)
        {
            Pause();
        }

        internal void Stop()
        {
            if (FileSystemWatcher != null)
            {
                FileSystemWatcher.EnableRaisingEvents = false;
            }
        }

        public void Dispose()
        {
            DisposeWatcher();
        }

        private void DisposeWatcher()
        {
            if (FileSystemWatcher != null)
            {
                FileSystemWatcher.EnableRaisingEvents = false;
                FileSystemWatcher.Dispose();

                FileSystemWatcher = null;
            }
        }
    }
}