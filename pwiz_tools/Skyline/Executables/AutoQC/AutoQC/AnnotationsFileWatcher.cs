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
            _fileWatcher.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            _fileWatcher.EnableRaisingEvents = false;
        }

        private void FileChanged()
        {
            _logger.Log("Annotations file was updated.");
            _configRunner.AnnotationsFileUpdated = true;
        }

        private void OnFileWatcherError(ErrorEventArgs e)
        {
            _logger.LogError(string.Format("There was an error watching the annotations file {0}.", _fileWatcher.Filter), e.GetException().ToString());
        }
    }
}