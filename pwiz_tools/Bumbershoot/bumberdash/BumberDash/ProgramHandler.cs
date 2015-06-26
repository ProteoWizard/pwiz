//
// $Id: ProgramHandler.cs 115 2013-08-13 13:54:51Z holmanjd $
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the Bumberdash project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari, Matt Chambers
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using BumberDash.Model;
using CustomProgressCell;

namespace BumberDash
{
    class ProgramHandler
    {
        #region Globals

        public delegate void PercentageDelegate(int value);
        public delegate void LogDelegate(string status);
        public delegate void StatusDelegate(string status, bool marqueeMode);
        public delegate void ExitDelegate(bool runNext, bool jobError);

        public PercentageDelegate PercentageUpdate;
        public LogDelegate LogUpdate;
        public LogDelegate ErrorForward;
        public StatusDelegate StatusUpdate;
        public ExitDelegate JobFinished;

        private bool _scanning; //Tells if ProgramHandler is active
        private bool _killed; //Indicates if a forced stop has been put into place
        private bool _barMode;
        private bool _versionCaught; //Tells if version number has been found and updated yet
        private int _currentRow; //Row in main form current job comes from
        private int _fileProcessing; //Total number of files in the current job
        private int _filesToProcess; //Files in current job that have been completed
        private double _minPercentage; //Highest completion percentage seen (thus minimum reportable)
        private string _destinationProgram = string.Empty; //Process (Name) ProgramHandler is currently working with
        private Thread _workThread;
        private Process _runningProgram;
        private List<string> _tempFiles;
        internal List<string> _completedFiles = new List<string>();
        private string _direcTagMask;
        private List<string> _moveFileList;
        private List<string> _msgfList;
        private HistoryItem _currentHI;

        #endregion

        /// <summary>
        /// Tells if ProgramHandler is active
        /// </summary>
        /// <returns></returns>
        internal bool JobIsRunning()
        {
            return _scanning;
        }

