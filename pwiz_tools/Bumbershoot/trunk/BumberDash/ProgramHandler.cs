using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace BumberDash
{
    class ProgramHandler
    {
        private string _savedStatusText = string.Empty;
        private bool _scanning = false;
        private bool _killed = false;
        private bool _barMode = false;
        private int _currentRow;
        private int _fileProcessing;
        private int _filesToProcess;
        private double _minPercentage;
        private string _destinationProgram = string.Empty;
        private string _mainDirectory;
        private QueueForm _mainForm;
        private Thread _workThread;
        private Process RunningProgram;
        internal List<string> _completedFiles = new List<string>();
        delegate void NoParamDelegate();
        delegate void JobDelegate(int n);

        public ProgramHandler(QueueForm parentForm)
        {
            _mainForm = parentForm;
            _mainDirectory = System.IO.Directory.GetCurrentDirectory();
        }

        internal bool JobIsRunning()
        {
            return _scanning;
        }

        private void SetPercentage(double percentage)
        {
            double newPercentage = 0;
            int newInt = 0;

            switch (_destinationProgram)
            {
                case "MyriMatch":
                    newPercentage = (percentage / (_filesToProcess)) + ((_fileProcessing - 1) * (100 / _filesToProcess));
            newInt = (int)Math.Round((decimal)newPercentage);
            break;
                case "DirecTag":
            newPercentage = ((percentage / (_filesToProcess)) +((_fileProcessing - 1) * (100 / _filesToProcess))) / 2;
            newInt = (int)Math.Round((decimal)newPercentage);
            break;
                case "TagRecon":
            newPercentage = ((percentage / (_filesToProcess)) + ((_fileProcessing - 1) * (100 / _filesToProcess))) / 2 + 50;
            newInt = (int)Math.Round((decimal)newPercentage);
            break;
                default:
            newInt = (int)Math.Round((decimal)percentage);
            break;
            }

            _mainForm.TrayIcon.Text = string.Format("{0} ({1}%)",_savedStatusText,newInt);
            _mainForm.JobQueueDGV.Rows[_currentRow].Cells[5].Value = newInt;
        }

        private void SendToLog(string data)
        {
            NoParamDelegate testDel = () => invokedSendToLog(data);
            try
            {
                _mainForm.Invoke(testDel);
            }
            catch
            {
                //For some reason program does not detect that _mainForm
                //has been disposed even after error caught
                //if (_mainForm != null && !_mainForm.IsDisposed)
                //    throw;
            }
        }

        private void SetRunStatus(string status)
        {
            NoParamDelegate testDel = () => invokedSetRunStatus(status);
            try
            {
                _mainForm.Invoke(testDel);
            }
            catch
            {
                //For some reason program does not detect that _mainForm
                //has been disposed even after error caught
                //if (_mainForm != null && !_mainForm.IsDisposed)
                //    throw;
            }
        }


        private void invokedSendToLog(string data)
        {
            if (data == "BumberDash- Job has started")
            {
                _mainForm.AddLogLine(string.Format("{0}{1}{1}{1}{0}" +
                    "{0}   Starting job \"{2}\" {0}{0}",System.Environment.NewLine,
                    "------------------------------------------------------------",
                    _mainForm.JobQueueDGV[0,_currentRow].Value.ToString()));
            }
            else
                _mainForm.AddLogLine(data);
        }

        private void invokedSetRunStatus(string status)
        {
            _savedStatusText = String.Format("BumberDash - {0}", _mainForm.JobQueueDGV.Rows[_currentRow].Cells[0].Value, status);

            _mainForm.JobQueueDGV.Rows[_currentRow].Cells[5].Tag = status;
            _mainForm.UpdateStatusText();
        }

        internal void StartNewJob(int rowNumber)
        {
            _scanning = true;
            _currentRow = rowNumber;
            _minPercentage = 0;
            _fileProcessing = 0;
            _filesToProcess = 0;
            

            NoParamDelegate forkDelegate = () => ProcessJob();
            _workThread = new Thread(new ThreadStart(forkDelegate));
            _workThread.Name = "Program Handler";

            if (_mainForm.JobQueueDGV[4, _currentRow].Value.ToString() == "Database Search")
            {
                _completedFiles = new List<string>();
                _destinationProgram = "MyriMatch";
                SendToLog("BumberDash- Job has started");
            }
            else if (_destinationProgram == "DirecTag")
            {
                _destinationProgram = "TagRecon";
                SendToLog(string.Format("{0}---{0}", System.Environment.NewLine));
            }
            else
            {
                _completedFiles = new List<string>();
                _destinationProgram = "DirecTag";
                SendToLog("BumberDash- Job has started");
            }

            _workThread.Start();
            
        }

        private void ProcessJob()
        {

            //initialize argument string
            string tempString;

            _killed = false;
            ProcessStartInfo psi;

            if (int.Parse(_mainForm.JobQueueDGV[6, _currentRow].Tag.ToString()) > 0)
                tempString = string.Format("-cpus {0} -cfg ", _mainForm.JobQueueDGV[6, _currentRow].Tag.ToString());
            else
                tempString = "-cfg ";
            
            switch (_destinationProgram)
            {
                case "MyriMatch":
                    //Set  location of the program
                    psi = new ProcessStartInfo(String.Format(@"""{0}\myrimatch\myrimatch.exe""", _mainDirectory));

                    //continue to set up argument string
                    tempString += String.Format("\"{0}\" -ProteinDatabase \"{1}\"",
                        _mainForm.JobQueueDGV[3, _currentRow].ToolTipText,
                        _mainForm.JobQueueDGV[2, _currentRow].ToolTipText);

                    //add files to scan to argument string
                    foreach (string str in _mainForm.JobQueueDGV[0, _currentRow].ToolTipText.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                    {
                        tempString += String.Format(" {0}", str);
                        _filesToProcess++;
                    }
                    break;
                case "DirecTag":
                    //Set  location of the program
                    psi = new ProcessStartInfo(String.Format(@"""{0}\directag\directag.exe""", _mainDirectory));

                    //continue to set up argument string
                    tempString += String.Format("\"{0}\"", (_mainForm.JobQueueDGV[3, _currentRow].ToolTipText.Split(System.Environment.NewLine.ToCharArray(),StringSplitOptions.RemoveEmptyEntries))[0]);

                    //add files to scan to argument string
                    foreach (string str in _mainForm.JobQueueDGV[0, _currentRow].ToolTipText.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                    {
                        tempString += String.Format(" {0}", str);
                        _filesToProcess++;
                    }
                    break;
                case "TagRecon":
                    //Set  location of the program
                    psi = new ProcessStartInfo(String.Format(@"""{0}\tagrecon\tagrecon.exe""", _mainDirectory));

                    //continue to set up argument string
                    tempString += String.Format(@"""{0}"" -ProteinDatabase ""{1}""",
                        (_mainForm.JobQueueDGV[3, _currentRow].ToolTipText.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries))[1],
                        _mainForm.JobQueueDGV[2, _currentRow].ToolTipText);

                    //add files to scan to argument string
                    foreach (string str in _completedFiles)
                    {
                        tempString += String.Format(@" ""{0}\{1}""", _mainForm.JobQueueDGV[1, _currentRow].ToolTipText, str);
                        _filesToProcess++;
                    }
                    break;
                default:
                    psi = new ProcessStartInfo();
                    //should never be called, throw error if it is
                    throw new Exception(String.Format("Destination Program not set to known value: {0}", _destinationProgram));
            }

            psi.WorkingDirectory = _mainForm.JobQueueDGV[1, _currentRow].ToolTipText;

            psi.Arguments = tempString;


            //Make sure window stays hidden
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            RunningProgram = new Process();
            RunningProgram.StartInfo = psi;
            RunningProgram.Start();
            RunningProgram.PriorityClass = ProcessPriorityClass.BelowNormal;
            RunningProgram.BeginOutputReadLine();
            RunningProgram.OutputDataReceived += new DataReceivedEventHandler(DataReceived);
        } 

        private void DataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null || RunningProgram.HasExited)
            {
                try
                {
                    NoParamDelegate testDel = () => ExitDetected();
                    _mainForm.Invoke(testDel);
                }
                catch
                {
                    //This is triggered when the main form has exited but
                    //the program is still recieving data
                }
            }
            else
            {
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
                }
            }
        }

        private void HandleMyriLine(string recievedLine)
        {
            if (_barMode)
            {
                string InfoOnly = string.Empty;
                string[] Explode;
                double ElapsedTime = 0;
                double RemainingTime = 0;
                double TotalTime = 0;
                int Percentage;
                Regex StatRx = new Regex(@"\d+(?:.\d+)? elapsed, \d+(?:.\d+)? remaining");

                if (recievedLine.Contains("has finished database search"))
                {
                    SetPercentage(100);
                    _barMode = false;
                    _minPercentage = 0;
                    SetRunStatus("--- Performing cross-correlation analysis ---");
                }
                else
                {
                    foreach (Match RxMatch in StatRx.Matches(recievedLine))
                        InfoOnly = RxMatch.Value;
                    Explode = InfoOnly.Split();
                    try
                    {
                        ElapsedTime = double.Parse(Explode[0]);
                        RemainingTime = double.Parse(Explode[2]);
                        if (RemainingTime > 0)
                        {
                            TotalTime = ElapsedTime + RemainingTime;
                            Percentage = (int)Math.Floor(100 * (ElapsedTime / TotalTime));
                            if (Percentage > _minPercentage)
                            {
                                _minPercentage = Percentage;
                                SetPercentage(Percentage);
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

                SetRunStatus("--- Reading Database File ---");
            }
            else if (recievedLine.Contains("is reading spectra"))
            {
                _fileProcessing++;
                SetRunStatus(String.Format("--- Reading File {0} of {1} ---", _fileProcessing.ToString(), _filesToProcess.ToString()));

            }
            else if (recievedLine.Contains("is preparing"))
            {
                SetRunStatus(String.Format("--- Preprocessing File {0} of {1} ---", _fileProcessing.ToString(), _filesToProcess.ToString()));
            }
            else if (recievedLine.Contains("is commencing database search"))
            {
                SetRunStatus(String.Format("--- Searching File {0} of {1} ---", _fileProcessing.ToString(), _filesToProcess.ToString()));
                _barMode = true;
            }
            else if (recievedLine.Contains("is writing search results to file"))
            {
                string[] Delimiter = new string[1];
                string[] BrokenLine;

                SetRunStatus("--- Writing Results ---");

                Delimiter[0] = "is writing search results to file \"";
                BrokenLine = recievedLine.Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);

                BrokenLine[1] = BrokenLine[1].Remove(BrokenLine[1].Length - 2);
                _completedFiles.Add(BrokenLine[1]);
            }
        }

        private void HandleDTLine(string recievedLine)
        {
            if (_barMode)
            {
                string InfoOnly = string.Empty;
                string[] Explode;
                double ElapsedTime = 0;
                double RemainingTime = 0;
                double TotalTime = 0;
                double Percentage;
                Regex StatRx = new Regex(@"\d+(?:.\d+)? elapsed, \d+(?:.\d+)? remaining");

                if (recievedLine.Contains("is generating output of tags"))
                {
                    SetPercentage(100);
                    _barMode = false;
                    _minPercentage = 0;
                }
                else
                {
                    foreach (Match RxMatch in StatRx.Matches(recievedLine))
                        InfoOnly = RxMatch.Value;
                    Explode = InfoOnly.Split();
                    try
                    {
                        if (!string.IsNullOrEmpty(InfoOnly))
                        {
                            ElapsedTime = double.Parse(Explode[0]);
                            RemainingTime = double.Parse(Explode[2]);
                            if (RemainingTime > 0)
                            {
                                TotalTime = ElapsedTime + RemainingTime;
                                Percentage = (int)Math.Floor(100 * (ElapsedTime / TotalTime));
                                if (Percentage > _minPercentage)
                                {
                                    _minPercentage = Percentage;
                                    SetPercentage(Percentage);
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
            else if (recievedLine.Contains("is reading spectra from file \""))
            {
                _fileProcessing++;
                SetRunStatus(String.Format("--- Reading File {0} of {1} ---", _fileProcessing.ToString(), _filesToProcess.ToString()));

            }
            else if (recievedLine.Contains("is trimming spectra"))
            {
                SetRunStatus(String.Format("--- Preprocessing File {0} of {1} ---", _fileProcessing.ToString(), _filesToProcess.ToString()));
            }
            else if (recievedLine.Contains("has sequence tagged"))
            {
                SetRunStatus(String.Format("--- Searching File {0} of {1} ---", _fileProcessing.ToString(), _filesToProcess.ToString()));
                _barMode = true;
            }
            else if (recievedLine.Contains("is writing tags to \""))
            {
                string[] Delimiter = new string[1];
                string[] BrokenLine;

                SetRunStatus("--- Writing Results ---");

                Delimiter[0] = "is writing tags to \"";
                BrokenLine = recievedLine.Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);

                BrokenLine[1] = BrokenLine[1].Remove(BrokenLine[1].Length - 2);
                _completedFiles.Add(BrokenLine[1]);
            }
        }

        private void HandleTRLine(string recievedLine)
        {

            if (_barMode)
            {
                string InfoOnly = string.Empty;
                string[] Explode;
                double ElapsedTime = 0;
                double RemainingTime = 0;
                double TotalTime = 0;
                double Percentage;
                Regex StatRx = new Regex(@"\d+(?:.\d+)? elapsed, \d+(?:.\d+)? remaining");

                if (recievedLine.Contains("has finished database search"))
                {
                    SetPercentage(100);
                    SetRunStatus("--- Performing cross-correlation analysis ---");
                    _barMode = false;
                    _minPercentage = 0;
                }
                else
                {
                    foreach (Match RxMatch in StatRx.Matches(recievedLine))
                        InfoOnly = RxMatch.Value;
                    Explode = InfoOnly.Split();

                    try
                    {
                        ElapsedTime = double.Parse(Explode[0]);
                        RemainingTime = double.Parse(Explode[2]);
                        if (RemainingTime > 0)
                        {
                            TotalTime = ElapsedTime + RemainingTime;
                            Percentage = 100 * (ElapsedTime / TotalTime) / _filesToProcess * _fileProcessing;
                            if (Percentage > _minPercentage)
                            {
                                _minPercentage = Percentage;
                                SetPercentage(Percentage);
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

                SetRunStatus("--- Reading Database File ---");
            }
            else if (recievedLine.Contains("is reading spectra"))
            {
                _fileProcessing++;
                SetRunStatus(String.Format("--- Reading Tag File {0} of {1} ---", _fileProcessing.ToString(),_filesToProcess.ToString()));

            }
            else if (recievedLine.Contains("is parsing"))
            {
                SetRunStatus(String.Format("--- Preprocessing Tag File {0} of {1} ---", _fileProcessing, _filesToProcess.ToString()));
            }
            else if (recievedLine.Contains("is commencing database search"))
            {
                SetRunStatus(String.Format("--- Searching Tag File {0} of {1} ---", _fileProcessing, _filesToProcess.ToString()));
                _barMode = true;
            }
            else if (recievedLine.Contains("is writing search results to file"))
            {
                string[] Delimiter = new string[1];
                string[] BrokenLine;

                SetRunStatus("--- Writing Results ---");

                Delimiter[0] = "is writing search results to file \"";
                BrokenLine = recievedLine.Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);

                BrokenLine[1] = BrokenLine[1].Remove(BrokenLine[1].Length - 2);
                _completedFiles.Add(BrokenLine[1]);
            }
        }

        void ExitDetected()
        {
            _scanning = false;

            if (_barMode)
            {
                _barMode = false;
                _mainForm.IndicateRowError(_currentRow);
            }

            if (!_killed)
            {
                RunningProgram.Close();

                if (_destinationProgram == "DirecTag")
                {
                    _mainForm._jobProcess.StartNewJob(_currentRow);
                }
                else
                {
                    _destinationProgram = string.Empty;
                    SetPercentage(100);
                    _mainForm.InidcateJobDone();
                }
            }
            else
                _destinationProgram = string.Empty;
        }

        internal void ForceKill()
        {
            _killed = true;
            _barMode = false;
            if (RunningProgram != null && !RunningProgram.HasExited)
            {
                RunningProgram.Kill();
                RunningProgram.Close();
                RunningProgram.Dispose();
            }
            if (_workThread != null)
                _workThread.Abort();
        }

        internal void DeletedAbove()
        {
            if (_scanning)
                _currentRow--;
            _mainForm._lastCompleted--;
        }

    }
}
