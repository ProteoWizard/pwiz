using System;
using System.Collections.Generic;
using System.IO;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class CommandWriter
    {
        private static readonly string IN_COMMAND = "--in=";
        private static readonly string OUT_COMMAND = "--out=";

        private string _commandHolder;
        private readonly StreamWriter _writer;
        private readonly string _commandFile;

        public CommandWriter(Logger logger, bool multiLine, bool invariantReport)
        {
            _commandFile = Path.GetTempFileName();
            _commandHolder = string.Empty;
            CurrentSkylineFile = string.Empty;
            _writer = new StreamWriter(_commandFile);

            MultiLine = multiLine;
            ExportsInvariantReport = invariantReport;

            if (!ExportsInvariantReport)
            {
                logger.Log(string.Empty);
                logger.Log(string.Format(Resources.CommandWriter_Start_Notice__For_faster_Skyline_Batch_runs__use_Skyline_version__0__or_higher_, ConfigRunner.REPORT_INVARIANT_VERSION));
                logger.Log(string.Empty);
            }
        }

        public string CurrentSkylineFile { get; private set; } // Filepath of last opened Skyline file with --in or --out
        public readonly bool MultiLine; // If the Skyline version does not support --save on a new line (true for versions before 20.2.1.415)
        public readonly bool ExportsInvariantReport; // If the Skyline version does not guarantee a comma separated invariant exported report

        public void Write(string command, params Object[] args)
        {
            command = string.Format(command, args);
            if (!string.IsNullOrEmpty(_commandHolder))
            {
                var reopenCommand = _commandHolder;
                _commandHolder = string.Empty;
                if (!command.StartsWith(IN_COMMAND))
                    Write(reopenCommand);
            }
            UpdateCurrentFile(command);
            _writer.Write(command + " ");
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

        public void NewLine()
        {
            _writer.WriteLine();
        }

        public void EndCommandGroup()
        {
            if (string.IsNullOrEmpty(_commandHolder))
            {
                NewLine();
                if (!MultiLine)
                    _commandHolder = string.Format(SkylineBatchConfig.OPEN_SKYLINE_FILE_COMMAND, CurrentSkylineFile);
            }
        }

        public string GetCommandFile()
        {
            _writer.Close();
            return _commandFile;
        }

    }
}