        /// <summary>
        /// Starts Bumbershoot utility based on current row and destination program
        /// </summary>
        private void ProcessJob(HistoryItem hi)
        {
            var argumentString = new StringBuilder();
            string configString;

            _killed = false;
            ProcessStartInfo psi;

            if (hi.Cpus > 0 && (_destinationProgram != "Comet" && _destinationProgram != "MSGF"))
                argumentString.Append(string.Format("-cpus {0} ", hi.Cpus));

            var workingDirectory = hi.OutputDirectory.TrimEnd('*');

            switch (_destinationProgram)
            {
                case "MyriMatch":
                    //Set  location of the program
                    psi = new ProcessStartInfo(String.Format(@"""{0}\lib\Bumbershoot\MyriMatch\myrimatch.exe""",
                                                             AppDomain.CurrentDomain.BaseDirectory));

                    //determine configuration
                    configString = hi.InitialConfigFile.FilePath == "--Custom--"
                                       ? PropertyListToOverrideString(hi.InitialConfigFile.PropertyList)
                                       : string.Format("-cfg \"{0}\" ", hi.InitialConfigFile.FilePath);

                    //continue to set up argument string
                    argumentString.Append(String.Format("{0}-ProteinDatabase \"{1}\"",
                                                        configString,
                                                        hi.ProteinDatabase));

                    //add files to scan to argument string
                    if (hi.FileList.Count == 1 && hi.FileList[0].FilePath.StartsWith("!"))
                    {
                        var fullMask = hi.FileList[0].FilePath.Trim('!');
                        var initialDir = Path.GetDirectoryName(fullMask);
                        var mask = Path.GetFileName(fullMask);
                        var maskedFiles = Directory.GetFiles(initialDir, mask);
                        argumentString.Append(String.Format(" \"{0}\"", fullMask));
                        _filesToProcess = maskedFiles.Length;
                    }
                    else
                        foreach (var file in hi.FileList)
                        {
                            argumentString.Append(String.Format(" {0}", file.FilePath));
                            _filesToProcess++;
                        }
                    break;
                case "Comet":
                    //Set  location of the program
                    psi = new ProcessStartInfo(String.Format(@"""{0}\lib\comet.exe""",
                                                             AppDomain.CurrentDomain.BaseDirectory));

                    //create temp params file
                    var cometConfig = new List<ConfigProperty>(hi.InitialConfigFile.PropertyList).Find(x => x.Name == "config");
                    if (cometConfig == null)
                    {
                        JobFinished(false, true);
                        return;
                    }
                    var tempCometPath = Path.GetTempFileName();
                    _tempFiles.Add(tempCometPath);
                    var tempCometParams = new StreamWriter(tempCometPath);
                    tempCometParams.Write(cometConfig.Value);
                    tempCometParams.Flush();
                    tempCometParams.Close();

                    //make sure fasta has no spaces in name
                    var cometDB = hi.ProteinDatabase;
                    if (cometDB.Contains(" "))
                    {
                        cometDB = Path.GetTempFileName();
                        File.Copy(hi.ProteinDatabase, cometDB,true);
                        _tempFiles.Add(cometDB);
                    }

                    argumentString.Append(" -P\"" + tempCometPath + "\"");
                    argumentString.Append(" -D\"" + cometDB + "\"");

                    //add files to scan to argument string
                    _moveFileList = new List<string>();
                    if (hi.FileList.Count == 1 && hi.FileList[0].FilePath.StartsWith("!"))
                    {
                        var fullMask = hi.FileList[0].FilePath.Trim('!');
                        var initialDir = Path.GetDirectoryName(fullMask);
                        var mask = Path.GetFileName(fullMask);
                        var maskedFiles = Directory.GetFiles(initialDir, mask);
                        argumentString.Append(String.Format(" \"{0}\"", fullMask));
                        _filesToProcess = maskedFiles.Length;
                        foreach (var file in maskedFiles)
                        {
                            var noExtension = Path.Combine(initialDir,
                                                           Path.GetFileNameWithoutExtension(file) ?? string.Empty);
                            var cometParams = CometHandler.FileContentsToCometParams(cometConfig.Value);
                            _moveFileList.Add(noExtension + cometParams.OutputSuffix + ".pep.xml");
                        }
                    }
                    else
                    {
                        foreach (var file in hi.FileList)
                        {
                            var initialDir = Path.GetDirectoryName(hi.FileList[0].FilePath.Trim('"').Trim('!'));
                            var noExtension = Path.Combine(initialDir ?? string.Empty,
                                                           Path.GetFileNameWithoutExtension(file.FilePath.Trim('"')) ?? string.Empty);
                            var cometParams = CometHandler.FileContentsToCometParams(cometConfig.Value);
                            _moveFileList.Add(noExtension + cometParams.OutputSuffix + ".pep.xml");
                            argumentString.Append(String.Format(" {0}", file.FilePath));
                            _filesToProcess++;
                        }
                    }
                    break;
                case "MSGF":
                    //find java path
                    var javaPath = string.Empty;
                    string environmentPath = Environment.GetEnvironmentVariable("JAVA_HOME");
                    if (!string.IsNullOrEmpty(environmentPath))
                        javaPath = environmentPath;
                    else
                    {
                        var javaKey = "SOFTWARE\\JavaSoft\\Java Runtime Environment\\";
                        var rk = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(javaKey);
                        if (rk != null)
                        {
                            string currentVersion = rk.GetValue("CurrentVersion").ToString();
                            using (Microsoft.Win32.RegistryKey key = rk.OpenSubKey(currentVersion))
                                javaPath = key.GetValue("JavaHome").ToString();
                        }
                        else
                        {
                            javaKey = "SOFTWARE\\JavaSoft\\Java Development Kit\\";
                            rk = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(javaKey);
                            if (rk != null)
                            {
                                string currentVersion = rk.GetValue("CurrentVersion").ToString();
                                using (Microsoft.Win32.RegistryKey key = rk.OpenSubKey(currentVersion))
                                    javaPath = key.GetValue("JavaHome").ToString();
                            }
                        }
                        
                    }
                    if (javaPath == string.Empty)
                        throw new Exception("Could not locate Java path");

                    //Set  location of the program
                    psi = new ProcessStartInfo(Path.Combine(javaPath, "bin\\java.exe"));

                    if (_msgfList == null || _msgfList.Count == 0)
                    {
                        //create temp mods file
                        var msgfOverload = new List<ConfigProperty>(hi.InitialConfigFile.PropertyList).Find(x => x.Name == "config");
                        var msgfMods = new List<ConfigProperty>(hi.InitialConfigFile.PropertyList).Find(x => x.Name == "mods");
                        if (msgfOverload == null || msgfMods == null)
                        {
                            JobFinished(false, true);
                            return;
                        }
                        var tempMSGFPath = Path.GetTempFileName();
                        _tempFiles.Add(tempMSGFPath);
                        var tempMSGFMods = new StreamWriter(tempMSGFPath);
                        tempMSGFMods.Write(msgfMods.Value);
                        tempMSGFMods.Flush();
                        tempMSGFMods.Close();

                        //set up file list
                        var fileList = new List<string>();
                        _moveFileList = new List<string>();
                        if (hi.FileList.Count == 1 && hi.FileList[0].FilePath.StartsWith("!"))
                        {
                            var fullMask = hi.FileList[0].FilePath.Trim('!');
                            var initialDir = Path.GetDirectoryName(fullMask);
                            var mask = Path.GetFileName(fullMask);
                            var maskedFiles = Directory.GetFiles(initialDir, mask);
                            fileList.AddRange(maskedFiles.Select(file => "\"" + file + "\""));
                            foreach (var file in maskedFiles)
                            {
                                var noExtension = Path.Combine(initialDir,
                                                               Path.GetFileNameWithoutExtension(file) ?? string.Empty);
                                var msgfParams = MSGFHandler.OverloadToMSGFParams(msgfOverload.Value);
                                _moveFileList.Add(noExtension + msgfParams.OutputSuffix + ".mzid");
                            }
                        }
                        else
                        {
                            fileList.AddRange(hi.FileList.Select(file => file.FilePath));
                            foreach (var file in hi.FileList)
                            {
                                var initialDir = Path.GetDirectoryName(hi.FileList[0].FilePath.Trim('"').Trim('!'));
                                var noExtension = Path.Combine(initialDir,
                                                               Path.GetFileNameWithoutExtension(file.FilePath.Trim('"')) ?? string.Empty);
                                var msgfParams = MSGFHandler.OverloadToMSGFParams(msgfOverload.Value);
                                _moveFileList.Add(noExtension + msgfParams.OutputSuffix + ".mzid");
                            }
                        }
                        _filesToProcess = fileList.Count;

                        //create overload List
                        _msgfList = new List<string>();
                        foreach (var file in fileList)
                        {
                            var arg = new StringBuilder();
                            arg.AppendFormat(@"-d64 -Xmx4000M -jar ""{0}\lib\MSGFPlus.jar""", AppDomain.CurrentDomain.BaseDirectory);
                            arg.Append(" -d \"" + hi.ProteinDatabase + "\"");
                            arg.Append(" -mod \"" + tempMSGFPath + "\"");
                            var noExtension = Path.Combine(Path.GetDirectoryName(file.Trim('"')) ?? string.Empty,
                                                           Path.GetFileNameWithoutExtension(file.Trim('"')) ?? string.Empty);
                            arg.Append(" " + msgfOverload.Value.Trim()
                                                         .Replace("[FileNameOnly]", noExtension)
                                                         .Replace("[FullFileName]", file.Trim('"')));
                            _msgfList.Add(arg.ToString());
                        }
                    }
                    if (_fileProcessing >= 0 || _fileProcessing < _msgfList.Count)
                        argumentString.Append(_msgfList[_fileProcessing]);
                    else
                        JobFinished(false, true);
                    _fileProcessing++;

                    break;
                case "DirecTag":
                    //Set  location of the program
                    psi = new ProcessStartInfo(String.Format(@"""{0}\lib\Bumbershoot\DirecTag\directag.exe""",
                                                             AppDomain.CurrentDomain.BaseDirectory));

                    //determine configuration
                    configString = hi.InitialConfigFile.FilePath == "--Custom--"
                                       ? PropertyListToOverrideString(hi.InitialConfigFile.PropertyList)
                                       : string.Format("-cfg \"{0}\" ", hi.InitialConfigFile.FilePath);

                    //HACK: Remove when deisotoping is usable
                        configString += string.Format("-{0} {1} ", "DeisotopingMode", "0");

                    //continue to set up argument string
                    argumentString.Append(configString.Trim());

                    //add files to scan to argument string
                    _direcTagMask = null;
                    if (hi.FileList.Count == 1 && hi.FileList[0].FilePath.StartsWith("!"))
                    {
                        var fullMask = hi.FileList[0].FilePath.Trim('!');
                        _direcTagMask = fullMask;
                        var initialDir = Path.GetDirectoryName(fullMask);
                        var mask = Path.GetFileName(fullMask);
                        var maskedFiles = Directory.GetFiles(initialDir, mask);
                        argumentString.Append(String.Format(" \"{0}\"", fullMask));
                        _filesToProcess = maskedFiles.Length;
                    }
                    else
                        foreach (var file in hi.FileList)
                        {
                            argumentString.Append(String.Format(" {0}", file.FilePath));
                            _filesToProcess++;
                        }
                    break;
                case "TagRecon":
                    //Set  location of the program
                    psi = new ProcessStartInfo(String.Format(@"""{0}\lib\Bumbershoot\TagRecon\tagrecon.exe""",
                                                             AppDomain.CurrentDomain.BaseDirectory));

                    //determine configuration
                    if (hi.TagConfigFile.FilePath == "--Custom--")
                    {
                        configString = PropertyListToOverrideString(hi.TagConfigFile.PropertyList);
                        //use intranal blosum and unimod files if not specified
                        if (!configString.Contains("-Blosum "))
                            configString += string.Format("-{0} \"{1}\" ", "Blosum",
                                                          Path.Combine(
                                                              AppDomain.CurrentDomain.BaseDirectory,
                                                              @"lib\Bumbershoot\TagRecon\blosum62.fas"));
                        if (!configString.Contains("UnimodXML"))
                            configString += string.Format("-{0} \"{1}\" ", "UnimodXML",
                                                          Path.Combine(
                                                              AppDomain.CurrentDomain.BaseDirectory,
                                                              @"lib\Bumbershoot\TagRecon\unimod.xml"));

                    }
                    else
                    {
                        configString = string.Format("-cfg \"{0}\" ", hi.TagConfigFile.FilePath);
                        var configCheck = new StreamReader(hi.TagConfigFile.FilePath);
                        var entireFile = configCheck.ReadToEnd();
                        configCheck.Close();

                        if (!entireFile.Contains("Blosum ="))
                            configString += string.Format("-{0} \"{1}\" ", "Blosum",
                                                          Path.Combine(
                                                              AppDomain.CurrentDomain.BaseDirectory,
                                                              @"lib\Bumbershoot\TagRecon\blosum62.fas"));
                        if (!entireFile.Contains("UnimodXML ="))
                            configString += string.Format("-{0} \"{1}\" ", "UnimodXML",
                                                          Path.Combine(
                                                              AppDomain.CurrentDomain.BaseDirectory,
                                                              @"lib\Bumbershoot\TagRecon\unimod.xml"));
                    }

                    //continue to set up argument string
                    argumentString.Append(String.Format("{0}-ProteinDatabase \"{1}\"",
                                                        configString,
                                                        hi.ProteinDatabase));

                    //add files to scan to argument string
                    foreach (var file in _completedFiles)
                    {
                        argumentString.AppendFormat(" \"{0}\"", Path.Combine(hi.OutputDirectory.TrimEnd('*'), file));
                        _filesToProcess++;
                    }
                    
                    break;
                case "Pepitome":
                    //Set  location of the program
                    psi = new ProcessStartInfo(String.Format(@"""{0}\lib\Bumbershoot\Pepitome\pepitome.exe""",
                                                             AppDomain.CurrentDomain.BaseDirectory));

                    //determine configuration
                    configString = hi.InitialConfigFile.FilePath == "--Custom--"
                                       ? PropertyListToOverrideString(hi.InitialConfigFile.PropertyList)
                                       : string.Format("-cfg \"{0}\" ", hi.InitialConfigFile.FilePath);

                    //continue to set up argument string
                    argumentString.Append(String.Format("{0}-ProteinDatabase \"{1}\" -SpectralLibrary \"{2}\"",
                                                        configString, hi.ProteinDatabase, hi.SpectralLibrary));

                    //add files to scan to argument string
                    if (hi.FileList.Count == 1 && hi.FileList[0].FilePath.StartsWith("!"))
                    {
                        var fullMask = hi.FileList[0].FilePath.Trim('!');
                        var initialDir = Path.GetDirectoryName(fullMask);
                        var mask = Path.GetFileName(fullMask);
                        var maskedFiles = Directory.GetFiles(initialDir, mask);
                        argumentString.Append(String.Format(" \"{0}\"", fullMask));
                        _filesToProcess = maskedFiles.Length;
                    }
                    else
                        foreach (var file in hi.FileList)
                        {
                            argumentString.Append(String.Format(" {0}", file.FilePath));
                            _filesToProcess++;
                        }
                    break;
                default:
                    //should never be called, throw error if it is
                    throw new Exception(String.Format("Destination Program not set to known value: {0}",
                                                      _destinationProgram));
            }

            psi.WorkingDirectory = workingDirectory;
            psi.Arguments = argumentString.ToString();
            var commandGiven = (string.Format("Command given:{0}{1}>{2} {3}{0}{0}", Environment.NewLine, psi.WorkingDirectory, psi.FileName, psi.Arguments));
            SendToLog(commandGiven);


            //Make sure window stays hidden
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            _runningProgram = new Process
                                  {
                                      StartInfo = psi,
                                      EnableRaisingEvents = true
                                  };

            _runningProgram.Start();
            _runningProgram.PriorityClass = ProcessPriorityClass.BelowNormal;
            _runningProgram.BeginOutputReadLine();
            _runningProgram.OutputDataReceived += DataReceived;
            _runningProgram.BeginErrorReadLine();
            _runningProgram.ErrorDataReceived += ErrorCaught;
            _runningProgram.Exited += (x, y) =>
            {
                var bgWait = new BackgroundWorker();
                bgWait.DoWork += (zzz, e) =>
                {
                    SendToLog(" ---Program exit detected---");
                    Thread.Sleep(100);
                    SendToLog(" ---Calculating results---");
                    var a = ((object[])e.Argument)[0];
                    var b = (EventArgs)((object[])e.Argument)[1];
                    ProgramExited(a, b);
                };
                bgWait.RunWorkerAsync(new object[] { x, y });
                //ProgramExited(x,y);
            };
            
        }

