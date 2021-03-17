using System;
using System.Deployment.Application;
using System.IO;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class CommandWriter
    {
        public static readonly string ALLOW_NEWLINE_SAVE_VERSION = "20.2.1.415"; // TODO(Ali): Make sure this matches future Skyline-daily release with --save fix
        private static readonly string IN_COMMAND = "--in=";
        private static readonly string OUT_COMMAND = "--out=";

        private readonly StreamWriter _writer;
        private readonly string _commandFile;
        private readonly Logger _logger;
        private bool _reopenFile; // If the Skyline file needs to be reopened with --in (true if _multiLine is false and a line has ended)

        public CommandWriter(Logger logger, SkylineSettings skylineSettings, string newSkyFileName)
        {
            _commandFile = Path.GetTempFileName();
            CurrentSkylineFile = string.Empty;
            _logger = logger;
            _writer = new StreamWriter(_commandFile);
            MultiLine = skylineSettings.HigherVersion(ALLOW_NEWLINE_SAVE_VERSION);
            if (!MultiLine)
            {
                _logger.Log(string.Empty);
                _logger.Log(string.Format(Resources.CommandWriter_Start_Notice__For_faster_Skyline_Batch_runs__use_Skyline_version__0__or_higher_, ALLOW_NEWLINE_SAVE_VERSION));
                _logger.Log(string.Empty);
            }
        }

        public string CurrentSkylineFile { get; private set; } // Filepath of last opened Skyline file with --in or --out
        public readonly bool MultiLine; // If the Skyline version does not support --save on a new line (true for versions before 20.2.1.415)

        public void Write(string command, params Object[] args)
        {
            command = string.Format(command, args);
            if (_reopenFile)
            {
                _reopenFile = false;
                if (!command.StartsWith(IN_COMMAND))
                    Write(SkylineBatchConfig.OPEN_SKYLINE_FILE_COMMAND, CurrentSkylineFile);
            }
            UpdateCurrentFile(command);
            _logger.Log(command);
            if (MultiLine) _writer.WriteLine(command);
            else _writer.Write(command + " ");
        }

        public void UpdateCurrentFile(string command)
        {
            var inFile = GetPathFromCommand(command, IN_COMMAND);
            var outFile = GetPathFromCommand(command, OUT_COMMAND);
            if (outFile != null)
                CurrentSkylineFile = outFile;
            else if (inFile != null)
                CurrentSkylineFile = inFile;
        }

        public string GetPathFromCommand(string commandString, string commandName)
        {
            if (!commandString.StartsWith(commandName)) return null;
            var singleQuote = "\"";
            var startIndex = commandString.IndexOf(singleQuote, StringComparison.Ordinal) + 1;
            var endIndex = commandString.LastIndexOf(singleQuote, StringComparison.Ordinal);
            var path = commandString.Substring(startIndex, endIndex - startIndex);
            if (path.Contains(singleQuote))
                throw new Exception("Could not parse incorrect command format");
            return path;
        }

        public void EndCommandGroup()
        {
            if (!MultiLine)
                ReopenSkylineResultsFile();
        }

        public string ReturnCommandFile()
        {
            _writer.Close();
            return _commandFile;
        }

        public void ReopenSkylineResultsFile()
        {
            if (!_reopenFile) _writer.WriteLine();
            _reopenFile = true;
        }
    }
}
