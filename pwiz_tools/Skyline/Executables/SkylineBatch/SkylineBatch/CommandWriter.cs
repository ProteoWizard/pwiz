using System;
using System.IO;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class CommandWriter
    {
        public static readonly string ALLOW_NEWLINE_SAVE_VERSION = "20.2.1.415"; // TODO(Ali): Make sure this matches future Skyline-daily release with --save fix

        private readonly StreamWriter _writer;
        private readonly string _commandFile;
        private readonly bool _multiLine; // If the Skyline version does not support --save on a new line (true for versions before 20.2.1.415)
        private readonly string _newSkyFileName;
        private readonly Logger _logger;

        private bool _reopenFile; // If the Skyline file needs to be reopened with --in (true if _multiLine is false and a line has ended)

        public CommandWriter(Logger logger, SkylineSettings skylineSettings, string newSkyFileName)
        {
            _commandFile = Path.GetTempFileName();
            _newSkyFileName = newSkyFileName;
            _logger = logger;
            _writer = new StreamWriter(_commandFile);
            _multiLine = skylineSettings.HigherVersion(ALLOW_NEWLINE_SAVE_VERSION);
            if (!_multiLine)
            {
                _logger.Log(string.Empty);
                _logger.Log(string.Format(Resources.CommandWriter_Start_Notice__For_faster_Skyline_Batch_runs__use_Skyline_version__0__or_higher_, ALLOW_NEWLINE_SAVE_VERSION));
                _logger.Log(string.Empty);
            }
        }

        public void Write(string command, params Object[] args)
        {
            command = string.Format(command, args);
            if (_reopenFile)
            {
                _reopenFile = false;
                Write(SkylineBatchConfig.OPEN_SKYLINE_FILE_COMMAND, _newSkyFileName);
            }
            _logger.Log(command);
            if (_multiLine) _writer.WriteLine(command);
            else _writer.Write(command + " ");
        }

        public void EndCommandGroup()
        {
            if (!_multiLine)
                ReopenSkylineResultsFile();
        }

        public string ReturnCommandFile()
        {
            _writer.Close();
            return _commandFile;
        }

        public void ReopenSkylineResultsFile()
        {
            _writer.WriteLine();
            _reopenFile = true;
        }
    }
}