        private void ProgramExited(object sender, EventArgs e)
        {
            if (_runningProgram == null)
                return;
            //specail steps need to be taken to get MSGF output suffix to work properly
            if (_destinationProgram == "MSGF" && _currentHI != null && _fileProcessing < _msgfList.Count)
            {
                ProcessJob(_currentHI);
                return;
            }
            _msgfList = null;
            if ((_destinationProgram == "Comet" || _destinationProgram == "MSGF") && _currentHI != null)
            {
                foreach (var file in _moveFileList)
                {
                    var correctOutput =
                        Path.Combine(_currentHI.OutputDirectory, Path.GetFileName(file) ?? string.Empty)
                            .Replace("*", string.Empty).Replace("+", string.Empty);
                    if (File.Exists(file) && !File.Exists(correctOutput))
                        File.Move(file, correctOutput);
                }
                if (_filesToProcess == _completedFiles.Count + 1)
                    _completedFiles.Add("BonusFile");
                
            }
            _runningProgram.OutputDataReceived -= DataReceived;
            _runningProgram.ErrorDataReceived -= ErrorCaught;
            _runningProgram.Exited -= ProgramExited;

            _scanning = false;

            if (!_killed && JobFinished != null)
            {
                if ((_destinationProgram == "TagRecon" && _filesToProcess*2 > _completedFiles.Count) ||
                (_destinationProgram != "TagRecon" && _filesToProcess > _completedFiles.Count))
                {
                    _destinationProgram = string.Empty;
                    JobFinished(false, true);
                }
                else if (_destinationProgram == "DirecTag")
                    JobFinished(true, _runningProgram.ExitCode != 0);
                else
                {
                    _destinationProgram = string.Empty;
                    JobFinished(false, _runningProgram.ExitCode != 0);
                }
            }
            else
                _destinationProgram = string.Empty;

            _fullError = string.Empty;
            foreach (var file in _tempFiles)
                File.Delete(file);
        }

