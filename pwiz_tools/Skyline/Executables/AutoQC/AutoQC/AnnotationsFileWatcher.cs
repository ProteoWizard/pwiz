/*
 * Original author: Vagisha Sharma <vsharma .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

using System.IO;
using SharedBatch;

namespace AutoQC
{

    public class AnnotationsFileWatcher
    {
        private readonly Logger _logger;
        private readonly ConfigRunner _configRunner;

        private readonly FileSystemWatcher _fileWatcher;

        public AnnotationsFileWatcher(Logger logger, ConfigRunner configRunner)
        {
            _fileWatcher = InitFileSystemWatcher();

            _logger = logger;
            _configRunner = configRunner;
        }

        private FileSystemWatcher InitFileSystemWatcher()
        {
            var fileWatcher = new FileSystemWatcher();
            fileWatcher.Created += (s, e) => FileChanged();
            // fileWatcher.Renamed += (s, e) => FileRenamed(e);
            fileWatcher.Deleted += (s, e) => FileChanged();
            fileWatcher.Changed += (s, e) => FileChanged();
            fileWatcher.Error += (s, e) => OnFileWatcherError(e);
            return fileWatcher;
        }

        public void Init(AutoQcConfig config)
        {
            var mainSettings = config.MainSettings;

            _fileWatcher.EnableRaisingEvents = false;

            _fileWatcher.NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite;

            _fileWatcher.Filter = Path.GetFileName(mainSettings.AnnotationsFilePath);

            _fileWatcher.Path = Path.GetDirectoryName(mainSettings.AnnotationsFilePath);
        }

        public void StartWatching()
        {
            // Begin watching.
            _fileWatcher.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            _fileWatcher.EnableRaisingEvents = false;
        }

        private void FileChanged()
        {
            _configRunner.AnnotationsFileUpdated = true;
        }

        private void OnFileWatcherError(ErrorEventArgs e)
        {
            _logger.LogError(string.Format("There was an error watching the annotations file {0}.", _fileWatcher.Filter), e.GetException().ToString());
        }
    }
}