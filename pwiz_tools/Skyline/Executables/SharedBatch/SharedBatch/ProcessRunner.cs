using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using SharedBatch.Properties;

namespace SharedBatch
{
    public class ProcessRunner
    {


        private bool _canceled;
        private bool _running;

        public HandleDataReceived OnDataReceived;
        public HandleException OnException;
        public HandleError OnError;

        public delegate void HandleDataReceived(string data);
        public delegate void HandleException(Exception e, string message);
        public delegate void HandleError();

        public void Cancel()
        {
            if (_running)
                _canceled = true;
        }

        public ProcessRunner Copy()
        {
            return new ProcessRunner()
            {
                OnDataReceived = OnDataReceived,
                OnException = OnException,
                OnError = OnError
            };
        }
        
        public async Task Run(string exeFile, string arguments)
        {
            DataReceived(arguments);

            Process cmd = new Process();
            cmd.StartInfo.FileName = exeFile;
            cmd.StartInfo.Arguments = arguments;
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.RedirectStandardError = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.EnableRaisingEvents = true;
            cmd.Exited += (sender, e) =>
            {
                if (cmd.ExitCode != 0)
                    Error();
            };
            cmd.OutputDataReceived += (sender, e) => DataReceived(e.Data);
            cmd.ErrorDataReceived += (sender, e) => DataReceived(e.Data);
            try
            {
                cmd.Start();
                // Add process to tracker so the OS will dispose of it if SkylineBatch exits/crashes
                ChildProcessTracker.AddProcess(cmd);
                cmd.BeginOutputReadLine();
                cmd.BeginErrorReadLine();
            }
            catch (Exception e)
            {
                StartException(e, exeFile);
                return;
            }

            _running = true;

            while (!cmd.HasExited && !_canceled)
            {
                await Task.Delay(2000);
            }

            _canceled = false;

            // end cmd and all child processes if runner has been stopped before completion
            if (!cmd.HasExited)
            {
                // make sure no process children left running
                await KillProcessChildren((UInt32)cmd.Id);
                if (!cmd.HasExited) cmd.Kill();
            }

            _running = false;
        }

        private void DataReceived(string data)
        {
            if (OnDataReceived != null) OnDataReceived(data);
        }

        private void StartException(Exception e, string exeFile)
        {
            Error();
            if (OnException != null)
                OnException(e, string.Format(Resources.ProcessRunner_StartException_Unable_to_start__0__, Path.GetFileName(exeFile)));
            else
                throw e;
        }

        private void Error()
        {
            if (OnError != null) OnError();
        }

        private async Task KillProcessChildren(UInt32 parentProcessId)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "SELECT * " +
                "FROM Win32_Process " +
                "WHERE ParentProcessId=" + parentProcessId);

            ManagementObjectCollection collection = searcher.Get();
            if (collection.Count > 0)
            {
                ProgramLog.Info("Killing [" + collection.Count + "] processes spawned by process with Id [" + parentProcessId + "]");
                foreach (var item in collection)
                {
                    UInt32 childProcessId = (UInt32)item["ProcessId"];
                    if (childProcessId != Process.GetCurrentProcess().Id)
                    {
                        await KillProcessChildren(childProcessId);

                        try
                        {
                            var childProcess = Process.GetProcessById((int)childProcessId);
                            ProgramLog.Info("Killing child process [" + childProcess.ProcessName + "] with Id [" + childProcessId + "]");
                            childProcess.Kill();
                        }
                        catch (ArgumentException)
                        {
                            ProgramLog.Info("Child process already terminated");
                        }
                        catch (Win32Exception)
                        {
                            ProgramLog.Info("Cannot kill windows child process.");
                        }
                    }
                }
            }
        }
    }
}