        /// <summary>
        /// Takes list of properties and convers them to an argument string of override flags
        /// </summary>
        /// <param name="propList"></param>
        /// <returns></returns>
        private static string PropertyListToOverrideString(IEnumerable<ConfigProperty> propList)
        {
            var tempstring = new StringBuilder();
            //HACK: Remove constriant when deisotoping is usable
            foreach (var item in propList)
                if (item.Name != "DeisotopingMode")
                    tempstring.AppendFormat("-{0} {1} ", item.Name, item.Value);

            return tempstring.ToString();
        }

        /// <summary>
        /// Selects the destination program and sets it to run in the background
        /// </summary>
        /// <param name="rowNumber"></param>
        /// <param name="hi"></param>
        internal void StartNewJob(int rowNumber, HistoryItem hi)
        {
            _scanning = true;
            _versionCaught = false;
            _currentRow = rowNumber;
            _minPercentage = 0;
            _fileProcessing = 0;
            _filesToProcess = 0;
            _tempFiles = new List<string>();
            _currentHI = hi;

            ThreadStart ts = () => ProcessJob(hi);
            _workThread = new Thread(ts)
                              {
                                  Name = "Program Handler",
                                  IsBackground = true
                              };

            if (hi.JobType == JobType.Myrimatch)
            {
                _completedFiles = new List<string>();
                _destinationProgram = "MyriMatch";
                SendToLog("BumberDash- Job has started");
            }
            else if (hi.JobType == JobType.Comet)
            {
                _completedFiles = new List<string>();
                _destinationProgram = "Comet";
                SendToLog("BumberDash- Job has started");
            }
            else if (hi.JobType == JobType.MSGF)
            {
                _completedFiles = new List<string>();
                _destinationProgram = "MSGF";
                SendToLog("BumberDash- Job has started");
            }
            else if (hi.JobType == JobType.Library)
            {
                _completedFiles = new List<string>();
                _destinationProgram = "Pepitome";
                SendToLog("BumberDash- Job has started");
            }
            else if (_destinationProgram == "DirecTag")
            {
                _destinationProgram = "TagRecon";
                SendToLog(string.Format("{0}{1}{0}", Environment.NewLine, new string('-', 20)));
            }
            else
            {
                _completedFiles = new List<string>();
                _destinationProgram = "DirecTag";
                SendToLog("BumberDash- Job has started");
            }

            _workThread.Start();

        }

        /// <summary>
        /// Translates percentage recieved into overall completion percentage and sends to main form
        /// </summary>
        /// <param name="percentage"></param>
        private void SetPercentage(double percentage)
        {
            var newInt = (int) Math.Round((decimal) percentage);

            if (PercentageUpdate != null)
                PercentageUpdate(newInt);
        }

        /// <summary>
        /// Sends lines of data to LogForm through main QueueForm
        /// </summary>
        /// <param name="data"></param>
        private void SendToLog(string data)
        {
            if (LogUpdate != null)
            {
                if (data == "BumberDash- Job has started")
                    LogUpdate(string.Format("{0}{1}{1}{1}{0}" +
                                            "{0}   Starting job \"{2}\" {0}{0}",
                                            Environment.NewLine,
                                            new string('-', 50),
                                            "<<JobName>>"));
                else
                    LogUpdate(data);
            }
        }

        /// <summary>
        /// Sets row's status text and instructs main form to refresh
        /// </summary>
        /// <param name="status"></param>
        /// <param name="marqueeMode"></param>
        private void SetRunStatus(string status, bool marqueeMode)
        {
            if (StatusUpdate != null)
                StatusUpdate(status, marqueeMode);
        }

        /// <summary>
        /// Makes sure data can still be processed and sends it to correct handle function
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;

            if (_runningProgram == null || _runningProgram.HasExited)
            {
                var recievedLine = e.Data;
                if (_destinationProgram == "DirecTag" && recievedLine.Contains("Writing tags to \""))
                {
                    var delimiter = new string[1];

                    delimiter[0] = "Writing tags to \"";
                    var brokenLine = recievedLine.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
                    var lineEnd = brokenLine[brokenLine.Length - 1];

                    lineEnd = lineEnd.Remove(lineEnd.Length - 2);
                    _completedFiles.Add(lineEnd);
                }
                else if (recievedLine.Contains("Writing search results to file"))
                {
                    var delimiter = new string[1];

                    delimiter[0] = "Writing search results to file \"";
                    var brokenLine = recievedLine.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
                    var lineEnd = brokenLine[brokenLine.Length - 1];

                    lineEnd = lineEnd.Remove(lineEnd.Length - 2);
                    _completedFiles.Add(lineEnd);
                }
                SendToLog(e.Data);
                return;
            }

            SendToLog(e.Data);

            switch (_destinationProgram)
            {
                case "MyriMatch":
                    HandleMyriLine(e.Data);
                    break;
                case "DirecTag":
                    HandleDTLine(e.Data);
                    break;
                case "TagRecon":
                    HandleTRLine(e.Data);
                    break;
                case "Pepitome":
                    HandlePepLine(e.Data);
                    break;
                case "Comet":
                    HandleCometLine(e.Data);
                    break;
                case "MSGF":
                    HandleMSGFLine(e.Data);
                    break;
            }
        }

        private string _fullError = string.Empty;
        private void ErrorCaught(object sender, DataReceivedEventArgs e)
        {
            if (!_scanning || (_destinationProgram == "TagRecon"
                               && _filesToProcess*2 == _completedFiles.Count) ||
                (_destinationProgram != "TagRecon"
                 && _filesToProcess == _completedFiles.Count) ||
                string.IsNullOrEmpty(e.Data) ||
                e.Data.ToLower().Contains("could not find the default configuration file") ||
                e.Data.ToLower().Contains("could not find the default residue masses file"))
                return;

            //msgf workaround
            if (_destinationProgram == "MSGF" && (
                                                     e.Data.Contains("java.lang.NullPointerException") ||
                                                     e.Data.Contains("ScoredSpectraMap.makePepMassSpecKeyMa") ||
                                                     e.Data.Contains("ConcurrentMSGFPlus$RunMSGFPlus.run") ||
                                                     e.Data.Contains("ThreadPoolExecutor.runWorker") ||
                                                     e.Data.Contains("ThreadPoolExecutor$Worker.run") ||
                                                     e.Data.Contains("java.lang.Thread.run")))
                return;


            var data = e.Data;

            if (_fullError == string.Empty)
                _fullError = string.Format("[{0}] Error detected:", _destinationProgram);
            _fullError += Environment.NewLine + data;

            //if (ErrorForward != null)
            //    ErrorForward(string.Format("[{0}] Error detected- {1}", _destinationProgram, data));
        }

        public string GetErrorMessage()
        {
            return _fullError.Length > 0 ? _fullError : null;
        }

        /// <summary>
        /// Analyzes line from MyriMatch output and translates it into status update
        /// </summary>
        /// <param name="recievedLine"></param>
        private void HandleMyriLine(string recievedLine)
        {
            if (_barMode)
            {
                var infoOnly = string.Empty;
                var statRx = new Regex(@"\d+:\d+:\d+ elapsed, \d+:\d+:\d+ remaining");

                if (recievedLine.Contains("Computing cross-correlations."))
                {
                    _barMode = false;
                    _minPercentage = 0;
                    SetRunStatus("Preparing cross-correlation",true);
                    SetPercentage(0);
                }
                else
                {
                    foreach (Match rxMatch in statRx.Matches(recievedLine))
                        infoOnly = rxMatch.Value;
                    var explode = infoOnly.Split();
                    try
                    {
                        var elapsedArray = explode[0].Split(":".ToCharArray());
                        var remainingArray = explode[2].Split(":".ToCharArray());
                        var elapsedTime = (new TimeSpan(int.Parse(elapsedArray[0]),
                                                        int.Parse(elapsedArray[1]),
                                                        int.Parse(elapsedArray[2]))).TotalSeconds;
                        var remainingTime = (new TimeSpan(int.Parse(remainingArray[0]),
                                                          int.Parse(remainingArray[1]),
                                                          int.Parse(remainingArray[2]))).TotalSeconds;
                        if (remainingTime > 0)
                        {
                            var totalTime = elapsedTime + remainingTime;
                            var percentage = (int)Math.Floor(100 * (elapsedTime / totalTime));
                            if (percentage > _minPercentage)
                            {
                                _minPercentage = percentage;
                                SetPercentage(percentage);
                            }
                        }
                    }
                    catch
                    {
                        //This occurs when one of the time values is negative.
                        //Do not try to update the bar (it's probably at the end anyways)
                    }
                }
            }
            else if (recievedLine.ToLower().Contains(".fasta\""))
            {
                SetRunStatus("Reading Myrimatch File", true);
            }
            else if (recievedLine.Contains("Reading spectra from file"))
            {
                _fileProcessing++;
                SetRunStatus(String.Format("Preprocessing File {0} of {1}", _fileProcessing, _filesToProcess), true);
            }
            else if (recievedLine.Contains("Commencing database search"))
            {
                SetRunStatus(
                    String.Format("Searching File {0} of {1} ({2})", _fileProcessing, _filesToProcess,
                                  DataGridViewProgressCell.MessageSpecialValue.Percentage), false);
                _barMode = true;
            }
            else if (recievedLine.Contains("Writing search results to file"))
            {
                var delimiter = new string[1];

                SetRunStatus("Writing Results", true);

                delimiter[0] = "Writing search results to file \"";
                var brokenLine = recievedLine.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
                var lineEnd = brokenLine[brokenLine.Length - 1];

                lineEnd = lineEnd.Remove(lineEnd.Length - 2);
                _completedFiles.Add(lineEnd);
            }
            else if (!_versionCaught)
            {
                var introMatch = Regex.Match(recievedLine, @"MyriMatch (\d+.\d+.\d+)");
                if (introMatch.Success && introMatch.Groups.Count == 2)
                {
                    Properties.Settings.Default.MyriMatchVersion = introMatch.Groups[1].Value;
                    _versionCaught = true;
                    Properties.Settings.Default.Save();
                }
            }
        }

        /// <summary>
        /// Analyzes line from DirecTag output and translates it into status update
        /// </summary>
        /// <param name="recievedLine"></param>
        private void HandleDTLine(string recievedLine)
        {
            if (_barMode)
            {
                var infoOnly = string.Empty;
                var statRx = new Regex(@"\d+:\d+:\d+ elapsed, \d+:\d+:\d+ remaining");

                if (recievedLine.Contains("Finished sequence tagging spectra"))
                {
                    SetPercentage(100);
                    _barMode = false;
                    _minPercentage = 0;
                }
                else
                {
                    foreach (Match RxMatch in statRx.Matches(recievedLine))
                        infoOnly = RxMatch.Value;
                    var explode = infoOnly.Split();
                    try
                    {
                        if (!string.IsNullOrEmpty(infoOnly))
                        {
                            var elapsedArray = explode[0].Split(":".ToCharArray());
                            var remainingArray = explode[2].Split(":".ToCharArray());
                            var elapsedTime = (new TimeSpan(int.Parse(elapsedArray[0]),
                                                            int.Parse(elapsedArray[1]),
                                                            int.Parse(elapsedArray[2]))).TotalSeconds;
                            var remainingTime = (new TimeSpan(int.Parse(remainingArray[0]),
                                                              int.Parse(remainingArray[1]),
                                                              int.Parse(remainingArray[2]))).TotalSeconds;
                            if (remainingTime > 0)
                            {
                                var totalTime = elapsedTime + remainingTime;
                                var percentage = (int)Math.Floor(100 * (elapsedTime / totalTime));
                                if (percentage > _minPercentage)
                                {
                                    _minPercentage = percentage;
                                    SetPercentage(percentage);
                                }
                            }
                        }
                    }
                    catch
                    {
                        //Occurs when one of the time values is negative.
                        //No need to update progress bar
                    }



                }
            }
            else if (recievedLine.Contains("Reading spectra from file \""))
            {
                _fileProcessing++;
                SetRunStatus(String.Format("Reading File {0} of {1}", _fileProcessing, _filesToProcess), true);

            }
            else if (recievedLine.Contains("Trimming spectra with"))
            {
                SetRunStatus(String.Format("Preprocessing File {0} of {1}", _fileProcessing, _filesToProcess), true);
            }
            else if (recievedLine.Contains("Sequence tagged"))
            {
                SetRunStatus(
                    String.Format("Searching File {0} of {1} ({2})", _fileProcessing, _filesToProcess,
                                  DataGridViewProgressCell.MessageSpecialValue.Percentage), false);
                _barMode = true;
            }
            else if (recievedLine.Contains("Writing tags to \""))
            {
                var delimiter = new string[1];

                SetRunStatus("Writing Results", true);
                SetPercentage(0);

                delimiter[0] = "Writing tags to \"";
                var brokenLine = recievedLine.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
                var lineEnd = brokenLine[brokenLine.Length - 1];

                lineEnd = lineEnd.Remove(lineEnd.Length - 2);
                _completedFiles.Add(lineEnd);
            }
            else if (!_versionCaught)
            {
                var introMatch = Regex.Match(recievedLine, @"DirecTag (\d+.\d+.\d)");
                if (introMatch.Success && introMatch.Groups.Count == 2)
                {
                    Properties.Settings.Default.DirecTagVersion = introMatch.Groups[1].Value;
                    _versionCaught = true;
                    Properties.Settings.Default.Save();
                }
            }
        }

        /// <summary>
        /// Analyzes line from TagRecon output and translates it into status update
        /// </summary>
        /// <param name="recievedLine"></param>
        private void HandleTRLine(string recievedLine)
        {

            if (_barMode)
            {
                var infoOnly = string.Empty;
                var statRx = new Regex(@"\d+:\d+:\d+ elapsed, \d+:\d+:\d+ remaining");

                if (recievedLine.Contains("Finished database search"))
                {
                    SetRunStatus("Preparing cross-correlation", true);
                    SetPercentage(0);
                    _barMode = false;
                    _minPercentage = 0;
                }
                else
                {
                    foreach (Match rxMatch in statRx.Matches(recievedLine))
                        infoOnly = rxMatch.Value;
                    var explode = infoOnly.Split();

                    try
                    {
                        var elapsedArray = explode[0].Split(":".ToCharArray());
                        var remainingArray = explode[2].Split(":".ToCharArray());
                        var elapsedTime = (new TimeSpan(int.Parse(elapsedArray[0]),
                                                        int.Parse(elapsedArray[1]),
                                                        int.Parse(elapsedArray[2]))).TotalSeconds;
                        var remainingTime = (new TimeSpan(int.Parse(remainingArray[0]),
                                                          int.Parse(remainingArray[1]),
                                                          int.Parse(remainingArray[2]))).TotalSeconds;
                        if (remainingTime > 0)
                        {
                            var totalTime = elapsedTime + remainingTime;
                            var percentage = 100 * (elapsedTime / totalTime);
                            if (percentage > _minPercentage)
                            {
                                _minPercentage = percentage;
                                SetPercentage(percentage);
                            }
                        }
                    }
                    catch
                    {
                        //This occurs when one of the time values is negative. Do not try to update the bar (it's probably at the end anyways)
                    }



                }
            }
            else if (recievedLine.ToLower().Contains(".fasta\""))
            {

                SetRunStatus("Reading Myrimatch File", true);
            }
            else if (recievedLine.Contains("Reading spectra"))
            {
                _fileProcessing++;
                SetRunStatus(String.Format("Reading Tag File {0} of {1}", _fileProcessing,_filesToProcess), true);

            }
            else if (recievedLine.Contains("Parsing"))
            {
                SetRunStatus(String.Format("Preprocessing Tag File {0} of {1}", _fileProcessing, _filesToProcess), true);
            }
            else if (recievedLine.Contains("Commencing database search"))
            {
                SetRunStatus(String.Format("Searching Tag File {0} of {1} ({2})", _fileProcessing, _filesToProcess, DataGridViewProgressCell.MessageSpecialValue.Percentage), false);
                _barMode = true;
            }
            else if (recievedLine.Contains("Writing search results to file"))
            {
                var delimiter = new string[1];

                SetRunStatus("Writing Results", true);

                delimiter[0] = "Writing search results to file \"";
                var brokenLine = recievedLine.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
                var lineEnd = brokenLine[brokenLine.Length - 1];

                lineEnd = lineEnd.Remove(lineEnd.Length - 2);
                _completedFiles.Add(lineEnd);
            }
            else if (!_versionCaught)
            {
                Match introMatch = Regex.Match(recievedLine, @"TagRecon (\d+.\d+.\d)");
                if (introMatch.Success && introMatch.Groups.Count == 2)
                {
                    Properties.Settings.Default.TagReconVersion = introMatch.Groups[1].Value;
                    _versionCaught = true;
                    Properties.Settings.Default.Save();
                }
            }
        }

        private void HandlePepLine(string recievedLine)
        {
            if (_barMode)
            {
                var infoOnly = string.Empty;
                var statRx = new Regex(@"\d+:\d+:\d+ elapsed, \d+:\d+:\d+ remaining");

                if (recievedLine.Contains("Mapping library peptide matches"))
                {
                    _barMode = false;
                    _minPercentage = 0;
                    SetRunStatus("Mapping Matches", true);
                    SetPercentage(0);
                }
                else
                {
                    foreach (Match rxMatch in statRx.Matches(recievedLine))
                        infoOnly = rxMatch.Value;
                    var explode = infoOnly.Split();
                    try
                    {
                        var elapsedArray = explode[0].Split(":".ToCharArray());
                        var remainingArray = explode[2].Split(":".ToCharArray());
                        var elapsedTime = (new TimeSpan(int.Parse(elapsedArray[0]),
                                                        int.Parse(elapsedArray[1]),
                                                        int.Parse(elapsedArray[2]))).TotalSeconds;
                        var remainingTime = (new TimeSpan(int.Parse(remainingArray[0]),
                                                          int.Parse(remainingArray[1]),
                                                          int.Parse(remainingArray[2]))).TotalSeconds;
                        if (remainingTime > 0)
                        {
                            var totalTime = elapsedTime + remainingTime;
                            var percentage = (int)Math.Floor(100 * (elapsedTime / totalTime));
                            if (percentage > _minPercentage)
                            {
                                _minPercentage = percentage;
                                SetPercentage(percentage);
                            }
                        }
                    }
                    catch
                    {
                        //This occurs when one of the time values is negative.
                        //Do not try to update the bar (it's probably at the end anyways)
                    }
                }
            }
            else if (recievedLine.ToLower().Contains(".fasta\""))
            {
                SetRunStatus("Reading Myrimatch File", true);
            }
            else if (Regex.IsMatch(recievedLine, @"Read \d+ proteins"))
            {
                SetRunStatus("Reading Spectral Library", true);
            }
            else if (recievedLine.Contains("Reading spectra from file"))
            {
                _fileProcessing++;
                SetRunStatus(String.Format("Preprocessing File {0} of {1}", _fileProcessing, _filesToProcess), true);
            }
            else if (recievedLine.Contains("Commencing library search"))
            {
                SetRunStatus(
                    String.Format("Searching File {0} of {1} ({2})", _fileProcessing, _filesToProcess,
                                  DataGridViewProgressCell.MessageSpecialValue.Percentage), false);
                _barMode = true;
            }
            else if (recievedLine.Contains("Writing search results to file"))
            {
                var delimiter = new string[1];

                SetRunStatus("Writing Results", true);

                delimiter[0] = "Writing search results to file \"";
                var brokenLine = recievedLine.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
                var lineEnd = brokenLine[brokenLine.Length - 1];

                lineEnd = lineEnd.Remove(lineEnd.Length - 2);
                _completedFiles.Add(lineEnd);
            }
            else if (!_versionCaught)
            {
                var introMatch = Regex.Match(recievedLine, @"Pepitome (\d+.\d+.\d)");
                if (introMatch.Success && introMatch.Groups.Count == 2)
                {
                    Properties.Settings.Default.MyriMatchVersion = introMatch.Groups[1].Value;
                    _versionCaught = true;
                    Properties.Settings.Default.Save();
                }
            }
        }

        private void HandleCometLine(string recievedLine)
        {
            if (string.IsNullOrEmpty(recievedLine))
                return;
            if (recievedLine.Contains("Input file"))
            {
                _fileProcessing++;
                SetRunStatus(String.Format("Processing File {0} of {1}", _fileProcessing, _filesToProcess), true);
            }
            else if (recievedLine.Contains("Search end"))
            {
                SetRunStatus("Writing Results", true);
                _completedFiles.Add("CometFile");
            }
            else if (!_versionCaught)
            {
                var introMatch = Regex.Match(recievedLine, "version\\s\"([^\"]+)");
                if (introMatch.Success && introMatch.Groups.Count == 2)
                {
                    Properties.Settings.Default.CometVersion = introMatch.Groups[1].Value;
                    _versionCaught = true;
                    Properties.Settings.Default.Save();
                }
            }
        }

        private void HandleMSGFLine(string recievedLine)
        {
            if (_barMode)
            {
                if (recievedLine.Contains("Writing results..."))
                {
                    _barMode = false;
                    _minPercentage = 0;
                    SetRunStatus("Writing results", true);
                    SetPercentage(0);
                    _completedFiles.Add("MSGFFile");
                }
                else
                {
                    double percent;
                    var rx = new Regex(@"\.\.\.\s+([^%]+)%");
                    if (!rx.IsMatch(recievedLine))
                        return;
                    var matchGroups = rx.Match(recievedLine).Groups;
                    if (!double.TryParse(matchGroups[1].Value, out percent) || percent < 0 || percent > 100)
                        return;
                    SetPercentage(percent);
                    if (recievedLine.Contains("Myrimatch search"))
                        SetRunStatus(String.Format("Searching File {0} of {1} ({2})", _fileProcessing,
                                                   _filesToProcess, percent), false);
                    if (recievedLine.Contains("Computing spectral E-values"))
                        SetRunStatus(String.Format("Computing Spectral E-values ({0})", percent), false);

                }
            }
            else if (recievedLine.Contains("Loading database files"))
            {
                SetRunStatus(String.Format("Preprocessing File {0} of {1}", _fileProcessing, _msgfList.Count), true);
            }
            else if (recievedLine.Contains("Preprocessing spectra finished"))
            {
                SetRunStatus(String.Format("Searching File {0} of {1} ({2})", _fileProcessing,
                                           _filesToProcess, 0), false);
                _barMode = true;
            }
            else if (!_versionCaught)
            {
                var introMatch = Regex.Match(recievedLine, @"^MS-GF[^\(]+\(([^\)]+)");
                if (introMatch.Success && introMatch.Groups.Count == 2)
                {
                    Properties.Settings.Default.CometVersion = introMatch.Groups[1].Value;
                    _versionCaught = true;
                    Properties.Settings.Default.Save();
                }
            }
        }

        /// <summary>
        /// Forces the Bumbershoot utility to close and aborts the tread it is being run in
        /// </summary>
        internal void ForceKill()
        {
            _killed = true;
            _barMode = false;
            _scanning = false;
            if (_runningProgram != null && !_runningProgram.HasExited)
            {
                _runningProgram.OutputDataReceived -= DataReceived;
                _runningProgram.ErrorDataReceived -= ErrorCaught;
                _runningProgram.Exited -= ProgramExited;
                _runningProgram.Kill();
                _runningProgram.Close();
                _runningProgram = null;
            }
            if (_workThread != null)
                _workThread.Abort();
        }

        /// <summary>
        /// Indicates that the index of the current row has changed and adjusts accordingly
        /// </summary>
        internal void DeletedAbove()
        {
            if (_scanning)
                _currentRow--;
        }

    }
}
